namespace DurableDoc.Cli;

public enum CliVerbosity
{
    Quiet,
    Normal,
    Detailed,
}

public sealed class CliCommandContext
{
    public static CliCommandContext CreateDefault(CliVerbosity verbosity = CliVerbosity.Normal, bool ci = false)
    {
        return new CliCommandContext(Console.Out, Console.Error, verbosity, ci);
    }

    public CliCommandContext(TextWriter output, TextWriter error, CliVerbosity verbosity = CliVerbosity.Normal, bool ci = false)
    {
        Output = output;
        Error = error;
        Verbosity = verbosity;
        Ci = ci;
    }

    public TextWriter Output { get; }

    public TextWriter Error { get; }

    public CliVerbosity Verbosity { get; }

    public bool Ci { get; }

    public void Info(string message)
    {
        if (Verbosity == CliVerbosity.Quiet)
        {
            return;
        }

        Output.WriteLine(message);
    }

    public void Detail(string message)
    {
        if (Verbosity != CliVerbosity.Detailed)
        {
            return;
        }

        Output.WriteLine(message);
    }

    public void Warn(string message)
    {
        if (Verbosity == CliVerbosity.Quiet)
        {
            return;
        }

        var prefix = Ci ? "warning:" : "Warning: ";
        Error.WriteLine(prefix + message);
    }

    public void Fail(string message)
    {
        var prefix = Ci ? "error:" : "Error: ";
        Error.WriteLine(prefix + message);
    }
}
