using System.Text;
using Aom.App.Services.Ai;
using Xunit;

namespace Aom.App.Tests;

public sealed class OpenAiApiKeyResolverTests
{
    [Fact]
    public void Resolve_PrefersProcessEnvironmentVariable()
    {
        var temporaryDirectory = CreateTemporaryDirectory();
        File.WriteAllText(
            Path.Combine(temporaryDirectory, ".env"),
            "OPENAI_API_KEY=dotenv-value",
            Encoding.UTF8);

        var originalValue = Environment.GetEnvironmentVariable(OpenAiApiKeyResolver.EnvironmentVariableName);
        try
        {
            Environment.SetEnvironmentVariable(OpenAiApiKeyResolver.EnvironmentVariableName, "env-value");

            var resolution = new OpenAiApiKeyResolver(temporaryDirectory).Resolve();

            Assert.True(resolution.IsConfigured);
            Assert.Equal("env-value", resolution.ApiKey);
            Assert.Contains("process environment", resolution.SourceDescription, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Environment.SetEnvironmentVariable(OpenAiApiKeyResolver.EnvironmentVariableName, originalValue);
            Directory.Delete(temporaryDirectory, recursive: true);
        }
    }

    [Fact]
    public void Resolve_ReadsQuotedValueFromNearestDotEnvAncestor()
    {
        var rootDirectory = CreateTemporaryDirectory();
        var nestedDirectory = Path.Combine(rootDirectory, "nested", "bin");
        Directory.CreateDirectory(nestedDirectory);
        File.WriteAllText(
            Path.Combine(rootDirectory, ".env"),
            "export OPENAI_API_KEY=\"quoted-dotenv-value\"",
            Encoding.UTF8);

        var originalValue = Environment.GetEnvironmentVariable(OpenAiApiKeyResolver.EnvironmentVariableName);
        try
        {
            Environment.SetEnvironmentVariable(OpenAiApiKeyResolver.EnvironmentVariableName, null);

            var resolution = new OpenAiApiKeyResolver(nestedDirectory).Resolve();

            Assert.True(resolution.IsConfigured);
            Assert.Equal("quoted-dotenv-value", resolution.ApiKey);
            Assert.Contains(".env", resolution.SourceDescription, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Environment.SetEnvironmentVariable(OpenAiApiKeyResolver.EnvironmentVariableName, originalValue);
            Directory.Delete(rootDirectory, recursive: true);
        }
    }

    [Fact]
    public void Resolve_ReturnsMissingWhenApiKeyIsUnavailable()
    {
        var temporaryDirectory = CreateTemporaryDirectory();
        var originalValue = Environment.GetEnvironmentVariable(OpenAiApiKeyResolver.EnvironmentVariableName);
        var originalCurrentDirectory = Directory.GetCurrentDirectory();
        var hiddenDotEnvFiles = HideAncestorDotEnvFiles();
        try
        {
            Environment.SetEnvironmentVariable(OpenAiApiKeyResolver.EnvironmentVariableName, " ");
            Directory.SetCurrentDirectory(temporaryDirectory);

            var resolution = new OpenAiApiKeyResolver(temporaryDirectory).Resolve();

            Assert.False(resolution.IsConfigured);
            Assert.Null(resolution.ApiKey);
            Assert.Contains("not found", resolution.SourceDescription, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.SetCurrentDirectory(originalCurrentDirectory);
            Environment.SetEnvironmentVariable(OpenAiApiKeyResolver.EnvironmentVariableName, originalValue);
            RestoreHiddenDotEnvFiles(hiddenDotEnvFiles);
            Directory.Delete(temporaryDirectory, recursive: true);
        }
    }

    private static string CreateTemporaryDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "aom-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static List<(string OriginalPath, string HiddenPath)> HideAncestorDotEnvFiles()
    {
        var hiddenFiles = new List<(string OriginalPath, string HiddenPath)>();
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            var dotEnvPath = Path.Combine(current.FullName, ".env");
            if (File.Exists(dotEnvPath))
            {
                var hiddenPath = dotEnvPath + ".test-hidden";
                File.Move(dotEnvPath, hiddenPath, overwrite: true);
                hiddenFiles.Add((dotEnvPath, hiddenPath));
            }

            current = current.Parent;
        }

        return hiddenFiles;
    }

    private static void RestoreHiddenDotEnvFiles(IEnumerable<(string OriginalPath, string HiddenPath)> hiddenFiles)
    {
        foreach (var (originalPath, hiddenPath) in hiddenFiles.Reverse())
        {
            if (File.Exists(hiddenPath))
            {
                File.Move(hiddenPath, originalPath, overwrite: true);
            }
        }
    }
}