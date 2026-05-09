using System.IO.MemoryMappedFiles;
using System.Threading;

namespace Aom.App.Services.Overlay;

public sealed class OverlaySharedStateWriter : IDisposable
{
    private readonly object sync = new();
    private readonly byte[] buffer = new byte[OverlaySharedStatePacket.SharedMemoryCapacityBytes];
    private readonly MemoryMappedFile sharedMemory;
    private readonly MemoryMappedViewAccessor accessor;
    private readonly EventWaitHandle updatedEvent;
    private long sequence;

    public OverlaySharedStateWriter(
        string mappingName = OverlaySharedStatePacket.MappingName,
        string updatedEventName = OverlaySharedStatePacket.UpdatedEventName)
    {
        MappingName = mappingName;
        UpdatedEventName = updatedEventName;
        sharedMemory = MemoryMappedFile.CreateOrOpen(MappingName, OverlaySharedStatePacket.SharedMemoryCapacityBytes, MemoryMappedFileAccess.ReadWrite);
        accessor = sharedMemory.CreateViewAccessor(0, OverlaySharedStatePacket.SharedMemoryCapacityBytes, MemoryMappedFileAccess.Write);
        updatedEvent = new EventWaitHandle(false, EventResetMode.AutoReset, UpdatedEventName);
    }

    public string MappingName { get; }

    public string UpdatedEventName { get; }

    public void Publish(OverlaySharedStateSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        lock (sync)
        {
            sequence += 1;
            var bytesWritten = OverlaySharedStatePacket.Write(buffer, snapshot, sequence);
            accessor.WriteArray(0, buffer, 0, bytesWritten);
            accessor.Flush();
            updatedEvent.Set();
        }
    }

    public void Dispose()
    {
        accessor.Dispose();
        sharedMemory.Dispose();
        updatedEvent.Dispose();
    }
}