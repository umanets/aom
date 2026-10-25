using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Aom.App.Services.Videos;
using Microsoft.Win32;

namespace Aom.App.ViewModels;

public sealed class Il2RawVideoLibraryViewModel : INotifyPropertyChanged, IAsyncDisposable
{
    private PersistedIl2RawVideoCatalog catalog;
    private IIl2RawVideoDownloadService downloadService;
    private readonly LocalFileLauncher localFileLauncher;
    private readonly RelayCommand<object> downloadCommand;
    private readonly RelayCommand<object> chooseLibraryFolderCommand;
    private readonly Func<string, string?> chooseLibraryFolder;
    private bool ownsDownloadService;
    private List<Il2RawVideoListItemViewModel> videos = new();
    private readonly Queue<Il2RawVideoDownloadQueueItem> downloadQueue = new();
    private string pendingUrl = string.Empty;
    private string status = "Paste one or more YouTube URLs to queue raw video downloads.";
    private bool isDownloadInProgress;
    private CancellationTokenSource? downloadCancellation;
    private Task<Il2RawVideoRecord>? downloadTask;
    private Task? downloadQueueTask;
    private string? activeDownloadVideoId;

    public Il2RawVideoLibraryViewModel(
        string? libraryRootPath = null,
        PersistedIl2RawVideoCatalog? catalog = null,
        IIl2RawVideoDownloadService? downloadService = null,
        LocalFileLauncher? localFileLauncher = null,
        Func<string, string?>? chooseLibraryFolder = null)
    {
        this.catalog = catalog ?? new PersistedIl2RawVideoCatalog(new Il2RawVideoLibraryPaths(libraryRootPath));
        this.downloadService = downloadService ?? new Il2RawVideoDownloadService(this.catalog.Paths);
        this.localFileLauncher = localFileLauncher ?? new LocalFileLauncher();
        this.chooseLibraryFolder = chooseLibraryFolder ?? PickLibraryFolder;
        ownsDownloadService = downloadService is null;
        downloadCommand = new RelayCommand<object>(_ => QueuePendingUrls(), _ => CanDownload());
        chooseLibraryFolderCommand = new RelayCommand<object>(_ => ChooseLibraryFolder(), _ => CanChooseLibraryFolder());

        ReloadLibraryItems();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public event Action<Il2RawVideoRecord>? EditRequested;

    public string PendingUrl
    {
        get => pendingUrl;
        set
        {
            var normalized = value ?? string.Empty;
            if (string.Equals(pendingUrl, normalized, StringComparison.Ordinal))
            {
                return;
            }

            pendingUrl = normalized;
            OnPropertyChanged();
            downloadCommand.RaiseCanExecuteChanged();
        }
    }

    public string Status
    {
        get => status;
        private set
        {
            if (string.Equals(status, value, StringComparison.Ordinal))
            {
                return;
            }

            status = value;
            OnPropertyChanged();
        }
    }

    public bool IsDownloadInProgress
    {
        get => isDownloadInProgress;
        private set
        {
            if (isDownloadInProgress == value)
            {
                return;
            }

            isDownloadInProgress = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(DownloadButtonLabel));
            OnPropertyChanged(nameof(DownloadQueueSummary));
            downloadCommand.RaiseCanExecuteChanged();
            chooseLibraryFolderCommand.RaiseCanExecuteChanged();
        }
    }

    public string DownloadButtonLabel => IsDownloadInProgress ? "Add to Queue" : "Queue Download";

    public int QueuedDownloadCount => downloadQueue.Count;

    public string DownloadQueueSummary => IsDownloadInProgress
        ? downloadQueue.Count == 0
            ? "No queued downloads after the current video."
            : $"{downloadQueue.Count} queued after the current video."
        : downloadQueue.Count == 0
            ? "No queued downloads."
            : $"{downloadQueue.Count} queued download(s).";

    public string LibraryPath => catalog.LibraryRootPath;

    public string LibrarySummary => videos.Count == 0
        ? "No downloaded videos yet."
        : $"{videos.Count} downloaded video(s).";

    public bool HasVideos => videos.Count > 0;

    public IReadOnlyList<Il2RawVideoListItemViewModel> Videos => videos;

    public ICommand DownloadCommand => downloadCommand;

    public ICommand ChooseLibraryFolderCommand => chooseLibraryFolderCommand;

    internal Task? DownloadQueueTask => downloadQueueTask;

    public async ValueTask DisposeAsync()
    {
        if (downloadCancellation is not null)
        {
            downloadCancellation.Cancel();
        }

        if (downloadQueueTask is not null)
        {
            try
            {
                await downloadQueueTask;
            }
            catch (OperationCanceledException)
            {
            }
            catch
            {
            }
        }

        downloadCancellation?.Dispose();
        downloadCancellation = null;

        if (ownsDownloadService)
        {
            downloadService.Dispose();
        }
    }

    private bool CanDownload() => !string.IsNullOrWhiteSpace(PendingUrl);

    private bool CanChooseLibraryFolder() => !IsDownloadInProgress && downloadQueue.Count == 0;

    public bool ChangeLibraryPath(string? libraryRootPath)
    {
        if (IsDownloadInProgress || downloadQueue.Count > 0 || string.IsNullOrWhiteSpace(libraryRootPath))
        {
            return false;
        }

        var nextPaths = new Il2RawVideoLibraryPaths(libraryRootPath);
        if (string.Equals(nextPaths.LibraryRootPath, LibraryPath, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (ownsDownloadService)
        {
            downloadService.Dispose();
        }

        catalog = new PersistedIl2RawVideoCatalog(nextPaths);
        downloadService = new Il2RawVideoDownloadService(nextPaths);
        ownsDownloadService = true;
        ReloadLibraryItems();
        OnPropertyChanged(nameof(LibraryPath));
        Status = videos.Count == 0
            ? "Switched library folder. The selected library is empty. Existing downloads stay in the previous folder."
            : $"Switched library folder. Loaded {videos.Count} downloaded video(s). Existing downloads stay in the previous folder.";
        return true;
    }

    public void ChooseLibraryFolder()
    {
        if (!CanChooseLibraryFolder())
        {
            return;
        }

        var selectedPath = chooseLibraryFolder(LibraryPath);
        if (string.IsNullOrWhiteSpace(selectedPath))
        {
            return;
        }

        ChangeLibraryPath(selectedPath);
    }

    private void QueuePendingUrls()
    {
        var pendingUrls = SplitPendingUrls(PendingUrl);
        if (pendingUrls.Count == 0)
        {
            Status = "Paste a YouTube URL first.";
            return;
        }

        var invalidUrls = new List<string>();
        var queuedCount = 0;
        var skippedExistingCount = 0;
        var skippedDuplicateCount = 0;

        foreach (var pendingUrl in pendingUrls)
        {
            string normalizedVideoId;
            try
            {
                normalizedVideoId = Il2RawVideoDownloadService.NormalizeVideoId(pendingUrl);
            }
            catch (ArgumentException)
            {
                invalidUrls.Add(pendingUrl);
                continue;
            }

            if (TryFindAvailableVideo(normalizedVideoId, out _))
            {
                skippedExistingCount++;
                continue;
            }

            if (IsDownloadQueuedOrActive(normalizedVideoId))
            {
                skippedDuplicateCount++;
                continue;
            }

            downloadQueue.Enqueue(new Il2RawVideoDownloadQueueItem(pendingUrl, normalizedVideoId));
            queuedCount++;
        }

        PendingUrl = invalidUrls.Count == 0
            ? string.Empty
            : string.Join(Environment.NewLine, invalidUrls);
        NotifyDownloadQueueChanged();
        Status = BuildQueueResultStatus(queuedCount, skippedExistingCount, skippedDuplicateCount, invalidUrls.Count);
        EnsureDownloadQueueProcessing();
    }

    private async Task ProcessDownloadQueueAsync()
    {
        if (IsDownloadInProgress)
        {
            return;
        }

        IsDownloadInProgress = true;
        downloadCancellation = new CancellationTokenSource();
        var completedCount = 0;
        var failedCount = 0;
        var skippedCount = 0;
        string? lastDownloadedTitle = null;
        string? lastFailureMessage = null;
        var wasCanceled = false;

        try
        {
            while (downloadQueue.Count > 0)
            {
                var queuedDownload = downloadQueue.Dequeue();
                activeDownloadVideoId = queuedDownload.VideoId;
                NotifyDownloadQueueChanged();

                if (TryFindAvailableVideo(queuedDownload.VideoId, out var existingItem))
                {
                    skippedCount++;
                    Status = $"'{existingItem!.Title}' is already in the library.";
                    activeDownloadVideoId = null;
                    NotifyDownloadQueueChanged();
                    continue;
                }

                Status = BuildActiveDownloadStatus("Preparing download...");

                var progress = new Progress<double>(value =>
                {
                    var percent = (int)Math.Round(Math.Clamp(value, 0.0, 1.0) * 100.0);
                    Status = percent >= 100
                        ? BuildActiveDownloadStatus("Finalizing download...")
                        : BuildActiveDownloadStatus($"Downloading... {percent}%");
                });

                try
                {
                    downloadTask = downloadService.DownloadAsync(queuedDownload.SourceUrl, progress, downloadCancellation.Token);
                    var record = await downloadTask;

                    Upsert(record);
                    catalog.Save(videos.Select(item => item.Entry));
                    completedCount++;
                    lastDownloadedTitle = record.Title;
                    Status = downloadQueue.Count == 0
                        ? $"Downloaded '{record.Title}'."
                        : $"Downloaded '{record.Title}'. {downloadQueue.Count} queued download(s) remaining.";
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    failedCount++;
                    lastFailureMessage = $"Download failed: {exception.Message}";
                    Status = downloadQueue.Count == 0
                        ? lastFailureMessage
                        : $"{lastFailureMessage} {downloadQueue.Count} queued download(s) remaining.";
                }
                finally
                {
                    downloadTask = null;
                    activeDownloadVideoId = null;
                }
            }

            Status = BuildFinishedQueueStatus(completedCount, failedCount, skippedCount, lastDownloadedTitle, lastFailureMessage);
        }
        catch (OperationCanceledException)
        {
            wasCanceled = true;
            Status = "Video download canceled.";
            downloadQueue.Clear();
            NotifyDownloadQueueChanged();
        }
        finally
        {
            downloadTask = null;
            activeDownloadVideoId = null;
            downloadCancellation?.Dispose();
            downloadCancellation = null;
            IsDownloadInProgress = false;

            if (!wasCanceled && downloadQueue.Count > 0)
            {
                EnsureDownloadQueueProcessing();
            }
        }
    }

    private void Upsert(Il2RawVideoRecord record)
    {
        videos = videos
            .Where(item => !string.Equals(item.VideoId, record.VideoId, StringComparison.Ordinal))
            .Prepend(CreateVideoItem(record))
            .OrderByDescending(item => item.Entry.DownloadedAtUtc)
            .ToList();

        OnPropertyChanged(nameof(Videos));
        OnPropertyChanged(nameof(HasVideos));
        OnPropertyChanged(nameof(LibrarySummary));
    }

    private void ReloadLibraryItems()
    {
        videos = catalog.Load()
            .Select(CreateVideoItem)
            .ToList();

        OnPropertyChanged(nameof(Videos));
        OnPropertyChanged(nameof(HasVideos));
        OnPropertyChanged(nameof(LibrarySummary));

        if (videos.Count > 0)
        {
            Status = $"Loaded {videos.Count} downloaded video(s).";
        }
    }

    private Il2RawVideoListItemViewModel CreateVideoItem(Il2RawVideoRecord record) =>
        new(record, OpenVideo, EditVideo);

    private static IReadOnlyList<string> SplitPendingUrls(string pendingUrls) =>
        pendingUrls
            .Split(new[] { '\r', '\n', '\t', ' ', ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToArray();

    private void EnsureDownloadQueueProcessing()
    {
        if (IsDownloadInProgress || downloadQueue.Count == 0)
        {
            return;
        }

        downloadQueueTask = ProcessDownloadQueueAsync();
    }

    private bool TryFindAvailableVideo(string videoId, out Il2RawVideoListItemViewModel? existingItem)
    {
        existingItem = videos.FirstOrDefault(item => string.Equals(item.VideoId, videoId, StringComparison.Ordinal));
        return existingItem is not null && File.Exists(existingItem.LocalVideoPath);
    }

    private bool IsDownloadQueuedOrActive(string videoId) =>
        string.Equals(activeDownloadVideoId, videoId, StringComparison.Ordinal)
        || downloadQueue.Any(item => string.Equals(item.VideoId, videoId, StringComparison.Ordinal));

    private void NotifyDownloadQueueChanged()
    {
        OnPropertyChanged(nameof(QueuedDownloadCount));
        OnPropertyChanged(nameof(DownloadQueueSummary));
        chooseLibraryFolderCommand.RaiseCanExecuteChanged();
    }

    private string BuildActiveDownloadStatus(string prefix) => downloadQueue.Count == 0
        ? prefix
        : $"{prefix} {downloadQueue.Count} queued download(s) remaining.";

    private static string BuildQueueResultStatus(
        int queuedCount,
        int skippedExistingCount,
        int skippedDuplicateCount,
        int invalidCount)
    {
        var parts = new List<string>();

        if (queuedCount == 0)
        {
            if (invalidCount > 0)
            {
                return $"No valid YouTube URLs found. {invalidCount} invalid URL(s).";
            }

            if (skippedExistingCount > 0 && skippedDuplicateCount == 0)
            {
                return "Those videos are already in the library.";
            }

            if (skippedDuplicateCount > 0 && skippedExistingCount == 0)
            {
                return "Those videos are already queued.";
            }

            return "No new videos were queued.";
        }

        if (queuedCount > 0)
        {
            parts.Add($"Queued {queuedCount} video(s)");
        }

        if (skippedExistingCount > 0)
        {
            parts.Add($"skipped {skippedExistingCount} already downloaded");
        }

        if (skippedDuplicateCount > 0)
        {
            parts.Add($"skipped {skippedDuplicateCount} already queued");
        }

        if (invalidCount > 0)
        {
            parts.Add($"left {invalidCount} invalid URL(s) in the input");
        }

        return parts.Count == 0
            ? "No new videos were queued."
            : string.Join("; ", parts) + ".";
    }

    private static string BuildFinishedQueueStatus(
        int completedCount,
        int failedCount,
        int skippedCount,
        string? lastDownloadedTitle,
        string? lastFailureMessage)
    {
        if (completedCount == 0 && failedCount == 0 && skippedCount == 0)
        {
            return "No new videos were downloaded.";
        }

        if (completedCount == 1 && failedCount == 0 && skippedCount == 0 && !string.IsNullOrWhiteSpace(lastDownloadedTitle))
        {
            return $"Downloaded '{lastDownloadedTitle}'.";
        }

        if (completedCount == 0 && failedCount == 1 && skippedCount == 0 && !string.IsNullOrWhiteSpace(lastFailureMessage))
        {
            return lastFailureMessage;
        }

        var parts = new List<string>();
        if (completedCount > 0)
        {
            parts.Add($"{completedCount} downloaded");
        }

        if (failedCount > 0)
        {
            parts.Add($"{failedCount} failed");
        }

        if (skippedCount > 0)
        {
            parts.Add($"{skippedCount} skipped");
        }

        var message = $"Finished queue: {string.Join(", ", parts)}.";
        return failedCount > 0 && !string.IsNullOrWhiteSpace(lastFailureMessage)
            ? $"{message} Last error: {lastFailureMessage}"
            : message;
    }

    private static string? PickLibraryFolder(string currentLibraryPath)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Choose IL-2 Videos Raw library folder",
        };

        if (Directory.Exists(currentLibraryPath))
        {
            dialog.InitialDirectory = currentLibraryPath;
        }

        return dialog.ShowDialog() == true
            ? dialog.FolderName
            : null;
    }

    private void OpenVideo(Il2RawVideoListItemViewModel item)
    {
        ArgumentNullException.ThrowIfNull(item);

        if (!TryEnsureVideoExists(item))
        {
            return;
        }

        try
        {
            localFileLauncher.Open(item.LocalVideoPath);
            Status = $"Opened '{item.Title}' in the default player.";
        }
        catch (Exception exception)
        {
            Status = exception.Message;
        }
    }

    private void EditVideo(Il2RawVideoListItemViewModel item)
    {
        ArgumentNullException.ThrowIfNull(item);

        if (!TryEnsureVideoExists(item))
        {
            return;
        }

        Status = $"Opening editor for '{item.Title}'.";
        EditRequested?.Invoke(item.Entry);
    }

    private bool TryEnsureVideoExists(Il2RawVideoListItemViewModel item)
    {
        if (File.Exists(item.LocalVideoPath))
        {
            return true;
        }

        RemoveMissingVideo(item);
        return false;
    }

    private void RemoveMissingVideo(Il2RawVideoListItemViewModel item)
    {
        videos = videos
            .Where(existing => !string.Equals(existing.VideoId, item.VideoId, StringComparison.Ordinal))
            .ToList();
        catalog.Save(videos.Select(existing => existing.Entry));
        OnPropertyChanged(nameof(Videos));
        OnPropertyChanged(nameof(HasVideos));
        OnPropertyChanged(nameof(LibrarySummary));
        Status = $"'{item.Title}' is missing on disk and was removed from the library.";
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private sealed record Il2RawVideoDownloadQueueItem(string SourceUrl, string VideoId);
}