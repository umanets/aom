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
    private Il2RawVideoDownloadService downloadService;
    private readonly LocalFileLauncher localFileLauncher;
    private readonly RelayCommand<object> downloadCommand;
    private readonly RelayCommand<object> chooseLibraryFolderCommand;
    private readonly Func<string, string?> chooseLibraryFolder;
    private bool ownsDownloadService;
    private List<Il2RawVideoListItemViewModel> videos = new();
    private string pendingUrl = string.Empty;
    private string status = "Paste a YouTube URL to download the first raw video.";
    private bool isDownloadInProgress;
    private CancellationTokenSource? downloadCancellation;
    private Task<Il2RawVideoRecord>? downloadTask;

    public Il2RawVideoLibraryViewModel(
        string? libraryRootPath = null,
        PersistedIl2RawVideoCatalog? catalog = null,
        Il2RawVideoDownloadService? downloadService = null,
        LocalFileLauncher? localFileLauncher = null,
        Func<string, string?>? chooseLibraryFolder = null)
    {
        this.catalog = catalog ?? new PersistedIl2RawVideoCatalog(new Il2RawVideoLibraryPaths(libraryRootPath));
        this.downloadService = downloadService ?? new Il2RawVideoDownloadService(this.catalog.Paths);
        this.localFileLauncher = localFileLauncher ?? new LocalFileLauncher();
        this.chooseLibraryFolder = chooseLibraryFolder ?? PickLibraryFolder;
        ownsDownloadService = downloadService is null;
        downloadCommand = new RelayCommand<object>(_ => _ = StartDownloadAsync(), _ => CanDownload());
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
            downloadCommand.RaiseCanExecuteChanged();
            chooseLibraryFolderCommand.RaiseCanExecuteChanged();
        }
    }

    public string DownloadButtonLabel => IsDownloadInProgress ? "Downloading..." : "Download";

    public string LibraryPath => catalog.LibraryRootPath;

    public string LibrarySummary => videos.Count == 0
        ? "No downloaded videos yet."
        : $"{videos.Count} downloaded video(s).";

    public bool HasVideos => videos.Count > 0;

    public IReadOnlyList<Il2RawVideoListItemViewModel> Videos => videos;

    public ICommand DownloadCommand => downloadCommand;

    public ICommand ChooseLibraryFolderCommand => chooseLibraryFolderCommand;

    public async ValueTask DisposeAsync()
    {
        if (downloadCancellation is not null)
        {
            downloadCancellation.Cancel();
        }

        if (downloadTask is not null)
        {
            try
            {
                await downloadTask;
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

    private bool CanDownload() => !IsDownloadInProgress && !string.IsNullOrWhiteSpace(PendingUrl);

    private bool CanChooseLibraryFolder() => !IsDownloadInProgress;

    public bool ChangeLibraryPath(string? libraryRootPath)
    {
        if (IsDownloadInProgress || string.IsNullOrWhiteSpace(libraryRootPath))
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

    private async Task StartDownloadAsync()
    {
        if (IsDownloadInProgress)
        {
            return;
        }

        var urlSnapshot = PendingUrl.Trim();
        if (string.IsNullOrWhiteSpace(urlSnapshot))
        {
            Status = "Paste a YouTube URL first.";
            return;
        }

        string normalizedVideoId;
        try
        {
            normalizedVideoId = Il2RawVideoDownloadService.NormalizeVideoId(urlSnapshot);
        }
        catch (ArgumentException exception)
        {
            Status = exception.Message;
            return;
        }

        var existingItem = videos.FirstOrDefault(item => string.Equals(item.VideoId, normalizedVideoId, StringComparison.Ordinal));
        if (existingItem is not null && File.Exists(existingItem.LocalVideoPath))
        {
            Status = $"'{existingItem.Title}' is already in the library.";
            return;
        }

        IsDownloadInProgress = true;
        Status = "Preparing download...";
        downloadCancellation = new CancellationTokenSource();

        try
        {
            var progress = new Progress<double>(value =>
            {
                var percent = (int)Math.Round(Math.Clamp(value, 0.0, 1.0) * 100.0);
                Status = percent >= 100
                    ? "Finalizing download..."
                    : $"Downloading... {percent}%";
            });

            downloadTask = downloadService.DownloadAsync(urlSnapshot, progress, downloadCancellation.Token);
            var record = await downloadTask;

            Upsert(record);
            catalog.Save(videos.Select(item => item.Entry));
            PendingUrl = string.Empty;
            Status = $"Downloaded '{record.Title}'.";
        }
        catch (OperationCanceledException)
        {
            Status = "Video download canceled.";
        }
        catch (Exception exception)
        {
            Status = exception.Message;
        }
        finally
        {
            downloadTask = null;
            downloadCancellation?.Dispose();
            downloadCancellation = null;
            IsDownloadInProgress = false;
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
}