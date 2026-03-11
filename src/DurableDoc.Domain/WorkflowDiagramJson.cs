using System.Text.Json;

namespace DurableDoc.Domain;

public static class WorkflowDiagramJson
{
    public static string Serialize(WorkflowDiagram diagram)
    {
        return JsonSerializer.Serialize(diagram, new JsonSerializerOptions
        {
            WriteIndented = true,
        });
    }
}
