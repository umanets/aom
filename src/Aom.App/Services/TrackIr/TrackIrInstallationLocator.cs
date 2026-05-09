using System.IO;
using Microsoft.Win32;

namespace Aom.App.Services.TrackIr;

public sealed class TrackIrInstallationLocator
{
    private const string RegistryKey = @"Software\NaturalPoint\NATURALPOINT\NPClient Location";
    private const string PathValueName = "Path";
    private const string FreePieRealPathValueName = "Freepie_RealPath";

    public TrackIrInstallation? Locate()
    {
        var candidates = GetCandidatePaths()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(File.Exists)
            .ToArray();

        if (candidates.Length == 0)
        {
            return null;
        }

        var preferred = candidates
            .OrderByDescending(path => ContainsTrackIrHint(path))
            .ThenBy(path => path, StringComparer.OrdinalIgnoreCase)
            .First();

        var source = ResolveSource(preferred);
        return new TrackIrInstallation(preferred, source, candidates);
    }

    public IReadOnlyList<string> GetCandidatePaths()
    {
        var paths = new List<string>();

        AddRegistryCandidate(paths, FreePieRealPathValueName);
        AddRegistryCandidate(paths, PathValueName);

        paths.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "TrackIR5", "NPClient.dll"));
        paths.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "TrackIR5", "NPClient.dll"));
        paths.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "NaturalPoint", "TrackIR5", "NPClient.dll"));
        paths.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "NaturalPoint", "TrackIR5", "NPClient.dll"));

        return paths
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(Path.GetFullPath)
            .ToArray();
    }

    private static void AddRegistryCandidate(List<string> paths, string valueName)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RegistryKey);
        var value = key?.GetValue(valueName) as string;
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        if (value.EndsWith("NPClient.dll", StringComparison.OrdinalIgnoreCase))
        {
            paths.Add(value);
            return;
        }

        paths.Add(Path.Combine(value, "NPClient.dll"));
    }

    private static bool ContainsTrackIrHint(string path) =>
        path.Contains("trackir", StringComparison.OrdinalIgnoreCase) ||
        path.Contains("naturalpoint", StringComparison.OrdinalIgnoreCase);

    private static string ResolveSource(string dllPath)
    {
        if (dllPath.Contains("Freepie_RealPath", StringComparison.OrdinalIgnoreCase))
        {
            return "registry freepie real path";
        }

        if (ContainsTrackIrHint(dllPath))
        {
            return "trackir installation";
        }

        return "registry or fallback path";
    }
}