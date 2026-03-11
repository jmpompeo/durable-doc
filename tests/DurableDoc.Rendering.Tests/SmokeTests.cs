using DurableDoc.Domain;
using DurableDoc.Rendering.Mermaid;
using Xunit;

namespace DurableDoc.Rendering.Tests;

public class SmokeTests
{
    [Fact]
    public void Developer_mode_renders_workflow_nodes_and_edges()
    {
        var diagram = new WorkflowDiagram
        {
            Id = "order-fulfillment",
            OrchestratorName = "OrderFulfillment",
            Nodes =
            [
                new WorkflowNode { Id = "start", NodeType = WorkflowNodeType.OrchestratorStart, Name = "OrderFulfillment" },
                new WorkflowNode { Id = "validate", NodeType = WorkflowNodeType.Activity, Name = "ValidateCustomer" },
                new WorkflowNode { Id = "retry", NodeType = WorkflowNodeType.RetryActivity, Name = "Retry ChargePayment" },
                new WorkflowNode { Id = "fanout", NodeType = WorkflowNodeType.FanOut, Name = "Fan-out" },
                new WorkflowNode { Id = "email", NodeType = WorkflowNodeType.Activity, Name = "SendReceipt" },
                new WorkflowNode { Id = "erp", NodeType = WorkflowNodeType.SubOrchestrator, Name = "SyncERP" },
                new WorkflowNode { Id = "fanin", NodeType = WorkflowNodeType.FanIn, Name = "Fan-in" },
                new WorkflowNode { Id = "timer", NodeType = WorkflowNodeType.Timer, Name = "Wait 24h" },
            ],
            Edges =
            [
                new WorkflowEdge { FromNodeId = "start", ToNodeId = "validate" },
                new WorkflowEdge { FromNodeId = "validate", ToNodeId = "retry", ConditionLabel = "valid" },
                new WorkflowEdge { FromNodeId = "retry", ToNodeId = "fanout" },
                new WorkflowEdge { FromNodeId = "fanout", ToNodeId = "email" },
                new WorkflowEdge { FromNodeId = "fanout", ToNodeId = "erp" },
                new WorkflowEdge { FromNodeId = "email", ToNodeId = "fanin" },
                new WorkflowEdge { FromNodeId = "erp", ToNodeId = "fanin" },
                new WorkflowEdge { FromNodeId = "fanin", ToNodeId = "timer" },
            ],
        };

        var mermaid = MermaidRenderer.Render(diagram);

        Assert.Contains("flowchart TD", mermaid);
        Assert.Contains("([\"OrderFulfillment\"])", mermaid);
        Assert.Contains("{{\"Retry ChargePayment\"}}", mermaid);
        Assert.Contains("((\"Fan-out\"))", mermaid);
        Assert.Contains("[/\"Wait 24h\"/]", mermaid);
        Assert.Contains("-->|valid|", mermaid);
    }

    [Fact]
    public void Business_mode_collapses_groups_renames_nodes_and_hides_retry_steps()
    {
        var diagram = new WorkflowDiagram
        {
            Id = "payment",
            OrchestratorName = "PaymentOrchestrator",
            Nodes =
            [
                new WorkflowNode { Id = "start", NodeType = WorkflowNodeType.OrchestratorStart, Name = "PaymentOrchestrator", BusinessName = "Start payment" },
                new WorkflowNode { Id = "validate", NodeType = WorkflowNodeType.Activity, Name = "ValidateCustomer", BusinessName = "Validate customer" },
                new WorkflowNode { Id = "retry", NodeType = WorkflowNodeType.RetryActivity, Name = "Retry ChargePayment" },
                new WorkflowNode { Id = "charge", NodeType = WorkflowNodeType.Activity, Name = "ChargePayment", BusinessGroup = "Process payment" },
                new WorkflowNode { Id = "record", NodeType = WorkflowNodeType.Activity, Name = "RecordLedgerEntry", BusinessGroup = "Process payment" },
                new WorkflowNode { Id = "approval", NodeType = WorkflowNodeType.ExternalEvent, Name = "WaitForApproval", BusinessName = "Await approval" },
            ],
            Edges =
            [
                new WorkflowEdge { FromNodeId = "start", ToNodeId = "validate" },
                new WorkflowEdge { FromNodeId = "validate", ToNodeId = "retry" },
                new WorkflowEdge { FromNodeId = "retry", ToNodeId = "charge" },
                new WorkflowEdge { FromNodeId = "charge", ToNodeId = "record" },
                new WorkflowEdge { FromNodeId = "record", ToNodeId = "approval" },
            ],
        };

        var mermaid = MermaidRenderer.Render(diagram, MermaidRenderMode.Business);

        Assert.Contains("[\"Validate customer\"]", mermaid);
        Assert.Contains("[\"Process payment\"]", mermaid);
        Assert.Contains("[[\"Await approval\"]]", mermaid);
        Assert.DoesNotContain("Retry ChargePayment", mermaid);
        Assert.DoesNotContain("ChargePayment", mermaid);
        Assert.Equal(1, CountOccurrences(mermaid, "Process payment"));
    }

    [Fact]
    public void Business_mode_preserves_paths_when_hidden_nodes_are_removed()
    {
        var diagram = new WorkflowDiagram
        {
            Id = "approval",
            OrchestratorName = "ApprovalOrchestrator",
            Nodes =
            [
                new WorkflowNode { Id = "start", NodeType = WorkflowNodeType.OrchestratorStart, Name = "ApprovalOrchestrator" },
                new WorkflowNode { Id = "retry", NodeType = WorkflowNodeType.RetryActivity, Name = "Retry PollApproval" },
                new WorkflowNode { Id = "timer", NodeType = WorkflowNodeType.Timer, Name = "Wait 1h" },
                new WorkflowNode { Id = "approved", NodeType = WorkflowNodeType.Activity, Name = "CompleteApproval", BusinessName = "Complete approval" },
            ],
            Edges =
            [
                new WorkflowEdge { FromNodeId = "start", ToNodeId = "retry" },
                new WorkflowEdge { FromNodeId = "retry", ToNodeId = "timer" },
                new WorkflowEdge { FromNodeId = "timer", ToNodeId = "approved" },
            ],
        };

        var mermaid = MermaidRenderer.Render(diagram, MermaidRenderMode.Business);

        Assert.Contains("[/\"Wait 1h\"/]", mermaid);
        Assert.Contains("[\"Complete approval\"]", mermaid);
        Assert.Contains("n0 --> n1", mermaid);
        Assert.Contains("n1 --> n2", mermaid);
    }

    [Fact]
    public void Developer_mode_renders_retry_sub_orchestrators_as_retry_nodes()
    {
        var diagram = new WorkflowDiagram
        {
            Id = "retry-subflow",
            OrchestratorName = "RetrySubflow",
            Nodes =
            [
                new WorkflowNode { Id = "start", NodeType = WorkflowNodeType.OrchestratorStart, Name = "RetrySubflow" },
                new WorkflowNode { Id = "sub", NodeType = WorkflowNodeType.RetrySubOrchestrator, Name = "ScheduleFollowUp" },
            ],
            Edges =
            [
                new WorkflowEdge { FromNodeId = "start", ToNodeId = "sub" },
            ],
        };

        var mermaid = MermaidRenderer.Render(diagram);

        Assert.Contains("{{\"ScheduleFollowUp\"}}", mermaid);
    }

    private static int CountOccurrences(string value, string fragment)
    {
        var count = 0;
        var index = 0;

        while ((index = value.IndexOf(fragment, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += fragment.Length;
        }

        return count;
    }
}
