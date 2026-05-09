using System.Buffers.Binary;
using Aom.App.Services.Il2;
using Xunit;

namespace Aom.App.Tests;

public sealed class Il2TelemetryPacketReaderTests
{
    [Fact]
    public void TryRead_ParsesEasAndFlapsIndicators()
    {
        var packet = BuildPacket(
            (0, [1200f]),
            (6, [123.5f]),
            (11, [0.75f]));

        var parsed = Il2TelemetryPacketReader.TryRead(packet, new DateTimeOffset(2026, 4, 28, 12, 0, 0, TimeSpan.Zero), out var snapshot);

        Assert.True(parsed);
        Assert.Equal<uint>(42, snapshot.Tick);
        Assert.Equal(123.5f, snapshot.EasMetersPerSecond);
        Assert.Equal(0.75f, snapshot.FlapsPosition);
        Assert.Equal(444.6d, snapshot.EasKilometersPerHour!.Value, 1);
    }

    [Fact]
    public void TryRead_RejectsPacketsWithUnexpectedHeader()
    {
        var packet = BuildPacket((6, [88f]));
        BinaryPrimitives.WriteUInt32LittleEndian(packet, 0xDEADBEEF);

        var parsed = Il2TelemetryPacketReader.TryRead(packet, DateTimeOffset.UtcNow, out _);

        Assert.False(parsed);
    }

    [Fact]
    public void TryRead_ReturnsFalseWhenSpeedAndFlapsAreMissing()
    {
        var packet = BuildPacket((0, [900f]));

        var parsed = Il2TelemetryPacketReader.TryRead(packet, DateTimeOffset.UtcNow, out _);

        Assert.False(parsed);
    }

    private static byte[] BuildPacket(params (ushort indicatorId, float[] values)[] indicators)
    {
        var size = 11 + indicators.Sum(indicator => 3 + (indicator.values.Length * sizeof(float))) + 1;
        var packet = new byte[size];
        var offset = 0;

        BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(offset, 4), Il2TelemetryPacketReader.PacketId);
        offset += 4;
        BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(offset, 2), (ushort)size);
        offset += 2;
        BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(offset, 4), 42);
        offset += 4;
        packet[offset++] = (byte)indicators.Length;

        foreach (var (indicatorId, values) in indicators)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(offset, 2), indicatorId);
            offset += 2;
            packet[offset++] = (byte)values.Length;

            foreach (var value in values)
            {
                BinaryPrimitives.WriteInt32LittleEndian(packet.AsSpan(offset, 4), BitConverter.SingleToInt32Bits(value));
                offset += 4;
            }
        }

        packet[offset] = 0;
        return packet;
    }
}