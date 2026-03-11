using System.Text.Json;
using System.Text.Json.Serialization;

namespace DurableDoc.Domain;

public static class WorkflowDiagramJson
{
    public static string Serialize(WorkflowDiagram diagram)
    {
        return JsonSerializer.Serialize(SerializableWorkflowDiagram.From(diagram.ToDeterministic()), SerializerOptions) + Environment.NewLine;
    }

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters =
        {
            new JsonStringEnumConverter(),
        },
    };
}

internal sealed class SerializableWorkflowDiagram
{
    public string Id { get; init; } = string.Empty;

    public string OrchestratorName { get; init; } = string.Empty;

    public string? SourceFile { get; init; }

    public DateTimeOffset CreatedTimestamp { get; init; }

    public IReadOnlyList<SerializableWorkflowNode> Nodes { get; init; } = [];

    public IReadOnlyList<WorkflowEdge> Edges { get; init; } = [];

    public static SerializableWorkflowDiagram From(WorkflowDiagram diagram)
    {
        return new SerializableWorkflowDiagram
        {
            Id = diagram.Id,
            OrchestratorName = diagram.OrchestratorName,
            SourceFile = diagram.SourceFile,
            CreatedTimestamp = diagram.CreatedTimestamp,
            Nodes = diagram.Nodes.Select(SerializableWorkflowNode.From).ToArray(),
            Edges = diagram.Edges,
        };
    }
}

internal sealed class SerializableWorkflowNode
{
    public string Id { get; init; } = string.Empty;

    public string DisplayLabel { get; init; } = string.Empty;

    public WorkflowNodeType NodeType { get; init; }

    public string? Name { get; init; }

    public string? BusinessName { get; init; }

    public string? BusinessGroup { get; init; }

    public bool? HideInBusiness { get; init; }

    public string? SourceFile { get; init; }

    public int LineNumber { get; init; }

    public static SerializableWorkflowNode From(WorkflowNode node)
    {
        return new SerializableWorkflowNode
        {
            Id = node.Id,
            DisplayLabel = node.DisplayLabel,
            NodeType = node.NodeType,
            Name = NullIfWhiteSpace(node.Name),
            BusinessName = NullIfWhiteSpace(node.BusinessName),
            BusinessGroup = NullIfWhiteSpace(node.BusinessGroup),
            HideInBusiness = node.HideInBusiness ? true : null,
            SourceFile = node.SourceFile,
            LineNumber = node.LineNumber,
        };
    }

    private static string? NullIfWhiteSpace(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}
