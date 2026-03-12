using DurableDoc.Dashboard;
using DurableDoc.Domain;

namespace DurableDoc.Cli;

internal static class WorkflowSelection
{
    public static WorkflowDiagram[] FilterDiagrams(IReadOnlyList<WorkflowDiagram> diagrams, string? orchestratorName)
    {
        return diagrams
            .Where(diagram => Matches(diagram.OrchestratorName, orchestratorName))
            .OrderBy(diagram => diagram.OrchestratorName, StringComparer.Ordinal)
            .ToArray();
    }

    public static GeneratedDiagramArtifact[] FilterArtifacts(IReadOnlyList<GeneratedDiagramArtifact> artifacts, string? orchestratorName)
    {
        return artifacts
            .Where(artifact => Matches(artifact.OrchestratorName, orchestratorName))
            .OrderBy(artifact => artifact.OrchestratorName, StringComparer.Ordinal)
            .ThenBy(artifact => artifact.Mode, StringComparer.Ordinal)
            .ToArray();
    }

    public static string BuildFilterMismatchMessage(string? orchestratorName, IEnumerable<string> discoveredNames)
    {
        var discovered = string.Join(", ", discoveredNames.OrderBy(name => name, StringComparer.Ordinal));
        return $"No orchestrators matched filter '{orchestratorName}'. Discovered orchestrators: {discovered}.";
    }

    public static string? ResolvePreviewOrchestrator(string? requestedOrchestrator, IEnumerable<string> selectedNames)
    {
        var names = selectedNames
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return names.Length == 1 ? names[0] : requestedOrchestrator;
    }

    private static bool Matches(string candidate, string? orchestratorName)
    {
        return string.IsNullOrWhiteSpace(orchestratorName)
            || string.Equals(candidate, orchestratorName, StringComparison.OrdinalIgnoreCase);
    }
}
