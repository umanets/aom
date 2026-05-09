using Aom.App.Services.Settings;
using Aom.Core.Presets;
using Xunit;

namespace Aom.App.Tests;

public sealed class PersistedPresetCatalogTests
{
    [Fact]
    public void Load_UsesLegacyDisplayNameOverridesWhenSavedPresetsAreMissing()
    {
        var settings = new AppSettingsDocument
        {
            PresetDisplayNames = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["lagg"] = "Frontline Lagg",
            },
        };

        var presets = PersistedPresetCatalog.Load(settings);

        Assert.Equal("Frontline Lagg", presets.Single(preset => string.Equals(preset.Id, "lagg", StringComparison.Ordinal)).DisplayName);
    }

    [Fact]
    public void SaveAndLoad_RoundTripsSavedPresets()
    {
        var sourcePresets = new[]
        {
            new CameraPreset("custom-1", "Unknown", new[] { new PresetParameter("deltaX0", 1.25), new PresetParameter("deltaY0", -0.5) }),
        };

        var settings = new AppSettingsDocument
        {
            Presets = PersistedPresetCatalog.Save(sourcePresets).ToList(),
        };

        var presets = PersistedPresetCatalog.Load(settings);
        var preset = Assert.Single(presets);

        Assert.Equal("custom-1", preset.Id);
        Assert.Equal("Unknown", preset.DisplayName);
        Assert.Equal(1.25, preset.GetValueOrDefault("deltaX0"));
        Assert.Equal(-0.5, preset.GetValueOrDefault("deltaY0"));
    }
}