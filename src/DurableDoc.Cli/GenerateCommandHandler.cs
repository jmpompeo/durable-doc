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
        CancellationToken cancellationToken = default)
    {
        try
        {
            var renderMode = ParseMode(mode);
            var config = DurableDocConfigLoader.Load(configPath);
            var analyzer = new WorkflowAnalyzer();
            var diagrams = await analyzer.AnalyzeAsync(inputPath, config, cancellationToken).ConfigureAwait(false);

            var selectedDiagrams = diagrams
                .Where(diagram => string.IsNullOrWhiteSpace(orchestratorName) ||
                    string.Equals(diagram.OrchestratorName, orchestratorName, StringComparison.OrdinalIgnoreCase))
                .ToArray();

            if (selectedDiagrams.Length == 0)
            {
                Console.Error.WriteLine("No orchestrators matched the requested input.");
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
            });

            var result = DashboardGenerator.WriteArtifactsAndBuild(outputDirectory, artifacts);

            Console.WriteLine($"Generated {result.DiagramCount} diagram(s) in {Path.GetFullPath(outputDirectory)}.");
            Console.WriteLine($"Dashboard ready at {result.DashboardPath}");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
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
}
