using System.Diagnostics;
using System.IO;

namespace Aom.App.Services.Videos;

public sealed class Il2VideoEditExportService
{
    private readonly FfmpegExecutableResolver ffmpegExecutableResolver;
    private readonly Il2VideoFfmpegEncodingProfileResolver encodingProfileResolver;
    private readonly Il2VideoEditRenderPlanBuilder renderPlanBuilder;
    private readonly Il2VideoFfmpegCommandBuilder commandBuilder;
    private readonly Il2VideoFreezeFrameRenderer freezeFrameRenderer;

    public Il2VideoEditExportService(
        Il2RawVideoLibraryPaths? paths = null,
        FfmpegExecutableResolver? ffmpegExecutableResolver = null,
        Il2VideoFfmpegEncodingProfileResolver? encodingProfileResolver = null,
        Il2VideoEditRenderPlanBuilder? renderPlanBuilder = null,
        Il2VideoFfmpegCommandBuilder? commandBuilder = null,
        Il2VideoFreezeFrameRenderer? freezeFrameRenderer = null)
    {
        Paths = paths ?? new Il2RawVideoLibraryPaths();
        this.ffmpegExecutableResolver = ffmpegExecutableResolver ?? new FfmpegExecutableResolver();
        this.encodingProfileResolver = encodingProfileResolver ?? new Il2VideoFfmpegEncodingProfileResolver(this.ffmpegExecutableResolver);
        this.renderPlanBuilder = renderPlanBuilder ?? new Il2VideoEditRenderPlanBuilder();
        this.commandBuilder = commandBuilder ?? new Il2VideoFfmpegCommandBuilder();
        this.freezeFrameRenderer = freezeFrameRenderer ?? new Il2VideoFreezeFrameRenderer();
    }

    public Il2RawVideoLibraryPaths Paths { get; }

    public async Task<string> ExportAsync(
        Il2VideoEditProject project,
        TimeSpan sourceDuration,
        IProgress<Il2VideoEditExportProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(project);

        ReportProgress(progress, 0.0, "Preparing export", "Resolving FFmpeg and building the render plan.");
        Paths.EnsureDirectories();
        var ffmpegPath = ffmpegExecutableResolver.Resolve();
        var encodingProfile = await encodingProfileResolver.ResolveAsync(cancellationToken);
        var renderPlan = renderPlanBuilder.Build(project, sourceDuration);
        var totalWorkUnits = 2 + renderPlan.Segments.Sum(GetWorkUnits);
        var completedWorkUnits = 1;
        ReportProgress(
            progress,
            completedWorkUnits,
            totalWorkUnits,
            "Preparing segments",
            $"Planned {renderPlan.Segments.Length} segment(s) for export using {encodingProfile.DisplayName}.");

        var exportSuffix = $"edited-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}";
        var outputPath = Paths.GetExportPath(project.VideoId, exportSuffix);
        var workingDirectory = Path.Combine(Paths.ExportsDirectoryPath, ".tmp", project.VideoId, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workingDirectory);

        try
        {
            var segmentPaths = new List<string>();

            for (var index = 0; index < renderPlan.Segments.Length; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var segmentPath = Path.Combine(workingDirectory, $"segment-{index:000}.mp4");
                switch (renderPlan.Segments[index])
                {
                    case Il2VideoSourceRenderSegment sourceSegment:
                        ReportProgress(
                            progress,
                            completedWorkUnits,
                            totalWorkUnits,
                            "Encoding source segment",
                            $"Encoding segment {index + 1} of {renderPlan.Segments.Length}.");
                        await RunCommandAsync(
                            ffmpegPath,
                            workingDirectory,
                            commandBuilder.BuildSourceSegmentCommand(project.SourceVideoPath, sourceSegment, segmentPath, encodingProfile),
                            cancellationToken);
                        completedWorkUnits++;
                        break;

                    case Il2VideoFreezeRenderSegment freezeSegment:
                        var sourceFramePath = Path.Combine(workingDirectory, $"freeze-{index:000}-source.png");
                        var renderedStillPath = Path.Combine(workingDirectory, $"freeze-{index:000}-rendered.png");
                        ReportProgress(
                            progress,
                            completedWorkUnits,
                            totalWorkUnits,
                            "Extracting freeze frame",
                            $"Extracting source frame for freeze segment {index + 1} of {renderPlan.Segments.Length}.");
                        await RunCommandAsync(
                            ffmpegPath,
                            workingDirectory,
                            commandBuilder.BuildExtractFrameCommand(project.SourceVideoPath, freezeSegment.SourceTimestamp, sourceFramePath),
                            cancellationToken);
                        completedWorkUnits++;

                        ReportProgress(
                            progress,
                            completedWorkUnits,
                            totalWorkUnits,
                            "Rendering annotations",
                            $"Rendering annotations for freeze segment {index + 1} of {renderPlan.Segments.Length}.");
                        freezeFrameRenderer.RenderAnnotatedStill(sourceFramePath, freezeSegment.FreezeAnnotation, renderedStillPath);
                        completedWorkUnits++;

                        ReportProgress(
                            progress,
                            completedWorkUnits,
                            totalWorkUnits,
                            "Encoding freeze segment",
                            $"Encoding freeze segment {index + 1} of {renderPlan.Segments.Length}.");
                        await RunCommandAsync(
                            ffmpegPath,
                            workingDirectory,
                            commandBuilder.BuildFreezeSegmentCommand(renderedStillPath, freezeSegment.HoldDuration, segmentPath, encodingProfile),
                            cancellationToken);
                        completedWorkUnits++;
                        break;

                    default:
                        throw new InvalidOperationException($"Unsupported render segment type: {renderPlan.Segments[index].GetType().Name}");
                }

                segmentPaths.Add(segmentPath);
            }

            var concatListPath = Path.Combine(workingDirectory, "concat.txt");
            ReportProgress(progress, completedWorkUnits, totalWorkUnits, "Preparing final output", "Writing concat list for the final MP4.");
            await File.WriteAllTextAsync(concatListPath, commandBuilder.BuildConcatListContent(segmentPaths), cancellationToken);
            completedWorkUnits++;

            ReportProgress(progress, completedWorkUnits, totalWorkUnits, "Concatenating output", "Joining rendered segments into the edited MP4.");
            await RunCommandAsync(
                ffmpegPath,
                workingDirectory,
                commandBuilder.BuildConcatCommand(concatListPath, outputPath),
                cancellationToken);
            completedWorkUnits++;

            TryDeleteDirectory(workingDirectory);
            ReportProgress(progress, 1.0, "Export complete", $"Saved edited MP4 to {outputPath}");
            return outputPath;
        }
        catch
        {
            throw;
        }
    }

    private static int GetWorkUnits(Il2VideoEditRenderSegment segment)
    {
        return segment switch
        {
            Il2VideoSourceRenderSegment => 1,
            Il2VideoFreezeRenderSegment => 3,
            _ => throw new InvalidOperationException($"Unsupported render segment type: {segment.GetType().Name}"),
        };
    }

    private static void ReportProgress(
        IProgress<Il2VideoEditExportProgress>? progress,
        int completedWorkUnits,
        int totalWorkUnits,
        string stage,
        string detail)
    {
        if (progress is null)
        {
            return;
        }

        var completion = totalWorkUnits <= 0
            ? 0.0
            : Math.Clamp((double)completedWorkUnits / totalWorkUnits, 0.0, 1.0);
        progress.Report(new Il2VideoEditExportProgress(completion, stage, detail));
    }

    private static void ReportProgress(
        IProgress<Il2VideoEditExportProgress>? progress,
        double completion,
        string stage,
        string detail)
    {
        progress?.Report(new Il2VideoEditExportProgress(completion, stage, detail));
    }

    private static async Task RunCommandAsync(
        string ffmpegPath,
        string workingDirectory,
        Il2VideoFfmpegCommand command,
        CancellationToken cancellationToken)
    {
        using var process = new Process
        {
            StartInfo = command.CreateStartInfo(ffmpegPath, workingDirectory),
        };

        process.Start();
        var standardErrorTask = process.StandardError.ReadToEndAsync(cancellationToken);
        var standardOutputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        var standardError = await standardErrorTask;
        var standardOutput = await standardOutputTask;

        if (process.ExitCode == 0)
        {
            return;
        }

        var details = string.IsNullOrWhiteSpace(standardError)
            ? standardOutput
            : standardError;
        throw new InvalidOperationException($"FFmpeg export step failed with exit code {process.ExitCode}.{Environment.NewLine}{details}".Trim());
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch
        {
        }
    }
}