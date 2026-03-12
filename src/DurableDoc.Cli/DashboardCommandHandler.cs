using DurableDoc.Dashboard;

namespace DurableDoc.Cli;

public static class DashboardCommandHandler
{
    public static async Task<int> ExecuteAsync(
        string inputDirectory,
        int? port = null,
        bool noOpen = false,
        CliCommandContext? context = null,
        CancellationToken cancellationToken = default)
    {
        context ??= CliCommandContext.CreateDefault();
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            var result = DashboardGenerator.BuildFromArtifacts(inputDirectory);
            context.Info($"Dashboard ready at {result.DashboardPath}");

            if (!context.Ci)
            {
                var session = await DashboardServerLauncher.EnsureServerAsync(
                    inputDirectory,
                    port,
                    openBrowser: !noOpen,
                    cancellationToken).ConfigureAwait(false);
                context.Info($"Dashboard available at {session.Url}");
            }

            return 0;
        }
        catch (Exception ex)
        {
            context.Fail(ex.Message);
            return 1;
        }
    }

    public static async Task<int> ExecuteServeLoopAsync(
        string inputDirectory,
        int port,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await DashboardServerHost.RunAsync(inputDirectory, port, cancellationToken).ConfigureAwait(false);
            return 0;
        }
        catch
        {
            return 1;
        }
    }
}
