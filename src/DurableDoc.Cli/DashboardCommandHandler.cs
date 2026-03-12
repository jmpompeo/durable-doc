using DurableDoc.Dashboard;

namespace DurableDoc.Cli;

public static class DashboardCommandHandler
{
    public static async Task<int> ExecuteAsync(
        string inputDirectory,
        CliCommandContext? context = null,
        bool openDashboard = false,
        Func<Uri, CancellationToken, Task>? browserLauncher = null,
        CancellationToken cancellationToken = default)
    {
        context ??= CliCommandContext.CreateDefault();

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!DashboardPreviewHost.ValidateInteractivePreview(context, openDashboard))
            {
                return 1;
            }

            var result = DashboardGenerator.BuildFromArtifacts(inputDirectory);
            context.Info($"Dashboard ready at {result.DashboardPath}");

            if (openDashboard)
            {
                await DashboardPreviewHost.PreviewAsync(
                    inputDirectory,
                    context,
                    orchestratorName: null,
                    mode: null,
                    browserLauncher,
                    cancellationToken).ConfigureAwait(false);
            }

            return 0;
        }
        catch (Exception ex)
        {
            context.Fail(ex.Message);
            return 1;
        }
    }
}
