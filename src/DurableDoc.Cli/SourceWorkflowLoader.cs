using DurableDoc.Analysis;
using DurableDoc.Configuration;
using DurableDoc.Domain;

namespace DurableDoc.Cli;

internal static class SourceWorkflowLoader
{
    public static async Task<SourceWorkflowSelection> LoadSelectedDiagramsAsync(
        string inputPath,
        string? outputDirectory,
        string? orchestratorName,
        string? configPath,
        CancellationToken cancellationToken = default)
    {
        var config = DurableDocConfigLoader.Load(configPath);
        var resolvedOutputDirectory = GenerateCommandHandler.ResolveOutputDirectory(outputDirectory, config);
        var analyzer = new WorkflowAnalyzer();
        var analysis = await analyzer.AnalyzeWorkspaceAsync(inputPath, config, cancellationToken).ConfigureAwait(false);

        if (analysis.Diagrams.Count == 0)
        {
            throw new InvalidOperationException(GenerateCommandHandler.BuildNoDiscoveryMessage(analysis, inputPath));
        }

        var selectedDiagrams = WorkflowSelection.FilterDiagrams(analysis.Diagrams, orchestratorName);
        if (selectedDiagrams.Length == 0)
        {
            throw new InvalidOperationException(WorkflowSelection.BuildFilterMismatchMessage(
                orchestratorName,
                analysis.Diagrams.Select(diagram => diagram.OrchestratorName)));
        }

        return new SourceWorkflowSelection(config, selectedDiagrams, resolvedOutputDirectory);
    }
}

internal sealed record SourceWorkflowSelection(
    DurableDocConfig Config,
    IReadOnlyList<WorkflowDiagram> SelectedDiagrams,
    string OutputDirectory);
