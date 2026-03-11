namespace DurableDoc.Domain;

public sealed class WorkflowDiagram
{
    public string Id { get; init; } = string.Empty;
    public string OrchestratorName { get; init; } = string.Empty;
    public string? SourceFile { get; init; }
    public int StartLine { get; init; }
    public int EndLine { get; init; }
    public DateTimeOffset CreatedTimestamp { get; init; } = DateTimeOffset.UtcNow;
    public IReadOnlyList<WorkflowNode> Nodes { get; init; } = [];
    public IReadOnlyList<WorkflowEdge> Edges { get; init; } = [];
}

public sealed class WorkflowNode
{
    public string Id { get; init; } = string.Empty;
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
    ExternalEvent,
    Timer,
    FanOut,
    FanIn,
    Decision,
    Wrapper,
}
