using System.Net.Http;
using System.Text.Json;

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
            configPath: null,
            context: CreateCiContext());

        Assert.Equal(0, exitCode);
        Assert.Single(Directory.EnumerateFiles(fixture.OutputDirectory, "*.mmd"));
        Assert.Single(Directory.EnumerateFiles(fixture.OutputDirectory, "*.diagram.json"));
        Assert.True(File.Exists(Path.Combine(fixture.OutputDirectory, "dashboard-data.json")));

        var mermaid = File.ReadAllText(Directory.EnumerateFiles(fixture.OutputDirectory, "*.mmd").Single());
        var dashboard = File.ReadAllText(Path.Combine(fixture.OutputDirectory, "index.html"));
        var artifact = File.ReadAllText(Directory.EnumerateFiles(fixture.OutputDirectory, "*.diagram.json").Single());

        Assert.Contains("flowchart TD", mermaid);
        Assert.Contains("Workflow Studio", dashboard);
        Assert.Contains("\"mode\":\"developer\"", dashboard, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"nodes\":", artifact, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"edges\":", artifact, StringComparison.OrdinalIgnoreCase);
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
            configPath: null,
            context: CreateCiContext());

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
            configPath: null,
            context: CreateCiContext());

        Assert.Equal(0, generateExitCode);

        File.Delete(Path.Combine(fixture.OutputDirectory, "index.html"));

        var dashboardExitCode = await DurableDoc.Cli.DashboardCommandHandler.ExecuteAsync(
            fixture.OutputDirectory,
            context: CreateCiContext());

        Assert.Equal(0, dashboardExitCode);

        var dashboard = File.ReadAllText(Path.Combine(fixture.OutputDirectory, "index.html"));
        Assert.Contains("Workflow Studio", dashboard);
        Assert.Contains("Run", dashboard);
        Assert.Contains("mermaid.min.js", dashboard);
    }

    [Fact]
    public async Task Dashboard_serves_localhost_page_without_opening_browser()
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
    }
}
""");

        var generateExitCode = await DurableDoc.Cli.GenerateCommandHandler.ExecuteAsync(
            fixture.SourceDirectory,
            fixture.OutputDirectory,
            orchestratorName: null,
            mode: "developer",
            configPath: null,
            context: CreateCiContext());

        Assert.Equal(0, generateExitCode);

        var output = new StringWriter();
        var error = new StringWriter();
        var context = new DurableDoc.Cli.CliCommandContext(output, error, DurableDoc.Cli.CliVerbosity.Normal, ci: false);

        try
        {
            var dashboardExitCode = await DurableDoc.Cli.DashboardCommandHandler.ExecuteAsync(
                fixture.OutputDirectory,
                noOpen: true,
                context: context);

            Assert.Equal(0, dashboardExitCode);

            var urlLine = output.ToString()
                .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries)
                .Single(line => line.StartsWith("Dashboard available at ", StringComparison.Ordinal));
            var url = urlLine["Dashboard available at ".Length..];

            using var client = new HttpClient();
            var html = await client.GetStringAsync(url);

            Assert.Contains("Workflow Studio", html);
            Assert.Equal(string.Empty, error.ToString());
        }
        finally
        {
            StopDashboardServer(fixture.OutputDirectory);
        }
    }

    [Fact]
    public async Task List_writes_deterministic_orchestrator_summary()
    {
        using var fixture = new CliFixture(
            """
using System.Threading.Tasks;

public class Demo
{
    [OrchestrationTrigger]
    public async Task Beta(TaskOrchestrationContext ctx)
    {
        await ctx.CallSubOrchestratorAsync("Child");
    }

    [OrchestrationTrigger]
    public async Task Alpha(TaskOrchestrationContext ctx)
    {
        await ctx.CallActivityAsync("ValidateOrder");
        await ctx.WaitForExternalEvent<string>("Approved");
    }
}
""");

        var output = new StringWriter();
        var error = new StringWriter();
        var context = new DurableDoc.Cli.CliCommandContext(output, error, DurableDoc.Cli.CliVerbosity.Normal, ci: false);

        var exitCode = await DurableDoc.Cli.ListCommandHandler.ExecuteAsync(
            fixture.SourceDirectory,
            orchestratorName: null,
            configPath: null,
            context: context);

        Assert.Equal(0, exitCode);
        var lines = output.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(2, lines.Length);
        Assert.StartsWith("Alpha | ", lines[0], StringComparison.Ordinal);
        Assert.StartsWith("Beta | ", lines[1], StringComparison.Ordinal);
    }

    [Fact]
    public async Task Validate_strict_fails_when_warnings_are_present()
    {
        using var fixture = new CliFixture(
            """
using System.Threading.Tasks;

public class Demo
{
    [OrchestrationTrigger]
    public Task Run(TaskOrchestrationContext ctx)
    {
        return Task.CompletedTask;
    }
}
""");

        var output = new StringWriter();
        var error = new StringWriter();
        var context = new DurableDoc.Cli.CliCommandContext(output, error, DurableDoc.Cli.CliVerbosity.Normal, ci: true);

        var exitCode = await DurableDoc.Cli.ValidateCommandHandler.ExecuteAsync(
            fixture.SourceDirectory,
            configPath: null,
            strict: true,
            context: context);

        Assert.Equal(1, exitCode);
        Assert.Contains("warning:Run:", error.ToString(), StringComparison.Ordinal);
        Assert.Contains("error:Validation completed with warnings", error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Generate_quiet_suppresses_non_error_output()
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
    }
}
""");

        var output = new StringWriter();
        var error = new StringWriter();
        var context = new DurableDoc.Cli.CliCommandContext(output, error, DurableDoc.Cli.CliVerbosity.Quiet, ci: true);

        var exitCode = await DurableDoc.Cli.GenerateCommandHandler.ExecuteAsync(
            fixture.SourceDirectory,
            fixture.OutputDirectory,
            orchestratorName: null,
            mode: "developer",
            configPath: null,
            strict: false,
            context: context);

        Assert.Equal(0, exitCode);
        Assert.Equal(string.Empty, output.ToString());
        Assert.Equal(string.Empty, error.ToString());
    }

    [Fact]
    public async Task Generate_on_advanced_sample_project_produces_multiple_diagrams_for_manual_verification()
    {
        using var outputFixture = new OutputFixture();

        var exitCode = await DurableDoc.Cli.GenerateCommandHandler.ExecuteAsync(
            GetAdvancedSampleProjectPath(),
            outputFixture.OutputDirectory,
            orchestratorName: null,
            mode: "developer",
            configPath: null,
            context: CreateCiContext());

        Assert.Equal(0, exitCode);
        Assert.Equal(4, Directory.EnumerateFiles(outputFixture.OutputDirectory, "*.mmd").Count());
        Assert.Equal(4, Directory.EnumerateFiles(outputFixture.OutputDirectory, "*.diagram.json").Count());

        var mainDiagramPath = Directory.EnumerateFiles(outputFixture.OutputDirectory, "*runcustomeronboarding*.mmd").Single();
        var mermaid = File.ReadAllText(mainDiagramPath);
        var dashboard = File.ReadAllText(Path.Combine(outputFixture.OutputDirectory, "index.html"));

        Assert.Contains("LoadApplication", mermaid);
        Assert.Contains("{{\"ReserveCreditCheck\"}}", mermaid);
        Assert.Contains("[[\"WaitForCustomerApproval\"]]", mermaid);
        Assert.Contains("[/\"CreateTimer\"/]", mermaid);
        Assert.Contains("CollectDocumentsSubOrchestrator", mermaid);
        Assert.Contains("ProvisionAccountSubOrchestrator", dashboard);
        Assert.Contains("ScheduleFollowUpSubOrchestrator", dashboard);
    }

    [Fact]
    public async Task Validate_on_advanced_sample_project_succeeds_without_warnings()
    {
        var output = new StringWriter();
        var error = new StringWriter();
        var context = new DurableDoc.Cli.CliCommandContext(output, error, DurableDoc.Cli.CliVerbosity.Detailed, ci: false);

        var exitCode = await DurableDoc.Cli.ValidateCommandHandler.ExecuteAsync(
            GetAdvancedSampleProjectPath(),
            configPath: null,
            strict: true,
            context: context);

        Assert.Equal(0, exitCode);
        Assert.Contains("Validation succeeded for 4 orchestrator(s).", output.ToString());
        Assert.Equal(string.Empty, error.ToString());
    }

    [Fact]
    public async Task List_on_advanced_sample_project_reports_all_manual_test_orchestrations()
    {
        var output = new StringWriter();
        var error = new StringWriter();
        var context = new DurableDoc.Cli.CliCommandContext(output, error, DurableDoc.Cli.CliVerbosity.Normal, ci: false);

        var exitCode = await DurableDoc.Cli.ListCommandHandler.ExecuteAsync(
            GetAdvancedSampleProjectPath(),
            orchestratorName: null,
            configPath: null,
            context: context);

        Assert.Equal(0, exitCode);

        var lines = output.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(4, lines.Length);
        Assert.Contains(lines, line => line.StartsWith("RunCustomerOnboarding | ", StringComparison.Ordinal));
        Assert.Contains(lines, line => line.Contains("activities=4", StringComparison.Ordinal) && line.Contains("subOrchestrators=2", StringComparison.Ordinal));
        Assert.Equal(string.Empty, error.ToString());
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

    private sealed class OutputFixture : IDisposable
    {
        public OutputFixture()
        {
            OutputDirectory = Path.Combine(Path.GetTempPath(), "durable-doc-cli-output", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(OutputDirectory);
        }

        public string OutputDirectory { get; }

        public void Dispose()
        {
            if (Directory.Exists(OutputDirectory))
            {
                Directory.Delete(OutputDirectory, recursive: true);
            }
        }
    }

    private static string GetAdvancedSampleProjectPath()
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

    private static DurableDoc.Cli.CliCommandContext CreateCiContext()
    {
        return new DurableDoc.Cli.CliCommandContext(new StringWriter(), new StringWriter(), DurableDoc.Cli.CliVerbosity.Normal, ci: true);
    }

    private static void StopDashboardServer(string outputDirectory)
    {
        var statePath = Path.Combine(outputDirectory, ".durable-doc-dashboard.server.json");
        if (!File.Exists(statePath))
        {
            return;
        }

        var json = File.ReadAllText(statePath);
        using var document = JsonDocument.Parse(json);
        if (!document.RootElement.TryGetProperty("ProcessId", out var processIdElement))
        {
            processIdElement = document.RootElement.GetProperty("processId");
        }

        try
        {
            var process = System.Diagnostics.Process.GetProcessById(processIdElement.GetInt32());
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch
        {
        }
    }
}
