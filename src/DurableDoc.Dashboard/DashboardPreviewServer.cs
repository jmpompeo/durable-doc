using System.Net;
using System.Net.Sockets;
using System.Text;

namespace DurableDoc.Dashboard;

public sealed class DashboardPreviewSession : IAsyncDisposable
{
    private readonly TcpListener _listener;
    private readonly CancellationTokenSource _lifetimeCancellation;
    private readonly CancellationTokenRegistration _stopRegistration;
    private readonly Task _serverTask;
    private bool _disposed;

    internal DashboardPreviewSession(TcpListener listener, string rootDirectory, CancellationToken cancellationToken)
    {
        _listener = listener;
        RootDirectory = rootDirectory;
        DashboardUri = new Uri($"http://127.0.0.1:{((IPEndPoint)listener.LocalEndpoint).Port}/", UriKind.Absolute);
        _lifetimeCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _stopRegistration = _lifetimeCancellation.Token.Register(static state => ((TcpListener)state!).Stop(), _listener);
        _serverTask = Task.Run(() => RunAsync(_lifetimeCancellation.Token));
    }

    public Uri DashboardUri { get; }

    public string RootDirectory { get; }

    public Task WaitForShutdownAsync()
    {
        return _serverTask;
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _lifetimeCancellation.Cancel();
        _listener.Stop();

        try
        {
            await _serverTask.ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (_lifetimeCancellation.IsCancellationRequested)
        {
        }
        finally
        {
            _stopRegistration.Dispose();
            _lifetimeCancellation.Dispose();
        }
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            TcpClient client;
            try
            {
                client = await _listener.AcceptTcpClientAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (ObjectDisposedException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (SocketException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            _ = Task.Run(() => HandleClientAsync(client, cancellationToken), CancellationToken.None);
        }
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken cancellationToken)
    {
        using (client)
        await using (var stream = client.GetStream())
        using (var reader = new StreamReader(stream, Encoding.ASCII, detectEncodingFromByteOrderMarks: false, bufferSize: 1024, leaveOpen: true))
        {
            string? requestLine;

            try
            {
                requestLine = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(requestLine))
            {
                return;
            }

            string? headerLine;
            do
            {
                headerLine = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
            }
            while (!string.IsNullOrEmpty(headerLine));

            var requestParts = requestLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (requestParts.Length < 2)
            {
                await WriteTextResponseAsync(stream, 400, "Bad Request", "Malformed HTTP request.", includeBody: true, cancellationToken).ConfigureAwait(false);
                return;
            }

            var method = requestParts[0];
            var includeBody = !string.Equals(method, "HEAD", StringComparison.OrdinalIgnoreCase);
            if (!string.Equals(method, "GET", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(method, "HEAD", StringComparison.OrdinalIgnoreCase))
            {
                await WriteTextResponseAsync(
                    stream,
                    405,
                    "Method Not Allowed",
                    "Only GET and HEAD are supported.",
                    includeBody,
                    cancellationToken,
                    ("Allow", "GET, HEAD")).ConfigureAwait(false);
                return;
            }

            if (!TryResolvePath(requestParts[1], out var filePath))
            {
                await WriteTextResponseAsync(
                    stream,
                    403,
                    "Forbidden",
                    "The requested path is outside the dashboard root.",
                    includeBody,
                    cancellationToken).ConfigureAwait(false);
                return;
            }

            if (!File.Exists(filePath))
            {
                await WriteTextResponseAsync(stream, 404, "Not Found", "The requested file was not found.", includeBody, cancellationToken).ConfigureAwait(false);
                return;
            }

            var payload = await File.ReadAllBytesAsync(filePath, cancellationToken).ConfigureAwait(false);
            await WriteResponseAsync(
                stream,
                200,
                "OK",
                GetContentType(filePath),
                payload,
                includeBody,
                cancellationToken).ConfigureAwait(false);
        }
    }

    private bool TryResolvePath(string requestTarget, out string filePath)
    {
        var relativePath = "/";
        if (Uri.TryCreate(new Uri("http://127.0.0.1", UriKind.Absolute), requestTarget, out var requestUri))
        {
            relativePath = Uri.UnescapeDataString(requestUri.AbsolutePath);
        }

        if (string.IsNullOrWhiteSpace(relativePath) || relativePath == "/")
        {
            relativePath = "/index.html";
        }

        var trimmedPath = relativePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
        var candidatePath = Path.GetFullPath(Path.Combine(RootDirectory, trimmedPath));

        if (Directory.Exists(candidatePath))
        {
            candidatePath = Path.Combine(candidatePath, "index.html");
        }

        var rootWithSeparator = RootDirectory.EndsWith(Path.DirectorySeparatorChar)
            ? RootDirectory
            : RootDirectory + Path.DirectorySeparatorChar;

        if (!candidatePath.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase))
        {
            filePath = string.Empty;
            return false;
        }

        filePath = candidatePath;
        return true;
    }

    private static async Task WriteTextResponseAsync(
        Stream stream,
        int statusCode,
        string reasonPhrase,
        string message,
        bool includeBody,
        CancellationToken cancellationToken,
        params (string Name, string Value)[] headers)
    {
        await WriteResponseAsync(
            stream,
            statusCode,
            reasonPhrase,
            "text/plain; charset=utf-8",
            Encoding.UTF8.GetBytes(message),
            includeBody,
            cancellationToken,
            headers).ConfigureAwait(false);
    }

    private static async Task WriteResponseAsync(
        Stream stream,
        int statusCode,
        string reasonPhrase,
        string contentType,
        byte[] payload,
        bool includeBody,
        CancellationToken cancellationToken,
        params (string Name, string Value)[] headers)
    {
        var headerBuilder = new StringBuilder();
        headerBuilder.Append("HTTP/1.1 ").Append(statusCode).Append(' ').Append(reasonPhrase).Append("\r\n");
        headerBuilder.Append("Content-Type: ").Append(contentType).Append("\r\n");
        headerBuilder.Append("Content-Length: ").Append(payload.Length).Append("\r\n");
        headerBuilder.Append("Connection: close\r\n");
        headerBuilder.Append("Cache-Control: no-store\r\n");

        foreach (var (name, value) in headers)
        {
            headerBuilder.Append(name).Append(": ").Append(value).Append("\r\n");
        }

        headerBuilder.Append("\r\n");

        var headerBytes = Encoding.ASCII.GetBytes(headerBuilder.ToString());
        await stream.WriteAsync(headerBytes, cancellationToken).ConfigureAwait(false);

        if (includeBody)
        {
            await stream.WriteAsync(payload, cancellationToken).ConfigureAwait(false);
        }
    }

    private static string GetContentType(string filePath)
    {
        return Path.GetExtension(filePath).ToLowerInvariant() switch
        {
            ".html" => "text/html; charset=utf-8",
            ".js" => "application/javascript; charset=utf-8",
            ".json" => "application/json; charset=utf-8",
            ".mmd" => "text/plain; charset=utf-8",
            ".css" => "text/css; charset=utf-8",
            ".svg" => "image/svg+xml",
            _ => "application/octet-stream",
        };
    }
}

public static class DashboardPreviewServer
{
    public static Task<DashboardPreviewSession> StartAsync(string rootDirectory, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);

        var rootPath = Path.GetFullPath(rootDirectory);
        if (!Directory.Exists(rootPath))
        {
            throw new DirectoryNotFoundException($"Dashboard preview directory was not found: {rootPath}");
        }

        var listener = new TcpListener(IPAddress.Loopback, port: 0);
        listener.Start();
        return Task.FromResult(new DashboardPreviewSession(listener, rootPath, cancellationToken));
    }
}
