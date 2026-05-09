using Aom.App.Services.Videos;
using Xunit;

namespace Aom.App.Tests;

public sealed class Il2VideoFfmpegCommandBuilderTests
{
    private readonly Il2VideoFfmpegCommandBuilder builder = new();

    [Fact]
    public void BuildSourceSegmentCommand_UsesTrimSetPtsAndAtempoChain()
    {
        var command = builder.BuildSourceSegmentCommand(
            @"D:\work\aom\video.mp4",
            new Il2VideoSourceRenderSegment(TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(20), 0.25, "PitchCorrected"),
            @"D:\work\aom\segment.mp4");

        var filterComplex = command.Arguments[4];
        Assert.Contains("trim=start=10:end=20,setpts=(PTS-STARTPTS)/0.25,fps=30,format=yuv420p", filterComplex, StringComparison.Ordinal);
        Assert.Contains("atrim=start=10:end=20,asetpts=PTS-STARTPTS,atempo=0.5,atempo=0.5,aformat=sample_rates=48000:channel_layouts=stereo", filterComplex, StringComparison.Ordinal);

        Assert.Collection(
            command.Arguments,
            item => Assert.Equal("-y", item),
            item => Assert.Equal("-i", item),
            item => Assert.Equal(@"D:\work\aom\video.mp4", item),
            item => Assert.Equal("-filter_complex", item),
            item => Assert.Equal(filterComplex, item),
            item => Assert.Equal("-map", item),
            item => Assert.Equal("[v]", item),
            item => Assert.Equal("-map", item),
            item => Assert.Equal("[a]", item),
            item => Assert.Equal("-c:v", item),
            item => Assert.Equal("libx264", item),
            item => Assert.Equal("-preset", item),
            item => Assert.Equal("veryfast", item),
            item => Assert.Equal("-crf", item),
            item => Assert.Equal("18", item),
            item => Assert.Equal("-c:a", item),
            item => Assert.Equal("aac", item),
            item => Assert.Equal("-b:a", item),
            item => Assert.Equal("192k", item),
            item => Assert.Equal(@"D:\work\aom\segment.mp4", item));
    }

    [Fact]
    public void BuildFreezeSegmentCommand_CreatesStillLoopWithSilentAudio()
    {
        var command = builder.BuildFreezeSegmentCommand(@"D:\work\aom\freeze.png", TimeSpan.FromSeconds(3), @"D:\work\aom\freeze.mp4");

        Assert.Contains("anullsrc=channel_layout=stereo:sample_rate=48000", command.Arguments);
        Assert.Contains("-loop", command.Arguments);
        Assert.Contains("1", command.Arguments);
        Assert.Contains("-t", command.Arguments);
        Assert.Contains("3", command.Arguments);
    }

    [Fact]
    public void BuildSourceSegmentCommand_UsesNvencWhenRequested()
    {
        var command = builder.BuildSourceSegmentCommand(
            @"D:\work\aom\video.mp4",
            new Il2VideoSourceRenderSegment(TimeSpan.FromSeconds(0), TimeSpan.FromSeconds(10), 1.0, "Passthrough"),
            @"D:\work\aom\segment.mp4",
            Il2VideoFfmpegEncodingProfile.NvidiaNvencH264);

        Assert.Contains("-c:v", command.Arguments);
        Assert.Contains("h264_nvenc", command.Arguments);
        Assert.Contains("-preset", command.Arguments);
        Assert.Contains("p5", command.Arguments);
        Assert.Contains("-cq", command.Arguments);
        Assert.Contains("19", command.Arguments);
    }

    [Fact]
    public void BuildConcatListContent_QuotesSegmentPaths()
    {
        var content = builder.BuildConcatListContent(
        [
            @"D:\work\aom\segment-000.mp4",
            @"D:\work\aom\segment-001.mp4",
        ]);

        Assert.Equal(
            "file 'D:\\work\\aom\\segment-000.mp4'" + Environment.NewLine + "file 'D:\\work\\aom\\segment-001.mp4'",
            content);
    }
}