using System.IO;
using System.Text.Json;

namespace Aom.App.Services.Videos;

public sealed class PersistedIl2RawVideoCatalog
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
    };

    public PersistedIl2RawVideoCatalog(Il2RawVideoLibraryPaths? paths = null)
    {
        Paths = paths ?? new Il2RawVideoLibraryPaths();
    }

    public Il2RawVideoLibraryPaths Paths { get; }

    public string LibraryRootPath => Paths.LibraryRootPath;

    public IReadOnlyList<Il2RawVideoRecord> Load()
    {
        try
        {
            if (!File.Exists(Paths.CatalogPath))
            {
                Paths.EnsureDirectories();
                return Array.Empty<Il2RawVideoRecord>();
            }

            var json = File.ReadAllText(Paths.CatalogPath);
            var documents = JsonSerializer.Deserialize<List<SavedIl2RawVideoDocument>>(json, SerializerOptions) ?? new List<SavedIl2RawVideoDocument>();
            var entries = new List<Il2RawVideoRecord>(documents.Count);
            var mutated = false;

            foreach (var document in documents)
            {
                var record = document.ToRecord();
                if (record is null)
                {
                    mutated = true;
                    continue;
                }

                if (!File.Exists(record.LocalVideoPath))
                {
                    mutated = true;
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(record.ThumbnailPath) && !File.Exists(record.ThumbnailPath))
                {
                    record = record with { ThumbnailPath = null };
                    mutated = true;
                }

                entries.Add(record);
            }

            var orderedEntries = entries
                .OrderByDescending(record => record.DownloadedAtUtc)
                .ToArray();

            if (mutated)
            {
                Save(orderedEntries);
            }

            return orderedEntries;
        }
        catch
        {
            return Array.Empty<Il2RawVideoRecord>();
        }
    }

    public void Save(IEnumerable<Il2RawVideoRecord> records)
    {
        ArgumentNullException.ThrowIfNull(records);

        Paths.EnsureDirectories();

        var documents = records
            .OrderByDescending(record => record.DownloadedAtUtc)
            .Select(SavedIl2RawVideoDocument.FromRecord)
            .ToArray();

        var json = JsonSerializer.Serialize(documents, SerializerOptions);
        var tempPath = Paths.CatalogPath + ".tmp";
        File.WriteAllText(tempPath, json);
        File.Move(tempPath, Paths.CatalogPath, overwrite: true);
    }
}