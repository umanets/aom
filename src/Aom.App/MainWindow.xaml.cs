using System.Windows;
using System.Windows.Interop;
using Aom.App.Services.Videos;
using Aom.App.ViewModels;

namespace Aom.App;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private readonly OverlayWindow overlayWindow;
    private readonly MainWindowViewModel viewModel;
    private Il2VideoEditorWindow? videoEditorWindow;

    public MainWindow()
    {
        InitializeComponent();
        viewModel = new MainWindowViewModel();
        overlayWindow = new OverlayWindow
        {
            DataContext = viewModel,
        };
        viewModel.OverlayVisibilityChanged += OnOverlayVisibilityChanged;
        viewModel.Il2RawVideoLibrary.EditRequested += OnIl2RawVideoEditRequested;
        DataContext = viewModel;
        SourceInitialized += OnSourceInitialized;
        Closed += OnClosed;
    }

    private async void OnSourceInitialized(object? sender, EventArgs e)
    {
        SourceInitialized -= OnSourceInitialized;
        var handle = new WindowInteropHelper(this).Handle;
        await viewModel.InitializeHardwareDiagnosticsAsync(handle);
    }

    private async void OnClosed(object? sender, EventArgs e)
    {
        Closed -= OnClosed;
        viewModel.OverlayVisibilityChanged -= OnOverlayVisibilityChanged;
        viewModel.Il2RawVideoLibrary.EditRequested -= OnIl2RawVideoEditRequested;

        if (videoEditorWindow is not null)
        {
            videoEditorWindow.Closed -= OnVideoEditorClosed;
            videoEditorWindow.Close();
            videoEditorWindow = null;
        }

        if (overlayWindow.IsLoaded)
        {
            overlayWindow.Close();
        }

        await viewModel.DisposeAsync();
    }

    private void OnOverlayVisibilityChanged(bool isVisible)
    {
        overlayWindow.SetOverlayVisible(isVisible);
    }

    private void OnIl2RawVideoEditRequested(Il2RawVideoRecord record)
    {
        var projectStore = new PersistedIl2VideoEditProjectStore(new Il2RawVideoLibraryPaths(viewModel.Il2RawVideoLibrary.LibraryPath));

        if (videoEditorWindow is not null
            && videoEditorWindow.IsLoaded
            && string.Equals(videoEditorWindow.SourceVideoPath, record.LocalVideoPath, StringComparison.OrdinalIgnoreCase))
        {
            if (!videoEditorWindow.IsVisible)
            {
                videoEditorWindow.Show();
            }

            videoEditorWindow.Activate();
            return;
        }

        if (videoEditorWindow is not null)
        {
            videoEditorWindow.Closed -= OnVideoEditorClosed;
            videoEditorWindow.Close();
            videoEditorWindow = null;
        }

        videoEditorWindow = new Il2VideoEditorWindow(record, projectStore)
        {
            Owner = this,
        };
        videoEditorWindow.Closed += OnVideoEditorClosed;
        videoEditorWindow.Show();
    }

    private void OnVideoEditorClosed(object? sender, EventArgs e)
    {
        if (!ReferenceEquals(sender, videoEditorWindow))
        {
            return;
        }

        videoEditorWindow!.Closed -= OnVideoEditorClosed;
        videoEditorWindow = null;
    }
}