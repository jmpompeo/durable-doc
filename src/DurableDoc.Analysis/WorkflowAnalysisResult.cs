using DurableDoc.Domain;

namespace DurableDoc.Analysis;

public sealed class WorkflowAnalysisResult
{
    public string ResolvedInputPath { get; init; } = string.Empty;

    public WorkflowInputKind InputKind { get; init; }

    public IReadOnlyList<string> ScannedProjects { get; init; } = [];

    public IReadOnlyList<WorkflowDiagram> Diagrams { get; init; } = [];
}

public enum WorkflowInputKind
{
    Solution,
    Project,
    Directory,
    File,
}
