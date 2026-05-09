using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace Aom.App.Services.Ai;

public sealed class Il2WindowCaptureService
{
    private const long JpegQuality = 85L;

    public AiPartnerScreenshotCaptureResult CaptureLatest()
    {
        try
        {
            if (!TryFindIl2Window(out var windowHandle, out var windowTitle))
            {
                return new AiPartnerScreenshotCaptureResult(null, "IL-2 window not found.");
            }

            if (IsIconic(windowHandle))
            {
                return new AiPartnerScreenshotCaptureResult(null, $"IL-2 window \"{windowTitle}\" is minimized.");
            }

            if (!GetWindowRect(windowHandle, out var bounds))
            {
                return new AiPartnerScreenshotCaptureResult(null, $"Failed to read IL-2 window bounds (Win32 {Marshal.GetLastWin32Error()}).");
            }

            var width = bounds.Right - bounds.Left;
            var height = bounds.Bottom - bounds.Top;
            if (width <= 0 || height <= 0)
            {
                return new AiPartnerScreenshotCaptureResult(null, $"IL-2 window \"{windowTitle}\" has invalid bounds.");
            }

            using var bitmap = CaptureWindow(bounds);
            var targetSize = AiPartnerScreenshotResizePolicy.FitInside1080p(width, height);
            var imageBytes = targetSize.Width == width && targetSize.Height == height
                ? EncodeJpeg(bitmap)
                : EncodeResizedJpeg(bitmap, targetSize);

            var frame = new AiPartnerScreenshotFrame(
                imageBytes,
                "image/jpeg",
                targetSize.Width,
                targetSize.Height,
                DateTimeOffset.UtcNow,
                windowTitle);

            var sourceSizeSummary = $"{width}x{height}";
            var targetSizeSummary = $"{targetSize.Width}x{targetSize.Height}";
            var status = targetSizeSummary == sourceSizeSummary
                ? $"Captured {targetSizeSummary} from {windowTitle}."
                : $"Captured {targetSizeSummary} from {windowTitle} (downscaled from {sourceSizeSummary}).";

            return new AiPartnerScreenshotCaptureResult(frame, status);
        }
        catch (Exception exception)
        {
            return new AiPartnerScreenshotCaptureResult(null, $"Screenshot capture failed: {exception.Message}");
        }
    }

    private static Bitmap CaptureWindow(RECT bounds)
    {
        var width = bounds.Right - bounds.Left;
        var height = bounds.Bottom - bounds.Top;
        var bitmap = new Bitmap(width, height, PixelFormat.Format24bppRgb);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.CopyFromScreen(bounds.Left, bounds.Top, 0, 0, new Size(width, height), CopyPixelOperation.SourceCopy);
        return bitmap;
    }

    private static byte[] EncodeResizedJpeg(Bitmap source, AiPartnerScreenshotSize targetSize)
    {
        using var resized = new Bitmap(targetSize.Width, targetSize.Height, PixelFormat.Format24bppRgb);
        using (var graphics = Graphics.FromImage(resized))
        {
            graphics.CompositingQuality = CompositingQuality.HighQuality;
            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            graphics.SmoothingMode = SmoothingMode.HighQuality;
            graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            graphics.DrawImage(source, 0, 0, targetSize.Width, targetSize.Height);
        }

        return EncodeJpeg(resized);
    }

    private static byte[] EncodeJpeg(Image image)
    {
        using var stream = new MemoryStream();
        var jpegCodec = ImageCodecInfo.GetImageEncoders()
            .FirstOrDefault(codec => string.Equals(codec.MimeType, "image/jpeg", StringComparison.OrdinalIgnoreCase));

        if (jpegCodec is null)
        {
            image.Save(stream, ImageFormat.Jpeg);
            return stream.ToArray();
        }

        using var encoderParameters = new EncoderParameters(1);
        encoderParameters.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, JpegQuality);
        image.Save(stream, jpegCodec, encoderParameters);
        return stream.ToArray();
    }

    private static bool TryFindIl2Window(out nint windowHandle, out string windowTitle)
    {
        var foundHandle = nint.Zero;
        var foundTitle = string.Empty;

        EnumWindows(
            (candidateHandle, _) =>
            {
                if (!IsWindowVisible(candidateHandle) || IsIconic(candidateHandle))
                {
                    return true;
                }

                var title = GetWindowTitle(candidateHandle);
                if (!LooksLikeIl2Window(title))
                {
                    return true;
                }

                foundHandle = candidateHandle;
                foundTitle = title;
                return false;
            },
            nint.Zero);

        windowHandle = foundHandle;
        windowTitle = foundTitle;
        return windowHandle != nint.Zero;
    }

    private static bool LooksLikeIl2Window(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return false;
        }

        return title.Contains("IL-2", StringComparison.OrdinalIgnoreCase)
            || title.Contains("Sturmovik", StringComparison.OrdinalIgnoreCase)
            || title.Contains("Great Battles", StringComparison.OrdinalIgnoreCase);
    }

    private static string GetWindowTitle(nint windowHandle)
    {
        var length = GetWindowTextLength(windowHandle);
        if (length <= 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder(length + 1);
        _ = GetWindowText(windowHandle, builder, builder.Capacity);
        return builder.ToString().Trim();
    }

    private delegate bool EnumWindowsProc(nint windowHandle, nint lParam);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumWindows(EnumWindowsProc callback, nint lParam);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(nint windowHandle, out RECT rect);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(nint windowHandle);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsIconic(nint windowHandle);

    [DllImport("user32.dll", EntryPoint = "GetWindowTextLengthW", SetLastError = true)]
    private static extern int GetWindowTextLength(nint windowHandle);

    [DllImport("user32.dll", EntryPoint = "GetWindowTextW", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(nint windowHandle, StringBuilder buffer, int maxCount);

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct RECT
    {
        public int Left { get; init; }

        public int Top { get; init; }

        public int Right { get; init; }

        public int Bottom { get; init; }
    }
}