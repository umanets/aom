using System.Diagnostics;
using System.IO;

namespace Aom.App.Services.Videos;

public sealed class LocalFileLauncher
{
    public void Open(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("The downloaded video file was not found.", filePath);
        }

        Process.Start(new ProcessStartInfo(filePath)
        {
            UseShellExecute = true,
        });
    }
}