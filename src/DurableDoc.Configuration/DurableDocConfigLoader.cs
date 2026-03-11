using System.Text.Json;

namespace DurableDoc.Configuration;

public static class DurableDocConfigLoader
{
    private const string DefaultFileName = "durable-doc.json";

    public static DurableDocConfig Load(string? explicitPath = null, string? startDirectory = null)
    {
        var configPath = ResolveConfigPath(explicitPath, startDirectory ?? Directory.GetCurrentDirectory());

        DurableDocConfig config;
        if (configPath is null)
        {
            config = new DurableDocConfig();
        }
        else
        {
            var json = File.ReadAllText(configPath);
            config = JsonSerializer.Deserialize<DurableDocConfig>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? new DurableDocConfig();
        }

        config.Defaults ??= new DefaultsOptions();
        config.Analysis ??= new AnalysisOptions();
        config.Analysis.Wrappers ??= [];
        config.Analysis.IncludePatterns ??= [];
        config.Analysis.ExcludePatterns ??= [];
        config.BusinessView ??= new BusinessViewOptions();
        config.BusinessView.Orchestrators ??= [];
        config.Rendering ??= new RenderingOptions();
        config.Dashboard ??= new DashboardOptions();

        DurableDocConfigValidator.Validate(config);
        return config;
    }

    public static string? ResolveConfigPath(string? explicitPath, string startDirectory)
    {
        if (!string.IsNullOrWhiteSpace(explicitPath))
        {
            var absoluteExplicitPath = Path.GetFullPath(explicitPath);
            if (!File.Exists(absoluteExplicitPath))
            {
                throw new FileNotFoundException($"Config file was not found: {absoluteExplicitPath}");
            }

            return absoluteExplicitPath;
        }

        var currentDirectoryConfig = Path.Combine(startDirectory, DefaultFileName);
        if (File.Exists(currentDirectoryConfig))
        {
            return currentDirectoryConfig;
        }

        var solutionRoot = FindSolutionRoot(startDirectory);
        if (solutionRoot is null)
        {
            return null;
        }

        var solutionConfig = Path.Combine(solutionRoot, DefaultFileName);
        return File.Exists(solutionConfig) ? solutionConfig : null;
    }

    private static string? FindSolutionRoot(string startDirectory)
    {
        var directory = new DirectoryInfo(startDirectory);

        while (directory is not null)
        {
            if (directory.EnumerateFiles("*.sln", SearchOption.TopDirectoryOnly).Any())
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        return null;
    }
}
