namespace Aom.Core.Runtime;

public static class OpenTrackPacketEncoder
{
    public const int PacketSize = sizeof(double) * 6;

    public static byte[] Encode(HeadPose pose)
    {
        var buffer = new byte[PacketSize];
        WriteDouble(buffer, 0, pose.X);
        WriteDouble(buffer, 8, pose.Y);
        WriteDouble(buffer, 16, pose.Z);
        WriteDouble(buffer, 24, pose.Yaw);
        WriteDouble(buffer, 32, pose.Pitch);
        WriteDouble(buffer, 40, pose.Roll);
        return buffer;
    }

    private static void WriteDouble(byte[] buffer, int offset, double value)
    {
        BitConverter.GetBytes(value).CopyTo(buffer, offset);
    }
}