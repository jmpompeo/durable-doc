using DurableDoc.Domain;

namespace DurableDoc.Cli;

public enum CliDiagnosticSeverity
{
    Warning,
    Error,
}

public sealed record CliDiagnostic(CliDiagnosticSeverity Severity, string Message, string? OrchestratorName = null);

internal static class CliDiagnostics
{
    public static IReadOnlyList<CliDiagnostic> Evaluate(IReadOnlyList<WorkflowDiagram> diagrams)
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
        }

        return diagnostics;
    }
}
