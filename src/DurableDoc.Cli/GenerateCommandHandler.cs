using DurableDoc.Analysis;
using DurableDoc.Configuration;
using DurableDoc.Dashboard;
using DurableDoc.Rendering.Mermaid;
using System.Text;

namespace DurableDoc.Cli;

public static class GenerateCommandHandler
{
    public static async Task<int> ExecuteAsync(
        string inputPath,
        string? outputDirectory,
        string? orchestratorName,
        string mode,
        string? format = null,
        string? configPath = null,
        bool strict = false,
        CliCommandContext? context = null,
        CancellationToken cancellationToken = default)
    {
        context ??= CliCommandContext.CreateDefault();

        try
        {
            var config = DurableDocConfigLoader.Load(configPath);
            var renderMode = ParseMode(mode);
            ParseFormat(format, config);
            var resolvedOutputDirectory = ResolveOutputDirectory(outputDirectory, config);
            var analyzer = new WorkflowAnalyzer();
            var analysis = await analyzer.AnalyzeWorkspaceAsync(inputPath, config, cancellationToken).ConfigureAwait(false);

            if (analysis.Diagrams.Count == 0)
            {
                context.Fail(BuildNoDiscoveryMessage(analysis, inputPath));
                return 1;
            }

            var selectedDiagrams = analysis.Diagrams
                .Where(diagram => string.IsNullOrWhiteSpace(orchestratorName) ||
                    string.Equals(diagram.OrchestratorName, orchestratorName, StringComparison.OrdinalIgnoreCase))
                .OrderBy(diagram => diagram.OrchestratorName, StringComparer.Ordinal)
                .ToArray();

            if (selectedDiagrams.Length == 0)
            {
                context.Fail(BuildFilterMismatchMessage(orchestratorName, analysis.Diagrams));
                return 1;
            }

            var diagnostics = CliDiagnostics.Evaluate(selectedDiagrams, config);
            foreach (var warning in diagnostics.Where(d => d.Severity == CliDiagnosticSeverity.Warning))
            {
                context.Warn(FormatDiagnostic(warning));
            }

            if (strict && diagnostics.Any(d => d.Severity == CliDiagnosticSeverity.Warning))
            {
                context.Fail("Generation completed with warnings and '--strict' was specified.");
                return 1;
            }

            var generatedAt = DateTimeOffset.UtcNow;
            var artifacts = selectedDiagrams.Select(diagram => new GeneratedDiagramArtifact
            {
                DiagramId = diagram.Id,
                OrchestratorName = diagram.OrchestratorName,
                Mode = renderMode.ToString().ToLowerInvariant(),
                GeneratedAt = generatedAt,
                Mermaid = MermaidRenderer.Render(diagram, renderMode),
                SourceFile = diagram.SourceFile,
                SourceProjectPath = diagram.SourceProjectPath,
                Warnings = diagram.Diagnostics
                    .Where(issue => issue.Severity == DurableDoc.Domain.WorkflowIssueSeverity.Warning)
                    .Select(issue => issue.Message)
                    .Distinct(StringComparer.Ordinal)
                    .ToArray(),
            });

            var result = DashboardGenerator.WriteArtifactsAndBuild(resolvedOutputDirectory, artifacts);

            context.Info($"Generated {result.DiagramCount} diagram(s) in {Path.GetFullPath(resolvedOutputDirectory)}.");
            context.Info($"Dashboard ready at {result.DashboardPath}");
            return 0;
        }
        catch (Exception ex)
        {
            context.Fail(ex.Message);
            return 1;
        }
    }

    private static string ParseFormat(string? format, DurableDocConfig config)
    {
        var effectiveFormat = string.IsNullOrWhiteSpace(format)
            ? config.Defaults?.Format
            : format;

        if (string.IsNullOrWhiteSpace(effectiveFormat))
        {
            return "mermaid";
        }

        if (!string.Equals(effectiveFormat, "mermaid", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException($"Unsupported output format '{effectiveFormat}'. MVP supports only 'mermaid'.", nameof(format));
        }

        return "mermaid";
    }

    private static string ResolveOutputDirectory(string? outputDirectory, DurableDocConfig config)
    {
        if (!string.IsNullOrWhiteSpace(outputDirectory))
        {
            return outputDirectory;
        }

        if (!string.IsNullOrWhiteSpace(config.Defaults?.Output))
        {
            return config.Defaults.Output;
        }

        return Path.Combine(Directory.GetCurrentDirectory(), "docs", "diagrams");
    }

    private static MermaidRenderMode ParseMode(string mode)
    {
        if (Enum.TryParse<MermaidRenderMode>(mode, ignoreCase: true, out var renderMode))
        {
            return renderMode;
        }

        throw new ArgumentException($"Unsupported diagram mode '{mode}'. Use 'developer' or 'business'.", nameof(mode));
    }

    private static string FormatDiagnostic(CliDiagnostic diagnostic)
    {
        return diagnostic.OrchestratorName is null
            ? diagnostic.Message
            : $"{diagnostic.OrchestratorName}: {diagnostic.Message}";
    }

    internal static string BuildNoDiscoveryMessage(WorkflowAnalysisResult analysis, string inputPath)
    {
        var builder = new StringBuilder();
        builder.Append($"No orchestrators were discovered in '{Path.GetFullPath(inputPath)}'.");

        if (analysis.InputKind == WorkflowInputKind.Solution && analysis.ScannedProjects.Count > 0)
        {
            builder.Append(" The solution was analyzed strictly by project membership.");
            builder.Append(" Scanned projects: ");
            builder.Append(string.Join(", ", analysis.ScannedProjects.Select(Path.GetFileNameWithoutExtension)));
            builder.Append(". If the target project is outside the solution, use its .csproj path or a source folder instead.");
        }

        return builder.ToString();
    }

    internal static string BuildFilterMismatchMessage(string? orchestratorName, IReadOnlyList<DurableDoc.Domain.WorkflowDiagram> diagrams)
    {
        var discovered = string.Join(", ", diagrams.Select(diagram => diagram.OrchestratorName).OrderBy(name => name, StringComparer.Ordinal));
        return $"No orchestrators matched filter '{orchestratorName}'. Discovered orchestrators: {discovered}.";
    }
}
