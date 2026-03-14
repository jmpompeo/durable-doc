using DurableDoc.Analysis;
using DurableDoc.Configuration;

namespace DurableDoc.Cli;

public static class ValidateCommandHandler
{
    public static async Task<int> ExecuteAsync(
        string inputPath,
        string? orchestratorName = null,
        string audience = "developer",
        string? configPath = null,
        bool strict = false,
        CliCommandContext? context = null,
        CancellationToken cancellationToken = default)
    {
        context ??= CliCommandContext.CreateDefault();

        try
        {
            var config = DurableDocConfigLoader.Load(configPath);
            context.Detail("Configuration loaded successfully.");

            var analyzer = new WorkflowAnalyzer();
            var analysis = await analyzer.AnalyzeWorkspaceAsync(inputPath, config, cancellationToken).ConfigureAwait(false);
            if (analysis.Diagrams.Count == 0)
            {
                context.Fail(GenerateCommandHandler.BuildNoDiscoveryMessage(analysis, inputPath));
                return 1;
            }

            var diagrams = WorkflowSelection.FilterDiagrams(analysis.Diagrams, orchestratorName);

            if (diagrams.Length == 0)
            {
                context.Fail(WorkflowSelection.BuildFilterMismatchMessage(
                    orchestratorName,
                    analysis.Diagrams.Select(diagram => diagram.OrchestratorName)));
                return 1;
            }

            var diagnostics = CliDiagnostics.Evaluate(diagrams, config, GenerateCommandHandler.ParseAudience(audience));

            foreach (var warning in diagnostics.Where(d => d.Severity == CliDiagnosticSeverity.Warning))
            {
                context.Warn(FormatDiagnostic(warning));
            }

            var errors = diagnostics.Where(d => d.Severity == CliDiagnosticSeverity.Error).ToArray();
            if (errors.Length > 0)
            {
                foreach (var error in errors)
                {
                    context.Fail(FormatDiagnostic(error));
                }

                return 1;
            }

            if (strict && diagnostics.Any(d => d.Severity == CliDiagnosticSeverity.Warning))
            {
                context.Fail("Validation completed with warnings and '--strict' was specified.");
                return 1;
            }

            context.Info($"Validation succeeded for {diagrams.Length} orchestrator(s).");
            return 0;
        }
        catch (Exception ex)
        {
            context.Fail(ex.Message);
            return 1;
        }
    }

    private static string FormatDiagnostic(CliDiagnostic diagnostic)
    {
        return diagnostic.OrchestratorName is null
            ? diagnostic.Message
            : $"{diagnostic.OrchestratorName}: {diagnostic.Message}";
    }
}
