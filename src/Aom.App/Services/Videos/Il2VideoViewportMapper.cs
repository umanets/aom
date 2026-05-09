using System.Windows;

namespace Aom.App.Services.Videos;

internal static class Il2VideoViewportMapper
{
    public static Rect GetViewportRect(double containerWidth, double containerHeight, double sourceWidth, double sourceHeight)
    {
        var width = Math.Max(0, containerWidth);
        var height = Math.Max(0, containerHeight);
        if (width <= 0 || height <= 0)
        {
            return new Rect(0, 0, width, height);
        }

        if (sourceWidth <= 0 || sourceHeight <= 0)
        {
            return new Rect(0, 0, width, height);
        }

        var containerAspect = width / height;
        var sourceAspect = sourceWidth / sourceHeight;

        if (containerAspect > sourceAspect)
        {
            var viewportWidth = height * sourceAspect;
            var left = (width - viewportWidth) / 2;
            return new Rect(left, 0, viewportWidth, height);
        }

        var viewportHeight = width / sourceAspect;
        var top = (height - viewportHeight) / 2;
        return new Rect(0, top, width, viewportHeight);
    }

    public static Point ClampPointToViewport(Point point, Rect viewport)
    {
        if (viewport.Width <= 0 || viewport.Height <= 0)
        {
            return point;
        }

        return new Point(
            Math.Clamp(point.X, viewport.Left, viewport.Right),
            Math.Clamp(point.Y, viewport.Top, viewport.Bottom));
    }

    public static Point NormalizePoint(Point canvasPoint, Rect viewport)
    {
        if (viewport.Width <= 0 || viewport.Height <= 0)
        {
            return new Point(0, 0);
        }

        var clamped = ClampPointToViewport(canvasPoint, viewport);
        return new Point(
            Normalize(clamped.X - viewport.Left, viewport.Width),
            Normalize(clamped.Y - viewport.Top, viewport.Height));
    }

    public static Point ToCanvasPoint(Point normalizedPoint, Rect viewport)
    {
        if (viewport.Width <= 0 || viewport.Height <= 0)
        {
            return new Point(viewport.Left, viewport.Top);
        }

        return new Point(
            viewport.Left + (Math.Clamp(normalizedPoint.X, 0, 1) * viewport.Width),
            viewport.Top + (Math.Clamp(normalizedPoint.Y, 0, 1) * viewport.Height));
    }

    private static double Normalize(double value, double extent)
    {
        if (extent <= 0)
        {
            return 0;
        }

        return Math.Clamp(value / extent, 0, 1);
    }
}