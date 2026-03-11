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
        var includePatterns = config.Analysis?.IncludePatterns ?? [];
        var excludePatterns = config.Analysis?.ExcludePatterns ?? [];
        var duplicates = wrappers
            .Where(w => !string.IsNullOrWhiteSpace(w.MethodName))
            .GroupBy(w => w.MethodName, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToArray();
        var orchestrators = config.BusinessView?.Orchestrators ?? [];
        var duplicateOrchestrators = orchestrators
            .Where(o => !string.IsNullOrWhiteSpace(o.Name))
            .GroupBy(o => o.Name, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
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

        foreach (var pattern in includePatterns.Concat(excludePatterns))
        {
            if (string.IsNullOrWhiteSpace(pattern))
            {
                errors.Add("Analysis include/exclude patterns cannot be blank.");
            }
        }

        foreach (var orchestrator in orchestrators)
        {
            if (string.IsNullOrWhiteSpace(orchestrator.Name))
            {
                errors.Add("Business view orchestrator metadata requires 'name'.");
            }

            var steps = orchestrator.Steps ?? [];
            var duplicateSteps = steps
                .Where(step => !string.IsNullOrWhiteSpace(step.Name))
                .GroupBy(step => step.Name, StringComparer.OrdinalIgnoreCase)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToArray();

            foreach (var step in steps)
            {
                if (string.IsNullOrWhiteSpace(step.Name))
                {
                    errors.Add($"Business view step metadata for orchestrator '{orchestrator.Name}' requires 'name'.");
                }
            }

            if (duplicateSteps.Length > 0)
            {
                errors.Add($"Duplicate business view step names are not allowed for orchestrator '{orchestrator.Name}': {string.Join(", ", duplicateSteps)}.");
            }
        }

        if (duplicates.Length > 0)
        {
            errors.Add($"Duplicate wrapper method names are not allowed: {string.Join(", ", duplicates)}.");
        }

        if (duplicateOrchestrators.Length > 0)
        {
            errors.Add($"Duplicate business view orchestrator names are not allowed: {string.Join(", ", duplicateOrchestrators)}.");
        }

        if (errors.Count > 0)
        {
            throw new ConfigValidationException(errors);
        }
    }
}
