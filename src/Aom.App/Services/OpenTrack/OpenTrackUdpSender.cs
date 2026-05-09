using System.Net;
using System.Net.Sockets;
using Aom.Core.Runtime;

namespace Aom.App.Services.OpenTrack;

public sealed class OpenTrackUdpSender : IDisposable
{
    private readonly UdpClient udpClient;
    private readonly IPEndPoint endPoint;

    public OpenTrackUdpSender(string host = "127.0.0.1", int port = 5555)
    {
        udpClient = new UdpClient();
        endPoint = new IPEndPoint(IPAddress.Parse(host), port);
    }

    public string Destination => $"{endPoint.Address}:{endPoint.Port}";

    public void Send(HeadPose pose)
    {
        var packet = OpenTrackPacketEncoder.Encode(pose);
        udpClient.Send(packet, packet.Length, endPoint);
    }

    public void Dispose()
    {
        udpClient.Dispose();
    }
}