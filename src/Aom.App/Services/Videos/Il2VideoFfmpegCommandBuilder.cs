using System.Globalization;

namespace Aom.App.Services.Videos;

public sealed class Il2VideoFfmpegCommandBuilder
{
    private const string AudioCodec = "aac";
    private const string AudioBitrate = "192k";
    private const string VideoFrameRate = "30";
    private const string AudioFormatFilter = "aformat=sample_rates=48000:channel_layouts=stereo";

    public Il2VideoFfmpegCommand BuildExtractFrameCommand(string sourceVideoPath, TimeSpan timestamp, string outputImagePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceVideoPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputImagePath);

        return new Il2VideoFfmpegCommand(
        [
            "-y",
            "-ss",
            FormatTimestamp(timestamp),
            "-i",
            sourceVideoPath,
            "-frames:v",
            "1",
            outputImagePath,
        ]);
    }

    public Il2VideoFfmpegCommand BuildSourceSegmentCommand(
        string sourceVideoPath,
        Il2VideoSourceRenderSegment segment,
        string outputVideoPath,
        Il2VideoFfmpegEncodingProfile? encodingProfile = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceVideoPath);
        ArgumentNullException.ThrowIfNull(segment);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputVideoPath);

        var resolvedEncodingProfile = encodingProfile ?? Il2VideoFfmpegEncodingProfile.CpuH264;

        var videoFilter = $"trim=start={FormatTimestamp(segment.SourceStart)}:end={FormatTimestamp(segment.SourceEnd)},setpts=(PTS-STARTPTS)/{FormatSpeed(segment.SpeedFactor)},fps={VideoFrameRate},format=yuv420p";
        var audioFilters = new List<string>
        {
            $"atrim=start={FormatTimestamp(segment.SourceStart)}:end={FormatTimestamp(segment.SourceEnd)}",
            "asetpts=PTS-STARTPTS",
        };
        audioFilters.AddRange(BuildAtempoFilters(segment.SpeedFactor));
        audioFilters.Add(AudioFormatFilter);

        var filterComplex = $"[0:v]{videoFilter}[v];[0:a]{string.Join(",", audioFilters)}[a]";

        return new Il2VideoFfmpegCommand(
        [
            "-y",
            "-i",
            sourceVideoPath,
            "-filter_complex",
            filterComplex,
            "-map",
            "[v]",
            "-map",
            "[a]",
            "-c:v",
            resolvedEncodingProfile.VideoCodec,
            .. resolvedEncodingProfile.VideoCodecArguments,
            "-c:a",
            AudioCodec,
            "-b:a",
            AudioBitrate,
            outputVideoPath,
        ]);
    }

    public Il2VideoFfmpegCommand BuildFreezeSegmentCommand(
        string stillImagePath,
        TimeSpan holdDuration,
        string outputVideoPath,
        Il2VideoFfmpegEncodingProfile? encodingProfile = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stillImagePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputVideoPath);

        var resolvedEncodingProfile = encodingProfile ?? Il2VideoFfmpegEncodingProfile.CpuH264;

        return new Il2VideoFfmpegCommand(
        [
            "-y",
            "-loop",
            "1",
            "-framerate",
            VideoFrameRate,
            "-i",
            stillImagePath,
            "-f",
            "lavfi",
            "-i",
            "anullsrc=channel_layout=stereo:sample_rate=48000",
            "-t",
            FormatTimestamp(holdDuration),
            "-shortest",
            "-vf",
            "format=yuv420p",
            "-c:v",
            resolvedEncodingProfile.VideoCodec,
            .. resolvedEncodingProfile.VideoCodecArguments,
            "-c:a",
            AudioCodec,
            "-b:a",
            AudioBitrate,
            outputVideoPath,
        ]);
    }

    public Il2VideoFfmpegCommand BuildConcatCommand(string concatListPath, string outputVideoPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(concatListPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputVideoPath);

        return new Il2VideoFfmpegCommand(
        [
            "-y",
            "-f",
            "concat",
            "-safe",
            "0",
            "-i",
            concatListPath,
            "-c",
            "copy",
            "-movflags",
            "+faststart",
            outputVideoPath,
        ]);
    }

    public string BuildConcatListContent(IEnumerable<string> segmentPaths)
    {
        ArgumentNullException.ThrowIfNull(segmentPaths);

        return string.Join(
            Environment.NewLine,
            segmentPaths.Select(path => $"file '{path.Replace("'", "'\\''", StringComparison.Ordinal)}'"));
    }

    private static IEnumerable<string> BuildAtempoFilters(double speedFactor)
    {
        if (Math.Abs(speedFactor - 1.0) < 0.0001)
        {
            yield break;
        }

        var remaining = speedFactor;
        while (remaining < 0.5)
        {
            yield return "atempo=0.5";
            remaining /= 0.5;
        }

        while (remaining > 2.0)
        {
            yield return "atempo=2.0";
            remaining /= 2.0;
        }

        yield return $"atempo={FormatSpeed(remaining)}";
    }

    private static string FormatTimestamp(TimeSpan timestamp)
    {
        return timestamp.TotalSeconds.ToString("0.###", CultureInfo.InvariantCulture);
    }

    private static string FormatSpeed(double speedFactor)
    {
        return speedFactor.ToString("0.###", CultureInfo.InvariantCulture);
    }
}