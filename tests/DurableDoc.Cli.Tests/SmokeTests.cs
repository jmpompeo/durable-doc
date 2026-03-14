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
        Assert.True(File.Exists(Path.Combine(fixture.OutputDirectory, "mermaid.min.js")));
        Assert.True(File.Exists(Path.Combine(fixture.OutputDirectory, "dashboard.css")));
        Assert.True(File.Exists(Path.Combine(fixture.OutputDirectory, "dashboard.js")));
        Assert.True(File.Exists(Path.Combine(fixture.OutputDirectory, "dashboard-data.json")));
        Assert.True(File.Exists(Path.Combine(fixture.OutputDirectory, "dashboard.css")));
        Assert.True(File.Exists(Path.Combine(fixture.OutputDirectory, "dashboard.js")));
        Assert.True(File.Exists(Path.Combine(fixture.OutputDirectory, "dashboard-data.json")));

        var mermaid = File.ReadAllText(Directory.EnumerateFiles(fixture.OutputDirectory, "*.mmd").Single());
        var dashboard = File.ReadAllText(Path.Combine(fixture.OutputDirectory, "index.html"));
        var bundle = File.ReadAllText(Path.Combine(fixture.OutputDirectory, "mermaid.min.js"));
        var artifact = File.ReadAllText(Directory.EnumerateFiles(fixture.OutputDirectory, "*.diagram.json").Single());
        var dashboardData = File.ReadAllText(Path.Combine(fixture.OutputDirectory, "dashboard-data.json"));
        
        Assert.Contains("flowchart TD", mermaid);
        Assert.Contains("First", dashboard);
        Assert.Contains("\"mode\": \"developer\"", dashboard, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("dashboard-bootstrap", dashboard, StringComparison.Ordinal);
        Assert.Contains("dashboard.js", dashboard, StringComparison.Ordinal);
        Assert.Contains("dashboard.css", dashboard, StringComparison.Ordinal);
        Assert.Contains("\"mode\": \"developer\"", dashboard, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("dashboard-bootstrap", dashboard, StringComparison.Ordinal);
        Assert.Contains("dashboard.js", dashboard, StringComparison.Ordinal);
        Assert.Contains("dashboard.css", dashboard, StringComparison.Ordinal);
        Assert.DoesNotContain("__PAYLOAD__", dashboard, StringComparison.Ordinal);
        Assert.DoesNotContain("{{payload}}", dashboard, StringComparison.Ordinal);
        Assert.Contains(@"source.split(/\r?\n/)", bundle, StringComparison.Ordinal);
        Assert.DoesNotContain(@"<br\\/>", bundle, StringComparison.Ordinal);
        Assert.Contains("\"nodes\": [", artifact, StringComparison.Ordinal);
        Assert.Contains("\"edges\": [", artifact, StringComparison.Ordinal);
        Assert.Contains("\"nodeType\": \"OrchestratorStart\"", artifact, StringComparison.Ordinal);
        Assert.Contains("\"nodeType\": \"OrchestratorStart\"", dashboardData, StringComparison.Ordinal);
        Assert.Contains("\"nodes\": [", artifact, StringComparison.Ordinal);
        Assert.Contains("\"edges\": [", artifact, StringComparison.Ordinal);
        Assert.Contains("\"nodeType\": \"OrchestratorStart\"", artifact, StringComparison.Ordinal);
        Assert.Contains("\"nodeType\": \"OrchestratorStart\"", dashboardData, StringComparison.Ordinal);
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
    public async Task Generate_honors_orchestrator_filter_when_output_contains_previous_artifacts()
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

        var initialExitCode = await DurableDoc.Cli.GenerateCommandHandler.ExecuteAsync(
            fixture.SourceDirectory,
            fixture.OutputDirectory,
            orchestratorName: null,
            mode: "developer",
            configPath: null);

        Assert.Equal(0, initialExitCode);
        Assert.Equal(2, Directory.EnumerateFiles(fixture.OutputDirectory, "*.diagram.json").Count());

        var filteredExitCode = await DurableDoc.Cli.GenerateCommandHandler.ExecuteAsync(
            fixture.SourceDirectory,
            fixture.OutputDirectory,
            orchestratorName: "Second",
            mode: "developer",
            configPath: null);

        Assert.Equal(0, filteredExitCode);
        Assert.Single(Directory.EnumerateFiles(fixture.OutputDirectory, "*.diagram.json"));
        Assert.Single(Directory.EnumerateFiles(fixture.OutputDirectory, "*.mmd"));

        var dashboardData = File.ReadAllText(Path.Combine(fixture.OutputDirectory, "dashboard-data.json"));
        Assert.Contains("\"orchestratorName\": \"Second\"", dashboardData, StringComparison.Ordinal);
        Assert.DoesNotContain("\"orchestratorName\": \"First\"", dashboardData, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Generate_on_external_project_input_honors_orchestrator_filter()
    {
        using var fixture = new ProjectFixture(
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
            fixture.ProjectPath,
            fixture.OutputDirectory,
            orchestratorName: "Second",
            mode: "developer",
            configPath: null);

        Assert.Equal(0, exitCode);
        Assert.Single(Directory.EnumerateFiles(fixture.OutputDirectory, "*.diagram.json"));

        var dashboardData = File.ReadAllText(Path.Combine(fixture.OutputDirectory, "dashboard-data.json"));
        Assert.Contains("\"orchestratorName\": \"Second\"", dashboardData, StringComparison.Ordinal);
        Assert.DoesNotContain("\"orchestratorName\": \"First\"", dashboardData, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Generate_reports_discovered_orchestrators_when_filter_does_not_match()
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

        var output = new StringWriter();
        var error = new StringWriter();
        var context = new DurableDoc.Cli.CliCommandContext(output, error, DurableDoc.Cli.CliVerbosity.Normal, ci: false);

        var exitCode = await DurableDoc.Cli.GenerateCommandHandler.ExecuteAsync(
            fixture.SourceDirectory,
            fixture.OutputDirectory,
            orchestratorName: "Missing",
            mode: "developer",
            configPath: null,
            context: context);

        Assert.Equal(1, exitCode);
        Assert.Contains("No orchestrators matched filter 'Missing'.", error.ToString(), StringComparison.Ordinal);
        Assert.Contains("First", error.ToString(), StringComparison.Ordinal);
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
        Assert.Contains("Filter orchestrators", dashboard);
        Assert.Contains("Filter orchestrators", dashboard);
        Assert.Contains("Run", dashboard);
        Assert.Contains("mermaid.min.js", dashboard);
        Assert.Contains("dashboard.js", dashboard);
        Assert.Contains("dashboard.css", dashboard);
        Assert.Contains("dashboard.js", dashboard);
        Assert.Contains("dashboard.css", dashboard);
        Assert.True(File.Exists(Path.Combine(fixture.OutputDirectory, "mermaid.min.js")));
        Assert.True(File.Exists(Path.Combine(fixture.OutputDirectory, "dashboard.css")));
        Assert.True(File.Exists(Path.Combine(fixture.OutputDirectory, "dashboard.js")));
        Assert.True(File.Exists(Path.Combine(fixture.OutputDirectory, "dashboard-data.json")));
    }

    [Fact]
    public async Task Dashboard_on_source_project_input_generates_only_selected_orchestrator()
    {
        using var fixture = new ProjectFixture(
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

        var output = new StringWriter();
        var error = new StringWriter();
        var context = new DurableDoc.Cli.CliCommandContext(output, error, DurableDoc.Cli.CliVerbosity.Normal, ci: false);
        using var cancellation = new CancellationTokenSource();
        Uri? launchedUri = null;

        var commandTask = DurableDoc.Cli.DashboardCommandHandler.ExecuteAsync(
            fixture.ProjectPath,
            fixture.OutputDirectory,
            orchestratorName: "Second",
            mode: "developer",
            configPath: null,
            context: context,
            openDashboard: true,
            browserLauncher: (uri, _) =>
            {
                launchedUri = uri;
                return Task.CompletedTask;
            },
            cancellationToken: cancellation.Token);

        var previewUri = await WaitForPreviewUriAsync(output, commandTask);
        Assert.Equal(previewUri, launchedUri);

        using var client = new HttpClient();
        var dashboard = await client.GetStringAsync(previewUri);

        Assert.Equal("Second", GetQueryValue(previewUri, "orchestrator"));
        Assert.Equal("developer", GetQueryValue(previewUri, "mode"));
        Assert.Contains("Second", dashboard, StringComparison.Ordinal);
        Assert.DoesNotContain("First", dashboard, StringComparison.Ordinal);

        cancellation.Cancel();

        var exitCode = await commandTask;

        Assert.Equal(0, exitCode);
        Assert.Equal(string.Empty, error.ToString());
        Assert.Single(Directory.EnumerateFiles(fixture.OutputDirectory, "*.diagram.json"));
    }

    [Fact]
    public async Task Dashboard_on_artifact_input_honors_orchestrator_filter()
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

        var generateExitCode = await DurableDoc.Cli.GenerateCommandHandler.ExecuteAsync(
            fixture.SourceDirectory,
            fixture.OutputDirectory,
            orchestratorName: null,
            mode: "developer",
            configPath: null);

        Assert.Equal(0, generateExitCode);
        Assert.Equal(2, Directory.EnumerateFiles(fixture.OutputDirectory, "*.diagram.json").Count());

        var dashboardExitCode = await DurableDoc.Cli.DashboardCommandHandler.ExecuteAsync(
            fixture.OutputDirectory,
            orchestratorName: "Second");

        Assert.Equal(0, dashboardExitCode);
        Assert.Equal(2, Directory.EnumerateFiles(fixture.OutputDirectory, "*.diagram.json").Count());

        var dashboardData = File.ReadAllText(Path.Combine(fixture.OutputDirectory, "dashboard-data.json"));
        Assert.Contains("\"orchestratorName\": \"Second\"", dashboardData, StringComparison.Ordinal);
        Assert.DoesNotContain("\"orchestratorName\": \"First\"", dashboardData, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Dashboard_on_artifact_input_rejects_source_only_options()
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
            configPath: null);

        Assert.Equal(0, generateExitCode);

        var output = new StringWriter();
        var error = new StringWriter();
        var context = new DurableDoc.Cli.CliCommandContext(output, error, DurableDoc.Cli.CliVerbosity.Normal, ci: false);

        var dashboardExitCode = await DurableDoc.Cli.DashboardCommandHandler.ExecuteAsync(
            fixture.OutputDirectory,
            outputDirectory: Path.Combine(fixture.OutputDirectory, "alt"),
            context: context);

        Assert.Equal(1, dashboardExitCode);
        Assert.Contains("The '--output' option is only supported", error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Generate_open_serves_dashboard_until_cancellation()
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

        var output = new StringWriter();
        var error = new StringWriter();
        var context = new DurableDoc.Cli.CliCommandContext(output, error, DurableDoc.Cli.CliVerbosity.Normal, ci: false);
        using var cancellation = new CancellationTokenSource();
        Uri? launchedUri = null;

        var commandTask = DurableDoc.Cli.GenerateCommandHandler.ExecuteAsync(
            fixture.SourceDirectory,
            fixture.OutputDirectory,
            orchestratorName: null,
            mode: "developer",
            configPath: null,
            strict: false,
            context: context,
            openDashboard: true,
            browserLauncher: (uri, _) =>
            {
                launchedUri = uri;
                return Task.CompletedTask;
            },
            cancellationToken: cancellation.Token);

        var previewUri = await WaitForPreviewUriAsync(output, commandTask);

        Assert.Equal(previewUri, launchedUri);

        using var client = new HttpClient();
        var dashboard = await client.GetStringAsync(previewUri);
        var bundle = await client.GetStringAsync(new Uri(previewUri, "mermaid.min.js"));

        Assert.Equal("First", GetQueryValue(previewUri, "orchestrator"));
        Assert.Equal("developer", GetQueryValue(previewUri, "mode"));
        Assert.Equal("First", GetQueryValue(previewUri, "orchestrator"));
        Assert.Equal("developer", GetQueryValue(previewUri, "mode"));
        Assert.Contains("First", dashboard);
        Assert.Contains("\"displayLabel\": \"First\"", dashboard, StringComparison.Ordinal);
        Assert.Contains("\"displayLabel\": \"First\"", dashboard, StringComparison.Ordinal);
        Assert.Contains(@"source.split(/\r?\n/)", bundle, StringComparison.Ordinal);
        Assert.Contains("Press Ctrl+C to stop.", output.ToString(), StringComparison.Ordinal);

        cancellation.Cancel();

        var exitCode = await commandTask;

        Assert.Equal(0, exitCode);
        Assert.Equal(string.Empty, error.ToString());
    }

    [Fact]
    public async Task Dashboard_open_rebuilds_and_serves_dashboard_until_cancellation()
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

        var output = new StringWriter();
        var error = new StringWriter();
        var context = new DurableDoc.Cli.CliCommandContext(output, error, DurableDoc.Cli.CliVerbosity.Normal, ci: false);
        using var cancellation = new CancellationTokenSource();

        var commandTask = DurableDoc.Cli.DashboardCommandHandler.ExecuteAsync(
            fixture.OutputDirectory,
            context: context,
            openDashboard: true,
            browserLauncher: (_, _) => Task.CompletedTask,
            cancellationToken: cancellation.Token);

        var previewUri = await WaitForPreviewUriAsync(output, commandTask);

        using var client = new HttpClient();
        var dashboard = await client.GetStringAsync(previewUri);
        var bundleResponse = await client.GetAsync(new Uri(previewUri, "mermaid.min.js"));

        Assert.Contains("Run", dashboard);
        Assert.True(bundleResponse.IsSuccessStatusCode);

        cancellation.Cancel();

        var exitCode = await commandTask;

        Assert.Equal(0, exitCode);
        Assert.Equal(string.Empty, error.ToString());
    }

    [Fact]
    public async Task Generate_open_is_rejected_in_ci_mode()
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
        var context = new DurableDoc.Cli.CliCommandContext(output, error, DurableDoc.Cli.CliVerbosity.Normal, ci: true);

        var exitCode = await DurableDoc.Cli.GenerateCommandHandler.ExecuteAsync(
            fixture.SourceDirectory,
            fixture.OutputDirectory,
            orchestratorName: null,
            mode: "developer",
            configPath: null,
            strict: false,
            context: context,
            openDashboard: true);

        Assert.Equal(1, exitCode);
        Assert.Contains("The '--open' option is not supported with '--ci'", error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Generate_open_warns_when_browser_launch_fails_but_preview_still_serves()
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
        var context = new DurableDoc.Cli.CliCommandContext(output, error, DurableDoc.Cli.CliVerbosity.Normal, ci: false);
        using var cancellation = new CancellationTokenSource();

        var commandTask = DurableDoc.Cli.GenerateCommandHandler.ExecuteAsync(
            fixture.SourceDirectory,
            fixture.OutputDirectory,
            orchestratorName: null,
            mode: "developer",
            configPath: null,
            strict: false,
            context: context,
            openDashboard: true,
            browserLauncher: (_, _) => throw new InvalidOperationException("boom"),
            cancellationToken: cancellation.Token);

        var previewUri = await WaitForPreviewUriAsync(output, commandTask);

        using var client = new HttpClient();
        var dashboard = await client.GetStringAsync(previewUri);

        Assert.Contains("Run", dashboard);
        Assert.Contains("Could not open browser automatically: boom", error.ToString(), StringComparison.Ordinal);

        cancellation.Cancel();

        var exitCode = await commandTask;

        Assert.Equal(0, exitCode);
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
        Assert.Contains("activities=ValidateOrder", lines[0], StringComparison.Ordinal);
        Assert.Contains("subOrchestrators=Child", lines[1], StringComparison.Ordinal);
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
    public async Task Validate_warns_when_business_metadata_references_missing_step()
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

        var configPath = fixture.WriteConfig(
            """
            {
              "version": 1,
              "businessView": {
                "orchestrators": [
                  {
                    "name": "Run",
                    "steps": [
                      { "name": "MissingStep", "businessName": "Missing step" }
                    ]
                  }
                ]
              }
            }
            """);

        var output = new StringWriter();
        var error = new StringWriter();
        var context = new DurableDoc.Cli.CliCommandContext(output, error, DurableDoc.Cli.CliVerbosity.Normal, ci: false);

        var exitCode = await DurableDoc.Cli.ValidateCommandHandler.ExecuteAsync(
            fixture.SourceDirectory,
            configPath: configPath,
            strict: false,
            context: context);

        Assert.Equal(0, exitCode);
        Assert.Contains("MissingStep", error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Validate_warns_when_stakeholder_metadata_is_incomplete()
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

        var configPath = fixture.WriteConfig(
            """
            {
              "version": 1,
              "businessView": {
                "orchestrators": [
                  {
                    "name": "Run",
                    "businessName": "Order intake"
                  }
                ]
              }
            }
            """);

        var output = new StringWriter();
        var error = new StringWriter();
        var context = new DurableDoc.Cli.CliCommandContext(output, error, DurableDoc.Cli.CliVerbosity.Normal, ci: false);

        var exitCode = await DurableDoc.Cli.ValidateCommandHandler.ExecuteAsync(
            fixture.SourceDirectory,
            audience: "stakeholder",
            configPath: configPath,
            strict: false,
            context: context);

        Assert.Equal(0, exitCode);
        Assert.Contains("missing 'summary'", error.ToString(), StringComparison.Ordinal);
        Assert.Contains("missing 'capability'", error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Generate_stakeholder_defaults_to_business_mode_and_persists_metadata()
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
        await ctx.CallActivityAsync("CreateAccount");
    }
}
""");

        var configPath = fixture.WriteConfig(
            """
            {
              "version": 1,
              "businessView": {
                "orchestrators": [
                  {
                    "name": "Run",
                    "businessName": "Customer onboarding",
                    "summary": "Validates the order and opens the account.",
                    "capability": "Onboarding",
                    "audienceNotes": "Track completion during weekly launch reviews.",
                    "outcomes": ["Order validated", "Account created"],
                    "steps": [
                      { "name": "ValidateOrder", "businessName": "Validate order" },
                      { "name": "CreateAccount", "businessName": "Create account" }
                    ]
                  }
                ]
              }
            }
            """);

        var exitCode = await DurableDoc.Cli.GenerateCommandHandler.ExecuteAsync(
            fixture.SourceDirectory,
            fixture.OutputDirectory,
            orchestratorName: null,
            mode: null,
            configPath: configPath,
            strict: false,
            audience: "stakeholder");

        Assert.Equal(0, exitCode);

        var artifact = File.ReadAllText(Directory.EnumerateFiles(fixture.OutputDirectory, "*.diagram.json").Single());
        var dashboard = File.ReadAllText(Path.Combine(fixture.OutputDirectory, "index.html"));
        var dashboardCss = File.ReadAllText(Path.Combine(fixture.OutputDirectory, "dashboard.css"));

        Assert.Contains("\"mode\": \"business\"", artifact, StringComparison.Ordinal);
        Assert.Contains("\"audience\": \"stakeholder\"", artifact, StringComparison.Ordinal);
        Assert.Contains("\"capability\": \"Onboarding\"", artifact, StringComparison.Ordinal);
        Assert.Contains("\"summary\": \"Validates the order and opens the account.\"", artifact, StringComparison.Ordinal);
        Assert.Contains("\"outcomes\": [", artifact, StringComparison.Ordinal);
        Assert.Contains("data-audience=\"stakeholder\"", dashboard, StringComparison.Ordinal);
        Assert.Contains("stakeholder-overview", dashboard, StringComparison.Ordinal);
        Assert.Contains("technical-only", dashboard, StringComparison.Ordinal);
        Assert.Contains("body[data-audience=\"stakeholder\"] .technical-only", dashboardCss, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Dashboard_on_artifact_input_can_render_stakeholder_audience()
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
            configPath: null);

        Assert.Equal(0, generateExitCode);

        var dashboardExitCode = await DurableDoc.Cli.DashboardCommandHandler.ExecuteAsync(
            fixture.OutputDirectory,
            audience: "stakeholder");

        Assert.Equal(0, dashboardExitCode);

        var dashboard = File.ReadAllText(Path.Combine(fixture.OutputDirectory, "index.html"));
        Assert.Contains("data-audience=\"stakeholder\"", dashboard, StringComparison.Ordinal);
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
        var context = new DurableDoc.Cli.CliCommandContext(output, error, DurableDoc.Cli.CliVerbosity.Quiet, ci: false);

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
            configPath: null);

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
    public async Task Generate_uses_config_default_output_and_mermaid_format()
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

        var configPath = fixture.WriteConfig(
            $$"""
            {
              "version": 1,
              "defaults": {
                "output": "{{fixture.OutputDirectory}}",
                "format": "mermaid"
              }
            }
            """);

        var exitCode = await DurableDoc.Cli.GenerateCommandHandler.ExecuteAsync(
            fixture.SourceDirectory,
            outputDirectory: null,
            orchestratorName: null,
            mode: "developer",
            configPath: configPath);

        Assert.Equal(0, exitCode);
        Assert.Single(Directory.EnumerateFiles(fixture.OutputDirectory, "*.mmd"));
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
        Assert.Contains(lines, line => line.Contains("activities=LoadApplication, ReserveCreditCheck, SendWelcomeEmail, ValidateCustomer", StringComparison.Ordinal));
        Assert.Contains(lines, line => line.Contains("subOrchestrators=CollectDocumentsSubOrchestrator, ProvisionAccountSubOrchestrator", StringComparison.Ordinal));
        Assert.Equal(string.Empty, error.ToString());
    }

    [Fact]
    public async Task List_on_solution_input_discovers_sample_projects()
    {
        var output = new StringWriter();
        var error = new StringWriter();
        var context = new DurableDoc.Cli.CliCommandContext(output, error, DurableDoc.Cli.CliVerbosity.Normal, ci: false);

        var exitCode = await DurableDoc.Cli.ListCommandHandler.ExecuteAsync(
            GetSolutionPath(),
            orchestratorName: "RunCustomerOnboarding",
            configPath: null,
            context: context);

        Assert.Equal(0, exitCode);
        Assert.Contains("RunCustomerOnboarding | ", output.ToString(), StringComparison.Ordinal);
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

        public string WriteConfig(string json)
        {
            var path = Path.Combine(_rootDirectory, "durable-doc.json");
            File.WriteAllText(path, json);
            return path;
        }

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

    private sealed class ProjectFixture : IDisposable
    {
        private readonly string _rootDirectory;

        public ProjectFixture(string source)
        {
            _rootDirectory = Path.Combine(Path.GetTempPath(), "durable-doc-cli-projects", Guid.NewGuid().ToString("N"));
            SourceDirectory = Path.Combine(_rootDirectory, "src");
            OutputDirectory = Path.Combine(_rootDirectory, "out");
            ProjectPath = Path.Combine(_rootDirectory, "ExternalApp.csproj");

            Directory.CreateDirectory(SourceDirectory);
            Directory.CreateDirectory(OutputDirectory);
            File.WriteAllText(
                ProjectPath,
                """
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
</Project>
""");
            File.WriteAllText(Path.Combine(SourceDirectory, "Sample.cs"), source);
        }

        public string ProjectPath { get; }

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

    private static string GetAdvancedSampleProjectPath()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            var solutionPath = Path.Combine(directory.FullName, "durable-doc.sln");
            if (File.Exists(solutionPath))
            {
                return Path.Combine(directory.FullName, "samples", "DurableDoc.Sample.Advanced", "DurableDoc.Sample.Advanced.csproj");
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the durable-doc solution root.");
    }

    private static string GetSolutionPath()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            var solutionPath = Path.Combine(directory.FullName, "durable-doc.sln");
            if (File.Exists(solutionPath))
            {
                return solutionPath;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the durable-doc solution root.");
    }

    private static async Task<Uri> WaitForPreviewUriAsync(StringWriter output, Task<int> commandTask)
    {
        var timeout = DateTime.UtcNow.AddSeconds(5);

        while (DateTime.UtcNow < timeout)
        {
            var previewUri = ExtractPreviewUri(output.ToString());
            if (previewUri is not null)
            {
                return previewUri;
            }

            if (commandTask.IsCompleted)
            {
                break;
            }

            await Task.Delay(50);
        }

        throw new TimeoutException($"The dashboard preview URL was not written. Output was:{Environment.NewLine}{output}");
    }

    private static Uri? ExtractPreviewUri(string output)
    {
        const string prefix = "Dashboard preview at ";

        foreach (var line in output.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries))
        {
            if (line.StartsWith(prefix, StringComparison.Ordinal) &&
                Uri.TryCreate(line[prefix.Length..], UriKind.Absolute, out var uri))
            {
                return uri;
            }
        }

        return null;
    }

    private static string? GetQueryValue(Uri uri, string key)
    {
        var query = uri.Query.TrimStart('?');
        foreach (var segment in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = segment.Split('=', 2);
            if (parts.Length == 2 && string.Equals(parts[0], key, StringComparison.Ordinal))
            {
                return Uri.UnescapeDataString(parts[1]);
            }
        }

        return null;
    }
}
