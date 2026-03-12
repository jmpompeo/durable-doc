using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace DurableDoc.Dashboard;

public sealed record DashboardServerSession(string Url, int Port, bool ReusedExistingServer);

public static class DashboardServerLauncher
{
    internal const string StateFileName = ".durable-doc-dashboard.server.json";

    public static async Task<DashboardServerSession> EnsureServerAsync(
        string inputDirectory,
        int? preferredPort,
        bool openBrowser,
        CancellationToken cancellationToken = default)
    {
        var inputPath = Path.GetFullPath(inputDirectory);
        var statePath = GetStateFilePath(inputPath);

        if (TryReadState(statePath, out var existingState) &&
            IsProcessAlive(existingState.ProcessId) &&
            await IsHealthyAsync(existingState.Url, cancellationToken).ConfigureAwait(false))
        {
            if (openBrowser)
            {
                TryOpenBrowser(existingState.Url);
            }

            return new DashboardServerSession(existingState.Url, existingState.Port, true);
        }

        var port = FindAvailablePort(preferredPort);
        StartBackgroundServer(inputPath, port);

        var url = $"http://127.0.0.1:{port}/";
        await WaitForServerAsync(url, cancellationToken).ConfigureAwait(false);

        if (openBrowser)
        {
            TryOpenBrowser(url);
        }

        return new DashboardServerSession(url, port, false);
    }

    private static void StartBackgroundServer(string inputDirectory, int port)
    {
        var cliAssemblyPath = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(assembly => string.Equals(assembly.GetName().Name, "DurableDoc.Cli", StringComparison.Ordinal))
            ?.Location;
        if (string.IsNullOrWhiteSpace(cliAssemblyPath))
        {
            throw new InvalidOperationException("Unable to locate the durable-doc CLI assembly for background dashboard hosting.");
        }

        var hostPath = "dotnet";
        var startInfo = CreateStartInfo(hostPath, cliAssemblyPath, inputDirectory, port);
        Process.Start(startInfo);
    }

    private static ProcessStartInfo CreateStartInfo(string hostPath, string entryAssemblyPath, string inputDirectory, int port)
    {
        var escapedInput = Quote(inputDirectory);
        var arguments = $"__dashboard-serve --input {escapedInput} --port {port}";

        if (Path.GetFileNameWithoutExtension(hostPath).Equals("dotnet", StringComparison.OrdinalIgnoreCase))
        {
            return new ProcessStartInfo
            {
                FileName = hostPath,
                Arguments = $"{Quote(entryAssemblyPath)} {arguments}",
                CreateNoWindow = true,
                UseShellExecute = false,
            };
        }

        return new ProcessStartInfo
        {
            FileName = hostPath,
            Arguments = arguments,
            CreateNoWindow = true,
            UseShellExecute = false,
        };
    }

    private static string Quote(string value)
    {
        return "\"" + value.Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";
    }

    private static string GetStateFilePath(string inputDirectory)
    {
        return Path.Combine(inputDirectory, StateFileName);
    }

    private static bool TryReadState(string path, out DashboardServerState state)
    {
        state = default!;

        if (!File.Exists(path))
        {
            return false;
        }

        try
        {
            var json = File.ReadAllText(path);
            var parsed = JsonSerializer.Deserialize<DashboardServerState>(json);
            if (parsed is null)
            {
                return false;
            }

            state = parsed;
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsProcessAlive(int processId)
    {
        try
        {
            var process = Process.GetProcessById(processId);
            return !process.HasExited;
        }
        catch
        {
            return false;
        }
    }

    private static async Task<bool> IsHealthyAsync(string baseUrl, CancellationToken cancellationToken)
    {
        try
        {
            using var client = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(1),
            };

            using var response = await client.GetAsync(new Uri(new Uri(baseUrl), "health"), cancellationToken).ConfigureAwait(false);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    private static int FindAvailablePort(int? preferredPort)
    {
        var startingPort = preferredPort ?? 57341;
        for (var port = startingPort; port < startingPort + 50; port++)
        {
            if (IsPortAvailable(port))
            {
                return port;
            }
        }

        throw new InvalidOperationException("Unable to find an available localhost port for the dashboard server.");
    }

    private static bool IsPortAvailable(int port)
    {
        try
        {
            using var listener = new TcpListener(IPAddress.Loopback, port);
            listener.Start();
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static async Task WaitForServerAsync(string baseUrl, CancellationToken cancellationToken)
    {
        using var client = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(1),
        };

        var deadline = DateTimeOffset.UtcNow.AddSeconds(8);
        while (DateTimeOffset.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                using var response = await client.GetAsync(new Uri(new Uri(baseUrl), "health"), cancellationToken).ConfigureAwait(false);
                if (response.IsSuccessStatusCode)
                {
                    return;
                }
            }
            catch
            {
            }

            await Task.Delay(200, cancellationToken).ConfigureAwait(false);
        }

        throw new TimeoutException("Dashboard server did not start in time.");
    }

    private static void TryOpenBrowser(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true,
            });
        }
        catch
        {
        }
    }
}

public static class DashboardServerHost
{
    public static async Task RunAsync(string inputDirectory, int port, CancellationToken cancellationToken = default)
    {
        var inputPath = Path.GetFullPath(inputDirectory);
        if (!Directory.Exists(inputPath))
        {
            throw new DirectoryNotFoundException($"Input directory was not found: {inputPath}");
        }

        var prefix = $"http://127.0.0.1:{port}/";
        using var listener = new HttpListener();
        listener.Prefixes.Add(prefix);
        listener.Start();

        var statePath = Path.Combine(inputPath, DashboardServerLauncher.StateFileName);
        WriteState(statePath, new DashboardServerState(Process.GetCurrentProcess().Id, port, prefix));

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var context = await listener.GetContextAsync().ConfigureAwait(false);
                _ = Task.Run(() => HandleRequestAsync(context, inputPath), CancellationToken.None);
            }
        }
        finally
        {
            listener.Stop();
            DeleteStateFile(statePath);
        }
    }

    private static async Task HandleRequestAsync(HttpListenerContext context, string inputDirectory)
    {
        try
        {
            var path = context.Request.Url?.AbsolutePath ?? "/";
            if (string.Equals(path, "/health", StringComparison.Ordinal))
            {
                await WriteResponseAsync(context.Response, "ok", "text/plain; charset=utf-8").ConfigureAwait(false);
                return;
            }

            var relativePath = path == "/" ? "index.html" : Uri.UnescapeDataString(path.TrimStart('/'));
            var candidatePath = Path.GetFullPath(Path.Combine(inputDirectory, relativePath));
            if (!candidatePath.StartsWith(inputDirectory, StringComparison.Ordinal))
            {
                context.Response.StatusCode = 404;
                context.Response.Close();
                return;
            }

            if (!File.Exists(candidatePath))
            {
                context.Response.StatusCode = 404;
                context.Response.Close();
                return;
            }

            var bytes = await File.ReadAllBytesAsync(candidatePath).ConfigureAwait(false);
            context.Response.ContentType = GetContentType(candidatePath);
            context.Response.ContentLength64 = bytes.Length;
            context.Response.AddHeader("Cache-Control", "no-store");
            await context.Response.OutputStream.WriteAsync(bytes).ConfigureAwait(false);
            context.Response.Close();
        }
        catch
        {
            if (context.Response.OutputStream.CanWrite)
            {
                context.Response.StatusCode = 500;
                context.Response.Close();
            }
        }
    }

    private static Task WriteResponseAsync(HttpListenerResponse response, string content, string contentType)
    {
        var bytes = Encoding.UTF8.GetBytes(content);
        response.ContentType = contentType;
        response.ContentLength64 = bytes.Length;
        response.AddHeader("Cache-Control", "no-store");
        return response.OutputStream.WriteAsync(bytes).AsTask().ContinueWith(_ => response.Close(), TaskScheduler.Default);
    }

    private static string GetContentType(string path)
    {
        return Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".css" => "text/css; charset=utf-8",
            ".html" => "text/html; charset=utf-8",
            ".js" => "application/javascript; charset=utf-8",
            ".json" => "application/json; charset=utf-8",
            ".mmd" => "text/plain; charset=utf-8",
            _ => "application/octet-stream",
        };
    }

    private static void WriteState(string statePath, DashboardServerState state)
    {
        File.WriteAllText(statePath, JsonSerializer.Serialize(state, new JsonSerializerOptions
        {
            WriteIndented = true,
        }));
    }

    private static void DeleteStateFile(string statePath)
    {
        if (File.Exists(statePath))
        {
            File.Delete(statePath);
        }
    }
}

internal sealed record DashboardServerState(int ProcessId, int Port, string Url);
