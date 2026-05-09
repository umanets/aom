using System.IO;
using System.Text.Json;

namespace Aom.App.Services.Videos;

public sealed class PersistedIl2VideoEditProjectStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
    };

    public PersistedIl2VideoEditProjectStore(Il2RawVideoLibraryPaths? paths = null)
    {
        Paths = paths ?? new Il2RawVideoLibraryPaths();
    }

    public Il2RawVideoLibraryPaths Paths { get; }

    public string GetProjectPath(string videoId) => Paths.GetProjectPath(videoId);

    public Il2VideoEditProject LoadOrCreate(Il2RawVideoRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        Paths.EnsureDirectories();
        var projectPath = Paths.GetProjectPath(record.VideoId);
        if (!File.Exists(projectPath))
        {
            var createdProject = Il2VideoEditProject.Create(record);
            Save(createdProject);
            return createdProject;
        }

        try
        {
            var json = File.ReadAllText(projectPath);
            var existingProject = JsonSerializer.Deserialize<Il2VideoEditProject>(json, SerializerOptions);
            if (existingProject is null)
            {
                var createdProject = Il2VideoEditProject.Create(record);
                Save(createdProject);
                return createdProject;
            }

            if (string.Equals(existingProject.SourceVideoPath, record.LocalVideoPath, StringComparison.Ordinal)
                && string.Equals(existingProject.Title, record.Title, StringComparison.Ordinal))
            {
                return existingProject;
            }

            var synchronizedProject = existingProject with
            {
                SourceVideoPath = record.LocalVideoPath,
                Title = record.Title,
            };
            Save(synchronizedProject);
            return synchronizedProject;
        }
        catch
        {
            var createdProject = Il2VideoEditProject.Create(record);
            Save(createdProject);
            return createdProject;
        }
    }

    public void Save(Il2VideoEditProject project)
    {
        ArgumentNullException.ThrowIfNull(project);

        Paths.EnsureDirectories();

        var normalizedProject = project with
        {
            UpdatedAtUtc = DateTimeOffset.UtcNow,
        };

        var json = JsonSerializer.Serialize(normalizedProject, SerializerOptions);
        var finalPath = Paths.GetProjectPath(project.VideoId);
        var temporaryPath = finalPath + ".tmp";
        File.WriteAllText(temporaryPath, json);
        File.Move(temporaryPath, finalPath, overwrite: true);
    }
}