using System.Buffers.Binary;
using System.Text.Json;

namespace Aom.App.Services.Overlay;

public static class OverlaySharedStatePacket
{
    public const int Version = 1;
    public const int HeaderSizeBytes = 24;
    public const int SharedMemoryCapacityBytes = 64 * 1024;
    public const string MappingName = @"Local\AomDesktop.OverlayState";
    public const string UpdatedEventName = @"Local\AomDesktop.OverlayUpdated";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public static int Write(Span<byte> destination, OverlaySharedStateSnapshot snapshot, long sequence)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var payload = JsonSerializer.SerializeToUtf8Bytes(snapshot, SerializerOptions);
        var totalLength = HeaderSizeBytes + payload.Length;
        if (totalLength > destination.Length)
        {
            throw new InvalidOperationException($"Overlay snapshot is too large for the shared memory packet ({payload.Length} bytes)." );
        }

        BinaryPrimitives.WriteInt32LittleEndian(destination, Version);
        BinaryPrimitives.WriteInt32LittleEndian(destination[4..], payload.Length);
        BinaryPrimitives.WriteInt64LittleEndian(destination[8..], sequence);
        payload.CopyTo(destination[HeaderSizeBytes..]);
        BinaryPrimitives.WriteInt64LittleEndian(destination[16..], sequence);
        return totalLength;
    }

    public static bool TryRead(ReadOnlySpan<byte> source, out OverlaySharedStateFrame frame)
    {
        frame = default;

        if (source.Length < HeaderSizeBytes)
        {
            return false;
        }

        var version = BinaryPrimitives.ReadInt32LittleEndian(source);
        var payloadLength = BinaryPrimitives.ReadInt32LittleEndian(source[4..]);
        var startSequence = BinaryPrimitives.ReadInt64LittleEndian(source[8..]);
        var endSequence = BinaryPrimitives.ReadInt64LittleEndian(source[16..]);

        if (version != Version || payloadLength <= 0 || source.Length < HeaderSizeBytes + payloadLength)
        {
            return false;
        }

        if (startSequence == 0 || startSequence != endSequence)
        {
            return false;
        }

        var snapshot = JsonSerializer.Deserialize<OverlaySharedStateSnapshot>(
            source.Slice(HeaderSizeBytes, payloadLength),
            SerializerOptions);

        if (snapshot is null)
        {
            return false;
        }

        frame = new OverlaySharedStateFrame(startSequence, snapshot);
        return true;
    }
}