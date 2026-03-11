using DurableDoc.Analysis;
using DurableDoc.Configuration;
using DurableDoc.Domain;

namespace DurableDoc.Analysis.Tests;

public class SmokeTests
{
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
