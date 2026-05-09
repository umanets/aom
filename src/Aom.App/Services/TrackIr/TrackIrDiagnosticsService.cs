namespace Aom.App.Services.TrackIr;

public sealed class TrackIrDiagnosticsService
{
    private readonly TrackIrInstallationLocator installationLocator = new();

    public TrackIrProbeResult Probe(nint windowHandle)
    {
        var installation = installationLocator.Locate();
        var candidates = installation?.Candidates ?? installationLocator.GetCandidatePaths();

        if (installation is null)
        {
            return TrackIrProbeResult.NotFound(candidates);
        }

        try
        {
            using var client = new TrackIrNativeClient(installation.DllPath);
            var signature = client.GetSignature();
            var version = client.QueryVersion();
            client.Initialize(windowHandle);

            Aom.Core.Runtime.HeadPose? pose = null;
            for (var attempt = 0; attempt < 8; attempt++)
            {
                if (client.TryReadPose(out var livePose))
                {
                    pose = livePose;
                    break;
                }

                Thread.Sleep(5);
            }

            var detail = pose is null
                ? $"NPClient initialized from {installation.Source}, but no fresh frame was observed during the short probe window."
                : $"NPClient initialized from {installation.Source} and returned a live pose sample.";

            return new TrackIrProbeResult(
                true,
                true,
                pose is null ? "connected" : "live",
                detail,
                installation.DllPath,
                signature,
                version,
                pose,
                candidates,
                null);
        }
        catch (Exception exception)
        {
            return TrackIrProbeResult.Failed(
                $"Found NPClient at {installation.DllPath}, but the initialization sequence failed.",
                installation.DllPath,
                candidates,
                exception);
        }
    }
}