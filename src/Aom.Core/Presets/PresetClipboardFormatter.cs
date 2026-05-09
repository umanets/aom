using System.Globalization;

namespace Aom.Core.Presets;

public static class PresetClipboardFormatter
{
    public static string Format(CameraPreset preset)
    {
        ArgumentNullException.ThrowIfNull(preset);

        var lines = preset.Parameters
            .OrderBy(parameter => parameter.Name, StringComparer.Ordinal)
            .Select(parameter => $"    \"{parameter.Name}\": {parameter.Value.ToString("0.0000", CultureInfo.InvariantCulture)}");

        return "{\n" + string.Join(",\n", lines) + "\n}";
    }
}