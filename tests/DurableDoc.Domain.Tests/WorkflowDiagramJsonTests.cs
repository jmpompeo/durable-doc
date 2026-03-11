using System.Globalization;

namespace DurableDoc.Domain.Tests;

public class WorkflowDiagramJsonTests
{
    [Fact]
    public void Serialize_ProducesExpectedSnapshot()
    {
        var diagram = new WorkflowDiagram
        {
            Id = "customer-onboarding",
            OrchestratorName = "CustomerOnboarding",
            SourceFile = "src/Orchestrators/CustomerOnboarding.cs",
            CreatedTimestamp = new DateTimeOffset(2025, 1, 15, 9, 30, 0, TimeSpan.Zero),
            Nodes =
            [
                new WorkflowNode
                {
                    Id = "n3",
                    DisplayLabel = "Approval received",
                    NodeType = WorkflowNodeType.ExternalEvent,
                    SourceFile = "src/Orchestrators/CustomerOnboarding.cs",
                    LineNumber = 42,
                },
                new WorkflowNode
                {
                    Id = "n1",
                    DisplayLabel = "Start onboarding",
                    NodeType = WorkflowNodeType.OrchestratorStart,
                    SourceFile = "src/Orchestrators/CustomerOnboarding.cs",
                    LineNumber = 10,
                },
                new WorkflowNode
                {
                    Id = "n2",
                    DisplayLabel = "Validate request",
                    NodeType = WorkflowNodeType.Activity,
                    SourceFile = "src/Orchestrators/CustomerOnboarding.cs",
                    LineNumber = 18,
                },
            ],
            Edges =
            [
                new WorkflowEdge { FromNodeId = "n2", ToNodeId = "n3", ConditionLabel = "approved" },
                new WorkflowEdge { FromNodeId = "n1", ToNodeId = "n2", ConditionLabel = "default" },
            ],
        };

        var json = WorkflowDiagramJson.Serialize(diagram);
        var expectedJson = File.ReadAllText("Snapshots/workflow-diagram.json");

        Assert.Equal(NormalizeLineEndings(expectedJson), NormalizeLineEndings(json));
    }

    [Fact]
    public void ToDeterministic_SortsNodesAndEdgesConsistently()
    {
        var diagram = new WorkflowDiagram
        {
            Id = "id",
            OrchestratorName = "orch",
            Nodes =
            [
                new WorkflowNode { Id = "b", DisplayLabel = "B", NodeType = WorkflowNodeType.Activity, LineNumber = 20 },
                new WorkflowNode { Id = "a", DisplayLabel = "A", NodeType = WorkflowNodeType.Activity, LineNumber = 10 },
                new WorkflowNode { Id = "c", DisplayLabel = "C", NodeType = WorkflowNodeType.Activity, LineNumber = 20 },
            ],
            Edges =
            [
                new WorkflowEdge { FromNodeId = "n2", ToNodeId = "n3", ConditionLabel = "z" },
                new WorkflowEdge { FromNodeId = "n1", ToNodeId = "n2", ConditionLabel = "a" },
                new WorkflowEdge { FromNodeId = "n2", ToNodeId = "n3", ConditionLabel = "a" },
            ],
        };

        var deterministic = diagram.ToDeterministic();

        Assert.Equal(["a", "b", "c"], deterministic.Nodes.Select(n => n.Id));
        Assert.Equal(["a", "a", "z"], deterministic.Edges.Select(e => e.ConditionLabel));
    }

    private static string NormalizeLineEndings(string value)
        => value.Replace("\r\n", "\n", false, CultureInfo.InvariantCulture);
}
