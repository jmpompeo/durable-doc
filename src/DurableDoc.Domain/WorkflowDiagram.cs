namespace DurableDoc.Domain;

public sealed class WorkflowDiagram
{
    public string Id { get; init; } = string.Empty;
    public string OrchestratorName { get; init; } = string.Empty;
    public string? SourceFile { get; init; }
    public DateTimeOffset CreatedTimestamp { get; init; } = DateTimeOffset.UtcNow;
    public IReadOnlyList<WorkflowNode> Nodes { get; init; } = [];
    public IReadOnlyList<WorkflowEdge> Edges { get; init; } = [];

    public WorkflowDiagram ToDeterministic()
    {
        var orderedNodes = Nodes
            .OrderBy(node => node.LineNumber)
            .ThenBy(node => node.DisplayLabel, StringComparer.Ordinal)
            .ThenBy(node => node.Id, StringComparer.Ordinal)
            .ToArray();

        var orderedEdges = Edges
            .OrderBy(edge => edge.FromNodeId, StringComparer.Ordinal)
            .ThenBy(edge => edge.ToNodeId, StringComparer.Ordinal)
            .ThenBy(edge => edge.ConditionLabel, StringComparer.Ordinal)
            .ToArray();

        return new WorkflowDiagram
        {
            Id = Id,
            OrchestratorName = OrchestratorName,
            SourceFile = SourceFile,
            CreatedTimestamp = CreatedTimestamp,
            Nodes = orderedNodes,
            Edges = orderedEdges,
        };
    }
}

public sealed class WorkflowNode
{
    public string Id { get; init; } = string.Empty;
    public string DisplayLabel { get; init; } = string.Empty;
    public WorkflowNodeType NodeType { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? BusinessName { get; init; }
    public string? BusinessGroup { get; init; }
    public bool HideInBusiness { get; init; }
    public string? SourceFile { get; init; }
    public int LineNumber { get; init; }
}

public sealed class WorkflowEdge
{
    public string FromNodeId { get; init; } = string.Empty;
    public string ToNodeId { get; init; } = string.Empty;
    public string? ConditionLabel { get; init; }
}

public enum WorkflowNodeType
{
    OrchestratorStart,
    Activity,
    SubOrchestrator,
    RetryActivity,
    Decision,
    ParallelGroup,
    ExternalEvent,
    Timer,
    FanOut,
    FanIn,
    Wrapper,
}
