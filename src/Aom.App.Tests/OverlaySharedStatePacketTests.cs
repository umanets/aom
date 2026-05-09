using Aom.App.Services.Overlay;
using Xunit;

namespace Aom.App.Tests;

public sealed class OverlaySharedStatePacketTests
{
    [Fact]
    public void Write_AndTryRead_RoundTripSnapshot()
    {
        var snapshot = new OverlaySharedStateSnapshot(
            IsVisible: true,
            CurrentPresetDisplayName: "Lagg",
            LiveTrackIrStatus: "connected",
            UdpStreamingStatus: "streaming",
            OutputPoseSummary: "pose",
            RuntimeStateSummary: "runtime",
            TrackIrRateSummary: "Fresh 120 Hz",
            UdpRateSummary: "UDP send 120 Hz",
            UpdatedAtUtc: new DateTimeOffset(2026, 4, 26, 12, 30, 0, TimeSpan.Zero));

        var buffer = new byte[OverlaySharedStatePacket.SharedMemoryCapacityBytes];
        var bytesWritten = OverlaySharedStatePacket.Write(buffer, snapshot, sequence: 7);

        var parsed = OverlaySharedStatePacket.TryRead(buffer.AsSpan(0, bytesWritten), out var frame);

        Assert.True(parsed);
        Assert.Equal(7, frame.Sequence);
        Assert.Equal(snapshot, frame.Snapshot);
    }

    [Fact]
    public void TryRead_RejectsUnstableFrame()
    {
        var buffer = new byte[OverlaySharedStatePacket.HeaderSizeBytes + 2];
        buffer[0] = OverlaySharedStatePacket.Version;
        buffer[4] = 2;
        buffer[8] = 3;
        buffer[16] = 4;
        buffer[24] = (byte)'{';
        buffer[25] = (byte)'}';

        var parsed = OverlaySharedStatePacket.TryRead(buffer, out _);

        Assert.False(parsed);
    }
}