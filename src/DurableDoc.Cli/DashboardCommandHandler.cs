using DurableDoc.Dashboard;

namespace DurableDoc.Cli;

public static class DashboardCommandHandler
{
    public static Task<int> ExecuteAsync(
        string inputDirectory,
        CliCommandContext? context = null,
        CancellationToken cancellationToken = default)
    {
        context ??= CliCommandContext.CreateDefault();
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            var result = DashboardGenerator.BuildFromArtifacts(inputDirectory);
            context.Info($"Dashboard ready at {result.DashboardPath}");
            return Task.FromResult(0);
        }
        catch (Exception ex)
        {
            context.Fail(ex.Message);
            return Task.FromResult(1);
        }
    }
}
