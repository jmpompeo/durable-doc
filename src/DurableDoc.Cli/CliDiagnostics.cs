using DurableDoc.Configuration;
using DurableDoc.Domain;
using System.IO;

namespace DurableDoc.Cli;

public enum CliDiagnosticSeverity
{
    Warning,
    Error,
}

public sealed record CliDiagnostic(CliDiagnosticSeverity Severity, string Message, string? OrchestratorName = null);

internal static class CliDiagnostics
{
    public static IReadOnlyList<CliDiagnostic> Evaluate(IReadOnlyList<WorkflowDiagram> diagrams, DurableDocConfig? config = null)
    {
        var diagnostics = new List<CliDiagnostic>();

        if (diagrams.Count == 0)
        {
            diagnostics.Add(new CliDiagnostic(CliDiagnosticSeverity.Error, "No orchestrators were discovered in the requested input."));
            return diagnostics;
        }

        foreach (var diagram in diagrams.OrderBy(d => d.OrchestratorDisplayName, StringComparer.Ordinal))
        {
            if (diagram.Nodes.Count <= 1)
            {
                diagnostics.Add(new CliDiagnostic(
                    CliDiagnosticSeverity.Warning,
                    "No supported Durable calls were discovered. The generated diagram only contains the orchestrator entry point.",
                    diagram.OrchestratorDisplayName));
            }

            if (diagram.Nodes.Any(node => node.NodeType == WorkflowNodeType.Wrapper))
            {
                diagnostics.Add(new CliDiagnostic(
                    CliDiagnosticSeverity.Warning,
                    "Wrapper calls were detected and may need explicit config metadata for precise rendering.",
                    diagram.OrchestratorDisplayName));
            }

            foreach (var issue in diagram.Diagnostics)
            {
                diagnostics.Add(new CliDiagnostic(
                    issue.Severity == WorkflowIssueSeverity.Error ? CliDiagnosticSeverity.Error : CliDiagnosticSeverity.Warning,
                    issue.LineNumber > 0
                        ? $"{issue.Message} ({Path.GetFileName(issue.SourceFile)}:{issue.LineNumber})"
                        : issue.Message,
                    diagram.OrchestratorDisplayName));
            }
        }

        diagnostics.AddRange(ValidateMetadata(diagrams, config));
        return diagnostics;
    }

    private static IEnumerable<CliDiagnostic> ValidateMetadata(IReadOnlyList<WorkflowDiagram> diagrams, DurableDocConfig? config)
    {
        if (config?.BusinessView?.Orchestrators is null)
        {
            yield break;
        }

        foreach (var orchestrator in config.BusinessView.Orchestrators)
        {
            var resolution = OrchestratorMetadataResolver.ResolveDiagram(diagrams, orchestrator.Name);
            if (resolution.IsAmbiguous)
            {
                yield return new CliDiagnostic(
                    CliDiagnosticSeverity.Warning,
                    $"Business metadata references orchestrator '{orchestrator.Name}', but it matches multiple discovered orchestrators. Use one of: {string.Join(", ", diagrams.Where(diagram => string.Equals(diagram.OrchestratorName, orchestrator.Name, StringComparison.OrdinalIgnoreCase) || string.Equals(diagram.OrchestratorDisplayName, orchestrator.Name, StringComparison.OrdinalIgnoreCase)).Select(diagram => diagram.OrchestratorKey).OrderBy(name => name, StringComparer.Ordinal))}.");
                continue;
            }

            var diagram = resolution.Diagram;
            if (diagram is null)
            {
                yield return new CliDiagnostic(
                    CliDiagnosticSeverity.Warning,
                    $"Business metadata references orchestrator '{orchestrator.Name}', but it was not discovered in the requested input.");
                continue;
            }

            var nodeNames = new HashSet<string>(
                diagram.Nodes.SelectMany(node => new[] { node.Name, node.DisplayLabel }.Where(value => !string.IsNullOrWhiteSpace(value))),
                StringComparer.OrdinalIgnoreCase);

            foreach (var step in orchestrator.Steps ?? [])
            {
                if (!nodeNames.Contains(step.Name))
                {
                    yield return new CliDiagnostic(
                        CliDiagnosticSeverity.Warning,
                        $"Business metadata references step '{step.Name}', but it was not discovered in orchestrator '{orchestrator.Name}'.",
                        diagram.OrchestratorDisplayName);
                }
            }
        }
    }
}
