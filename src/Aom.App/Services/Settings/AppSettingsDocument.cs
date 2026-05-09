namespace Aom.App.Services.Settings;

public sealed class AppSettingsDocument
{
    public string? SelectedPresetId { get; set; }

    public string? TrackIrInputProfileId { get; set; }

    public double TrackIrYawMultiplier { get; set; } = 1.0;

    public int SelectedMainTabIndex { get; set; }

    public bool AutoStartUdpStreaming { get; set; }

    public bool AiPartnerEnabled { get; set; }

    public bool AiPartnerMicrophoneTestModeEnabled { get; set; }

    public string? AiPartnerBriefing { get; set; }

    public int AiPartnerScreenshotCadenceSeconds { get; set; } = 3;

    public string? Il2RawVideoLibraryPath { get; set; }

    public Dictionary<string, string> BindingTriggers { get; set; } = new(StringComparer.Ordinal);

    public Dictionary<string, string> PresetDisplayNames { get; set; } = new(StringComparer.Ordinal);

    public List<SavedPresetDocument> Presets { get; set; } = new();
}