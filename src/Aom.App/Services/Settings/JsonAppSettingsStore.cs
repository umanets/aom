using System.IO;
using System.Text.Json;

namespace Aom.App.Services.Settings;

public sealed class JsonAppSettingsStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
    };

    private readonly string settingsPath;

    public JsonAppSettingsStore(string? settingsPath = null)
    {
        this.settingsPath = settingsPath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Aom.Desktop",
            "settings.json");
    }

    public AppSettingsDocument Load()
    {
        try
        {
            if (!File.Exists(settingsPath))
            {
                return new AppSettingsDocument();
            }

            var json = File.ReadAllText(settingsPath);
            return JsonSerializer.Deserialize<AppSettingsDocument>(json, SerializerOptions) ?? new AppSettingsDocument();
        }
        catch
        {
            return new AppSettingsDocument();
        }
    }

    public void Save(AppSettingsDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var directory = Path.GetDirectoryName(settingsPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(document, SerializerOptions);
        File.WriteAllText(settingsPath, json);
    }
}