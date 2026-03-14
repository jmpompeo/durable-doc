using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using DurableDoc.Domain;

namespace DurableDoc.Dashboard;

public sealed class GeneratedDiagramArtifact
{
    public string DiagramId { get; init; } = string.Empty;
    public string OrchestratorName { get; init; } = string.Empty;
    public string Mode { get; init; } = string.Empty;
    public string Audience { get; init; } = "developer";
    public string? BusinessName { get; init; }
    public string? Capability { get; init; }
    public string? Summary { get; init; }
    public string? AudienceNotes { get; init; }
    public IReadOnlyList<string> Outcomes { get; init; } = [];
    public string? OrchestratorNotes { get; init; }
    public DateTimeOffset GeneratedAt { get; init; }
    public string Mermaid { get; init; } = string.Empty;
    public string MermaidFileName { get; init; } = string.Empty;
    public string? SourceFile { get; init; }
    public string? SourceProjectPath { get; init; }
    public IReadOnlyList<string> Warnings { get; init; } = [];
    public IReadOnlyList<WorkflowNode> Nodes { get; init; } = [];
    public IReadOnlyList<WorkflowEdge> Edges { get; init; } = [];
    public IReadOnlyList<WorkflowIssue> Diagnostics { get; init; } = [];
}

public sealed record DashboardBuildResult(string DashboardPath, int DiagramCount);

public static class DashboardGenerator
{
    internal const string MermaidBundleFileName = "mermaid.min.js";
    internal const string DashboardHtmlFileName = "index.html";
    internal const string DashboardCssFileName = "dashboard.css";
    internal const string DashboardScriptFileName = "dashboard.js";
    internal const string DashboardDataFileName = "dashboard-data.json";

    public static DashboardBuildResult WriteArtifactsAndBuild(
        string outputDirectory,
        IEnumerable<GeneratedDiagramArtifact> diagrams,
        DashboardAudience? audience = null)
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
            throw new InvalidOperationException("No diagrams were provided for dashboard generation.");
        }

        DiagramArtifactStore.RemoveStaleArtifacts(outputPath, materialized);

        foreach (var diagram in materialized)
        {
            DiagramArtifactStore.Write(outputPath, diagram);
        }

        return WriteDashboard(outputPath, materialized, audience);
    }

    public static IReadOnlyList<GeneratedDiagramArtifact> ReadArtifacts(string inputDirectory)
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

        return diagrams;
    }

    public static DashboardBuildResult BuildFromArtifacts(string inputDirectory, DashboardAudience? audience = null)
    {
        var inputPath = Path.GetFullPath(inputDirectory);
        var diagrams = ReadArtifacts(inputPath);
        return WriteDashboard(inputPath, diagrams, audience);
    }

    public static DashboardBuildResult BuildDashboard(
        string outputDirectory,
        IEnumerable<GeneratedDiagramArtifact> diagrams,
        DashboardAudience? audience = null)
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
            throw new InvalidOperationException("No diagrams were provided for dashboard generation.");
        }

        return WriteDashboard(outputPath, materialized, audience);
    }

    private static DashboardBuildResult WriteDashboard(
        string outputDirectory,
        IReadOnlyList<GeneratedDiagramArtifact> diagrams,
        DashboardAudience? audience)
    {
        var payload = SerializeDashboardData(diagrams);
        var resolvedAudience = ResolveAudience(diagrams, audience);

        File.WriteAllText(Path.Combine(outputDirectory, MermaidBundleFileName), MermaidCompatibilityBundle.Render());
        File.WriteAllText(Path.Combine(outputDirectory, DashboardCssFileName), DashboardCssTemplate.Render());
        File.WriteAllText(Path.Combine(outputDirectory, DashboardScriptFileName), DashboardScriptTemplate.Render());
        File.WriteAllText(Path.Combine(outputDirectory, DashboardDataFileName), payload);

        var dashboardPath = Path.Combine(outputDirectory, DashboardHtmlFileName);
        File.WriteAllText(
            dashboardPath,
            DashboardHtmlTemplate.Render(
                payload,
                MermaidBundleFileName,
                DashboardCssFileName,
                DashboardScriptFileName,
                resolvedAudience.ToString().ToLowerInvariant()));

        return new DashboardBuildResult(dashboardPath, diagrams.Count);
    }

    internal static string SerializeDashboardData(IReadOnlyList<GeneratedDiagramArtifact> diagrams)
    {
        return JsonSerializer.Serialize(diagrams, DashboardJson.SerializerOptions)
            .Replace("</", "<\\/", StringComparison.Ordinal);
    }

    private static DashboardAudience ResolveAudience(
        IReadOnlyList<GeneratedDiagramArtifact> diagrams,
        DashboardAudience? audienceOverride)
    {
        if (audienceOverride is not null)
        {
            return audienceOverride.Value;
        }

        var audiences = diagrams
            .Select(diagram => diagram.Audience)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (audiences.Length == 1 &&
            Enum.TryParse<DashboardAudience>(audiences[0], ignoreCase: true, out var parsedAudience))
        {
            return parsedAudience;
        }

        return DashboardAudience.Developer;
    }
}

internal static class DiagramArtifactStore
{
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
            Audience = artifact.Audience,
            BusinessName = artifact.BusinessName,
            Capability = artifact.Capability,
            Summary = artifact.Summary,
            AudienceNotes = artifact.AudienceNotes,
            Outcomes = artifact.Outcomes,
            OrchestratorNotes = artifact.OrchestratorNotes,
            GeneratedAt = artifact.GeneratedAt,
            Mermaid = artifact.Mermaid,
            MermaidFileName = mermaidFileName,
            SourceFile = artifact.SourceFile,
            SourceProjectPath = artifact.SourceProjectPath,
            Warnings = artifact.Warnings,
            Nodes = artifact.Nodes,
            Edges = artifact.Edges,
            Diagnostics = artifact.Diagnostics,
        };

        File.WriteAllText(Path.Combine(outputDirectory, mermaidFileName), artifact.Mermaid);
        File.WriteAllText(
            Path.Combine(outputDirectory, artifactFileName),
            JsonSerializer.Serialize(persistedArtifact, DashboardJson.SerializerOptions));
    }

    public static IReadOnlyList<GeneratedDiagramArtifact> Read(string inputDirectory)
    {
        return Directory.EnumerateFiles(inputDirectory, "*.diagram.json", SearchOption.TopDirectoryOnly)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .Select(ReadArtifact)
            .ToArray();
    }

    public static void RemoveStaleArtifacts(string outputDirectory, IReadOnlyList<GeneratedDiagramArtifact> desiredArtifacts)
    {
        var desiredDiagramIds = desiredArtifacts
            .Select(artifact => artifact.DiagramId)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var existingArtifactPath in Directory.EnumerateFiles(outputDirectory, "*.diagram.json", SearchOption.TopDirectoryOnly))
        {
            var existingArtifact = ReadArtifact(existingArtifactPath);
            if (desiredDiagramIds.Contains(existingArtifact.DiagramId))
            {
                continue;
            }

            File.Delete(existingArtifactPath);

            if (!string.IsNullOrWhiteSpace(existingArtifact.MermaidFileName))
            {
                var mermaidPath = Path.Combine(outputDirectory, existingArtifact.MermaidFileName);
                if (File.Exists(mermaidPath))
                {
                    File.Delete(mermaidPath);
                }
            }
        }
    }

    private static GeneratedDiagramArtifact ReadArtifact(string path)
    {
        var json = File.ReadAllText(path);
        var artifact = JsonSerializer.Deserialize<GeneratedDiagramArtifact>(json, DashboardJson.SerializerOptions);
        if (artifact is null)
        {
            throw new InvalidOperationException($"Generated diagram artifact could not be read: {path}");
        }

        return artifact;
    }

    private static string CreateFileBaseName(GeneratedDiagramArtifact artifact)
    {
        var rawValue = $"{artifact.OrchestratorName}-{artifact.Mode}-{artifact.DiagramId}";
        var builder = new StringBuilder(rawValue.Length);

        foreach (var character in rawValue)
        {
            builder.Append(char.IsLetterOrDigit(character) ? char.ToLowerInvariant(character) : '-');
        }

        var normalized = builder.ToString().Trim('-');
        return string.IsNullOrWhiteSpace(normalized) ? "diagram" : normalized;
    }

}

internal static class DashboardJson
{
    internal static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters =
        {
            new JsonStringEnumConverter(),
        },
    };
}
