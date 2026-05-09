using System.IO;

namespace Aom.App.Services.Videos;

public sealed class FfmpegExecutableResolver
{
    private const string ExecutableName = "ffmpeg.exe";

    public string Resolve()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var candidatePath in GetCandidatePaths())
        {
            if (!seen.Add(candidatePath))
            {
                continue;
            }

            if (File.Exists(candidatePath))
            {
                return candidatePath;
            }
        }

        throw new FileNotFoundException(
            "FFmpeg was not found. Install FFmpeg and ensure ffmpeg.exe is on PATH, or set FFMPEG_PATH.");
    }

    private static IEnumerable<string> GetCandidatePaths()
    {
        var configuredPath = Environment.GetEnvironmentVariable("FFMPEG_PATH");
        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            if (Directory.Exists(configuredPath))
            {
                yield return Path.Combine(configuredPath, ExecutableName);
            }
            else
            {
                yield return configuredPath;
            }
        }

        yield return Path.Combine(AppContext.BaseDirectory, ExecutableName);

        var pathValue = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(pathValue))
        {
            yield break;
        }

        foreach (var segment in pathValue.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            yield return Path.Combine(segment, ExecutableName);
        }
    }
}