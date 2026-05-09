using System.IO;

namespace Aom.App.Services.Ai;

public sealed class AiPartnerMapReferenceCatalog
{
    private readonly object sync = new();
    private IReadOnlyList<AiPartnerReferenceImage>? cachedReferenceMaps;

    public IReadOnlyList<AiPartnerReferenceImage> GetReferenceMaps()
    {
        if (cachedReferenceMaps is not null)
        {
            return cachedReferenceMaps;
        }

        lock (sync)
        {
            cachedReferenceMaps ??= LoadReferenceMaps();
            return cachedReferenceMaps;
        }
    }

    private static IReadOnlyList<AiPartnerReferenceImage> LoadReferenceMaps()
    {
        var mapsDirectoryPath = Path.Combine(AppContext.BaseDirectory, "Maps");
        if (!Directory.Exists(mapsDirectoryPath))
        {
            return Array.Empty<AiPartnerReferenceImage>();
        }

        return Directory.EnumerateFiles(mapsDirectoryPath, "*Map050.jpg", SearchOption.TopDirectoryOnly)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .Select(path => new AiPartnerReferenceImage(ParseMapLabel(path), File.ReadAllBytes(path), "image/jpeg"))
            .ToArray();
    }

    private static string ParseMapLabel(string filePath)
    {
        var fileName = Path.GetFileNameWithoutExtension(filePath);
        var markerIndex = fileName.LastIndexOf("Map", StringComparison.Ordinal);
        var rawLabel = markerIndex > 0
            ? fileName[..markerIndex]
            : fileName;

        return string.Concat(rawLabel.Select((character, index) =>
            index > 0 && char.IsDigit(character) && char.IsLetter(rawLabel[index - 1])
                ? $" {character}"
                : character.ToString()));
    }
}