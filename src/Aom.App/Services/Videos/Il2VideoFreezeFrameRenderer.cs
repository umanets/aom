using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace Aom.App.Services.Videos;

public sealed class Il2VideoFreezeFrameRenderer
{
    public string RenderAnnotatedStill(string sourceFramePath, FreezeFrameAnnotationProjectItem freezeAnnotation, string outputImagePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceFramePath);
        ArgumentNullException.ThrowIfNull(freezeAnnotation);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputImagePath);

        var sourceBitmap = LoadBitmap(sourceFramePath);
        var width = sourceBitmap.PixelWidth;
        var height = sourceBitmap.PixelHeight;

        var container = new Grid
        {
            Width = width,
            Height = height,
            Background = Brushes.Transparent,
        };
        container.Children.Add(new Image
        {
            Source = sourceBitmap,
            Width = width,
            Height = height,
            Stretch = Stretch.Fill,
        });

        var overlay = new Canvas
        {
            Width = width,
            Height = height,
            Background = Brushes.Transparent,
        };
        container.Children.Add(overlay);

        foreach (var descriptor in freezeAnnotation.Shapes.Select(TryDeserializeDescriptor).OfType<Il2VideoAnnotationDescriptor>())
        {
            var element = CreateCanvasElement(descriptor, width, height);
            if (element is not null)
            {
                overlay.Children.Add(element);
            }
        }

        foreach (var descriptor in freezeAnnotation.TextAnnotations.Select(TryDeserializeDescriptor).OfType<Il2VideoAnnotationDescriptor>())
        {
            var element = CreateCanvasElement(descriptor, width, height);
            if (element is not null)
            {
                overlay.Children.Add(element);
            }
        }

        container.Measure(new Size(width, height));
        container.Arrange(new Rect(0, 0, width, height));
        container.UpdateLayout();

        var renderTarget = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        renderTarget.Render(container);

        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(outputImagePath)!);
        using var stream = File.Create(outputImagePath);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(renderTarget));
        encoder.Save(stream);
        return outputImagePath;
    }

    private static BitmapImage LoadBitmap(string sourceFramePath)
    {
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.UriSource = new Uri(sourceFramePath, UriKind.Absolute);
        bitmap.EndInit();
        bitmap.Freeze();
        return bitmap;
    }

    private static Il2VideoAnnotationDescriptor? TryDeserializeDescriptor(string serialized)
    {
        try
        {
            return JsonSerializer.Deserialize<Il2VideoAnnotationDescriptor>(serialized);
        }
        catch
        {
            return null;
        }
    }

    private static UIElement? CreateCanvasElement(Il2VideoAnnotationDescriptor descriptor, double width, double height)
    {
        var startPoint = new Point(descriptor.StartX * width, descriptor.StartY * height);
        var endPoint = new Point(descriptor.EndX * width, descriptor.EndY * height);

        return descriptor.Tool switch
        {
            "Line" => new Line
            {
                X1 = startPoint.X,
                Y1 = startPoint.Y,
                X2 = endPoint.X,
                Y2 = endPoint.Y,
                Stroke = CreateStrokeBrush(descriptor.StrokeHex),
                StrokeThickness = descriptor.StrokeThickness,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
            },
            "Rectangle" => CreateRectangle(startPoint, endPoint, descriptor),
            "Circle" => CreateEllipse(startPoint, endPoint, descriptor),
            "Text" => CreateTextElement(startPoint, descriptor),
            _ => null,
        };
    }

    private static Rectangle CreateRectangle(Point startPoint, Point endPoint, Il2VideoAnnotationDescriptor descriptor)
    {
        var rectangle = new Rectangle
        {
            Width = Math.Abs(endPoint.X - startPoint.X),
            Height = Math.Abs(endPoint.Y - startPoint.Y),
            Stroke = CreateStrokeBrush(descriptor.StrokeHex),
            StrokeThickness = descriptor.StrokeThickness,
        };
        Canvas.SetLeft(rectangle, Math.Min(startPoint.X, endPoint.X));
        Canvas.SetTop(rectangle, Math.Min(startPoint.Y, endPoint.Y));
        return rectangle;
    }

    private static Ellipse CreateEllipse(Point startPoint, Point endPoint, Il2VideoAnnotationDescriptor descriptor)
    {
        var ellipse = new Ellipse
        {
            Width = Math.Abs(endPoint.X - startPoint.X),
            Height = Math.Abs(endPoint.Y - startPoint.Y),
            Stroke = CreateStrokeBrush(descriptor.StrokeHex),
            StrokeThickness = descriptor.StrokeThickness,
        };
        Canvas.SetLeft(ellipse, Math.Min(startPoint.X, endPoint.X));
        Canvas.SetTop(ellipse, Math.Min(startPoint.Y, endPoint.Y));
        return ellipse;
    }

    private static Border CreateTextElement(Point startPoint, Il2VideoAnnotationDescriptor descriptor)
    {
        var border = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(180, 8, 17, 30)),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(6, 3, 6, 3),
            Child = new TextBlock
            {
                Text = descriptor.Text ?? string.Empty,
                Foreground = Brushes.White,
                FontSize = 18,
                FontWeight = FontWeights.SemiBold,
            },
        };
        Canvas.SetLeft(border, startPoint.X);
        Canvas.SetTop(border, startPoint.Y);
        return border;
    }

    private static Brush CreateStrokeBrush(string strokeHex)
    {
        try
        {
            return (Brush)new BrushConverter().ConvertFromString(strokeHex)!;
        }
        catch
        {
            return new SolidColorBrush(Color.FromRgb(0x4F, 0xD1, 0xC5));
        }
    }
}