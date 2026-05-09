namespace Aom.App.Services.TrackIr;

public static class TrackIrInputProfileCatalog
{
    public const string LegacyAomId = "legacy-aom-trackir";
    public const string DefaultTrackIrId = "default-trackir";

    public static IReadOnlyList<TrackIrInputProfile> All { get; } =
        new[]
        {
            new TrackIrInputProfile(
                LegacyAomId,
                "Legacy AOM TrackIR",
                AutoCornerStart: 30,
                AutoCornerEnd: 140,
                AutoCornerXEnd: 140,
                YawScale: 0.1,
                PitchScale: 1.0,
                XDeadZone: 1.0,
                YEngageYawThreshold: 45),
            new TrackIrInputProfile(
                DefaultTrackIrId,
                "Default TrackIR",
                AutoCornerStart: 20,
                AutoCornerEnd: 140,
                AutoCornerXEnd: 45,
                YawScale: 0.12,
                PitchScale: 0.09,
                XDeadZone: 0.35,
                YEngageYawThreshold: 35),
        };

    public static TrackIrInputProfile DefaultProfile => All[1];

    public static TrackIrInputProfile Resolve(string? id)
    {
        return All.FirstOrDefault(profile => string.Equals(profile.Id, id, StringComparison.Ordinal)) ?? DefaultProfile;
    }
}