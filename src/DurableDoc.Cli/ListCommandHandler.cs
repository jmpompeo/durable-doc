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
            var analysis = await analyzer.AnalyzeWorkspaceAsync(inputPath, config, cancellationToken).ConfigureAwait(false);

            if (analysis.Diagrams.Count == 0)
            {
                context.Fail(GenerateCommandHandler.BuildNoDiscoveryMessage(analysis, inputPath));
                return 1;
            }

            var selected = WorkflowSelection.FilterDiagrams(analysis.Diagrams, orchestratorName);

            if (selected.Length == 0)
            {
                context.Fail(WorkflowSelection.BuildFilterMismatchMessage(
                    orchestratorName,
                    analysis.Diagrams.Select(diagram => diagram.OrchestratorName)));
                return 1;
            }

            foreach (var diagnostic in CliDiagnostics.Evaluate(selected, config).Where(d => d.Severity == CliDiagnosticSeverity.Warning))
            {
                context.Warn(FormatDiagnostic(diagnostic));
            }

            foreach (var diagram in selected)
            {
                var activities = GetLabels(diagram, WorkflowNodeType.Activity, WorkflowNodeType.RetryActivity);
                var subOrchestrators = GetLabels(diagram, WorkflowNodeType.SubOrchestrator, WorkflowNodeType.RetrySubOrchestrator);
                var externalEvents = GetLabels(diagram, WorkflowNodeType.ExternalEvent);

                context.Info($"{diagram.OrchestratorName} | {diagram.SourceFile ?? "(unknown source)"} | activities={FormatList(activities)} | subOrchestrators={FormatList(subOrchestrators)} | externalEvents={FormatList(externalEvents)}");

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

    private static string[] GetLabels(WorkflowDiagram diagram, params WorkflowNodeType[] nodeTypes)
    {
        return diagram.Nodes
            .Where(node => nodeTypes.Contains(node.NodeType))
            .Select(node => string.IsNullOrWhiteSpace(node.DisplayLabel) ? node.Name : node.DisplayLabel)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(label => label, StringComparer.Ordinal)
            .ToArray();
    }

    private static string FormatList(string[] values)
    {
        return values.Length == 0 ? "(none)" : string.Join(", ", values);
    }

    private static string FormatDiagnostic(CliDiagnostic diagnostic)
    {
        return diagnostic.OrchestratorName is null
            ? diagnostic.Message
            : $"{diagnostic.OrchestratorName}: {diagnostic.Message}";
    }
}
