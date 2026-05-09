using System.IO;

namespace Aom.App.Services.Videos;

public sealed class Il2RawVideoLibraryPaths
{
    public Il2RawVideoLibraryPaths(string? libraryRootPath = null)
    {
        var resolvedRootPath = string.IsNullOrWhiteSpace(libraryRootPath)
            ? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Aom.Desktop",
                "VideosRaw")
            : Path.GetFullPath(libraryRootPath.Trim());

        LibraryRootPath = resolvedRootPath;

        CatalogPath = Path.Combine(LibraryRootPath, "library.json");
        VideosDirectoryPath = Path.Combine(LibraryRootPath, "videos");
        ThumbnailsDirectoryPath = Path.Combine(LibraryRootPath, "thumbnails");
        EditsDirectoryPath = Path.Combine(LibraryRootPath, "edits");
        ProjectsDirectoryPath = Path.Combine(EditsDirectoryPath, "projects");
        ExportsDirectoryPath = Path.Combine(LibraryRootPath, "exports");
    }

    public string LibraryRootPath { get; }

    public string CatalogPath { get; }

    public string VideosDirectoryPath { get; }

    public string ThumbnailsDirectoryPath { get; }

    public string EditsDirectoryPath { get; }

    public string ProjectsDirectoryPath { get; }

    public string ExportsDirectoryPath { get; }

    public string GetVideoPath(string videoId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(videoId);
        return Path.Combine(VideosDirectoryPath, $"{videoId}.mp4");
    }

    public string GetTemporaryVideoPath(string videoId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(videoId);
        return Path.Combine(VideosDirectoryPath, $"{videoId}.partial.mp4");
    }

    public string GetThumbnailPath(string videoId, string extension)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(videoId);
        ArgumentException.ThrowIfNullOrWhiteSpace(extension);

        var normalizedExtension = extension.StartsWith(".", StringComparison.Ordinal)
            ? extension
            : $".{extension}";

        return Path.Combine(ThumbnailsDirectoryPath, $"{videoId}{normalizedExtension}");
    }

    public string GetProjectPath(string videoId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(videoId);
        return Path.Combine(ProjectsDirectoryPath, $"{videoId}.editor.json");
    }

    public string GetExportPath(string videoId, string suffix)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(videoId);
        ArgumentException.ThrowIfNullOrWhiteSpace(suffix);
        return Path.Combine(ExportsDirectoryPath, $"{videoId}-{suffix}.mp4");
    }

    public void EnsureDirectories()
    {
        Directory.CreateDirectory(LibraryRootPath);
        Directory.CreateDirectory(VideosDirectoryPath);
        Directory.CreateDirectory(ThumbnailsDirectoryPath);
        Directory.CreateDirectory(EditsDirectoryPath);
        Directory.CreateDirectory(ProjectsDirectoryPath);
        Directory.CreateDirectory(ExportsDirectoryPath);
    }
}