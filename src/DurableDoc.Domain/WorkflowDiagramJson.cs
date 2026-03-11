using System.Text.Json;
using System.Text.Json.Serialization;

namespace DurableDoc.Domain;

public static class WorkflowDiagramJson
{
    public static string Serialize(WorkflowDiagram diagram)
    {
        return JsonSerializer.Serialize(diagram.ToDeterministic(), SerializerOptions);
    }

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        Converters =
        {
            new JsonStringEnumConverter(),
        },
    };
}
