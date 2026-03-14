using DurableDoc.Analysis;
using DurableDoc.Configuration;
using DurableDoc.Dashboard;

namespace DurableDoc.Cli;

public static class DashboardCommandHandler
{
    public static async Task<int> ExecuteAsync(
        string inputPath,
        string? outputDirectory = null,
        string? orchestratorName = null,
        string? mode = null,
        string audience = "developer",
        string? configPath = null,
        CliCommandContext? context = null,
        bool openDashboard = false,
        Func<Uri, CancellationToken, Task>? browserLauncher = null,
        CancellationToken cancellationToken = default)
    {
        context ??= CliCommandContext.CreateDefault();

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!DashboardPreviewHost.ValidateInteractivePreview(context, openDashboard))
            {
                return 1;
            }

            var inputKind = ResolveInputKind(inputPath);
            var parsedAudience = GenerateCommandHandler.ParseAudience(audience);
            DashboardBuildResult result;
            string previewDirectory;
            string? previewMode = mode;
            IReadOnlyList<string> selectedOrchestratorNames;

            switch (inputKind)
            {
                case DashboardInputKind.ArtifactDirectory:
                {
                    RejectSourceOnlyOptions(outputDirectory, configPath);

                    var artifacts = DashboardGenerator.ReadArtifacts(inputPath);
                    var selectedArtifacts = WorkflowSelection.FilterArtifacts(artifacts, orchestratorName);

                    if (selectedArtifacts.Length == 0)
                    {
                        context.Fail(WorkflowSelection.BuildFilterMismatchMessage(
                            orchestratorName,
                            artifacts.Select(artifact => artifact.OrchestratorName).Distinct(StringComparer.Ordinal)));
                        return 1;
                    }

                    result = DashboardGenerator.BuildDashboard(inputPath, selectedArtifacts, parsedAudience);
                    previewDirectory = inputPath;
                    selectedOrchestratorNames = selectedArtifacts.Select(artifact => artifact.OrchestratorName).ToArray();
                    break;
                }
                case DashboardInputKind.Source:
                {
                    var config = DurableDocConfigLoader.Load(configPath);
                    var renderMode = GenerateCommandHandler.ParseMode(mode, parsedAudience);
                    var resolvedOutputDirectory = GenerateCommandHandler.ResolveOutputDirectory(outputDirectory, config, parsedAudience);
                    var analyzer = new WorkflowAnalyzer();
                    var analysis = await analyzer.AnalyzeWorkspaceAsync(inputPath, config, cancellationToken).ConfigureAwait(false);

                    if (analysis.Diagrams.Count == 0)
                    {
                        context.Fail(GenerateCommandHandler.BuildNoDiscoveryMessage(analysis, inputPath));
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

                    result = DashboardGenerator.WriteArtifactsAndBuild(
                        resolvedOutputDirectory,
                        GenerateCommandHandler.CreateArtifacts(selectedDiagrams, renderMode, config, parsedAudience),
                        parsedAudience);
                    previewDirectory = resolvedOutputDirectory;
                    previewMode = renderMode.ToString().ToLowerInvariant();
                    selectedOrchestratorNames = selectedDiagrams.Select(diagram => diagram.OrchestratorName).ToArray();
                    context.Info($"Prepared {result.DiagramCount} diagram(s) in {Path.GetFullPath(resolvedOutputDirectory)}.");
                    break;
                }
                default:
                    throw new InvalidOperationException($"Unsupported dashboard input kind '{inputKind}'.");
            }

            context.Info($"Dashboard ready at {result.DashboardPath}");

            if (openDashboard)
            {
                await DashboardPreviewHost.PreviewAsync(
                    previewDirectory,
                    context,
                    WorkflowSelection.ResolvePreviewOrchestrator(orchestratorName, selectedOrchestratorNames),
                    previewMode,
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

    private static DashboardInputKind ResolveInputKind(string inputPath)
    {
        if (string.IsNullOrWhiteSpace(inputPath))
        {
            throw new ArgumentException("Input path is required.", nameof(inputPath));
        }

        var fullPath = Path.GetFullPath(inputPath);
        if (File.Exists(fullPath))
        {
            var extension = Path.GetExtension(fullPath);
            if (extension.Equals(".sln", StringComparison.OrdinalIgnoreCase)
                || extension.Equals(".csproj", StringComparison.OrdinalIgnoreCase)
                || extension.Equals(".cs", StringComparison.OrdinalIgnoreCase))
            {
                return DashboardInputKind.Source;
            }
        }

        if (Directory.Exists(fullPath))
        {
            return Directory.EnumerateFiles(fullPath, "*.diagram.json", SearchOption.TopDirectoryOnly).Any()
                ? DashboardInputKind.ArtifactDirectory
                : DashboardInputKind.Source;
        }

        throw new FileNotFoundException($"Input path was not found: {inputPath}");
    }

    private static void RejectSourceOnlyOptions(string? outputDirectory, string? configPath)
    {
        if (!string.IsNullOrWhiteSpace(outputDirectory))
        {
            throw new ArgumentException("The '--output' option is only supported when '--input' points to source input.", nameof(outputDirectory));
        }

        if (!string.IsNullOrWhiteSpace(configPath))
        {
            throw new ArgumentException("The '--config' option is only supported when '--input' points to source input.", nameof(configPath));
        }
    }
}

internal enum DashboardInputKind
{
    Source,
    ArtifactDirectory,
}
