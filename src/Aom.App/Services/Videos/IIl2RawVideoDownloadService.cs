namespace Aom.App.Services.Videos;

public interface IIl2RawVideoDownloadService : IDisposable
{
    Task<Il2RawVideoRecord> DownloadAsync(
        string videoIdOrUrl,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default);
}