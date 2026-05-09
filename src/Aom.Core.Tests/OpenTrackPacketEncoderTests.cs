using Aom.Core.Runtime;
using Xunit;

namespace Aom.Core.Tests;

public sealed class OpenTrackPacketEncoderTests
{
    [Fact]
    public void Encode_ProducesFortyEightBytePacket()
    {
        var packet = OpenTrackPacketEncoder.Encode(new HeadPose(1, 2, 3, 4, 5, 6));

        Assert.Equal(OpenTrackPacketEncoder.PacketSize, packet.Length);
    }

    [Fact]
    public void Encode_WritesValuesInOpenTrackOrder()
    {
        var pose = new HeadPose(Yaw: 11, Pitch: 12, Roll: 13, X: 21, Y: 22, Z: 23);

        var packet = OpenTrackPacketEncoder.Encode(pose);

        Assert.Equal(21, BitConverter.ToDouble(packet, 0));
        Assert.Equal(22, BitConverter.ToDouble(packet, 8));
        Assert.Equal(23, BitConverter.ToDouble(packet, 16));
        Assert.Equal(11, BitConverter.ToDouble(packet, 24));
        Assert.Equal(12, BitConverter.ToDouble(packet, 32));
        Assert.Equal(13, BitConverter.ToDouble(packet, 40));
    }
}