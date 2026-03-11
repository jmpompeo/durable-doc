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
    public async Task AnalyzeAsync_RecognizesBuiltInWrapperWithoutConfig()
    {
        using var fixture = new TempSourceFixture("""
using System.Threading.Tasks;

public class Demo
{
    [OrchestrationTrigger]
    public async Task Run(TaskOrchestrationContext ctx)
    {
        await CallActivityWithVoidResult("ShipOrder");
    }

    private Task CallActivityWithVoidResult(string name) => Task.CompletedTask;
}
""");

        var analyzer = new WorkflowAnalyzer();
        var diagrams = await analyzer.AnalyzeAsync(fixture.DirectoryPath);

        var diagram = Assert.Single(diagrams);
        Assert.Contains(diagram.Nodes, n => n.NodeType == WorkflowNodeType.Activity && n.DisplayLabel == "ShipOrder");
    }

    [Fact]
    public async Task AnalyzeAsync_InlinesHelpers_AndDetectsBranchesAndFanOut()
    {
        using var fixture = new TempSourceFixture("""
using System.Threading.Tasks;

public class Demo
{
    [OrchestrationTrigger]
    public async Task Run(TaskOrchestrationContext ctx)
    {
        await RunValidation(ctx);

        if (true)
        {
            await ctx.CallSubOrchestratorAsync("ApproveOrder");
        }
        else
        {
            await ctx.CallActivityAsync("RejectOrder");
        }

        await Task.WhenAll(
            ctx.CallActivityAsync("NotifyWarehouse"),
            ctx.CallActivityAsync("NotifyCustomer"));
    }

    private async Task RunValidation(TaskOrchestrationContext ctx)
    {
        await ctx.CallActivityAsync("ValidateOrder");
    }
}
""");

        var analyzer = new WorkflowAnalyzer();
        var diagrams = await analyzer.AnalyzeAsync(fixture.DirectoryPath);

        var diagram = Assert.Single(diagrams);
        Assert.Contains(diagram.Nodes, node => node.NodeType == WorkflowNodeType.Activity && node.DisplayLabel == "ValidateOrder");
        Assert.Contains(diagram.Nodes, node => node.NodeType == WorkflowNodeType.Decision);
        Assert.Contains(diagram.Nodes, node => node.NodeType == WorkflowNodeType.FanOut);
        Assert.Contains(diagram.Nodes, node => node.NodeType == WorkflowNodeType.FanIn);
        Assert.Contains(diagram.Edges, edge => edge.ConditionLabel == "true");
        Assert.Contains(diagram.Edges, edge => edge.ConditionLabel == "false");
    }

    [Fact]
    public async Task AnalyzeAsync_OnSingleFileInput_OnlyAnalyzesThatFile()
    {
        using var fixture = new TempSourceFixture(
            """
using System.Threading.Tasks;

public class FirstDemo
{
    [OrchestrationTrigger]
    public async Task First(TaskOrchestrationContext ctx)
    {
        await ctx.CallActivityAsync("ValidateOrder");
    }
}
""",
            """
using System.Threading.Tasks;

public class SecondDemo
{
    [OrchestrationTrigger]
    public async Task Second(TaskOrchestrationContext ctx)
    {
        await ctx.CallActivityAsync("ChargePayment");
    }
}
""");

        var analyzer = new WorkflowAnalyzer();
        var diagrams = await analyzer.AnalyzeAsync(fixture.PrimaryFilePath);

        var diagram = Assert.Single(diagrams);
        Assert.Equal("First", diagram.OrchestratorName);
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

    public string PrimaryFilePath { get; }

    public TempSourceFixture(params string[] sources)
    {
        DirectoryPath = Path.Combine(Path.GetTempPath(), "durable-doc-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(DirectoryPath);
        PrimaryFilePath = Path.Combine(DirectoryPath, "Sample0.cs");

        for (var index = 0; index < sources.Length; index++)
        {
            File.WriteAllText(Path.Combine(DirectoryPath, $"Sample{index}.cs"), sources[index]);
        }
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
