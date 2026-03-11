using System.Text.Json.Serialization;

namespace DurableDoc.Configuration;

public sealed class DurableDocConfig
{
    [JsonPropertyName("version")]
    public int Version { get; set; } = 1;

    [JsonPropertyName("defaults")]
    public DefaultsOptions? Defaults { get; set; } = new();

    [JsonPropertyName("analysis")]
    public AnalysisOptions? Analysis { get; set; } = new();

    [JsonPropertyName("businessView")]
    public BusinessViewOptions? BusinessView { get; set; } = new();

    [JsonPropertyName("rendering")]
    public RenderingOptions? Rendering { get; set; } = new();

    [JsonPropertyName("dashboard")]
    public DashboardOptions? Dashboard { get; set; } = new();
}

public sealed class DefaultsOptions
{
    [JsonPropertyName("output")]
    public string? Output { get; set; }

    [JsonPropertyName("format")]
    public string? Format { get; set; }
}

public sealed class AnalysisOptions
{
    [JsonPropertyName("wrappers")]
    public List<WrapperDefinition>? Wrappers { get; set; } = [];
}

public sealed class WrapperDefinition
{
    [JsonPropertyName("methodName")]
    public string MethodName { get; set; } = string.Empty;

    [JsonPropertyName("kind")]
    public string Kind { get; set; } = string.Empty;

    [JsonPropertyName("targetNameArgumentIndex")]
    public int? TargetNameArgumentIndex { get; set; }
}

public sealed class BusinessViewOptions
{
    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; } = true;
}

public sealed class RenderingOptions
{
    [JsonPropertyName("theme")]
    public string? Theme { get; set; }
}

public sealed class DashboardOptions
{
    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; } = true;
}
