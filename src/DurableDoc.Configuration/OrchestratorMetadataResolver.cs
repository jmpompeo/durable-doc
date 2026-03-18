using DurableDoc.Domain;

namespace DurableDoc.Configuration;

public static class OrchestratorMetadataResolver
{
    public static OrchestratorReferenceResolution ResolveDiagram(IReadOnlyList<WorkflowDiagram> diagrams, string? referenceName)
    {
        if (string.IsNullOrWhiteSpace(referenceName))
        {
            return new OrchestratorReferenceResolution(referenceName, null, false);
        }

        var keyMatches = diagrams
            .Where(diagram => string.Equals(diagram.OrchestratorKey, referenceName, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (keyMatches.Length == 1)
        {
            return new OrchestratorReferenceResolution(referenceName, keyMatches[0], false);
        }

        if (keyMatches.Length > 1)
        {
            return new OrchestratorReferenceResolution(referenceName, null, true);
        }

        var displayMatches = diagrams
            .Where(diagram => string.Equals(diagram.OrchestratorDisplayName, referenceName, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (displayMatches.Length == 1)
        {
            return new OrchestratorReferenceResolution(referenceName, displayMatches[0], false);
        }

        if (displayMatches.Length > 1)
        {
            return new OrchestratorReferenceResolution(referenceName, null, true);
        }

        var nameMatches = diagrams
            .Where(diagram => string.Equals(diagram.OrchestratorName, referenceName, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (nameMatches.Length == 1)
        {
            return new OrchestratorReferenceResolution(referenceName, nameMatches[0], false);
        }

        return new OrchestratorReferenceResolution(referenceName, null, nameMatches.Length > 1);
    }
}

public sealed record OrchestratorReferenceResolution(string? ReferenceName, WorkflowDiagram? Diagram, bool IsAmbiguous);
