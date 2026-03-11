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

    private static readonly HashSet<string> DurableMethodToNodeType = new(StringComparer.Ordinal)
    {
        ["CallActivityAsync"] = nameof(WorkflowNodeType.Activity),
        ["CallSubOrchestratorAsync"] = nameof(WorkflowNodeType.SubOrchestrator),
        ["CallActivityWithRetryAsync"] = nameof(WorkflowNodeType.RetryActivity),
        ["WaitForExternalEvent"] = nameof(WorkflowNodeType.ExternalEvent),
        ["CreateTimer"] = nameof(WorkflowNodeType.Timer),
    };

    public async Task<IReadOnlyList<WorkflowDiagram>> AnalyzeAsync(string inputPath, DurableDocConfig? config = null, CancellationToken cancellationToken = default)
    {
        var sourceMethods = await WorkspaceSourceLoader.LoadAsync(inputPath, cancellationToken).ConfigureAwait(false);
        var wrapperMap = BuildWrapperMap(config);

        var diagrams = new List<WorkflowDiagram>();
        foreach (var sourceMethod in sourceMethods.Where(IsOrchestrator).OrderBy(x => x.Method.Identifier.ValueText, StringComparer.Ordinal))
        {
            diagrams.Add(BuildDiagram(sourceMethod, wrapperMap));
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
        var method = sourceMethod.Method;
        var span = method.GetLocation().GetLineSpan();

        var nodes = new List<WorkflowNode>();
        var edges = new List<WorkflowEdge>();

        var startNode = new WorkflowNode
        {
            Id = "n0",
            NodeType = WorkflowNodeType.OrchestratorStart,
            Name = method.Identifier.ValueText,
            SourceFile = span.Path,
            LineNumber = span.StartLinePosition.Line + 1,
        };

        nodes.Add(startNode);
        var previousNodeId = startNode.Id;
        var nodeIndex = 1;

        foreach (var invocation in method.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            var methodName = GetInvokedMethodName(invocation);
            var nodeType = ResolveNodeType(methodName, wrapperMap);
            if (nodeType is null)
            {
                continue;
            }

            var invocationSpan = invocation.GetLocation().GetLineSpan();
            var node = new WorkflowNode
            {
                Id = $"n{nodeIndex}",
                NodeType = nodeType.Value,
                Name = ExtractStepName(invocation, methodName),
                SourceFile = invocationSpan.Path,
                LineNumber = invocationSpan.StartLinePosition.Line + 1,
            };

            nodes.Add(node);
            edges.Add(new WorkflowEdge
            {
                FromNodeId = previousNodeId,
                ToNodeId = node.Id,
            });

            previousNodeId = node.Id;
            nodeIndex++;
        }

        return new WorkflowDiagram
        {
            Id = $"{method.Identifier.ValueText}:{span.StartLinePosition.Line + 1}",
            OrchestratorName = method.Identifier.ValueText,
            SourceFile = span.Path,
            StartLine = span.StartLinePosition.Line + 1,
            EndLine = span.EndLinePosition.Line + 1,
            Nodes = nodes,
            Edges = edges,
        };
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
        if (DurableMethodToNodeType.TryGetValue(methodName, out var nodeTypeName) && Enum.TryParse<WorkflowNodeType>(nodeTypeName, out var parsed))
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
}

internal sealed record SourceMethod(MethodDeclarationSyntax Method);

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
            methods.AddRange(ExtractMethods(syntaxTree));
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

            methods.AddRange(ExtractMethods(syntaxTree));
        }

        return methods;
    }

    private static IEnumerable<SourceMethod> ExtractMethods(SyntaxTree syntaxTree)
    {
        var root = syntaxTree.GetRoot();
        return root.DescendantNodes().OfType<MethodDeclarationSyntax>().Select(m => new SourceMethod(m));
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
