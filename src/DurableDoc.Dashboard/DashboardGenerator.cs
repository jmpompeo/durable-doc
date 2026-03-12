using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using DurableDoc.Domain;

namespace DurableDoc.Dashboard;

public sealed class GeneratedDiagramArtifact
{
    public string DiagramId { get; init; } = string.Empty;
    public string OrchestratorName { get; init; } = string.Empty;
    public string Mode { get; init; } = string.Empty;
    public DateTimeOffset GeneratedAt { get; init; }
    public string Mermaid { get; init; } = string.Empty;
    public string MermaidFileName { get; init; } = string.Empty;
    public string? SourceFile { get; init; }
    public string? SourceProjectPath { get; init; }
    public IReadOnlyList<string> Warnings { get; init; } = [];
    public IReadOnlyList<WorkflowNode> Nodes { get; init; } = [];
    public IReadOnlyList<WorkflowEdge> Edges { get; init; } = [];
}

public sealed record DashboardBuildResult(string DashboardPath, string DashboardDataPath, int DiagramCount);

public static class DashboardGenerator
{
    public static int WriteArtifacts(string outputDirectory, IEnumerable<GeneratedDiagramArtifact> diagrams)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);

        var outputPath = Path.GetFullPath(outputDirectory);
        Directory.CreateDirectory(outputPath);

        var materialized = diagrams
            .OrderBy(diagram => diagram.OrchestratorName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(diagram => diagram.Mode, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (materialized.Length == 0)
        {
            throw new InvalidOperationException("No diagrams were provided for artifact generation.");
        }

        foreach (var diagram in materialized)
        {
            DiagramArtifactStore.Write(outputPath, diagram);
        }

        return materialized.Length;
    }

    public static DashboardBuildResult BuildFromArtifacts(string inputDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(inputDirectory);

        var inputPath = Path.GetFullPath(inputDirectory);
        if (!Directory.Exists(inputPath))
        {
            throw new DirectoryNotFoundException($"Input directory was not found: {inputPath}");
        }

        var diagrams = DiagramArtifactStore.Read(inputPath);
        if (diagrams.Count == 0)
        {
            throw new InvalidOperationException($"No generated diagram artifacts were found in {inputPath}");
        }

        DashboardAssetWriter.Write(inputPath, diagrams);
        return new DashboardBuildResult(
            Path.Combine(inputPath, "index.html"),
            Path.Combine(inputPath, "dashboard-data.json"),
            diagrams.Count);
    }
}

internal static class DiagramArtifactStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Converters =
        {
            new JsonStringEnumConverter(),
        },
    };

    public static void Write(string outputDirectory, GeneratedDiagramArtifact artifact)
    {
        var fileBaseName = CreateFileBaseName(artifact);
        var mermaidFileName = $"{fileBaseName}.mmd";
        var artifactFileName = $"{fileBaseName}.diagram.json";
        var persistedArtifact = new GeneratedDiagramArtifact
        {
            DiagramId = artifact.DiagramId,
            OrchestratorName = artifact.OrchestratorName,
            Mode = artifact.Mode,
            GeneratedAt = artifact.GeneratedAt,
            Mermaid = artifact.Mermaid,
            MermaidFileName = mermaidFileName,
            SourceFile = artifact.SourceFile,
            SourceProjectPath = artifact.SourceProjectPath,
            Warnings = artifact.Warnings,
            Nodes = artifact.Nodes,
            Edges = artifact.Edges,
        };

        File.WriteAllText(Path.Combine(outputDirectory, mermaidFileName), artifact.Mermaid);
        File.WriteAllText(
            Path.Combine(outputDirectory, artifactFileName),
            JsonSerializer.Serialize(persistedArtifact, SerializerOptions));
    }

    public static IReadOnlyList<GeneratedDiagramArtifact> Read(string inputDirectory)
    {
        return Directory.EnumerateFiles(inputDirectory, "*.diagram.json", SearchOption.TopDirectoryOnly)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .Select(ReadArtifact)
            .ToArray();
    }

    private static GeneratedDiagramArtifact ReadArtifact(string path)
    {
        var json = File.ReadAllText(path);
        var artifact = JsonSerializer.Deserialize<GeneratedDiagramArtifact>(json, SerializerOptions);
        if (artifact is null)
        {
            throw new InvalidOperationException($"Generated diagram artifact could not be read: {path}");
        }

        return artifact;
    }

    private static string CreateFileBaseName(GeneratedDiagramArtifact artifact)
    {
        var rawValue = $"{artifact.OrchestratorName}-{artifact.Mode}-{artifact.DiagramId}";
        var builder = new System.Text.StringBuilder(rawValue.Length);

        foreach (var character in rawValue)
        {
            builder.Append(char.IsLetterOrDigit(character) ? char.ToLowerInvariant(character) : '-');
        }

        var normalized = builder.ToString().Trim('-');
        return string.IsNullOrWhiteSpace(normalized) ? "diagram" : normalized;
    }
}

internal static class DashboardAssetWriter
{
    private static readonly JsonSerializerOptions PayloadSerializerOptions = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters =
        {
            new JsonStringEnumConverter(),
        },
    };

    public static void Write(string outputDirectory, IReadOnlyList<GeneratedDiagramArtifact> diagrams)
    {
        var payload = JsonSerializer.Serialize(diagrams, PayloadSerializerOptions)
            .Replace("</", "<\\/", StringComparison.Ordinal);

        File.WriteAllText(Path.Combine(outputDirectory, "dashboard-data.json"), payload);
        File.WriteAllText(
            Path.Combine(outputDirectory, "index.html"),
            DashboardAssetLoader.ReadText("index.html").Replace("__BOOTSTRAP_DATA__", payload, StringComparison.Ordinal));
        File.WriteAllText(Path.Combine(outputDirectory, "dashboard.css"), DashboardAssetLoader.ReadText("dashboard.css"));
        File.WriteAllText(Path.Combine(outputDirectory, "dashboard.js"), DashboardAssetLoader.ReadText("dashboard.js"));
        File.WriteAllText(Path.Combine(outputDirectory, "mermaid.min.js"), DashboardAssetLoader.ReadText("mermaid.min.js"));
    }
}

internal static class DashboardAssetLoader
{
    private static readonly Assembly Assembly = typeof(DashboardAssetLoader).Assembly;

    public static string ReadText(string fileName)
    {
        var resourceName = Assembly.GetManifestResourceNames()
            .Single(name => name.EndsWith($".Assets.{fileName}", StringComparison.OrdinalIgnoreCase));

        using var stream = Assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded dashboard asset not found: {fileName}");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
