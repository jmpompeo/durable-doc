using System.Diagnostics;
using DurableDoc.Dashboard;

namespace DurableDoc.Cli;

internal static class DashboardPreviewHost
{
    public static async Task PreviewAsync(
        string outputDirectory,
        CliCommandContext context,
        Func<Uri, CancellationToken, Task>? browserLauncher,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        browserLauncher ??= BrowserLauncher.OpenAsync;

        using var lifetime = DashboardPreviewLifetime.Create(cancellationToken);
        await using var session = await DashboardPreviewServer.StartAsync(outputDirectory, lifetime.Token).ConfigureAwait(false);

        context.Output.WriteLine($"Dashboard preview at {session.DashboardUri}");
        context.Output.WriteLine("Press Ctrl+C to stop.");

        try
        {
            await browserLauncher(session.DashboardUri, lifetime.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (lifetime.Token.IsCancellationRequested)
        {
            return;
        }
        catch (Exception ex)
        {
            context.Warn($"Could not open browser automatically: {ex.Message}");
        }

        try
        {
            await session.WaitForShutdownAsync().ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (lifetime.Token.IsCancellationRequested)
        {
        }
    }

    public static bool ValidateInteractivePreview(CliCommandContext context, bool openDashboard)
    {
        if (!openDashboard || !context.Ci)
        {
            return true;
        }

        context.Fail("The '--open' option is not supported with '--ci' because dashboard preview requires an interactive local session.");
        return false;
    }
}

internal static class BrowserLauncher
{
    public static Task OpenAsync(Uri uri, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var process = Process.Start(new ProcessStartInfo
        {
            FileName = uri.ToString(),
            UseShellExecute = true,
        });

        if (process is null)
        {
            throw new InvalidOperationException($"The browser could not be launched for {uri}.");
        }

        return Task.CompletedTask;
    }
}

internal sealed class DashboardPreviewLifetime : IDisposable
{
    private readonly CancellationTokenSource _linkedCancellation;
    private readonly ConsoleCancelEventHandler _cancelHandler;
    private bool _disposed;

    private DashboardPreviewLifetime(CancellationToken cancellationToken)
    {
        _linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _cancelHandler = (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            _linkedCancellation.Cancel();
        };

        Console.CancelKeyPress += _cancelHandler;
    }

    public CancellationToken Token => _linkedCancellation.Token;

    public static DashboardPreviewLifetime Create(CancellationToken cancellationToken)
    {
        return new DashboardPreviewLifetime(cancellationToken);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Console.CancelKeyPress -= _cancelHandler;
        _linkedCancellation.Dispose();
    }
}
