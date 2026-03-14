using DurableDoc.Configuration;
using DurableDoc.Dashboard;
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
    public static IReadOnlyList<CliDiagnostic> Evaluate(
        IReadOnlyList<WorkflowDiagram> diagrams,
        DurableDocConfig? config = null,
        DashboardAudience audience = DashboardAudience.Developer)
    {
        var diagnostics = new List<CliDiagnostic>();

        if (diagrams.Count == 0)
        {
            diagnostics.Add(new CliDiagnostic(CliDiagnosticSeverity.Error, "No orchestrators were discovered in the requested input."));
            return diagnostics;
        }

        foreach (var diagram in diagrams.OrderBy(d => d.OrchestratorName, StringComparer.Ordinal))
        {
            if (diagram.Nodes.Count <= 1)
            {
                diagnostics.Add(new CliDiagnostic(
                    CliDiagnosticSeverity.Warning,
                    "No supported Durable calls were discovered. The generated diagram only contains the orchestrator entry point.",
                    diagram.OrchestratorName));
            }

            if (diagram.Nodes.Any(node => node.NodeType == WorkflowNodeType.Wrapper))
            {
                diagnostics.Add(new CliDiagnostic(
                    CliDiagnosticSeverity.Warning,
                    "Wrapper calls were detected and may need explicit config metadata for precise rendering.",
                    diagram.OrchestratorName));
            }

            foreach (var issue in diagram.Diagnostics)
            {
                diagnostics.Add(new CliDiagnostic(
                    issue.Severity == WorkflowIssueSeverity.Error ? CliDiagnosticSeverity.Error : CliDiagnosticSeverity.Warning,
                    issue.LineNumber > 0
                        ? $"{issue.Message} ({Path.GetFileName(issue.SourceFile)}:{issue.LineNumber})"
                        : issue.Message,
                    diagram.OrchestratorName));
            }
        }

        diagnostics.AddRange(ValidateMetadata(diagrams, config, audience));
        return diagnostics;
    }

    private static IEnumerable<CliDiagnostic> ValidateMetadata(
        IReadOnlyList<WorkflowDiagram> diagrams,
        DurableDocConfig? config,
        DashboardAudience audience)
    {
        var diagramsByName = diagrams.ToDictionary(diagram => diagram.OrchestratorName, StringComparer.OrdinalIgnoreCase);
        var metadataByName = (config?.BusinessView?.Orchestrators ?? [])
            .Where(orchestrator => !string.IsNullOrWhiteSpace(orchestrator.Name))
            .ToDictionary(orchestrator => orchestrator.Name, StringComparer.OrdinalIgnoreCase);

        if (audience == DashboardAudience.Stakeholder)
        {
            foreach (var diagram in diagrams)
            {
                if (!metadataByName.TryGetValue(diagram.OrchestratorName, out var stakeholderMetadata))
                {
                    yield return new CliDiagnostic(
                        CliDiagnosticSeverity.Warning,
                        $"Stakeholder audience requested, but orchestrator '{diagram.OrchestratorName}' is missing business metadata with 'summary' and 'capability'.",
                        diagram.OrchestratorName);
                    continue;
                }

                if (string.IsNullOrWhiteSpace(stakeholderMetadata.Summary))
                {
                    yield return new CliDiagnostic(
                        CliDiagnosticSeverity.Warning,
                        "Stakeholder audience requested, but business metadata is missing 'summary'.",
                        diagram.OrchestratorName);
                }

                if (string.IsNullOrWhiteSpace(stakeholderMetadata.Capability))
                {
                    yield return new CliDiagnostic(
                        CliDiagnosticSeverity.Warning,
                        "Stakeholder audience requested, but business metadata is missing 'capability'.",
                        diagram.OrchestratorName);
                }
            }
        }

        foreach (var orchestrator in metadataByName.Values)
        {
            if (!diagramsByName.TryGetValue(orchestrator.Name, out var diagram))
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
                        orchestrator.Name);
                }
            }
        }
    }
}
