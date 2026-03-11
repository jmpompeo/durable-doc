using System.IO.Enumeration;
using DurableDoc.Configuration;
using DurableDoc.Domain;
using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.MSBuild;

namespace DurableDoc.Analysis;

public sealed class WorkflowAnalyzer
{
    private static readonly HashSet<string> OrchestratorContextTypeNames =
    [
        "IDurableOrchestrationContext",
        "TaskOrchestrationContext",
    ];

    private static readonly Dictionary<string, WrapperRecognizer> BuiltInRecognizers = new(StringComparer.Ordinal)
    {
        ["CallActivityAsync"] = new(WorkflowNodeType.Activity, 0),
        ["CallActivityWithRetryAsync"] = new(WorkflowNodeType.RetryActivity, 0),
        ["CallSubOrchestratorAsync"] = new(WorkflowNodeType.SubOrchestrator, 0),
        ["CallSubOrchestratorWithRetryAsync"] = new(WorkflowNodeType.RetrySubOrchestrator, 0),
        ["WaitForExternalEvent"] = new(WorkflowNodeType.ExternalEvent, 0),
        ["CreateTimer"] = new(WorkflowNodeType.Timer, null),
        ["CallActivityWithResult"] = new(WorkflowNodeType.Activity, 0),
        ["CallActivityWithVoidResult"] = new(WorkflowNodeType.Activity, 0),
    };

    public async Task<IReadOnlyList<WorkflowDiagram>> AnalyzeAsync(string inputPath, DurableDocConfig? config = null, CancellationToken cancellationToken = default)
    {
        var result = await AnalyzeWorkspaceAsync(inputPath, config, cancellationToken).ConfigureAwait(false);
        return result.Diagrams;
    }

    public async Task<WorkflowAnalysisResult> AnalyzeWorkspaceAsync(string inputPath, DurableDocConfig? config = null, CancellationToken cancellationToken = default)
    {
        var workspace = await WorkspaceSourceLoader.LoadAsync(inputPath, config, cancellationToken).ConfigureAwait(false);
        var recognizers = BuildRecognizerMap(config);

        var diagrams = workspace.Methods
            .Where(IsOrchestrator)
            .OrderBy(method => method.Method.Identifier.ValueText, StringComparer.Ordinal)
            .Select(method => WorkflowBuilder.Build(method, recognizers, config))
            .ToArray();

        return new WorkflowAnalysisResult
        {
            ResolvedInputPath = workspace.ResolvedInputPath,
            InputKind = workspace.InputKind,
            ScannedProjects = workspace.ScannedProjects,
            Diagrams = diagrams,
        };
    }

    private static Dictionary<string, WrapperRecognizer> BuildRecognizerMap(DurableDocConfig? config)
    {
        var recognizers = new Dictionary<string, WrapperRecognizer>(BuiltInRecognizers, StringComparer.Ordinal);

        foreach (var wrapper in config?.Analysis?.Wrappers ?? [])
        {
            if (!Enum.TryParse<WorkflowNodeType>(wrapper.Kind, ignoreCase: true, out var parsed))
            {
                parsed = WorkflowNodeType.Wrapper;
            }

            recognizers[wrapper.MethodName] = new WrapperRecognizer(parsed, wrapper.TargetNameArgumentIndex);
        }

        return recognizers;
    }

    private static bool IsOrchestrator(SourceMethod sourceMethod)
    {
        var method = sourceMethod.Method;

        var hasTriggerAttribute = method.AttributeLists
            .SelectMany(x => x.Attributes)
            .Select(x => x.Name.ToString())
            .Any(name => name.Contains("OrchestrationTrigger", StringComparison.Ordinal));

        var hasContextParameter = method.ParameterList.Parameters
            .Any(p => OrchestratorContextTypeNames.Any(ctx => p.Type?.ToString().Contains(ctx, StringComparison.Ordinal) == true));

        var isPrivate = method.Modifiers.Any(modifier => modifier.IsKind(SyntaxKind.PrivateKeyword));

        return hasTriggerAttribute || (hasContextParameter && !isPrivate);
    }
}

internal sealed class WorkflowBuilder
{
    private readonly SourceMethod _sourceMethod;
    private readonly IReadOnlyDictionary<string, WrapperRecognizer> _recognizers;
    private readonly DurableDocConfig? _config;
    private readonly List<WorkflowNode> _nodes = [];
    private readonly List<WorkflowEdge> _edges = [];
    private readonly List<WorkflowIssue> _issues = [];
    private readonly Dictionary<string, MethodDeclarationSyntax> _helperMethods;
    private readonly Dictionary<string, List<InvocationExpressionSyntax>> _deferredFanOuts = new(StringComparer.Ordinal);
    private readonly HashSet<string> _visitedHelpers = new(StringComparer.Ordinal);
    private int _nodeIndex = 1;

    private WorkflowBuilder(SourceMethod sourceMethod, IReadOnlyDictionary<string, WrapperRecognizer> recognizers, DurableDocConfig? config)
    {
        _sourceMethod = sourceMethod;
        _recognizers = recognizers;
        _config = config;
        _helperMethods = BuildHelperMap(sourceMethod.Method);
    }

    public static WorkflowDiagram Build(SourceMethod sourceMethod, IReadOnlyDictionary<string, WrapperRecognizer> recognizers, DurableDocConfig? config)
    {
        var builder = new WorkflowBuilder(sourceMethod, recognizers, config);
        return builder.Build();
    }

    private WorkflowDiagram Build()
    {
        var method = _sourceMethod.Method;
        var span = method.GetLocation().GetLineSpan();
        var startNode = new WorkflowNode
        {
            Id = "n0",
            DisplayLabel = method.Identifier.ValueText,
            NodeType = WorkflowNodeType.OrchestratorStart,
            Name = method.Identifier.ValueText,
            SourceFile = span.Path,
            LineNumber = span.StartLinePosition.Line + 1,
        };

        _nodes.Add(startNode);
        ProcessStatements(GetStatements(method), [new PendingEdge(startNode.Id, null)], insideBranch: false);

        var diagram = new WorkflowDiagram
        {
            Id = $"{method.Identifier.ValueText}:{span.StartLinePosition.Line + 1}",
            OrchestratorName = method.Identifier.ValueText,
            SourceFile = span.Path,
            SourceProjectPath = _sourceMethod.ProjectPath,
            Nodes = _nodes,
            Edges = _edges,
            Diagnostics = _issues,
        };

        return BusinessMetadataApplicator.Apply(diagram, _config);
    }

    private IReadOnlyList<PendingEdge> ProcessStatements(IEnumerable<StatementSyntax> statements, IReadOnlyList<PendingEdge> incoming, bool insideBranch)
    {
        var current = incoming;

        foreach (var statement in statements)
        {
            current = ProcessStatement(statement, current, insideBranch);
        }

        return current;
    }

    private IReadOnlyList<PendingEdge> ProcessStatement(StatementSyntax statement, IReadOnlyList<PendingEdge> incoming, bool insideBranch)
    {
        return statement switch
        {
            BlockSyntax block => ProcessStatements(block.Statements, incoming, insideBranch),
            IfStatementSyntax ifStatement => ProcessIf(ifStatement, incoming),
            SwitchStatementSyntax switchStatement => ProcessSwitch(switchStatement, incoming),
            LocalDeclarationStatementSyntax localDeclaration => ProcessLocalDeclaration(localDeclaration, incoming),
            ExpressionStatementSyntax expressionStatement => ProcessExpression(expressionStatement.Expression, incoming),
            ReturnStatementSyntax returnStatement when returnStatement.Expression is not null => ProcessExpression(returnStatement.Expression, incoming),
            TryStatementSyntax tryStatement => ProcessTry(tryStatement, incoming),
            ForEachStatementSyntax forEachStatement => ProcessStatements(GetEmbeddedStatements(forEachStatement.Statement), incoming, insideBranch),
            UsingStatementSyntax usingStatement => usingStatement.Statement is null
                ? incoming
                : ProcessStatements(GetEmbeddedStatements(usingStatement.Statement), incoming, insideBranch),
            _ => ProcessNestedInvocations(statement.DescendantNodes().OfType<InvocationExpressionSyntax>(), incoming),
        };
    }

    private IReadOnlyList<PendingEdge> ProcessIf(IfStatementSyntax ifStatement, IReadOnlyList<PendingEdge> incoming)
    {
        var decision = CreateNode(ifStatement.Condition.ToString(), WorkflowNodeType.Decision, ifStatement.GetLocation().GetLineSpan(), technicalName: ifStatement.Condition.ToString());
        Connect(incoming, decision.Id);

        var thenOutgoing = ProcessStatements(
            GetEmbeddedStatements(ifStatement.Statement),
            [new PendingEdge(decision.Id, "true")],
            insideBranch: true);

        IReadOnlyList<PendingEdge> elseOutgoing;
        if (ifStatement.Else is null)
        {
            elseOutgoing = [new PendingEdge(decision.Id, "false")];
        }
        else
        {
            elseOutgoing = ProcessStatements(
                GetEmbeddedStatements(ifStatement.Else.Statement),
                [new PendingEdge(decision.Id, "false")],
                insideBranch: true);
        }

        return thenOutgoing.Concat(elseOutgoing).ToArray();
    }

    private IReadOnlyList<PendingEdge> ProcessSwitch(SwitchStatementSyntax switchStatement, IReadOnlyList<PendingEdge> incoming)
    {
        var decision = CreateNode(switchStatement.Expression.ToString(), WorkflowNodeType.Decision, switchStatement.GetLocation().GetLineSpan(), technicalName: switchStatement.Expression.ToString());
        Connect(incoming, decision.Id);

        var terminals = new List<PendingEdge>();
        foreach (var section in switchStatement.Sections)
        {
            var label = string.Join(", ", section.Labels.Select(FormatSwitchLabel));
            var sectionTerminals = ProcessStatements(section.Statements, [new PendingEdge(decision.Id, label)], insideBranch: true);
            terminals.AddRange(sectionTerminals);
        }

        return terminals.Count == 0 ? [new PendingEdge(decision.Id, null)] : terminals;
    }

    private IReadOnlyList<PendingEdge> ProcessLocalDeclaration(LocalDeclarationStatementSyntax statement, IReadOnlyList<PendingEdge> incoming)
    {
        foreach (var variable in statement.Declaration.Variables)
        {
            if (variable.Initializer?.Value is not { } initializer)
            {
                continue;
            }

            var invocations = ExtractCollectionInvocations(initializer).ToArray();
            if (invocations.Length > 0)
            {
                _deferredFanOuts[variable.Identifier.ValueText] = invocations.ToList();
                continue;
            }

            incoming = ProcessExpression(initializer, incoming);
        }

        return incoming;
    }

    private IReadOnlyList<PendingEdge> ProcessExpression(ExpressionSyntax expression, IReadOnlyList<PendingEdge> incoming)
    {
        if (expression is AwaitExpressionSyntax awaitExpression)
        {
            return ProcessExpression(awaitExpression.Expression, incoming);
        }

        if (expression is InvocationExpressionSyntax invocation)
        {
            if (IsTaskWhenAll(invocation))
            {
                return ProcessFanOut(invocation, incoming);
            }

            if (TryCreateDurableNode(invocation, incoming, out var durableOutgoing))
            {
                return durableOutgoing;
            }

            if (TryInlineHelper(invocation, incoming, out var helperOutgoing))
            {
                return helperOutgoing;
            }

            var nestedInvocations = invocation.ArgumentList.Arguments
                .SelectMany(argument => argument.DescendantNodesAndSelf().OfType<InvocationExpressionSyntax>())
                .ToArray();

            return ProcessNestedInvocations(nestedInvocations, incoming);
        }

        return ProcessNestedInvocations(expression.DescendantNodes().OfType<InvocationExpressionSyntax>(), incoming);
    }

    private IReadOnlyList<PendingEdge> ProcessTry(TryStatementSyntax tryStatement, IReadOnlyList<PendingEdge> incoming)
    {
        if (tryStatement.Catches.Count > 0)
        {
            ReportIssue(
                "unsupported-try-catch",
                "Try/catch blocks are only partially modeled. Catch and finally paths may need manual review.",
                tryStatement.GetLocation().GetLineSpan());
        }

        var current = ProcessStatements(tryStatement.Block.Statements, incoming, insideBranch: false);

        if (tryStatement.Finally is not null)
        {
            current = ProcessStatements(tryStatement.Finally.Block.Statements, current, insideBranch: false);
        }

        return current;
    }

    private IReadOnlyList<PendingEdge> ProcessFanOut(InvocationExpressionSyntax whenAllInvocation, IReadOnlyList<PendingEdge> incoming)
    {
        var invocations = ResolveFanOutInvocations(whenAllInvocation).ToArray();
        if (invocations.Length == 0)
        {
            ReportIssue(
                "unsupported-fanout",
                "Task.WhenAll was detected but no supported Durable calls could be resolved from its inputs.",
                whenAllInvocation.GetLocation().GetLineSpan());
            return incoming;
        }

        var fanOut = CreateNode("Fan-out", WorkflowNodeType.FanOut, whenAllInvocation.GetLocation().GetLineSpan(), technicalName: "Task.WhenAll");
        Connect(incoming, fanOut.Id);

        var taskTerminals = new List<PendingEdge>();
        foreach (var invocation in invocations)
        {
            if (!TryCreateDurableNode(invocation, [new PendingEdge(fanOut.Id, null)], out var branchOutgoing))
            {
                ReportIssue(
                    "unsupported-fanout-call",
                    $"Task.WhenAll contains an unsupported invocation '{GetInvokedMethodName(invocation)}'.",
                    invocation.GetLocation().GetLineSpan());
                continue;
            }

            taskTerminals.AddRange(branchOutgoing);
        }

        var fanIn = CreateNode("Fan-in", WorkflowNodeType.FanIn, whenAllInvocation.GetLocation().GetLineSpan(), technicalName: "Task.WhenAll");
        Connect(taskTerminals.Count == 0 ? [new PendingEdge(fanOut.Id, null)] : taskTerminals, fanIn.Id);
        return [new PendingEdge(fanIn.Id, null)];
    }

    private IReadOnlyList<PendingEdge> ProcessNestedInvocations(IEnumerable<InvocationExpressionSyntax> invocations, IReadOnlyList<PendingEdge> incoming)
    {
        var current = incoming;

        foreach (var invocation in invocations.OrderBy(i => i.SpanStart))
        {
            if (TryCreateDurableNode(invocation, current, out var durableOutgoing))
            {
                current = durableOutgoing;
                continue;
            }

            if (TryInlineHelper(invocation, current, out var helperOutgoing))
            {
                current = helperOutgoing;
            }
        }

        return current;
    }

    private bool TryInlineHelper(InvocationExpressionSyntax invocation, IReadOnlyList<PendingEdge> incoming, out IReadOnlyList<PendingEdge> outgoing)
    {
        outgoing = incoming;

        if (!TryResolveHelperMethod(invocation, out var helperMethod))
        {
            return false;
        }

        var helperKey = GetMethodKey(helperMethod);
        if (!_visitedHelpers.Add(helperKey))
        {
            ReportIssue(
                "recursive-helper",
                $"Helper method '{helperMethod.Identifier.ValueText}' is recursive and was not expanded.",
                helperMethod.GetLocation().GetLineSpan());
            return false;
        }

        try
        {
            outgoing = ProcessStatements(GetStatements(helperMethod), incoming, insideBranch: false);
            return true;
        }
        finally
        {
            _visitedHelpers.Remove(helperKey);
        }
    }

    private bool TryCreateDurableNode(InvocationExpressionSyntax invocation, IReadOnlyList<PendingEdge> incoming, out IReadOnlyList<PendingEdge> outgoing)
    {
        outgoing = incoming;
        var methodName = GetInvokedMethodName(invocation);
        if (!_recognizers.TryGetValue(methodName, out var recognizer))
        {
            return false;
        }

        var span = invocation.GetLocation().GetLineSpan();
        var stepName = ExtractStepName(invocation, recognizer, methodName, span);
        var retryHint = recognizer.NodeType is WorkflowNodeType.RetryActivity or WorkflowNodeType.RetrySubOrchestrator
            ? "Retry policy configured via Durable API wrapper."
            : null;
        var node = CreateNode(stepName, recognizer.NodeType, span, technicalName: methodName, retryHint: retryHint);
        Connect(incoming, node.Id);

        outgoing = [new PendingEdge(node.Id, null)];
        return true;
    }

    private bool TryResolveHelperMethod(InvocationExpressionSyntax invocation, out MethodDeclarationSyntax helperMethod)
    {
        var methodName = GetInvokedMethodName(invocation);
        var key = BuildHelperLookupKey(methodName, invocation.ArgumentList.Arguments.Count);
        if (_helperMethods.TryGetValue(key, out var resolved))
        {
            helperMethod = resolved;
            return true;
        }

        helperMethod = null!;
        return false;
    }

    private IEnumerable<InvocationExpressionSyntax> ResolveFanOutInvocations(InvocationExpressionSyntax whenAllInvocation)
    {
        foreach (var argument in whenAllInvocation.ArgumentList.Arguments)
        {
            if (argument.Expression is IdentifierNameSyntax identifier &&
                _deferredFanOuts.TryGetValue(identifier.Identifier.ValueText, out var deferred))
            {
                foreach (var invocation in deferred)
                {
                    yield return invocation;
                }

                continue;
            }

            foreach (var invocation in argument.Expression.DescendantNodesAndSelf().OfType<InvocationExpressionSyntax>())
            {
                if (_recognizers.ContainsKey(GetInvokedMethodName(invocation)))
                {
                    yield return invocation;
                }
            }
        }
    }

    private static IEnumerable<StatementSyntax> GetStatements(MethodDeclarationSyntax method)
    {
        if (method.Body is not null)
        {
            return method.Body.Statements;
        }

        if (method.ExpressionBody is not null)
        {
            return [SyntaxFactory.ExpressionStatement(method.ExpressionBody.Expression)];
        }

        return [];
    }

    private static IEnumerable<StatementSyntax> GetEmbeddedStatements(StatementSyntax statement)
    {
        return statement is BlockSyntax block ? block.Statements : [statement];
    }

    private static Dictionary<string, MethodDeclarationSyntax> BuildHelperMap(MethodDeclarationSyntax rootMethod)
    {
        var methods = new Dictionary<string, MethodDeclarationSyntax>(StringComparer.Ordinal);
        var containingType = rootMethod.Parent;
        if (containingType is null)
        {
            return methods;
        }

        foreach (var method in containingType.ChildNodes().OfType<MethodDeclarationSyntax>())
        {
            methods[BuildHelperLookupKey(method.Identifier.ValueText, method.ParameterList.Parameters.Count)] = method;
        }

        return methods;
    }

    private static string BuildHelperLookupKey(string name, int parameterCount)
        => $"{name}|{parameterCount}";

    private static string GetMethodKey(MethodDeclarationSyntax method)
        => $"{method.SyntaxTree.FilePath}|{method.Identifier.ValueText}|{method.SpanStart}";

    private static bool IsTaskWhenAll(InvocationExpressionSyntax invocation)
        => string.Equals(GetInvokedMethodName(invocation), "WhenAll", StringComparison.Ordinal);

    private static string GetInvokedMethodName(InvocationExpressionSyntax invocation)
    {
        return invocation.Expression switch
        {
            MemberAccessExpressionSyntax memberAccess => memberAccess.Name.Identifier.ValueText,
            IdentifierNameSyntax identifierName => identifierName.Identifier.ValueText,
            GenericNameSyntax genericName => genericName.Identifier.ValueText,
            _ => invocation.Expression.ToString(),
        };
    }

    private static IEnumerable<InvocationExpressionSyntax> ExtractCollectionInvocations(ExpressionSyntax initializer)
    {
        return initializer switch
        {
            ArrayCreationExpressionSyntax arrayCreation when arrayCreation.Initializer is not null =>
                arrayCreation.Initializer.Expressions.SelectMany(CollectInvocations),
            ImplicitArrayCreationExpressionSyntax implicitArray when implicitArray.Initializer is not null =>
                implicitArray.Initializer.Expressions.SelectMany(CollectInvocations),
            CollectionExpressionSyntax collectionExpression =>
                collectionExpression.Elements.OfType<ExpressionElementSyntax>().SelectMany(element => CollectInvocations(element.Expression)),
            InitializerExpressionSyntax directInitializer =>
                directInitializer.Expressions.SelectMany(CollectInvocations),
            _ => [],
        };
    }

    private static IEnumerable<InvocationExpressionSyntax> CollectInvocations(ExpressionSyntax expression)
        => expression.DescendantNodesAndSelf().OfType<InvocationExpressionSyntax>();

    private string ExtractStepName(InvocationExpressionSyntax invocation, WrapperRecognizer recognizer, string methodName, FileLinePositionSpan span)
    {
        if (recognizer.NodeType == WorkflowNodeType.Timer)
        {
            return methodName;
        }

        var index = recognizer.TargetNameArgumentIndex ?? 0;
        if (index >= invocation.ArgumentList.Arguments.Count)
        {
            ReportIssue(
                "missing-target-argument",
                $"Invocation '{methodName}' does not expose the configured target-name argument index {index}.",
                span);
            return methodName;
        }

        var expression = invocation.ArgumentList.Arguments[index].Expression;
        if (TryGetStringValue(expression, out var resolved))
        {
            return resolved;
        }

        ReportIssue(
            "unresolved-target-name",
            $"Invocation '{methodName}' uses a non-literal target name and could not be resolved statically.",
            span);
        return methodName;
    }

    private static bool TryGetStringValue(ExpressionSyntax expression, out string value)
    {
        switch (expression)
        {
            case LiteralExpressionSyntax literal when literal.IsKind(SyntaxKind.StringLiteralExpression):
                value = literal.Token.ValueText;
                return true;
            case InvocationExpressionSyntax nameofInvocation when nameofInvocation.Expression.ToString() == "nameof":
                value = nameofInvocation.ArgumentList.Arguments.FirstOrDefault()?.Expression.ToString() ?? "nameof";
                return true;
            case IdentifierNameSyntax identifierName:
                value = identifierName.Identifier.ValueText;
                return true;
            default:
                value = string.Empty;
                return false;
        }
    }

    private WorkflowNode CreateNode(string displayLabel, WorkflowNodeType nodeType, FileLinePositionSpan span, string? technicalName, string? retryHint = null)
    {
        var node = new WorkflowNode
        {
            Id = $"n{_nodeIndex++}",
            DisplayLabel = displayLabel,
            NodeType = nodeType,
            Name = displayLabel,
            TechnicalNameOverride = technicalName,
            RetryHint = retryHint,
            SourceFile = span.Path,
            LineNumber = span.StartLinePosition.Line + 1,
        };

        _nodes.Add(node);
        return node;
    }

    private void Connect(IReadOnlyList<PendingEdge> incoming, string targetNodeId)
    {
        foreach (var edge in incoming)
        {
            _edges.Add(new WorkflowEdge
            {
                FromNodeId = edge.NodeId,
                ToNodeId = targetNodeId,
                ConditionLabel = edge.Label,
            });
        }
    }

    private void ReportIssue(string code, string message, FileLinePositionSpan span, WorkflowIssueSeverity severity = WorkflowIssueSeverity.Warning)
    {
        _issues.Add(new WorkflowIssue
        {
            Severity = severity,
            Code = code,
            Message = message,
            SourceFile = span.Path,
            LineNumber = span.StartLinePosition.Line + 1,
        });
    }

    private static string FormatSwitchLabel(SwitchLabelSyntax label)
    {
        return label switch
        {
            CaseSwitchLabelSyntax caseLabel => caseLabel.Value.ToString(),
            DefaultSwitchLabelSyntax => "default",
            CasePatternSwitchLabelSyntax patternLabel => patternLabel.Pattern.ToString(),
            _ => label.ToString(),
        };
    }

    private readonly record struct PendingEdge(string NodeId, string? Label);
}

internal static class BusinessMetadataApplicator
{
    public static WorkflowDiagram Apply(WorkflowDiagram diagram, DurableDocConfig? config)
    {
        var orchestratorMetadata = config?.BusinessView?.Orchestrators?
            .FirstOrDefault(entry => string.Equals(entry.Name, diagram.OrchestratorName, StringComparison.OrdinalIgnoreCase));

        if (orchestratorMetadata is null)
        {
            return diagram;
        }

        var stepOverrides = (orchestratorMetadata.Steps ?? [])
            .Where(step => !string.IsNullOrWhiteSpace(step.Name))
            .ToDictionary(step => step.Name, StringComparer.OrdinalIgnoreCase);

        var nodes = diagram.Nodes.Select(node =>
        {
            if (node.NodeType == WorkflowNodeType.OrchestratorStart)
            {
                return new WorkflowNode
                {
                    Id = node.Id,
                    DisplayLabel = string.IsNullOrWhiteSpace(orchestratorMetadata.BusinessName) ? node.DisplayLabel : orchestratorMetadata.BusinessName!,
                    NodeType = node.NodeType,
                    Name = node.Name,
                    BusinessName = string.IsNullOrWhiteSpace(orchestratorMetadata.BusinessName) ? node.BusinessName : orchestratorMetadata.BusinessName,
                    BusinessGroup = node.BusinessGroup,
                    HideInBusiness = node.HideInBusiness,
                    Notes = string.IsNullOrWhiteSpace(orchestratorMetadata.Notes) ? node.Notes : orchestratorMetadata.Notes,
                    TechnicalNameOverride = node.TechnicalNameOverride,
                    RetryHint = node.RetryHint,
                    SourceFile = node.SourceFile,
                    LineNumber = node.LineNumber,
                };
            }

            if (!stepOverrides.TryGetValue(node.DisplayLabel, out var step) &&
                !stepOverrides.TryGetValue(node.Name, out step))
            {
                return node;
            }

            return new WorkflowNode
            {
                Id = node.Id,
                DisplayLabel = string.IsNullOrWhiteSpace(step.TechnicalName) ? node.DisplayLabel : step.TechnicalName!,
                NodeType = node.NodeType,
                Name = string.IsNullOrWhiteSpace(step.TechnicalName) ? node.Name : step.TechnicalName!,
                BusinessName = string.IsNullOrWhiteSpace(step.BusinessName) ? node.BusinessName : step.BusinessName,
                BusinessGroup = string.IsNullOrWhiteSpace(step.BusinessGroup) ? node.BusinessGroup : step.BusinessGroup,
                HideInBusiness = step.HideInBusiness || node.HideInBusiness,
                Notes = string.IsNullOrWhiteSpace(step.Notes) ? node.Notes : step.Notes,
                TechnicalNameOverride = string.IsNullOrWhiteSpace(step.TechnicalName) ? node.TechnicalNameOverride : step.TechnicalName,
                RetryHint = node.RetryHint,
                SourceFile = node.SourceFile,
                LineNumber = node.LineNumber,
            };
        }).ToArray();

        return new WorkflowDiagram
        {
            Id = diagram.Id,
            OrchestratorName = diagram.OrchestratorName,
            SourceFile = diagram.SourceFile,
            SourceProjectPath = diagram.SourceProjectPath,
            CreatedTimestamp = diagram.CreatedTimestamp,
            Nodes = nodes,
            Edges = diagram.Edges,
            Diagnostics = diagram.Diagnostics,
        };
    }
}

internal sealed record WrapperRecognizer(WorkflowNodeType NodeType, int? TargetNameArgumentIndex);

internal sealed record SourceMethod(MethodDeclarationSyntax Method, string? ProjectPath);

internal sealed class WorkspaceLoadResult
{
    public string ResolvedInputPath { get; init; } = string.Empty;

    public WorkflowInputKind InputKind { get; init; }

    public IReadOnlyList<string> ScannedProjects { get; init; } = [];

    public IReadOnlyList<SourceMethod> Methods { get; init; } = [];
}

internal static class WorkspaceSourceLoader
{
    private static int _msbuildInitialized;

    public static async Task<WorkspaceLoadResult> LoadAsync(string inputPath, DurableDocConfig? config, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(inputPath))
        {
            throw new ArgumentException("Input path is required.", nameof(inputPath));
        }

        var fullPath = Path.GetFullPath(inputPath);
        if (File.Exists(fullPath))
        {
            var extension = Path.GetExtension(fullPath);
            if (extension.Equals(".sln", StringComparison.OrdinalIgnoreCase))
            {
                return await LoadFromSolutionAsync(fullPath, config, cancellationToken).ConfigureAwait(false);
            }

            if (extension.Equals(".csproj", StringComparison.OrdinalIgnoreCase))
            {
                return await LoadFromProjectAsync(fullPath, config, cancellationToken).ConfigureAwait(false);
            }

            if (extension.Equals(".cs", StringComparison.OrdinalIgnoreCase))
            {
                return await LoadFromFilesAsync([fullPath], WorkflowInputKind.File, config, cancellationToken).ConfigureAwait(false);
            }
        }

        if (Directory.Exists(fullPath))
        {
            var files = Directory.EnumerateFiles(fullPath, "*.cs", SearchOption.AllDirectories)
                .Where(file => MatchesPathFilter(file, fullPath, config))
                .OrderBy(file => file, StringComparer.Ordinal)
                .ToArray();

            return await LoadFromFilesAsync(files, WorkflowInputKind.Directory, config, cancellationToken).ConfigureAwait(false);
        }

        throw new FileNotFoundException($"Input path was not found: {inputPath}");
    }

    private static async Task<WorkspaceLoadResult> LoadFromSolutionAsync(string solutionPath, DurableDocConfig? config, CancellationToken cancellationToken)
    {
        InitializeMsBuild();
        using var workspace = MSBuildWorkspace.Create();
        var solution = await workspace.OpenSolutionAsync(solutionPath, cancellationToken: cancellationToken).ConfigureAwait(false);

        var methods = new List<SourceMethod>();
        var scannedProjects = new List<string>();
        foreach (var project in solution.Projects.OrderBy(project => project.FilePath, StringComparer.OrdinalIgnoreCase))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (project.FilePath is not null)
            {
                scannedProjects.Add(project.FilePath);
            }

            methods.AddRange(await GetMethodsFromProjectAsync(project, config, cancellationToken).ConfigureAwait(false));
        }

        return new WorkspaceLoadResult
        {
            ResolvedInputPath = solutionPath,
            InputKind = WorkflowInputKind.Solution,
            ScannedProjects = scannedProjects,
            Methods = methods,
        };
    }

    private static async Task<WorkspaceLoadResult> LoadFromProjectAsync(string projectPath, DurableDocConfig? config, CancellationToken cancellationToken)
    {
        InitializeMsBuild();
        using var workspace = MSBuildWorkspace.Create();
        var project = await workspace.OpenProjectAsync(projectPath, cancellationToken: cancellationToken).ConfigureAwait(false);
        var methods = await GetMethodsFromProjectAsync(project, config, cancellationToken).ConfigureAwait(false);

        return new WorkspaceLoadResult
        {
            ResolvedInputPath = projectPath,
            InputKind = WorkflowInputKind.Project,
            ScannedProjects = [projectPath],
            Methods = methods,
        };
    }

    private static async Task<WorkspaceLoadResult> LoadFromFilesAsync(IReadOnlyList<string> files, WorkflowInputKind inputKind, DurableDocConfig? config, CancellationToken cancellationToken)
    {
        var methods = new List<SourceMethod>();
        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var sourceText = await File.ReadAllTextAsync(file, cancellationToken).ConfigureAwait(false);
            var syntaxTree = CSharpSyntaxTree.ParseText(sourceText, path: file, cancellationToken: cancellationToken);
            methods.AddRange(ExtractMethods(syntaxTree, null));
        }

        return new WorkspaceLoadResult
        {
            ResolvedInputPath = files.FirstOrDefault() ?? string.Empty,
            InputKind = inputKind,
            Methods = methods,
        };
    }

    private static async Task<IReadOnlyList<SourceMethod>> GetMethodsFromProjectAsync(Project project, DurableDocConfig? config, CancellationToken cancellationToken)
    {
        var methods = new List<SourceMethod>();
        var projectRoot = Path.GetDirectoryName(project.FilePath ?? string.Empty) ?? Directory.GetCurrentDirectory();

        foreach (var document in project.Documents.OrderBy(document => document.FilePath, StringComparer.OrdinalIgnoreCase))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (document.FilePath is null || !document.FilePath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!MatchesPathFilter(document.FilePath, projectRoot, config))
            {
                continue;
            }

            var syntaxTree = await document.GetSyntaxTreeAsync(cancellationToken).ConfigureAwait(false);
            if (syntaxTree is null)
            {
                continue;
            }

            methods.AddRange(ExtractMethods(syntaxTree, project.FilePath));
        }

        return methods;
    }

    private static IEnumerable<SourceMethod> ExtractMethods(SyntaxTree syntaxTree, string? projectPath)
    {
        var root = syntaxTree.GetRoot();
        return root.DescendantNodes().OfType<MethodDeclarationSyntax>().Select(method => new SourceMethod(method, projectPath));
    }

    private static bool MatchesPathFilter(string path, string rootPath, DurableDocConfig? config)
    {
        var includePatterns = config?.Analysis?.IncludePatterns ?? [];
        var excludePatterns = config?.Analysis?.ExcludePatterns ?? [];
        var relativePath = Path.GetRelativePath(rootPath, path).Replace('\\', '/');
        var fileName = Path.GetFileName(path);

        var included = includePatterns.Count == 0 || includePatterns.Any(pattern => MatchesPattern(pattern, relativePath, fileName));
        if (!included)
        {
            return false;
        }

        return !excludePatterns.Any(pattern => MatchesPattern(pattern, relativePath, fileName));
    }

    private static bool MatchesPattern(string pattern, string relativePath, string fileName)
    {
        var normalized = pattern.Replace('\\', '/').Trim();
        if (normalized.StartsWith("**/", StringComparison.Ordinal))
        {
            normalized = normalized[3..];
        }

        return FileSystemName.MatchesSimpleExpression(normalized, relativePath, ignoreCase: true)
            || FileSystemName.MatchesSimpleExpression(normalized, fileName, ignoreCase: true);
    }

    private static void InitializeMsBuild()
    {
        if (Interlocked.Exchange(ref _msbuildInitialized, 1) == 1)
        {
            return;
        }

        if (!MSBuildLocator.IsRegistered)
        {
            MSBuildLocator.RegisterDefaults();
        }
    }
}
