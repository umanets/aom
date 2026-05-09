using System.IO;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Aom.App.Services.Videos;

namespace Aom.App.ViewModels;

public sealed class Il2RawVideoListItemViewModel
{
    private readonly Action<Il2RawVideoListItemViewModel> openRequested;
    private readonly Action<Il2RawVideoListItemViewModel> editRequested;

    public Il2RawVideoListItemViewModel(
        Il2RawVideoRecord entry,
        Action<Il2RawVideoListItemViewModel>? openRequested = null,
        Action<Il2RawVideoListItemViewModel>? editRequested = null)
    {
        Entry = entry;
        this.openRequested = openRequested ?? (_ => { });
        this.editRequested = editRequested ?? (_ => { });
        MetaLine = BuildMetaLine(entry);
        ThumbnailImage = LoadThumbnail(entry.ThumbnailPath);
        OpenCommand = new RelayCommand<object>(_ => Open());
        EditCommand = new RelayCommand<object>(_ => Edit());
    }

    public Il2RawVideoRecord Entry { get; }

    public string VideoId => Entry.VideoId;

    public string Title => Entry.Title;

    public string LocalVideoPath => Entry.LocalVideoPath;

    public string MetaLine { get; }

    public ImageSource? ThumbnailImage { get; }

    public ICommand OpenCommand { get; }

    public ICommand EditCommand { get; }

    public void Open()
    {
        openRequested(this);
    }

    public void Edit()
    {
        editRequested(this);
    }

    private static string BuildMetaLine(Il2RawVideoRecord entry)
    {
        var parts = new List<string>();

        if (!string.IsNullOrWhiteSpace(entry.ChannelTitle))
        {
            parts.Add(entry.ChannelTitle);
        }

        if (entry.Duration.HasValue)
        {
            parts.Add(FormatDuration(entry.Duration.Value));
        }

        parts.Add(FormatFileSize(entry.FileSizeBytes));
        parts.Add($"Downloaded {entry.DownloadedAtUtc.LocalDateTime:yyyy-MM-dd HH:mm}");

        return string.Join(" | ", parts);
    }

    private static string FormatDuration(TimeSpan duration) =>
        duration.TotalHours >= 1
            ? $"{(int)duration.TotalHours}:{duration.Minutes:00}:{duration.Seconds:00}"
            : $"{duration.Minutes:00}:{duration.Seconds:00}";

    private static string FormatFileSize(long bytes)
    {
        const double BytesPerMegabyte = 1024 * 1024;
        const double BytesPerGigabyte = BytesPerMegabyte * 1024;

        return bytes >= BytesPerGigabyte
            ? $"{bytes / BytesPerGigabyte:0.00} GB"
            : $"{bytes / BytesPerMegabyte:0.0} MB";
    }

    private static ImageSource? LoadThumbnail(string? thumbnailPath)
    {
        if (string.IsNullOrWhiteSpace(thumbnailPath) || !File.Exists(thumbnailPath))
        {
            return null;
        }

        try
        {
            var image = new BitmapImage();
            using var stream = File.OpenRead(thumbnailPath);
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.StreamSource = stream;
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch
        {
            return null;
        }
    }
}