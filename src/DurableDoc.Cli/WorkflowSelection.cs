using DurableDoc.Dashboard;
using DurableDoc.Domain;

namespace DurableDoc.Cli;

internal static class WorkflowSelection
{
    public static WorkflowDiagram[] FilterDiagrams(IReadOnlyList<WorkflowDiagram> diagrams, string? orchestratorName)
    {
        return Filter(
            diagrams,
            orchestratorName,
            diagram => diagram.OrchestratorKey,
            diagram => GetDisplayName(diagram.OrchestratorDisplayName, diagram.OrchestratorName),
            diagram => diagram.OrchestratorName);
    }

    public static GeneratedDiagramArtifact[] FilterArtifacts(IReadOnlyList<GeneratedDiagramArtifact> artifacts, string? orchestratorName)
    {
        return Filter(
            artifacts,
            orchestratorName,
            artifact => GetArtifactKey(artifact),
            artifact => GetArtifactDisplayName(artifact),
            artifact => artifact.OrchestratorName)
            .OrderBy(artifact => GetArtifactDisplayName(artifact), StringComparer.Ordinal)
            .ThenBy(artifact => artifact.Mode, StringComparer.Ordinal)
            .ToArray();
    }

    public static string BuildFilterMismatchMessage(string? orchestratorName, IEnumerable<string> discoveredNames)
    {
        var discovered = string.Join(", ", discoveredNames.OrderBy(name => name, StringComparer.Ordinal));
        return $"No orchestrators matched filter '{orchestratorName}'. Discovered orchestrators: {discovered}.";
    }

    public static string BuildAmbiguousFilterMessage(string? orchestratorName, IEnumerable<string> qualifiedNames)
    {
        var candidates = string.Join(", ", qualifiedNames.OrderBy(name => name, StringComparer.Ordinal));
        return $"The orchestrator filter '{orchestratorName}' is ambiguous. Use one of: {candidates}.";
    }

    public static string? ResolvePreviewOrchestrator(string? requestedOrchestrator, IEnumerable<string> selectedNames)
    {
        var names = selectedNames
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return names.Length == 1 ? names[0] : requestedOrchestrator;
    }

    private static T[] Filter<T>(
        IReadOnlyList<T> items,
        string? orchestratorName,
        Func<T, string> keySelector,
        Func<T, string> displaySelector,
        Func<T, string> nameSelector)
    {
        if (string.IsNullOrWhiteSpace(orchestratorName))
        {
            return items
                .OrderBy(displaySelector, StringComparer.Ordinal)
                .ToArray();
        }

        var keyMatches = items
            .Where(item => string.Equals(keySelector(item), orchestratorName, StringComparison.OrdinalIgnoreCase))
            .OrderBy(displaySelector, StringComparer.Ordinal)
            .ToArray();
        if (keyMatches.Length > 0)
        {
            return keyMatches;
        }

        var displayMatches = items
            .Where(item => string.Equals(displaySelector(item), orchestratorName, StringComparison.OrdinalIgnoreCase))
            .OrderBy(displaySelector, StringComparer.Ordinal)
            .ToArray();
        if (displayMatches.Length == 1)
        {
            return displayMatches;
        }

        if (displayMatches.Length > 1)
        {
            throw new InvalidOperationException(BuildAmbiguousFilterMessage(orchestratorName, displayMatches.Select(keySelector)));
        }

        var nameMatches = items
            .Where(item => string.Equals(nameSelector(item), orchestratorName, StringComparison.OrdinalIgnoreCase))
            .OrderBy(displaySelector, StringComparer.Ordinal)
            .ToArray();
        if (nameMatches.Length == 1)
        {
            return nameMatches;
        }

        if (nameMatches.Length > 1)
        {
            throw new InvalidOperationException(BuildAmbiguousFilterMessage(orchestratorName, nameMatches.Select(keySelector)));
        }

        return [];
    }

    private static string GetArtifactKey(GeneratedDiagramArtifact artifact)
        => !string.IsNullOrWhiteSpace(artifact.OrchestratorKey)
            ? artifact.OrchestratorKey
            : GetArtifactDisplayName(artifact);

    private static string GetArtifactDisplayName(GeneratedDiagramArtifact artifact)
        => GetDisplayName(artifact.OrchestratorDisplayName, artifact.OrchestratorName);

    private static string GetDisplayName(string? displayName, string orchestratorName)
        => string.IsNullOrWhiteSpace(displayName) ? orchestratorName : displayName;
}
