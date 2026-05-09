using Aom.Core.Presets;

namespace Aom.App.Services.Settings;

public sealed class SavedPresetDocument
{
    public string Id { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public Dictionary<string, double> Parameters { get; set; } = new(StringComparer.Ordinal);

    public CameraPreset ToPreset()
    {
        var orderedParameters = Parameters
            .OrderBy(parameter => parameter.Key, StringComparer.Ordinal)
            .Select(parameter => new PresetParameter(parameter.Key, parameter.Value))
            .ToArray();

        return new CameraPreset(Id, DisplayName, orderedParameters);
    }

    public static SavedPresetDocument FromPreset(CameraPreset preset)
    {
        ArgumentNullException.ThrowIfNull(preset);

        return new SavedPresetDocument
        {
            Id = preset.Id,
            DisplayName = preset.DisplayName,
            Parameters = preset.Parameters.ToDictionary(parameter => parameter.Name, parameter => parameter.Value, StringComparer.Ordinal),
        };
    }
}