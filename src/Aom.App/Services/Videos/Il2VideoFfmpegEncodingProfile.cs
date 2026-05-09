namespace Aom.App.Services.Videos;

public sealed record Il2VideoFfmpegEncodingProfile(
    string DisplayName,
    string VideoCodec,
    string[] VideoCodecArguments)
{
    public static Il2VideoFfmpegEncodingProfile CpuH264 { get; } = new(
        "CPU H.264",
        "libx264",
        [
            "-preset",
            "veryfast",
            "-crf",
            "18",
        ]);

    public static Il2VideoFfmpegEncodingProfile NvidiaNvencH264 { get; } = new(
        "NVIDIA NVENC H.264",
        "h264_nvenc",
        [
            "-preset",
            "p5",
            "-rc",
            "vbr",
            "-cq",
            "19",
            "-b:v",
            "0",
        ]);
}