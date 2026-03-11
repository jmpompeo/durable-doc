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

        if (duplicates.Length > 0)
        {
            errors.Add($"Duplicate wrapper method names are not allowed: {string.Join(", ", duplicates)}.");
        }

        if (errors.Count > 0)
        {
            throw new ConfigValidationException(errors);
        }
    }
}
