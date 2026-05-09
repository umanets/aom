using Aom.Core.Presets;

namespace Aom.App.Services.Settings;

public static class PersistedPresetCatalog
{
    public static IReadOnlyList<CameraPreset> Load(AppSettingsDocument settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        if (settings.Presets.Count > 0)
        {
            return settings.Presets
                .Select(document => document.ToPreset())
                .ToArray();
        }

        return PresetCatalog.All
            .Select(preset =>
            {
                if (!settings.PresetDisplayNames.TryGetValue(preset.Id, out var displayName) || string.IsNullOrWhiteSpace(displayName))
                {
                    return preset;
                }

                return preset with { DisplayName = displayName.Trim() };
            })
            .ToArray();
    }

    public static IReadOnlyList<SavedPresetDocument> Save(IEnumerable<CameraPreset> presets)
    {
        ArgumentNullException.ThrowIfNull(presets);

        return presets
            .Select(SavedPresetDocument.FromPreset)
            .ToArray();
    }
}