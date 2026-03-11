using DurableDoc.Analysis;
using DurableDoc.Configuration;
using DurableDoc.Domain;

namespace DurableDoc.Cli;

public static class ListCommandHandler
{
    public static async Task<int> ExecuteAsync(
        string inputPath,
        string? orchestratorName,
        string? configPath,
        CliCommandContext? context = null,
        CancellationToken cancellationToken = default)
    {
        context ??= CliCommandContext.CreateDefault();

        try
        {
            var config = DurableDocConfigLoader.Load(configPath);
            var analyzer = new WorkflowAnalyzer();
            var diagrams = await analyzer.AnalyzeAsync(inputPath, config, cancellationToken).ConfigureAwait(false);
            var selected = Filter(diagrams, orchestratorName);

            if (selected.Length == 0)
            {
                context.Fail("No orchestrators matched the requested input.");
                return 1;
            }

            foreach (var diagnostic in CliDiagnostics.Evaluate(selected).Where(d => d.Severity == CliDiagnosticSeverity.Warning))
            {
                context.Warn(FormatDiagnostic(diagnostic));
            }

            foreach (var diagram in selected)
            {
                var activityCount = Count(diagram, WorkflowNodeType.Activity, WorkflowNodeType.RetryActivity);
                var subOrchestratorCount = Count(diagram, WorkflowNodeType.SubOrchestrator);
                var externalEventCount = Count(diagram, WorkflowNodeType.ExternalEvent);

                context.Info($"{diagram.OrchestratorName} | {diagram.SourceFile ?? "(unknown source)"} | activities={activityCount} | subOrchestrators={subOrchestratorCount} | externalEvents={externalEventCount}");

                if (context.Verbosity == CliVerbosity.Detailed)
                {
                    foreach (var node in diagram.Nodes.Skip(1))
                    {
                        var label = string.IsNullOrWhiteSpace(node.DisplayLabel) ? node.Name : node.DisplayLabel;
                        context.Detail($"  - {node.NodeType}: {label}");
                    }
                }
            }

            return 0;
        }
        catch (Exception ex)
        {
            context.Fail(ex.Message);
            return 1;
        }
    }

    private static WorkflowDiagram[] Filter(IReadOnlyList<WorkflowDiagram> diagrams, string? orchestratorName)
    {
        return diagrams
            .Where(diagram => string.IsNullOrWhiteSpace(orchestratorName) ||
                string.Equals(diagram.OrchestratorName, orchestratorName, StringComparison.OrdinalIgnoreCase))
            .OrderBy(diagram => diagram.OrchestratorName, StringComparer.Ordinal)
            .ToArray();
    }

    private static int Count(WorkflowDiagram diagram, params WorkflowNodeType[] nodeTypes)
    {
        return diagram.Nodes.Count(node => nodeTypes.Contains(node.NodeType));
    }

    private static string FormatDiagnostic(CliDiagnostic diagnostic)
    {
        return diagnostic.OrchestratorName is null
            ? diagnostic.Message
            : $"{diagnostic.OrchestratorName}: {diagnostic.Message}";
    }
}
