namespace Aom.App.Services.TrackIr;

public static class TrackIrStatusResolver
{
    public static string Resolve(bool receivedFreshFrame, bool hasLivePose, DateTimeOffset sampledAt, DateTimeOffset lastFrameAt, TimeSpan staleThreshold)
    {
        if (receivedFreshFrame)
        {
            return "live";
        }

        if (!hasLivePose)
        {
            return "waiting for frame";
        }

        return sampledAt - lastFrameAt <= staleThreshold
            ? "live"
            : "waiting for frame";
    }
}