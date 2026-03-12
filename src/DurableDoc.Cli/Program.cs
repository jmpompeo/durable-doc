using DurableDoc.Configuration;
using System.CommandLine;
using System.CommandLine.Invocation;

var configOption = new Option<FileInfo?>("--config", "Path to durable-doc.json");
var verbosityOption = new Option<string>("--verbosity", () => "normal", "Log verbosity: quiet, normal, or detailed");
var ciOption = new Option<bool>("--ci", "Use CI-friendly logging");
var strictOption = new Option<bool>("--strict", "Treat warnings as failures where supported");

var root = new RootCommand("durable-doc CLI");
root.AddGlobalOption(configOption);
root.AddGlobalOption(verbosityOption);
root.AddGlobalOption(ciOption);
root.AddGlobalOption(strictOption);

root.AddCommand(CreateGenerateCommand(configOption, verbosityOption, ciOption, strictOption));
root.AddCommand(CreateListCommand(configOption, verbosityOption, ciOption));
root.AddCommand(CreateValidateCommand(configOption, verbosityOption, ciOption, strictOption));
root.AddCommand(CreateDashboardCommand(verbosityOption, ciOption));

return await root.InvokeAsync(args);

static Command CreateGenerateCommand(
    Option<FileInfo?> configOption,
    Option<string> verbosityOption,
    Option<bool> ciOption,
    Option<bool> strictOption)
{
    var inputOption = new Option<string>("--input", "Path to solution, project, or source folder")
    {
        IsRequired = true,
    };
    var orchestratorOption = new Option<string?>("--orchestrator", "Optional orchestrator name filter");
    var modeOption = new Option<string>("--mode", () => "developer", "Diagram mode: developer or business");
    var formatOption = new Option<string?>("--format", "Output format. MVP supports 'mermaid'.");
    var outputOption = new Option<DirectoryInfo?>(
        "--output",
        "Directory for generated diagrams and dashboard");
    var openOption = new Option<bool>("--open", "Serve the generated dashboard on localhost and open it in the default browser");

    var command = new Command("generate", "Generate Mermaid diagrams and build the static dashboard");
    command.AddOption(inputOption);
    command.AddOption(orchestratorOption);
    command.AddOption(modeOption);
    command.AddOption(formatOption);
    command.AddOption(outputOption);
    command.AddOption(openOption);
    command.SetHandler(async (InvocationContext invocationContext) =>
    {
        var input = invocationContext.ParseResult.GetValueForOption(inputOption)!;
        var orchestrator = invocationContext.ParseResult.GetValueForOption(orchestratorOption);
        var mode = invocationContext.ParseResult.GetValueForOption(modeOption)!;
        var format = invocationContext.ParseResult.GetValueForOption(formatOption);
        var output = invocationContext.ParseResult.GetValueForOption(outputOption);
        var open = invocationContext.ParseResult.GetValueForOption(openOption);
        var configFile = invocationContext.ParseResult.GetValueForOption(configOption);
        var verbosity = invocationContext.ParseResult.GetValueForOption(verbosityOption)!;
        var ci = invocationContext.ParseResult.GetValueForOption(ciOption);
        var strict = invocationContext.ParseResult.GetValueForOption(strictOption);
        var context = CreateContext(verbosity, ci);
        Environment.ExitCode = await DurableDoc.Cli.GenerateCommandHandler.ExecuteAsync(
            input,
            output?.FullName,
            orchestrator,
            mode,
            format,
            configFile?.FullName,
            strict,
            context,
            open);
    });

    return command;
}

static Command CreateListCommand(
    Option<FileInfo?> configOption,
    Option<string> verbosityOption,
    Option<bool> ciOption)
{
    var inputOption = new Option<string>("--input", "Path to solution, project, or source folder")
    {
        IsRequired = true,
    };
    var orchestratorOption = new Option<string?>("--orchestrator", "Optional orchestrator name filter");

    var command = new Command("list", "List discovered orchestrators and basic workflow counts");
    command.AddOption(inputOption);
    command.AddOption(orchestratorOption);
    command.SetHandler(async (string input, string? orchestrator, FileInfo? configFile, string verbosity, bool ci) =>
    {
        var context = CreateContext(verbosity, ci);
        Environment.ExitCode = await DurableDoc.Cli.ListCommandHandler.ExecuteAsync(
            input,
            orchestrator,
            configFile?.FullName,
            context);
    }, inputOption, orchestratorOption, configOption, verbosityOption, ciOption);

    return command;
}

static Command CreateValidateCommand(
    Option<FileInfo?> configOption,
    Option<string> verbosityOption,
    Option<bool> ciOption,
    Option<bool> strictOption)
{
    var inputOption = new Option<string>("--input", "Path to solution, project, or source folder")
    {
        IsRequired = true,
    };
    var orchestratorOption = new Option<string?>("--orchestrator", "Optional orchestrator name filter");

    var command = new Command("validate", "Validate configuration and analyze the requested input for warnings");
    command.AddOption(inputOption);
    command.AddOption(orchestratorOption);
    command.SetHandler(async (string input, string? orchestrator, FileInfo? configFile, string verbosity, bool ci, bool strict) =>
    {
        var context = CreateContext(verbosity, ci);
        Environment.ExitCode = await DurableDoc.Cli.ValidateCommandHandler.ExecuteAsync(
            input,
            orchestrator,
            configFile?.FullName,
            strict,
            context);
    }, inputOption, orchestratorOption, configOption, verbosityOption, ciOption, strictOption);

    return command;
}

static Command CreateDashboardCommand(Option<string> verbosityOption, Option<bool> ciOption)
{
    var inputOption = new Option<string>("--input", "Directory containing generated diagram artifacts")
    {
        IsRequired = true,
    };
    var openOption = new Option<bool>("--open", "Serve the dashboard on localhost and open it in the default browser");

    var command = new Command("dashboard", "Build the static dashboard from generated diagram artifacts");
    command.AddOption(inputOption);
    command.AddOption(openOption);
    command.SetHandler(async (InvocationContext invocationContext) =>
    {
        var input = invocationContext.ParseResult.GetValueForOption(inputOption)!;
        var open = invocationContext.ParseResult.GetValueForOption(openOption);
        var verbosity = invocationContext.ParseResult.GetValueForOption(verbosityOption)!;
        var ci = invocationContext.ParseResult.GetValueForOption(ciOption);
        var context = CreateContext(verbosity, ci);
        Environment.ExitCode = await DurableDoc.Cli.DashboardCommandHandler.ExecuteAsync(input, context, open);
    });

    return command;
}

static DurableDoc.Cli.CliCommandContext CreateContext(string verbosity, bool ci)
{
    if (!Enum.TryParse<DurableDoc.Cli.CliVerbosity>(verbosity, ignoreCase: true, out var parsed))
    {
        throw new ArgumentException($"Unsupported verbosity '{verbosity}'. Use 'quiet', 'normal', or 'detailed'.", nameof(verbosity));
    }

    return DurableDoc.Cli.CliCommandContext.CreateDefault(parsed, ci);
}
