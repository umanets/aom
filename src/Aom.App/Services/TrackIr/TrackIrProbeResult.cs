using Aom.Core.Runtime;

namespace Aom.App.Services.TrackIr;

public sealed record TrackIrProbeResult(
    bool IsDetected,
    bool IsInitialized,
    string Status,
    string Detail,
    string? DllPath,
    string? Signature,
    short? Version,
    HeadPose? SamplePose,
    IReadOnlyList<string> Candidates,
    string? Error)
{
    public static TrackIrProbeResult NotFound(IReadOnlyList<string> candidates) =>
        new(false, false, "not found", "No usable NPClient.dll installation was discovered.", null, null, null, null, candidates, null);

    public static TrackIrProbeResult Failed(string detail, string? dllPath, IReadOnlyList<string> candidates, Exception exception) =>
        new(true, false, "probe failed", detail, dllPath, null, null, null, candidates, exception.Message);
}