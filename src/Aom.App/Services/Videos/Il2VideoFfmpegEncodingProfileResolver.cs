using System.Diagnostics;

namespace Aom.App.Services.Videos;

public sealed class Il2VideoFfmpegEncodingProfileResolver
{
    private readonly FfmpegExecutableResolver ffmpegExecutableResolver;

    public Il2VideoFfmpegEncodingProfileResolver(FfmpegExecutableResolver? ffmpegExecutableResolver = null)
    {
        this.ffmpegExecutableResolver = ffmpegExecutableResolver ?? new FfmpegExecutableResolver();
    }

    public async Task<Il2VideoFfmpegEncodingProfile> ResolveAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var ffmpegPath = ffmpegExecutableResolver.Resolve();
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo(ffmpegPath)
                {
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                },
            };
            process.StartInfo.ArgumentList.Add("-hide_banner");
            process.StartInfo.ArgumentList.Add("-encoders");

            process.Start();
            var standardOutputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var standardErrorTask = process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);
            var output = await standardOutputTask;
            var error = await standardErrorTask;

            if (process.ExitCode != 0)
            {
                return Il2VideoFfmpegEncodingProfile.CpuH264;
            }

            return ResolveFromEncoderList(string.Join(Environment.NewLine, output, error));
        }
        catch
        {
            return Il2VideoFfmpegEncodingProfile.CpuH264;
        }
    }

    public Il2VideoFfmpegEncodingProfile ResolveFromEncoderList(string encoderList)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(encoderList);

        return encoderList.Contains("h264_nvenc", StringComparison.OrdinalIgnoreCase)
            ? Il2VideoFfmpegEncodingProfile.NvidiaNvencH264
            : Il2VideoFfmpegEncodingProfile.CpuH264;
    }
}