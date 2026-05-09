namespace Aom.App.Services.Settings;

public static class PresetNameAllocator
{
    public static string AllocateUnknownDisplayName(IEnumerable<string> existingNames)
    {
        ArgumentNullException.ThrowIfNull(existingNames);

        var occupiedNames = existingNames
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (!occupiedNames.Contains("Unknown"))
        {
            return "Unknown";
        }

        for (var index = 2; ; index++)
        {
            var candidate = $"Unknown ({index})";
            if (!occupiedNames.Contains(candidate))
            {
                return candidate;
            }
        }
    }
}