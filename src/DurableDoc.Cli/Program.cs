using System.CommandLine;

var root = new RootCommand("durable-doc CLI (Phase 0 scaffold)");

root.AddCommand(CreatePlaceholderCommand("generate"));
root.AddCommand(CreatePlaceholderCommand("list"));
root.AddCommand(CreatePlaceholderCommand("validate"));
root.AddCommand(CreatePlaceholderCommand("dashboard"));

return await root.InvokeAsync(args);

static Command CreatePlaceholderCommand(string name)
{
    var command = new Command(name, $"{name} command");
    command.SetHandler(() => Console.WriteLine("Phase 0 scaffold only: Not yet implemented."));
    return command;
}
