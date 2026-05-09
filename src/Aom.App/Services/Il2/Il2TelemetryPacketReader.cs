using System.Buffers.Binary;
using System.Runtime.InteropServices;

namespace Aom.App.Services.Il2;

public static class Il2TelemetryPacketReader
{
    public const uint PacketId = 0x54000101;
    private const int HeaderSizeBytes = 11;
    private const ushort EasIndicatorId = 6;
    private const ushort FlapsIndicatorId = 11;

    public static bool TryRead(ReadOnlySpan<byte> packet, DateTimeOffset receivedAtUtc, out Il2TelemetrySnapshot snapshot)
    {
        snapshot = new Il2TelemetrySnapshot(0, null, null, receivedAtUtc);

        if (packet.Length < HeaderSizeBytes)
        {
            return false;
        }

        var packetId = BinaryPrimitives.ReadUInt32LittleEndian(packet);
        if (packetId != PacketId)
        {
            return false;
        }

        var messageSize = BinaryPrimitives.ReadUInt16LittleEndian(packet.Slice(4));
        if (messageSize > packet.Length)
        {
            return false;
        }

        var tick = BinaryPrimitives.ReadUInt32LittleEndian(packet.Slice(6));
        var indicatorsCount = packet[10];
        var offset = HeaderSizeBytes;
        float? easMetersPerSecond = null;
        float? flapsPosition = null;

        for (var index = 0; index < indicatorsCount; index++)
        {
            if (offset + 3 > messageSize)
            {
                return false;
            }

            var indicatorId = BinaryPrimitives.ReadUInt16LittleEndian(packet.Slice(offset, 2));
            var valuesCount = packet[offset + 2];
            offset += 3;

            var valuesByteCount = valuesCount * sizeof(float);
            if (offset + valuesByteCount > messageSize)
            {
                return false;
            }

            if (valuesCount > 0)
            {
                var firstValue = ReadSingle(packet.Slice(offset, sizeof(float)));
                if (indicatorId == EasIndicatorId)
                {
                    easMetersPerSecond = firstValue;
                }
                else if (indicatorId == FlapsIndicatorId)
                {
                    flapsPosition = firstValue;
                }
            }

            offset += valuesByteCount;
        }

        if (offset + 1 > messageSize)
        {
            return false;
        }

        snapshot = new Il2TelemetrySnapshot(tick, easMetersPerSecond, flapsPosition, receivedAtUtc);
        return snapshot.HasAnyValue;
    }

    private static float ReadSingle(ReadOnlySpan<byte> value)
    {
        var bits = BinaryPrimitives.ReadInt32LittleEndian(value);
        return BitConverter.Int32BitsToSingle(bits);
    }
}