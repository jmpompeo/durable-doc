using DurableDoc.Analysis;
using DurableDoc.Configuration;
using DurableDoc.Dashboard;
using DurableDoc.Rendering.Mermaid;

namespace DurableDoc.Cli;

public static class GenerateCommandHandler
{
    public static async Task<int> ExecuteAsync(
        string inputPath,
        string outputDirectory,
        string? orchestratorName,
        string mode,
        string? configPath,
        bool strict = false,
        bool noDashboard = false,
        bool noOpen = false,
        int? port = null,
        CliCommandContext? context = null,
        CancellationToken cancellationToken = default)
    {
        context ??= CliCommandContext.CreateDefault();

        try
        {
            var renderMode = ParseMode(mode);
            var config = DurableDocConfigLoader.Load(configPath);
            var analyzer = new WorkflowAnalyzer();
            var diagrams = await analyzer.AnalyzeAsync(inputPath, config, cancellationToken).ConfigureAwait(false);
            var selectedDiagrams = diagrams
                .Where(diagram => string.IsNullOrWhiteSpace(orchestratorName) ||
                    string.Equals(diagram.OrchestratorName, orchestratorName, StringComparison.OrdinalIgnoreCase))
                .OrderBy(diagram => diagram.OrchestratorName, StringComparer.Ordinal)
                .ToArray();

            if (selectedDiagrams.Length == 0)
            {
                context.Fail("No orchestrators matched the requested input.");
                return 1;
            }

            var diagnostics = CliDiagnostics.Evaluate(selectedDiagrams);
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
            var warningsByOrchestrator = diagnostics
                .Where(diagnostic => diagnostic.Severity == CliDiagnosticSeverity.Warning && diagnostic.OrchestratorName is not null)
                .GroupBy(diagnostic => diagnostic.OrchestratorName!, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    group => group.Key,
                    group => (IReadOnlyList<string>)group.Select(FormatDiagnostic).ToArray(),
                    StringComparer.OrdinalIgnoreCase);
            var artifacts = selectedDiagrams.Select(diagram => new GeneratedDiagramArtifact
            {
                DiagramId = diagram.Id,
                OrchestratorName = diagram.OrchestratorName,
                Mode = renderMode.ToString().ToLowerInvariant(),
                GeneratedAt = generatedAt,
                Mermaid = MermaidRenderer.Render(diagram, renderMode),
                SourceFile = diagram.SourceFile,
                SourceProjectPath = diagram.SourceProjectPath,
                Warnings = warningsByOrchestrator.TryGetValue(diagram.OrchestratorName, out var warnings) ? warnings : [],
                Nodes = diagram.Nodes,
                Edges = diagram.Edges,
            });

            var generatedCount = DashboardGenerator.WriteArtifacts(outputDirectory, artifacts);
            DashboardBuildResult? result = null;
            if (!noDashboard)
            {
                result = DashboardGenerator.BuildFromArtifacts(outputDirectory);
            }

            context.Info($"Generated {generatedCount} diagram(s) in {Path.GetFullPath(outputDirectory)}.");

            if (result is not null)
            {
                context.Info($"Dashboard ready at {result.DashboardPath}");

                if (!context.Ci)
                {
                    var session = await DashboardServerLauncher.EnsureServerAsync(
                        outputDirectory,
                        port,
                        openBrowser: !noOpen,
                        cancellationToken).ConfigureAwait(false);
                    context.Info($"Dashboard available at {session.Url}");
                }
            }

            return 0;
        }
        catch (Exception ex)
        {
            context.Fail(ex.Message);
            return 1;
        }
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
}
