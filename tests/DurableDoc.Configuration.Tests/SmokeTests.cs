using DurableDoc.Configuration;

namespace DurableDoc.Configuration.Tests;

public class SmokeTests
{
    [Fact]
    public void Load_ParsesValidConfig()
    {
        using var fixture = new ConfigFixture();
        var configPath = fixture.WriteConfig(
            """
            {
              "version": 1,
              "analysis": {
                "wrappers": [
                  { "methodName": "CallActivityWithResult", "kind": "Activity", "targetNameArgumentIndex": 0 }
                ]
              },
              "businessView": {
                "steps": [
                  { "orchestrator": "Run", "step": "ValidateOrder", "label": "Validate order", "group": "Review", "hide": true }
                ]
              }
            }
            """);

        var config = DurableDocConfigLoader.Load(configPath, fixture.WorkingDirectory);

        Assert.Equal(1, config.Version);
        Assert.Single(config.Analysis!.Wrappers!);
        Assert.Equal("CallActivityWithResult", config.Analysis!.Wrappers![0].MethodName);
        Assert.Single(config.BusinessView!.Steps!);
        Assert.Equal("Validate order", config.BusinessView!.Steps![0].Label);
    }

    [Fact]
    public void Load_ThrowsForInvalidVersion()
    {
        using var fixture = new ConfigFixture();
        var configPath = fixture.WriteConfig("{ " + '"' + "version" + '"' + ": 2 }");

        var ex = Assert.Throws<ConfigValidationException>(() => DurableDocConfigLoader.Load(configPath, fixture.WorkingDirectory));

        Assert.Contains("version", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Load_ThrowsForDuplicateWrappers()
    {
        using var fixture = new ConfigFixture();
        var configPath = fixture.WriteConfig(
            """
            {
              "version": 1,
              "analysis": {
                "wrappers": [
                  { "methodName": "CallActivityWithResult", "kind": "Activity" },
                  { "methodName": "callactivitywithresult", "kind": "Activity" }
                ]
              }
            }
            """);

        var ex = Assert.Throws<ConfigValidationException>(() => DurableDocConfigLoader.Load(configPath, fixture.WorkingDirectory));

        Assert.Contains("Duplicate wrapper", ex.Message);
    }

    [Fact]
    public void Load_ThrowsWhenWrapperMissingRequiredFields()
    {
        using var fixture = new ConfigFixture();
        var configPath = fixture.WriteConfig(
            """
            {
              "version": 1,
              "analysis": {
                "wrappers": [
                  { "methodName": "", "kind": "" }
                ]
              }
            }
            """);

        var ex = Assert.Throws<ConfigValidationException>(() => DurableDocConfigLoader.Load(configPath, fixture.WorkingDirectory));

        Assert.Contains("requires", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Load_ThrowsForDuplicateBusinessStepOverlays()
    {
        using var fixture = new ConfigFixture();
        var configPath = fixture.WriteConfig(
            """
            {
              "version": 1,
              "businessView": {
                "steps": [
                  { "orchestrator": "Run", "step": "ValidateOrder" },
                  { "orchestrator": "run", "step": "validateorder" }
                ]
              }
            }
            """);

        var ex = Assert.Throws<ConfigValidationException>(() => DurableDocConfigLoader.Load(configPath, fixture.WorkingDirectory));

        Assert.Contains("Duplicate business step overlays", ex.Message);
    }

    [Fact]
    public void ResolveConfigPath_FindsCurrentDirectoryConfigBeforeSolutionRoot()
    {
        using var fixture = new ConfigFixture();
        fixture.WriteSolutionFile();
        var currentConfig = fixture.WriteConfig("{ \"version\": 1 }");

        var resolved = DurableDocConfigLoader.ResolveConfigPath(null, fixture.WorkingDirectory);

        Assert.Equal(currentConfig, resolved);
    }

    private sealed class ConfigFixture : IDisposable
    {
        public ConfigFixture()
        {
            WorkingDirectory = Path.Combine(Path.GetTempPath(), $"durable-doc-tests-{Guid.NewGuid():N}");
            Directory.CreateDirectory(WorkingDirectory);
        }

        public string WorkingDirectory { get; }

        public string WriteConfig(string json)
        {
            var path = Path.Combine(WorkingDirectory, "durable-doc.json");
            File.WriteAllText(path, json);
            return path;
        }

        public void WriteSolutionFile()
        {
            File.WriteAllText(Path.Combine(WorkingDirectory, "durable-doc.sln"), "");
        }

        public void Dispose()
        {
            if (Directory.Exists(WorkingDirectory))
            {
                Directory.Delete(WorkingDirectory, true);
            }
        }
    }
}
