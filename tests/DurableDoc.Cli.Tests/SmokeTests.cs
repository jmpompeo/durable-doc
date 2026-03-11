using Xunit;

namespace DurableDoc.Cli.Tests;

public class SmokeTests
{
    [Fact]
    public async Task Generate_writes_mermaid_artifacts_and_dashboard()
    {
        using var fixture = new CliFixture(
            """
using System.Threading.Tasks;

public class Demo
{
    [OrchestrationTrigger]
    public async Task First(TaskOrchestrationContext ctx)
    {
        await ctx.CallActivityAsync("ValidateOrder");
    }
}
""");

        var exitCode = await DurableDoc.Cli.GenerateCommandHandler.ExecuteAsync(
            fixture.SourceDirectory,
            fixture.OutputDirectory,
            orchestratorName: null,
            mode: "developer",
            configPath: null);

        Assert.Equal(0, exitCode);
        Assert.Single(Directory.EnumerateFiles(fixture.OutputDirectory, "*.mmd"));
        Assert.Single(Directory.EnumerateFiles(fixture.OutputDirectory, "*.diagram.json"));

        var mermaid = File.ReadAllText(Directory.EnumerateFiles(fixture.OutputDirectory, "*.mmd").Single());
        var dashboard = File.ReadAllText(Path.Combine(fixture.OutputDirectory, "index.html"));

        Assert.Contains("flowchart TD", mermaid);
        Assert.Contains("First", dashboard);
        Assert.Contains("\"mode\":\"developer\"", dashboard, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Generate_honors_orchestrator_filter()
    {
        using var fixture = new CliFixture(
            """
using System.Threading.Tasks;

public class Demo
{
    [OrchestrationTrigger]
    public async Task First(TaskOrchestrationContext ctx)
    {
        await ctx.CallActivityAsync("ValidateOrder");
    }

    [OrchestrationTrigger]
    public async Task Second(TaskOrchestrationContext ctx)
    {
        await ctx.CallActivityAsync("ChargePayment");
    }
}
""");

        var exitCode = await DurableDoc.Cli.GenerateCommandHandler.ExecuteAsync(
            fixture.SourceDirectory,
            fixture.OutputDirectory,
            orchestratorName: "Second",
            mode: "business",
            configPath: null);

        Assert.Equal(0, exitCode);

        var artifactPath = Directory.EnumerateFiles(fixture.OutputDirectory, "*.diagram.json").Single();
        var artifact = File.ReadAllText(artifactPath);

        Assert.Contains("\"orchestratorName\": \"Second\"", artifact);
        Assert.Contains("\"mode\": \"business\"", artifact);
    }

    [Fact]
    public async Task Dashboard_rebuilds_static_html_from_existing_artifacts()
    {
        using var fixture = new CliFixture(
            """
using System.Threading.Tasks;

public class Demo
{
    [OrchestrationTrigger]
    public async Task Run(TaskOrchestrationContext ctx)
    {
        await ctx.CallActivityAsync("ValidateOrder");
        await ctx.WaitForExternalEvent<string>("Approved");
    }
}
""");

        var generateExitCode = await DurableDoc.Cli.GenerateCommandHandler.ExecuteAsync(
            fixture.SourceDirectory,
            fixture.OutputDirectory,
            orchestratorName: null,
            mode: "developer",
            configPath: null);

        Assert.Equal(0, generateExitCode);

        File.Delete(Path.Combine(fixture.OutputDirectory, "index.html"));

        var dashboardExitCode = await DurableDoc.Cli.DashboardCommandHandler.ExecuteAsync(fixture.OutputDirectory);

        Assert.Equal(0, dashboardExitCode);

        var dashboard = File.ReadAllText(Path.Combine(fixture.OutputDirectory, "index.html"));
        Assert.Contains("Filter by orchestrator", dashboard);
        Assert.Contains("Run", dashboard);
        Assert.Contains("mermaid.min.js", dashboard);
    }

    private sealed class CliFixture : IDisposable
    {
        private readonly string _rootDirectory;

        public CliFixture(string source)
        {
            _rootDirectory = Path.Combine(Path.GetTempPath(), "durable-doc-cli-tests", Guid.NewGuid().ToString("N"));
            SourceDirectory = Path.Combine(_rootDirectory, "src");
            OutputDirectory = Path.Combine(_rootDirectory, "out");

            Directory.CreateDirectory(SourceDirectory);
            Directory.CreateDirectory(OutputDirectory);
            File.WriteAllText(Path.Combine(SourceDirectory, "Sample.cs"), source);
        }

        public string SourceDirectory { get; }

        public string OutputDirectory { get; }

        public void Dispose()
        {
            if (Directory.Exists(_rootDirectory))
            {
                Directory.Delete(_rootDirectory, recursive: true);
            }
        }
    }
}
