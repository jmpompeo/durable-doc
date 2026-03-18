using DurableDoc.Dashboard;

namespace DurableDoc.Cli;

public static class DashboardCommandHandler
{
    public static async Task<int> ExecuteAsync(
        string inputPath,
        string? outputDirectory = null,
        string? orchestratorName = null,
        string? mode = null,
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
                    GeneratedDiagramArtifact[] selectedArtifacts;
                    try
                    {
                        selectedArtifacts = WorkflowSelection.FilterArtifacts(artifacts, orchestratorName);
                    }
                    catch (InvalidOperationException ex)
                    {
                        context.Fail(ex.Message);
                        return 1;
                    }

                    if (selectedArtifacts.Length == 0)
                    {
                        context.Fail(WorkflowSelection.BuildFilterMismatchMessage(
                            orchestratorName,
                            artifacts.Select(artifact => string.IsNullOrWhiteSpace(artifact.OrchestratorDisplayName) ? artifact.OrchestratorName : artifact.OrchestratorDisplayName)
                                .Distinct(StringComparer.Ordinal)));
                        return 1;
                    }

                    result = DashboardGenerator.BuildDashboard(inputPath, selectedArtifacts);
                    previewDirectory = inputPath;
                    selectedOrchestratorNames = selectedArtifacts.Select(artifact => string.IsNullOrWhiteSpace(artifact.OrchestratorKey) ? artifact.OrchestratorName : artifact.OrchestratorKey).ToArray();
                    break;
                }
                case DashboardInputKind.Source:
                {
                    var renderMode = GenerateCommandHandler.ParseMode(mode ?? "developer");
                    SourceWorkflowSelection sourceSelection;
                    try
                    {
                        sourceSelection = await SourceWorkflowLoader.LoadSelectedDiagramsAsync(
                            inputPath,
                            outputDirectory,
                            orchestratorName,
                            configPath,
                            cancellationToken).ConfigureAwait(false);
                    }
                    catch (InvalidOperationException ex)
                    {
                        context.Fail(ex.Message);
                        return 1;
                    }

                    result = DashboardGenerator.WriteArtifactsAndBuild(
                        sourceSelection.OutputDirectory,
                        GenerateCommandHandler.CreateArtifacts(sourceSelection.SelectedDiagrams, renderMode));
                    previewDirectory = sourceSelection.OutputDirectory;
                    previewMode = renderMode.ToString().ToLowerInvariant();
                    selectedOrchestratorNames = sourceSelection.SelectedDiagrams.Select(diagram => diagram.OrchestratorKey).ToArray();
                    context.Info($"Prepared {result.DiagramCount} diagram(s) in {Path.GetFullPath(sourceSelection.OutputDirectory)}.");
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
