using DurableDoc.Configuration;
using System.CommandLine;

var configOption = new Option<FileInfo?>("--config", "Path to durable-doc.json");

var root = new RootCommand("durable-doc CLI");
root.AddGlobalOption(configOption);

root.AddCommand(CreatePlaceholderCommand("generate"));
root.AddCommand(CreatePlaceholderCommand("list"));
root.AddCommand(CreateValidateCommand(configOption));
root.AddCommand(CreatePlaceholderCommand("dashboard"));

return await root.InvokeAsync(args);

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
