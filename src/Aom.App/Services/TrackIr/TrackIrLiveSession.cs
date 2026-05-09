using Aom.Core.Runtime;

namespace Aom.App.Services.TrackIr;

public sealed class TrackIrLiveSession : IDisposable
{
    private readonly TrackIrNativeClient client;

    public TrackIrLiveSession(string dllPath, nint windowHandle)
    {
        DllPath = dllPath;
        client = new TrackIrNativeClient(dllPath);
        client.Initialize(windowHandle);
    }

    public string DllPath { get; }

    public bool TryReadPose(out HeadPose pose) => client.TryReadPose(out pose);

    public void Dispose()
    {
        client.Dispose();
    }
}