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

        WorkflowDiagram[] selectedDiagrams;
        try
        {
            selectedDiagrams = WorkflowSelection.FilterDiagrams(analysis.Diagrams, orchestratorName);
        }
        catch (InvalidOperationException ex)
        {
            throw new InvalidOperationException(ex.Message, ex);
        }

        if (selectedDiagrams.Length == 0)
        {
            throw new InvalidOperationException(WorkflowSelection.BuildFilterMismatchMessage(
                orchestratorName,
                analysis.Diagrams.Select(diagram => diagram.OrchestratorDisplayName)));
        }

        return new SourceWorkflowSelection(config, selectedDiagrams, resolvedOutputDirectory);
    }
}

internal sealed record SourceWorkflowSelection(
    DurableDocConfig Config,
    IReadOnlyList<WorkflowDiagram> SelectedDiagrams,
    string OutputDirectory);
