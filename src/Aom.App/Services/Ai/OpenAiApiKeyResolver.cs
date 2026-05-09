using System.IO;
using System.Text;

namespace Aom.App.Services.Ai;

public sealed class OpenAiApiKeyResolver
{
    public const string EnvironmentVariableName = "OPENAI_API_KEY";

    private readonly string? searchStartDirectory;

    public OpenAiApiKeyResolver(string? searchStartDirectory = null)
    {
        this.searchStartDirectory = searchStartDirectory;
    }

    public OpenAiApiKeyResolution Resolve()
    {
        var environmentValue = Environment.GetEnvironmentVariable(EnvironmentVariableName);
        if (!string.IsNullOrWhiteSpace(environmentValue))
        {
            return new OpenAiApiKeyResolution(environmentValue.Trim(), $"{EnvironmentVariableName} loaded from process environment.");
        }

        var dotEnvPath = FindDotEnvPath();
        if (dotEnvPath is null)
        {
            return new OpenAiApiKeyResolution(null, $"{EnvironmentVariableName} was not found in the process environment or a nearby .env file.");
        }

        var dotEnvValue = TryReadFromDotEnv(dotEnvPath);
        if (!string.IsNullOrWhiteSpace(dotEnvValue))
        {
            return new OpenAiApiKeyResolution(dotEnvValue, $"{EnvironmentVariableName} loaded from {dotEnvPath}.");
        }

        return new OpenAiApiKeyResolution(null, $"{EnvironmentVariableName} was not found in {dotEnvPath}.");
    }

    private string? FindDotEnvPath()
    {
        var candidates = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var directory in EnumerateCandidateDirectories())
        {
            var current = new DirectoryInfo(directory);
            while (current is not null)
            {
                if (candidates.Add(current.FullName))
                {
                    var dotEnvPath = Path.Combine(current.FullName, ".env");
                    if (File.Exists(dotEnvPath))
                    {
                        return dotEnvPath;
                    }
                }

                current = current.Parent;
            }
        }

        return null;
    }

    private IEnumerable<string> EnumerateCandidateDirectories()
    {
        if (!string.IsNullOrWhiteSpace(searchStartDirectory))
        {
            yield return Path.GetFullPath(searchStartDirectory);
        }

        yield return Directory.GetCurrentDirectory();
        yield return AppContext.BaseDirectory;
    }

    private static string? TryReadFromDotEnv(string dotEnvPath)
    {
        foreach (var rawLine in File.ReadLines(dotEnvPath, Encoding.UTF8))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
            {
                continue;
            }

            if (line.StartsWith("export ", StringComparison.Ordinal))
            {
                line = line["export ".Length..].Trim();
            }

            var separatorIndex = line.IndexOf('=');
            if (separatorIndex <= 0)
            {
                continue;
            }

            var key = line[..separatorIndex].Trim();
            if (!key.Equals(EnvironmentVariableName, StringComparison.Ordinal))
            {
                continue;
            }

            var value = line[(separatorIndex + 1)..].Trim();
            if (value.Length >= 2)
            {
                var first = value[0];
                var last = value[^1];
                if ((first == '"' && last == '"') || (first == '\'' && last == '\''))
                {
                    value = value[1..^1];
                }
            }

            return string.IsNullOrWhiteSpace(value)
                ? null
                : value;
        }

        return null;
    }
}