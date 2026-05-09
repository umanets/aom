using System.Net;
using System.Net.Sockets;

namespace Aom.App.Services.Il2;

public sealed class Il2TelemetryReceiver : IDisposable
{
    private readonly UdpClient udpClient;
    private readonly IPEndPoint receiveEndPoint;

    public Il2TelemetryReceiver(int port = 4322)
    {
        receiveEndPoint = new IPEndPoint(IPAddress.Any, port);
        udpClient = new UdpClient(receiveEndPoint);
    }

    public int LocalPort => ((IPEndPoint)udpClient.Client.LocalEndPoint!).Port;

    public bool TryReadLatest(out Il2TelemetrySnapshot snapshot)
    {
        snapshot = new Il2TelemetrySnapshot(0, null, null, DateTimeOffset.MinValue);
        var found = false;
        IPEndPoint? remoteEndPoint = null;

        while (udpClient.Available > 0)
        {
            var datagram = udpClient.Receive(ref remoteEndPoint);
            if (Il2TelemetryPacketReader.TryRead(datagram, DateTimeOffset.UtcNow, out var nextSnapshot))
            {
                snapshot = nextSnapshot;
                found = true;
            }
        }

        return found;
    }

    public void Dispose()
    {
        udpClient.Dispose();
    }
}