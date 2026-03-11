using DurableDoc.Configuration;
using System.CommandLine;

var configOption = new Option<FileInfo?>("--config", "Path to durable-doc.json");

var root = new RootCommand("durable-doc CLI");
root.AddGlobalOption(configOption);

root.AddCommand(CreateGenerateCommand(configOption));
root.AddCommand(CreatePlaceholderCommand("list"));
root.AddCommand(CreateValidateCommand(configOption));
root.AddCommand(CreateDashboardCommand());

return await root.InvokeAsync(args);

static Command CreateGenerateCommand(Option<FileInfo?> configOption)
{
    var inputOption = new Option<string>("--input", "Path to solution, project, or source folder")
    {
        IsRequired = true,
    };
    var orchestratorOption = new Option<string?>("--orchestrator", "Optional orchestrator name filter");
    var modeOption = new Option<string>("--mode", () => "developer", "Diagram mode: developer or business");
    var outputOption = new Option<DirectoryInfo>(
        "--output",
        () => new DirectoryInfo(Path.Combine(Directory.GetCurrentDirectory(), "docs", "diagrams")),
        "Directory for generated diagrams and dashboard");

    var command = new Command("generate", "Generate Mermaid diagrams and build the static dashboard");
    command.AddOption(inputOption);
    command.AddOption(orchestratorOption);
    command.AddOption(modeOption);
    command.AddOption(outputOption);
    command.SetHandler(async (string input, string? orchestrator, string mode, DirectoryInfo output, FileInfo? configFile) =>
    {
        Environment.ExitCode = await DurableDoc.Cli.GenerateCommandHandler.ExecuteAsync(
            input,
            output.FullName,
            orchestrator,
            mode,
            configFile?.FullName);
    }, inputOption, orchestratorOption, modeOption, outputOption, configOption);

    return command;
}

static Command CreatePlaceholderCommand(string name)
{
    var command = new Command(name, $"{name} command");
    command.SetHandler(() => Console.WriteLine($"'{name}' is scaffolded in Phase 1 and will be implemented in a later phase."));
    return command;
}

static Command CreateValidateCommand(Option<FileInfo?> configOption)
{
    var command = new Command("validate", "Validate durable-doc configuration");
    command.SetHandler((FileInfo? configFile) =>
    {
        try
        {
            var configPath = configFile?.FullName;
            DurableDocConfigLoader.Load(configPath);
            Console.WriteLine("Configuration is valid.");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
            Environment.ExitCode = 1;
        }
    }, configOption);

    return command;
}

static Command CreateDashboardCommand()
{
    var inputOption = new Option<string>("--input", "Directory containing generated diagram artifacts")
    {
        IsRequired = true,
    };

    var command = new Command("dashboard", "Build the static dashboard from generated diagram artifacts");
    command.AddOption(inputOption);
    command.SetHandler(async (string input) =>
    {
        Environment.ExitCode = await DurableDoc.Cli.DashboardCommandHandler.ExecuteAsync(input);
    }, inputOption);

    return command;
}
