using Aom.App.Services.Videos;
using Xunit;

namespace Aom.App.Tests;

public sealed class Il2VideoFfmpegEncodingProfileResolverTests
{
    private readonly Il2VideoFfmpegEncodingProfileResolver resolver = new();

    [Fact]
    public void ResolveFromEncoderList_PrefersNvencWhenAvailable()
    {
        var profile = resolver.ResolveFromEncoderList(
            " V..... h264_nvenc           NVIDIA NVENC H.264 encoder\n V..... libx264               libx264 H.264 / AVC / MPEG-4 AVC / MPEG-4 part 10");

        Assert.Equal(Il2VideoFfmpegEncodingProfile.NvidiaNvencH264, profile);
    }

    [Fact]
    public void ResolveFromEncoderList_FallsBackToCpuWhenNvencIsMissing()
    {
        var profile = resolver.ResolveFromEncoderList(
            " V..... libx264               libx264 H.264 / AVC / MPEG-4 AVC / MPEG-4 part 10");

        Assert.Equal(Il2VideoFfmpegEncodingProfile.CpuH264, profile);
    }
}