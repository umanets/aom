using Aom.App.Services.TrackIr;
using Xunit;

namespace Aom.App.Tests;

public sealed class TrackIrStatusResolverTests
{
    private static readonly TimeSpan StaleThreshold = TimeSpan.FromMilliseconds(100);

    [Fact]
    public void Resolve_ReturnsLiveWhenFreshFrameArrives()
    {
        var now = DateTimeOffset.UtcNow;

        var status = TrackIrStatusResolver.Resolve(
            receivedFreshFrame: true,
            hasLivePose: true,
            sampledAt: now,
            lastFrameAt: now.AddMilliseconds(-500),
            staleThreshold: StaleThreshold);

        Assert.Equal("live", status);
    }

    [Fact]
    public void Resolve_KeepsLiveStatusWithinStaleThreshold()
    {
        var now = DateTimeOffset.UtcNow;

        var status = TrackIrStatusResolver.Resolve(
            receivedFreshFrame: false,
            hasLivePose: true,
            sampledAt: now,
            lastFrameAt: now.AddMilliseconds(-40),
            staleThreshold: StaleThreshold);

        Assert.Equal("live", status);
    }

    [Fact]
    public void Resolve_ReturnsWaitingWhenNoRecentFrameExists()
    {
        var now = DateTimeOffset.UtcNow;

        var status = TrackIrStatusResolver.Resolve(
            receivedFreshFrame: false,
            hasLivePose: true,
            sampledAt: now,
            lastFrameAt: now.AddMilliseconds(-250),
            staleThreshold: StaleThreshold);

        Assert.Equal("waiting for frame", status);
    }

    [Fact]
    public void Resolve_ReturnsWaitingBeforeFirstFrame()
    {
        var now = DateTimeOffset.UtcNow;

        var status = TrackIrStatusResolver.Resolve(
            receivedFreshFrame: false,
            hasLivePose: false,
            sampledAt: now,
            lastFrameAt: DateTimeOffset.MinValue,
            staleThreshold: StaleThreshold);

        Assert.Equal("waiting for frame", status);
    }
}