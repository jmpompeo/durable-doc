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
        string? mode,
        string? format = null,
        string? configPath = null,
        bool strict = false,
        CliCommandContext? context = null,
        bool openDashboard = false,
        Func<Uri, CancellationToken, Task>? browserLauncher = null,
        string audience = "developer",
        CancellationToken cancellationToken = default)
    {
        context ??= CliCommandContext.CreateDefault();

        try
        {
            if (!DashboardPreviewHost.ValidateInteractivePreview(context, openDashboard))
            {
                return 1;
            }

            var config = DurableDocConfigLoader.Load(configPath);
            var parsedAudience = ParseAudience(audience);
            var renderMode = ParseMode(mode, parsedAudience);
            ParseFormat(format, config);
            var resolvedOutputDirectory = ResolveOutputDirectory(outputDirectory, config, parsedAudience);
            var analyzer = new WorkflowAnalyzer();
            var analysis = await analyzer.AnalyzeWorkspaceAsync(inputPath, config, cancellationToken).ConfigureAwait(false);

            if (analysis.Diagrams.Count == 0)
            {
                context.Fail(BuildNoDiscoveryMessage(analysis, inputPath));
                return 1;
            }

            var selectedDiagrams = WorkflowSelection.FilterDiagrams(analysis.Diagrams, orchestratorName);

            if (selectedDiagrams.Length == 0)
            {
                context.Fail(WorkflowSelection.BuildFilterMismatchMessage(
                    orchestratorName,
                    analysis.Diagrams.Select(diagram => diagram.OrchestratorName)));
                return 1;
            }

            var diagnostics = CliDiagnostics.Evaluate(selectedDiagrams, config, parsedAudience);
            foreach (var warning in diagnostics.Where(d => d.Severity == CliDiagnosticSeverity.Warning))
            {
                context.Warn(FormatDiagnostic(warning));
            }

            if (strict && diagnostics.Any(d => d.Severity == CliDiagnosticSeverity.Warning))
            {
                context.Fail("Generation completed with warnings and '--strict' was specified.");
                return 1;
            }

            var artifacts = CreateArtifacts(selectedDiagrams, renderMode, config, parsedAudience);

            var result = DashboardGenerator.WriteArtifactsAndBuild(resolvedOutputDirectory, artifacts, parsedAudience);

            context.Info($"Generated {result.DiagramCount} diagram(s) in {Path.GetFullPath(resolvedOutputDirectory)}.");
            context.Info($"Dashboard ready at {result.DashboardPath}");

            if (openDashboard)
            {
                await DashboardPreviewHost.PreviewAsync(
                    resolvedOutputDirectory,
                    context,
                    WorkflowSelection.ResolvePreviewOrchestrator(
                        orchestratorName,
                        selectedDiagrams.Select(diagram => diagram.OrchestratorName)),
                    renderMode.ToString().ToLowerInvariant(),
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

    internal static IReadOnlyList<GeneratedDiagramArtifact> CreateArtifacts(
        IReadOnlyList<DurableDoc.Domain.WorkflowDiagram> diagrams,
        MermaidRenderMode renderMode,
        DurableDocConfig? config,
        DashboardAudience audience)
    {
        var generatedAt = DateTimeOffset.UtcNow;
        return diagrams.Select(diagram =>
        {
            var renderedDiagram = MermaidRenderer.Prepare(diagram, renderMode).ToDeterministic();
            var metadata = ResolveMetadata(config, diagram.OrchestratorName);
            return new GeneratedDiagramArtifact
            {
                DiagramId = renderedDiagram.Id,
                OrchestratorName = renderedDiagram.OrchestratorName,
                Mode = renderMode.ToString().ToLowerInvariant(),
                Audience = FormatAudience(audience),
                GeneratedAt = generatedAt,
                Mermaid = MermaidRenderer.Render(renderedDiagram),
                SourceFile = renderedDiagram.SourceFile,
                SourceProjectPath = renderedDiagram.SourceProjectPath,
                BusinessName = metadata?.BusinessName,
                Capability = metadata?.Capability,
                Summary = metadata?.Summary,
                AudienceNotes = metadata?.AudienceNotes,
                Outcomes = (metadata?.Outcomes ?? []).Where(value => !string.IsNullOrWhiteSpace(value)).ToArray(),
                OrchestratorNotes = metadata?.Notes,
                Warnings = renderedDiagram.Diagnostics
                    .Where(issue => issue.Severity == DurableDoc.Domain.WorkflowIssueSeverity.Warning)
                    .Select(issue => issue.Message)
                    .Distinct(StringComparer.Ordinal)
                    .ToArray(),
                Nodes = renderedDiagram.Nodes,
                Edges = renderedDiagram.Edges,
                Diagnostics = renderedDiagram.Diagnostics,
            };
        }).ToArray();
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

    internal static string ResolveOutputDirectory(string? outputDirectory, DurableDocConfig config, DashboardAudience audience)
    {
        if (!string.IsNullOrWhiteSpace(outputDirectory))
        {
            return outputDirectory;
        }

        if (audience == DashboardAudience.Stakeholder)
        {
            return Path.Combine(Directory.GetCurrentDirectory(), "docs", "stakeholder");
        }

        if (!string.IsNullOrWhiteSpace(config.Defaults?.Output))
        {
            return config.Defaults.Output;
        }

        return Path.Combine(Directory.GetCurrentDirectory(), "docs", "diagrams");
    }

    internal static MermaidRenderMode ParseMode(string? mode, DashboardAudience audience)
    {
        var effectiveMode = string.IsNullOrWhiteSpace(mode)
            ? audience == DashboardAudience.Stakeholder ? "business" : "developer"
            : mode;

        if (Enum.TryParse<MermaidRenderMode>(effectiveMode, ignoreCase: true, out var renderMode))
        {
            return renderMode;
        }

        throw new ArgumentException($"Unsupported diagram mode '{effectiveMode}'. Use 'developer' or 'business'.", nameof(mode));
    }

    internal static DashboardAudience ParseAudience(string? audience)
    {
        var effectiveAudience = string.IsNullOrWhiteSpace(audience) ? "developer" : audience;
        if (Enum.TryParse<DashboardAudience>(effectiveAudience, ignoreCase: true, out var parsedAudience))
        {
            return parsedAudience;
        }

        throw new ArgumentException($"Unsupported audience '{effectiveAudience}'. Use 'developer' or 'stakeholder'.", nameof(audience));
    }

    internal static string FormatAudience(DashboardAudience audience)
    {
        return audience.ToString().ToLowerInvariant();
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

    private static OrchestratorMetadata? ResolveMetadata(DurableDocConfig? config, string orchestratorName)
    {
        return config?.BusinessView?.Orchestrators?
            .FirstOrDefault(entry => string.Equals(entry.Name, orchestratorName, StringComparison.OrdinalIgnoreCase));
    }

}
