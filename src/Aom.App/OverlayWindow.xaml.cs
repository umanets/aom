using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Microsoft.Win32;

namespace Aom.App;

public partial class OverlayWindow : Window
{
    private const int GwlExStyle = -20;
    private const int WsExTransparent = 0x20;
    private const int WsExLayered = 0x00080000;
    private const int WsExToolWindow = 0x00000080;
    private const int WsExNoActivate = 0x08000000;
    private const uint SwpNoSize = 0x0001;
    private const uint SwpNoMove = 0x0002;
    private const uint SwpNoActivate = 0x0010;
    private const uint SwpShowWindow = 0x0040;
    private const uint SwpNoOwnerZOrder = 0x0200;
    private static readonly nint HwndTopmost = new(-1);

    public OverlayWindow()
    {
        InitializeComponent();
        SourceInitialized += OnSourceInitialized;
        Closed += OnClosed;
        SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
    }

    public void SetOverlayVisible(bool isVisible)
    {
        if (isVisible)
        {
            if (!IsVisible)
            {
                Show();
            }

            RefreshBoundsAndZOrder();
            return;
        }

        if (IsVisible)
        {
            Hide();
        }
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        ApplyClickThroughStyles();
        RefreshBoundsAndZOrder();
    }

    private void OnDisplaySettingsChanged(object? sender, EventArgs e)
    {
        if (!IsVisible)
        {
            return;
        }

        Dispatcher.Invoke(RefreshBoundsAndZOrder);
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        Closed -= OnClosed;
        SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
    }

    private void ApplyClickThroughStyles()
    {
        var handle = new WindowInteropHelper(this).Handle;
        if (handle == nint.Zero)
        {
            return;
        }

        var styles = (long)GetWindowLongPtr(handle, GwlExStyle);
        styles |= WsExTransparent | WsExLayered | WsExToolWindow | WsExNoActivate;
        SetWindowLongPtr(handle, GwlExStyle, (nint)styles);
    }

    private void RefreshBoundsAndZOrder()
    {
        var handle = new WindowInteropHelper(this).Handle;
        if (handle == nint.Zero)
        {
            return;
        }

        ResizeToVirtualScreen();
        SetWindowPos(handle, HwndTopmost, 0, 0, 0, 0, SwpNoMove | SwpNoSize | SwpNoActivate | SwpShowWindow | SwpNoOwnerZOrder);
    }

    private void ResizeToVirtualScreen()
    {
        var source = PresentationSource.FromVisual(this);
        if (source?.CompositionTarget is null)
        {
            return;
        }

        var transform = source.CompositionTarget.TransformFromDevice;
        var topLeft = transform.Transform(new Point(SystemParameters.VirtualScreenLeft, SystemParameters.VirtualScreenTop));
        var bottomRight = transform.Transform(
            new Point(
                SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth,
                SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight));

        Left = topLeft.X;
        Top = topLeft.Y;
        Width = Math.Max(1, bottomRight.X - topLeft.X);
        Height = Math.Max(1, bottomRight.Y - topLeft.Y);
    }

    private static nint GetWindowLongPtr(nint handle, int index)
    {
        if (IntPtr.Size == 8)
        {
            return GetWindowLongPtr64(handle, index);
        }

        return GetWindowLong32(handle, index);
    }

    private static void SetWindowLongPtr(nint handle, int index, nint value)
    {
        if (IntPtr.Size == 8)
        {
            _ = SetWindowLongPtr64(handle, index, value);
            return;
        }

        _ = SetWindowLong32(handle, index, unchecked((int)value));
    }

    [DllImport("user32.dll", EntryPoint = "GetWindowLong")]
    private static extern nint GetWindowLong32(nint handle, int index);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr")]
    private static extern nint GetWindowLongPtr64(nint handle, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLong")]
    private static extern nint SetWindowLong32(nint handle, int index, int value);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr")]
    private static extern nint SetWindowLongPtr64(nint handle, int index, nint value);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(nint handle, nint insertAfter, int x, int y, int width, int height, uint flags);
}