using DurableDoc.Analysis;
using DurableDoc.Configuration;
using DurableDoc.Domain;

namespace DurableDoc.Analysis.Tests;

public class SmokeTests
{
    [Fact]
    public async Task AnalyzeAsync_OnAdvancedSample_DiscoversMainAndSubOrchestrations()
    {
        var analyzer = new WorkflowAnalyzer();

        var diagrams = await analyzer.AnalyzeAsync(SampleProjectLocator.GetAdvancedSampleProjectPath());

        Assert.Equal(
            [
                "CollectDocumentsSubOrchestrator",
                "ProvisionAccountSubOrchestrator",
                "RunCustomerOnboarding",
                "ScheduleFollowUpSubOrchestrator",
            ],
            diagrams.Select(diagram => diagram.OrchestratorName).ToArray());

        var mainDiagram = Assert.Single(diagrams, diagram => diagram.OrchestratorName == "RunCustomerOnboarding");
        Assert.EndsWith("DurableDoc.Sample.Advanced.csproj", mainDiagram.SourceProjectPath, StringComparison.Ordinal);

        Assert.Equal(
            [
                WorkflowNodeType.OrchestratorStart,
                WorkflowNodeType.Activity,
                WorkflowNodeType.Activity,
                WorkflowNodeType.RetryActivity,
                WorkflowNodeType.SubOrchestrator,
                WorkflowNodeType.SubOrchestrator,
                WorkflowNodeType.ExternalEvent,
                WorkflowNodeType.Timer,
                WorkflowNodeType.Activity,
            ],
            mainDiagram.Nodes.Select(node => node.NodeType).ToArray());

        Assert.Equal(
            [
                "RunCustomerOnboarding",
                "LoadApplication",
                "ValidateCustomer",
                "ReserveCreditCheck",
                "CollectDocumentsSubOrchestrator",
                "ProvisionAccountSubOrchestrator",
                "WaitForCustomerApproval",
                "CreateTimer",
                "SendWelcomeEmail",
            ],
            mainDiagram.Nodes.Select(node => node.DisplayLabel).ToArray());
    }

    [Fact]
    public async Task AnalyzeAsync_DiscoversOrchestratorAndDurableCalls()
    {
        using var fixture = new TempSourceFixture("""
using System.Threading.Tasks;

public class Demo
{
    [OrchestrationTrigger]
    public async Task Run(TaskOrchestrationContext ctx)
    {
        await ctx.CallActivityAsync("ValidateOrder");
        await ctx.CallSubOrchestratorAsync("SubFlow");
        await ctx.WaitForExternalEvent<string>("Approved");
        await ctx.CreateTimer(System.DateTime.UtcNow, default);
    }
}
""");

        var analyzer = new WorkflowAnalyzer();

        var diagrams = await analyzer.AnalyzeAsync(fixture.DirectoryPath);

        var diagram = Assert.Single(diagrams);
        Assert.Equal("Run", diagram.OrchestratorName);
        Assert.Collection(
            diagram.Nodes,
            n => Assert.Equal(WorkflowNodeType.OrchestratorStart, n.NodeType),
            n => Assert.Equal(WorkflowNodeType.Activity, n.NodeType),
            n => Assert.Equal(WorkflowNodeType.SubOrchestrator, n.NodeType),
            n => Assert.Equal(WorkflowNodeType.ExternalEvent, n.NodeType),
            n => Assert.Equal(WorkflowNodeType.Timer, n.NodeType));
        Assert.Equal(4, diagram.Edges.Count);
    }

    [Fact]
    public async Task AnalyzeAsync_Captures_if_else_as_decision_edges()
    {
        using var fixture = new TempSourceFixture("""
using System.Threading.Tasks;

public class Demo
{
    [OrchestrationTrigger]
    public async Task Run(TaskOrchestrationContext ctx, bool approved)
    {
        if (approved)
        {
            await ctx.CallActivityAsync("ApproveOrder");
        }
        else
        {
            await ctx.CallActivityAsync("RejectOrder");
        }
    }
}
""");

        var analyzer = new WorkflowAnalyzer();
        var diagram = Assert.Single(await analyzer.AnalyzeAsync(fixture.DirectoryPath));

        Assert.Contains(diagram.Nodes, node => node.NodeType == WorkflowNodeType.Decision && node.DisplayLabel == "approved");
        Assert.Contains(diagram.Edges, edge => edge.ConditionLabel == "approved");
        Assert.Contains(diagram.Edges, edge => edge.ConditionLabel == "else");
        Assert.Contains(diagram.Nodes, node => node.DisplayLabel == "ApproveOrder");
        Assert.Contains(diagram.Nodes, node => node.DisplayLabel == "RejectOrder");
    }

    [Fact]
    public async Task AnalyzeAsync_Captures_when_all_as_fan_out_and_fan_in()
    {
        using var fixture = new TempSourceFixture("""
using System.Threading.Tasks;

public class Demo
{
    [OrchestrationTrigger]
    public async Task Run(TaskOrchestrationContext ctx)
    {
        await Task.WhenAll(
            ctx.CallActivityAsync("LoadDocuments"),
            ctx.CallActivityAsync("ValidateAccount"));
        await ctx.CallActivityAsync("Complete");
    }
}
""");

        var analyzer = new WorkflowAnalyzer();
        var diagram = Assert.Single(await analyzer.AnalyzeAsync(fixture.DirectoryPath));

        Assert.Contains(diagram.Nodes, node => node.NodeType == WorkflowNodeType.FanOut);
        Assert.Contains(diagram.Nodes, node => node.NodeType == WorkflowNodeType.FanIn);
        Assert.Contains(diagram.Nodes, node => node.DisplayLabel == "LoadDocuments");
        Assert.Contains(diagram.Nodes, node => node.DisplayLabel == "ValidateAccount");
        Assert.Contains(diagram.Nodes, node => node.DisplayLabel == "Complete");
    }

    [Fact]
    public async Task AnalyzeAsync_DetectsConfiguredWrapperMethod()
    {
        using var fixture = new TempSourceFixture("""
using System.Threading.Tasks;

public class Demo
{
    [OrchestrationTrigger]
    public async Task Run(TaskOrchestrationContext ctx)
    {
        await CallActivityWithResult("DoWork");
    }

    private Task CallActivityWithResult(string name) => Task.CompletedTask;
}
""");

        var config = new DurableDocConfig
        {
            Analysis = new AnalysisOptions
            {
                Wrappers =
                [
                    new WrapperDefinition { MethodName = "CallActivityWithResult", Kind = "Activity" },
                ],
            },
        };

        var analyzer = new WorkflowAnalyzer();
        var diagrams = await analyzer.AnalyzeAsync(fixture.DirectoryPath, config);

        var diagram = Assert.Single(diagrams);
        Assert.Contains(diagram.Nodes, n => n.NodeType == WorkflowNodeType.Activity && n.Name == "DoWork");
    }

    [Fact]
    public async Task AnalyzeAsync_Applies_business_step_overlays()
    {
        using var fixture = new TempSourceFixture("""
using System.Threading.Tasks;

public class Demo
{
    [OrchestrationTrigger]
    public async Task Run(TaskOrchestrationContext ctx)
    {
        await ctx.CallActivityAsync("ValidateOrder");
        await ctx.CallActivityWithRetryAsync("ChargePayment");
    }
}
""");

        var config = new DurableDocConfig
        {
            BusinessView = new BusinessViewOptions
            {
                Steps =
                [
                    new BusinessStepOverlay
                    {
                        Orchestrator = "Run",
                        Step = "ValidateOrder",
                        Label = "Validate order",
                        Group = "Review order",
                    },
                    new BusinessStepOverlay
                    {
                        Orchestrator = "Run",
                        Step = "ChargePayment",
                        Hide = true,
                    },
                ],
            },
        };

        var analyzer = new WorkflowAnalyzer();
        var diagram = Assert.Single(await analyzer.AnalyzeAsync(fixture.DirectoryPath, config));

        var validateNode = Assert.Single(diagram.Nodes, node => node.Name == "ValidateOrder");
        Assert.Equal("Validate order", validateNode.BusinessName);
        Assert.Equal("Review order", validateNode.BusinessGroup);

        var retryNode = Assert.Single(diagram.Nodes, node => node.Name == "ChargePayment");
        Assert.True(retryNode.HideInBusiness);
    }

    [Fact]
    public async Task WorkflowDiagramJson_SerializesResult()
    {
        using var fixture = new TempSourceFixture("""
using System.Threading.Tasks;

public class Demo
{
    [OrchestrationTrigger]
    public async Task Run(TaskOrchestrationContext ctx)
    {
        await ctx.CallActivityAsync("One");
    }
}
""");

        var analyzer = new WorkflowAnalyzer();
        var diagrams = await analyzer.AnalyzeAsync(fixture.DirectoryPath);

        var json = WorkflowDiagramJson.Serialize(Assert.Single(diagrams));

        Assert.Contains("\"orchestratorName\"", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"nodes\"", json, StringComparison.OrdinalIgnoreCase);
    }
}

internal sealed class TempSourceFixture : IDisposable
{
    public string DirectoryPath { get; }

    public TempSourceFixture(string source)
    {
        DirectoryPath = Path.Combine(Path.GetTempPath(), "durable-doc-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(DirectoryPath);
        File.WriteAllText(Path.Combine(DirectoryPath, "Sample.cs"), source);
    }

    public void Dispose()
    {
        if (Directory.Exists(DirectoryPath))
        {
            Directory.Delete(DirectoryPath, recursive: true);
        }
    }
}

internal static class SampleProjectLocator
{
    public static string GetAdvancedSampleProjectPath()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            var solutionPath = Path.Combine(directory.FullName, "durable-doc.sln");
            if (File.Exists(solutionPath))
            {
                return Path.Combine(
                    directory.FullName,
                    "samples",
                    "DurableDoc.Sample.Advanced",
                    "DurableDoc.Sample.Advanced.csproj");
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the durable-doc solution root.");
    }
}
