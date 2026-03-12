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

    private static readonly Dictionary<string, WorkflowNodeType> DurableMethodToNodeType = new(StringComparer.Ordinal)
    {
        ["CallActivityAsync"] = WorkflowNodeType.Activity,
        ["CallSubOrchestratorAsync"] = WorkflowNodeType.SubOrchestrator,
        ["CallActivityWithRetryAsync"] = WorkflowNodeType.RetryActivity,
        ["WaitForExternalEvent"] = WorkflowNodeType.ExternalEvent,
        ["CreateTimer"] = WorkflowNodeType.Timer,
    };

    public async Task<IReadOnlyList<WorkflowDiagram>> AnalyzeAsync(string inputPath, DurableDocConfig? config = null, CancellationToken cancellationToken = default)
    {
        var sourceMethods = await WorkspaceSourceLoader.LoadAsync(inputPath, cancellationToken).ConfigureAwait(false);
        var wrapperMap = BuildWrapperMap(config);
        var businessOverlayMap = BusinessOverlayMap.Create(config);

        var diagrams = new List<WorkflowDiagram>();
        foreach (var sourceMethod in sourceMethods.Where(IsOrchestrator).OrderBy(x => x.Method.Identifier.ValueText, StringComparer.Ordinal))
        {
            var diagram = BuildDiagram(sourceMethod, wrapperMap);
            diagrams.Add(BusinessOverlayApplicator.Apply(diagram, businessOverlayMap));
        }

        return diagrams;
    }

    private static Dictionary<string, WorkflowNodeType> BuildWrapperMap(DurableDocConfig? config)
    {
        var map = new Dictionary<string, WorkflowNodeType>(StringComparer.Ordinal);
        if (config?.Analysis?.Wrappers is null)
        {
            return map;
        }

        foreach (var wrapper in config.Analysis.Wrappers)
        {
            if (Enum.TryParse<WorkflowNodeType>(wrapper.Kind, ignoreCase: true, out var parsed))
            {
                map[wrapper.MethodName] = parsed;
            }
            else
            {
                map[wrapper.MethodName] = WorkflowNodeType.Wrapper;
            }
        }

        return map;
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

        return hasTriggerAttribute || hasContextParameter;
    }

    private static WorkflowDiagram BuildDiagram(SourceMethod sourceMethod, Dictionary<string, WorkflowNodeType> wrapperMap)
    {
        var builder = new WorkflowGraphBuilder(sourceMethod, wrapperMap);
        return builder.Build();
    }

    private static string GetInvokedMethodName(InvocationExpressionSyntax invocation)
    {
        return invocation.Expression switch
        {
            MemberAccessExpressionSyntax memberAccess => memberAccess.Name.Identifier.ValueText,
            IdentifierNameSyntax identifierName => identifierName.Identifier.ValueText,
            _ => invocation.Expression.ToString(),
        };
    }

    private static WorkflowNodeType? ResolveNodeType(string methodName, Dictionary<string, WorkflowNodeType> wrapperMap)
    {
        if (DurableMethodToNodeType.TryGetValue(methodName, out var parsed))
        {
            return parsed;
        }

        if (wrapperMap.TryGetValue(methodName, out var wrapperType))
        {
            return wrapperType;
        }

        return null;
    }

    private static string ExtractStepName(InvocationExpressionSyntax invocation, string methodName)
    {
        var firstArgument = invocation.ArgumentList.Arguments.FirstOrDefault()?.Expression;
        return firstArgument is LiteralExpressionSyntax literal && literal.IsKind(SyntaxKind.StringLiteralExpression)
            ? literal.Token.ValueText
            : methodName;
    }

    private sealed class WorkflowGraphBuilder
    {
        private readonly SourceMethod _sourceMethod;
        private readonly Dictionary<string, WorkflowNodeType> _wrapperMap;
        private readonly List<WorkflowNode> _nodes = [];
        private readonly List<WorkflowEdge> _edges = [];
        private readonly HashSet<string> _edgeKeys = new(StringComparer.Ordinal);
        private readonly Dictionary<string, PendingTaskBinding> _pendingTasks = new(StringComparer.Ordinal);
        private int _nextNodeIndex;
        private int _pendingSequence;

        public WorkflowGraphBuilder(SourceMethod sourceMethod, Dictionary<string, WorkflowNodeType> wrapperMap)
        {
            _sourceMethod = sourceMethod;
            _wrapperMap = wrapperMap;
        }

        public WorkflowDiagram Build()
        {
            var method = _sourceMethod.Method;
            var span = method.GetLocation().GetLineSpan();
            var startNode = CreateNode(
                method.Identifier.ValueText,
                WorkflowNodeType.OrchestratorStart,
                method.GetLocation());

            var tails = new List<FlowTail> { new(startNode.Id) };
            if (method.Body is not null)
            {
                tails = AppendStatements(method.Body.Statements, tails);
            }
            else if (method.ExpressionBody is not null)
            {
                tails = AppendExpression(method.ExpressionBody.Expression, tails);
            }

            FlushDanglingPendingTasks();

            return new WorkflowDiagram
            {
                Id = $"{method.Identifier.ValueText}:{span.StartLinePosition.Line + 1}",
                OrchestratorName = method.Identifier.ValueText,
                SourceFile = span.Path,
                SourceProjectPath = _sourceMethod.ProjectPath,
                Nodes = _nodes,
                Edges = _edges,
            };
        }

        private List<FlowTail> AppendStatements(IEnumerable<StatementSyntax> statements, List<FlowTail> currentTails)
        {
            var tails = currentTails;
            foreach (var statement in statements)
            {
                tails = AppendStatement(statement, tails);
            }

            return tails;
        }

        private List<FlowTail> AppendStatement(StatementSyntax statement, List<FlowTail> currentTails)
        {
            return statement switch
            {
                BlockSyntax block => AppendStatements(block.Statements, currentTails),
                IfStatementSyntax ifStatement => AppendIf(ifStatement, currentTails),
                LocalDeclarationStatementSyntax localDeclaration => AppendLocalDeclaration(localDeclaration, currentTails),
                ExpressionStatementSyntax expressionStatement => AppendExpression(expressionStatement.Expression, currentTails),
                ReturnStatementSyntax returnStatement when returnStatement.Expression is not null => AppendExpression(returnStatement.Expression, currentTails),
                _ => AppendFallback(statement, currentTails),
            };
        }

        private List<FlowTail> AppendIf(IfStatementSyntax ifStatement, List<FlowTail> currentTails)
        {
            var conditionLabel = FormatConditionLabel(ifStatement.Condition);
            var decisionNode = CreateNode(
                conditionLabel,
                WorkflowNodeType.Decision,
                ifStatement.IfKeyword.GetLocation());

            Connect(currentTails, decisionNode.Id);

            var trueTails = AppendStatements(
                NormalizeStatements(ifStatement.Statement),
                [new FlowTail(decisionNode.Id, conditionLabel)]);

            var falseTails = ifStatement.Else is null
                ? [new FlowTail(decisionNode.Id, "else")]
                : AppendStatements(NormalizeStatements(ifStatement.Else.Statement), [new FlowTail(decisionNode.Id, "else")]);

            return MergeTails(trueTails, falseTails);
        }

        private List<FlowTail> AppendLocalDeclaration(LocalDeclarationStatementSyntax declaration, List<FlowTail> currentTails)
        {
            var tails = currentTails;

            foreach (var variable in declaration.Declaration.Variables)
            {
                if (variable.Initializer is null)
                {
                    continue;
                }

                if (TryStorePendingTask(variable, variable.Initializer.Value, tails))
                {
                    continue;
                }

                tails = AppendExpression(variable.Initializer.Value, tails);
            }

            return tails;
        }

        private List<FlowTail> AppendExpression(ExpressionSyntax expression, List<FlowTail> currentTails)
        {
            if (TryAppendWhenAll(expression, currentTails, out var whenAllTails))
            {
                return whenAllTails;
            }

            if (TryAppendAwaitedPendingTask(expression, out var pendingTails))
            {
                return pendingTails;
            }

            if (TryExtractDurableInvocation(expression, out var durableInvocation))
            {
                var node = CreateNode(
                    durableInvocation.StepName,
                    durableInvocation.NodeType,
                    durableInvocation.Location);

                Connect(currentTails, node.Id);
                return [new FlowTail(node.Id)];
            }

            return currentTails;
        }

        private bool TryStorePendingTask(VariableDeclaratorSyntax variable, ExpressionSyntax expression, List<FlowTail> currentTails)
        {
            if (expression is AwaitExpressionSyntax)
            {
                return false;
            }

            if (!TryExtractDurableInvocation(expression, out var durableInvocation))
            {
                return false;
            }

            var node = CreateNode(
                durableInvocation.StepName,
                durableInvocation.NodeType,
                durableInvocation.Location);

            _pendingTasks[variable.Identifier.ValueText] = new PendingTaskBinding(
                variable.Identifier.ValueText,
                node.Id,
                CopyTails(currentTails),
                _pendingSequence++);

            return true;
        }

        private bool TryAppendAwaitedPendingTask(ExpressionSyntax expression, out List<FlowTail> tails)
        {
            tails = [];

            if (expression is not AwaitExpressionSyntax awaitExpression)
            {
                return false;
            }

            if (awaitExpression.Expression is not IdentifierNameSyntax identifier ||
                !_pendingTasks.Remove(identifier.Identifier.ValueText, out var pending))
            {
                return false;
            }

            Connect(pending.SourceTails, pending.NodeId);
            tails = [new FlowTail(pending.NodeId)];
            return true;
        }

        private bool TryAppendWhenAll(ExpressionSyntax expression, List<FlowTail> currentTails, out List<FlowTail> tails)
        {
            tails = currentTails;

            if (!TryGetWhenAllInvocation(expression, out var invocation))
            {
                return false;
            }

            var resolvedTasks = ResolveWhenAllTasks(invocation, currentTails);
            if (resolvedTasks.Count == 0)
            {
                return false;
            }

            if (resolvedTasks.Count == 1)
            {
                var singleTask = resolvedTasks[0];
                Connect(singleTask.SourceTails, singleTask.NodeId);
                tails = [new FlowTail(singleTask.NodeId)];
                return true;
            }

            var fanOutNode = CreateNode("Fan-out", WorkflowNodeType.FanOut, invocation.GetLocation());
            Connect(MergeTaskSourceTails(resolvedTasks), fanOutNode.Id);

            foreach (var task in resolvedTasks)
            {
                Connect([new FlowTail(fanOutNode.Id)], task.NodeId);
            }

            var fanInNode = CreateNode("Fan-in", WorkflowNodeType.FanIn, invocation.GetLocation());
            Connect(resolvedTasks.Select(task => new FlowTail(task.NodeId)).ToList(), fanInNode.Id);
            tails = [new FlowTail(fanInNode.Id)];
            return true;
        }

        private List<ResolvedParallelTask> ResolveWhenAllTasks(InvocationExpressionSyntax invocation, List<FlowTail> currentTails)
        {
            var tasks = new List<ResolvedParallelTask>();

            foreach (var argument in invocation.ArgumentList.Arguments.SelectMany(argument => ExpandWhenAllArguments(argument.Expression)))
            {
                if (argument is IdentifierNameSyntax identifier &&
                    _pendingTasks.Remove(identifier.Identifier.ValueText, out var pending))
                {
                    tasks.Add(new ResolvedParallelTask(pending.NodeId, pending.SourceTails, pending.Order));
                    continue;
                }

                if (!TryExtractDurableInvocation(argument, out var durableInvocation))
                {
                    continue;
                }

                var node = CreateNode(
                    durableInvocation.StepName,
                    durableInvocation.NodeType,
                    durableInvocation.Location);

                tasks.Add(new ResolvedParallelTask(node.Id, CopyTails(currentTails), _pendingSequence++));
            }

            return tasks
                .OrderBy(task => task.Order)
                .ToList();
        }

        private List<FlowTail> AppendFallback(StatementSyntax statement, List<FlowTail> currentTails)
        {
            var tails = currentTails;

            foreach (var invocation in statement.DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                if (!TryExtractDurableInvocation(invocation, out var durableInvocation))
                {
                    continue;
                }

                var node = CreateNode(
                    durableInvocation.StepName,
                    durableInvocation.NodeType,
                    durableInvocation.Location);

                Connect(tails, node.Id);
                tails = [new FlowTail(node.Id)];
            }

            return tails;
        }

        private void FlushDanglingPendingTasks()
        {
            foreach (var pending in _pendingTasks.Values.OrderBy(binding => binding.Order))
            {
                Connect(pending.SourceTails, pending.NodeId);
            }

            _pendingTasks.Clear();
        }

        private WorkflowNode CreateNode(string displayLabel, WorkflowNodeType nodeType, Location location)
        {
            var span = location.GetLineSpan();
            var node = new WorkflowNode
            {
                Id = $"n{_nextNodeIndex++}",
                DisplayLabel = displayLabel,
                NodeType = nodeType,
                Name = displayLabel,
                SourceFile = span.Path,
                LineNumber = span.StartLinePosition.Line + 1,
            };

            _nodes.Add(node);
            return node;
        }

        private void Connect(List<FlowTail> fromTails, string toNodeId)
        {
            foreach (var fromTail in fromTails)
            {
                var edgeKey = $"{fromTail.NodeId}|{toNodeId}|{fromTail.EdgeLabel}";
                if (!_edgeKeys.Add(edgeKey))
                {
                    continue;
                }

                _edges.Add(new WorkflowEdge
                {
                    FromNodeId = fromTail.NodeId,
                    ToNodeId = toNodeId,
                    ConditionLabel = fromTail.EdgeLabel,
                });
            }
        }

        private static List<FlowTail> MergeTails(params List<FlowTail>[] groups)
        {
            return groups
                .SelectMany(group => group)
                .GroupBy(tail => $"{tail.NodeId}|{tail.EdgeLabel}", StringComparer.Ordinal)
                .Select(group => group.First())
                .ToList();
        }

        private static List<FlowTail> MergeTaskSourceTails(IEnumerable<ResolvedParallelTask> tasks)
        {
            return tasks
                .SelectMany(task => task.SourceTails)
                .GroupBy(tail => $"{tail.NodeId}|{tail.EdgeLabel}", StringComparer.Ordinal)
                .Select(group => group.First())
                .ToList();
        }

        private static List<FlowTail> CopyTails(List<FlowTail> tails)
        {
            return tails.Select(tail => new FlowTail(tail.NodeId, tail.EdgeLabel)).ToList();
        }

        private bool TryExtractDurableInvocation(ExpressionSyntax expression, out DurableInvocation durableInvocation)
        {
            var unwrapped = expression is AwaitExpressionSyntax awaitExpression
                ? awaitExpression.Expression
                : expression;

            if (unwrapped is not InvocationExpressionSyntax invocation)
            {
                durableInvocation = default;
                return false;
            }

            var methodName = GetInvokedMethodName(invocation);
            var nodeType = ResolveNodeType(methodName, _wrapperMap);
            if (nodeType is null)
            {
                durableInvocation = default;
                return false;
            }

            durableInvocation = new DurableInvocation(
                ExtractStepName(invocation, methodName),
                nodeType.Value,
                invocation.GetLocation());

            return true;
        }

        private static IEnumerable<ExpressionSyntax> ExpandWhenAllArguments(ExpressionSyntax expression)
        {
            return expression switch
            {
                ArrayCreationExpressionSyntax arrayCreation when arrayCreation.Initializer is not null => arrayCreation.Initializer.Expressions,
                ImplicitArrayCreationExpressionSyntax implicitArray when implicitArray.Initializer is not null => implicitArray.Initializer.Expressions,
                _ => [expression],
            };
        }

        private static bool TryGetWhenAllInvocation(ExpressionSyntax expression, out InvocationExpressionSyntax invocation)
        {
            invocation = expression switch
            {
                AwaitExpressionSyntax awaitExpression when awaitExpression.Expression is InvocationExpressionSyntax awaitedInvocation => awaitedInvocation,
                InvocationExpressionSyntax directInvocation => directInvocation,
                _ => null!,
            };

            if (invocation is null)
            {
                return false;
            }

            return string.Equals(GetInvokedMethodName(invocation), "WhenAll", StringComparison.Ordinal);
        }

        private static IEnumerable<StatementSyntax> NormalizeStatements(StatementSyntax statement)
        {
            return statement is BlockSyntax block ? block.Statements : [statement];
        }

        private static string FormatConditionLabel(ExpressionSyntax condition)
        {
            var raw = condition
                .ToString()
                .Replace("\r\n", " ", StringComparison.Ordinal)
                .Replace('\n', ' ')
                .Trim();

            return raw.Length <= 60 ? raw : raw[..57] + "...";
        }
    }

    private sealed class BusinessOverlayApplicator
    {
        public static WorkflowDiagram Apply(WorkflowDiagram diagram, BusinessOverlayMap overlayMap)
        {
            var overlays = overlayMap.GetForOrchestrator(diagram.OrchestratorName);
            if (overlays.Count == 0)
            {
                return diagram;
            }

            return new WorkflowDiagram
            {
                Id = diagram.Id,
                OrchestratorName = diagram.OrchestratorName,
                SourceFile = diagram.SourceFile,
                SourceProjectPath = diagram.SourceProjectPath,
                CreatedTimestamp = diagram.CreatedTimestamp,
                Nodes = diagram.Nodes.Select(node => ApplyNode(node, overlays)).ToArray(),
                Edges = diagram.Edges,
            };
        }

        private static WorkflowNode ApplyNode(WorkflowNode node, IReadOnlyDictionary<string, BusinessStepOverlay> overlays)
        {
            if (string.IsNullOrWhiteSpace(node.Name) || !overlays.TryGetValue(node.Name, out var overlay))
            {
                return node;
            }

            return new WorkflowNode
            {
                Id = node.Id,
                DisplayLabel = node.DisplayLabel,
                NodeType = node.NodeType,
                Name = node.Name,
                BusinessName = string.IsNullOrWhiteSpace(overlay.Label) ? node.BusinessName : overlay.Label,
                BusinessGroup = string.IsNullOrWhiteSpace(overlay.Group) ? node.BusinessGroup : overlay.Group,
                HideInBusiness = overlay.Hide || node.HideInBusiness,
                SourceFile = node.SourceFile,
                LineNumber = node.LineNumber,
            };
        }
    }

    private sealed class BusinessOverlayMap
    {
        private readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, BusinessStepOverlay>> _overlays;

        private BusinessOverlayMap(IReadOnlyDictionary<string, IReadOnlyDictionary<string, BusinessStepOverlay>> overlays)
        {
            _overlays = overlays;
        }

        public static BusinessOverlayMap Create(DurableDocConfig? config)
        {
            var overlays = (config?.BusinessView?.Steps ?? [])
                .Where(step => !string.IsNullOrWhiteSpace(step.Orchestrator) && !string.IsNullOrWhiteSpace(step.Step))
                .GroupBy(step => step.Orchestrator.Trim(), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    orchestratorGroup => orchestratorGroup.Key,
                    orchestratorGroup => (IReadOnlyDictionary<string, BusinessStepOverlay>)orchestratorGroup
                        .ToDictionary(step => step.Step.Trim(), StringComparer.OrdinalIgnoreCase),
                    StringComparer.OrdinalIgnoreCase);

            return new BusinessOverlayMap(overlays);
        }

        public IReadOnlyDictionary<string, BusinessStepOverlay> GetForOrchestrator(string orchestratorName)
        {
            return _overlays.TryGetValue(orchestratorName, out var overlays)
                ? overlays
                : Empty;
        }

        private static readonly IReadOnlyDictionary<string, BusinessStepOverlay> Empty =
            new Dictionary<string, BusinessStepOverlay>(StringComparer.OrdinalIgnoreCase);
    }
}

internal sealed record SourceMethod(MethodDeclarationSyntax Method, string? ProjectPath);

internal readonly record struct FlowTail(string NodeId, string? EdgeLabel = null);

internal readonly record struct DurableInvocation(string StepName, WorkflowNodeType NodeType, Location Location);

internal sealed record PendingTaskBinding(string Name, string NodeId, List<FlowTail> SourceTails, int Order);

internal sealed record ResolvedParallelTask(string NodeId, List<FlowTail> SourceTails, int Order);

internal static class WorkspaceSourceLoader
{
    private static int _msbuildInitialized;

    public static async Task<IReadOnlyList<SourceMethod>> LoadAsync(string inputPath, CancellationToken cancellationToken)
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
                return await LoadFromSolutionAsync(fullPath, cancellationToken).ConfigureAwait(false);
            }

            if (extension.Equals(".csproj", StringComparison.OrdinalIgnoreCase))
            {
                return await LoadFromProjectAsync(fullPath, cancellationToken).ConfigureAwait(false);
            }

            if (extension.Equals(".cs", StringComparison.OrdinalIgnoreCase))
            {
                return await LoadFromDirectoryAsync(Path.GetDirectoryName(fullPath)!, cancellationToken).ConfigureAwait(false);
            }
        }

        if (Directory.Exists(fullPath))
        {
            return await LoadFromDirectoryAsync(fullPath, cancellationToken).ConfigureAwait(false);
        }

        throw new FileNotFoundException($"Input path was not found: {inputPath}");
    }

    private static async Task<IReadOnlyList<SourceMethod>> LoadFromSolutionAsync(string solutionPath, CancellationToken cancellationToken)
    {
        InitializeMsBuild();
        using var workspace = MSBuildWorkspace.Create();
        var solution = await workspace.OpenSolutionAsync(solutionPath, cancellationToken: cancellationToken).ConfigureAwait(false);

        var methods = new List<SourceMethod>();
        foreach (var project in solution.Projects)
        {
            methods.AddRange(await GetMethodsFromProjectAsync(project, cancellationToken).ConfigureAwait(false));
        }

        return methods;
    }

    private static async Task<IReadOnlyList<SourceMethod>> LoadFromProjectAsync(string projectPath, CancellationToken cancellationToken)
    {
        InitializeMsBuild();
        using var workspace = MSBuildWorkspace.Create();
        var project = await workspace.OpenProjectAsync(projectPath, cancellationToken: cancellationToken).ConfigureAwait(false);
        return await GetMethodsFromProjectAsync(project, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<IReadOnlyList<SourceMethod>> LoadFromDirectoryAsync(string directoryPath, CancellationToken cancellationToken)
    {
        var files = Directory.EnumerateFiles(directoryPath, "*.cs", SearchOption.AllDirectories)
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToList();

        var methods = new List<SourceMethod>();
        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var sourceText = await File.ReadAllTextAsync(file, cancellationToken).ConfigureAwait(false);
            var syntaxTree = CSharpSyntaxTree.ParseText(sourceText, path: file, cancellationToken: cancellationToken);
            methods.AddRange(ExtractMethods(syntaxTree, projectPath: null));
        }

        return methods;
    }

    private static async Task<IReadOnlyList<SourceMethod>> GetMethodsFromProjectAsync(Project project, CancellationToken cancellationToken)
    {
        var methods = new List<SourceMethod>();
        foreach (var document in project.Documents)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (document.FilePath is null || !document.FilePath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
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
