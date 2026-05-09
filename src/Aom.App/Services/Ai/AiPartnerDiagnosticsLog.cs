using System.IO;

namespace Aom.App.Services.Ai;

public sealed class AiPartnerDiagnosticsLog
{
    private readonly object sync = new();

    public AiPartnerDiagnosticsLog(string? logFilePath = null)
    {
        LogFilePath = logFilePath
            ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Aom",
                "Logs",
                "ai-partner.log");
    }

    public string LogFilePath { get; }

    public void WriteInfo(string message)
    {
        Write("INFO", message, null);
    }

    public void WriteError(string message, Exception? exception = null)
    {
        Write("ERROR", message, exception);
    }

    private void Write(string level, string message, Exception? exception)
    {
        try
        {
            var directoryPath = Path.GetDirectoryName(LogFilePath);
            if (!string.IsNullOrWhiteSpace(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }

            var line = $"[{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff}] {level}: {message}";
            if (exception is not null)
            {
                line = $"{line}{Environment.NewLine}{exception}";
            }

            lock (sync)
            {
                File.AppendAllText(LogFilePath, line + Environment.NewLine);
            }
        }
        catch
        {
        }
    }
}