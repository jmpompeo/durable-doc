namespace DurableDoc.Configuration;

public static class DurableDocConfigValidator
{
    public static void Validate(DurableDocConfig config)
    {
        var errors = new List<string>();

        if (config.Version != 1)
        {
            errors.Add("'version' must be set to 1.");
        }

        var wrappers = config.Analysis?.Wrappers ?? [];
        var duplicates = wrappers
            .Where(w => !string.IsNullOrWhiteSpace(w.MethodName))
            .GroupBy(w => w.MethodName, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToArray();
        var businessSteps = config.BusinessView?.Steps ?? [];
        var duplicateBusinessSteps = businessSteps
            .Where(step => !string.IsNullOrWhiteSpace(step.Orchestrator) && !string.IsNullOrWhiteSpace(step.Step))
            .GroupBy(
                step => $"{step.Orchestrator.Trim()}::{step.Step.Trim()}",
                StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key.Replace("::", " / ", StringComparison.Ordinal))
            .ToArray();

        foreach (var wrapper in wrappers)
        {
            if (string.IsNullOrWhiteSpace(wrapper.MethodName))
            {
                errors.Add("Wrapper definition requires 'methodName'.");
            }

            if (string.IsNullOrWhiteSpace(wrapper.Kind))
            {
                errors.Add($"Wrapper '{wrapper.MethodName}' requires 'kind'.");
            }
        }

        foreach (var step in businessSteps)
        {
            if (string.IsNullOrWhiteSpace(step.Orchestrator))
            {
                errors.Add("Business step overlay requires 'orchestrator'.");
            }

            if (string.IsNullOrWhiteSpace(step.Step))
            {
                errors.Add($"Business step overlay for orchestrator '{step.Orchestrator}' requires 'step'.");
            }
        }

        if (duplicates.Length > 0)
        {
            errors.Add($"Duplicate wrapper method names are not allowed: {string.Join(", ", duplicates)}.");
        }

        if (duplicateBusinessSteps.Length > 0)
        {
            errors.Add($"Duplicate business step overlays are not allowed: {string.Join(", ", duplicateBusinessSteps)}.");
        }

        if (errors.Count > 0)
        {
            throw new ConfigValidationException(errors);
        }
    }
}
