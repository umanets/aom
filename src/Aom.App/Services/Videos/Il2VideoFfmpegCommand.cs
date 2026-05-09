using System.Diagnostics;

namespace Aom.App.Services.Videos;

public sealed record Il2VideoFfmpegCommand(IReadOnlyList<string> Arguments)
{
    public ProcessStartInfo CreateStartInfo(string ffmpegPath, string workingDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ffmpegPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(workingDirectory);

        var startInfo = new ProcessStartInfo(ffmpegPath)
        {
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };

        foreach (var argument in Arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        return startInfo;
    }
}