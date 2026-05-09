using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using System.ComponentModel;
using Aom.App.Services.Videos;

namespace Aom.App;

public partial class Il2VideoEditorWindow : Window
{
    private const string AnnotationStrokeHex = "#4FD1C5";
    private const double AnnotationStrokeThickness = 3.0;
    private const double LineHandleSize = 12.0;
    private static readonly TimeSpan FreezeTriggerTolerance = TimeSpan.FromMilliseconds(200);
    private readonly Il2RawVideoRecord record;
    private readonly PersistedIl2VideoEditProjectStore projectStore;
    private readonly Il2VideoEditExportService exportService;
    private readonly Il2VideoPreviewPlanner previewPlanner = new();
    private Il2VideoEditProject currentProject;
    private readonly DispatcherTimer positionTimer;
    private readonly DispatcherTimer openTimeoutTimer;
    private readonly DispatcherTimer freezeHoldTimer;
    private bool isMediaReady;
    private bool isInternalSeekUpdate;
    private bool isSeekActive;
    private bool bootstrapOpenPending;
    private TimeSpan? pendingSlowRangeStart;
    private TimeSpan? pendingSlowRangeEnd;
    private Point? annotationStartPoint;
    private Shape? annotationPreviewShape;
    private string? currentAnnotationTool;
    private FreezePrimitiveListItem? pendingPrimitiveEdit;
    private FreezePrimitiveListItem? activeCanvasPrimitive;
    private string? activeCanvasHandleRole;
    private string? activeCanvasTextDraft;
    private TextBox? activeCanvasTextEditor;
    private bool isUpdatingActiveCanvasTextEditor;
    private bool isActiveCanvasTextEditing;
    private bool hasMigratedLegacyAnnotationCoordinates;
    private int activePreviewFreezeIndex = -1;
    private int nextFreezePreviewIndex = -1;
    private double basePreviewSpeedRatio = 1.0;
    private bool isExporting;
    private bool isPlaybackActive;

    public Il2VideoEditorWindow(Il2RawVideoRecord record, PersistedIl2VideoEditProjectStore projectStore)
    {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentNullException.ThrowIfNull(projectStore);

        if (!File.Exists(record.LocalVideoPath))
        {
            throw new FileNotFoundException("The downloaded video file was not found.", record.LocalVideoPath);
        }

        this.record = record;
        this.projectStore = projectStore;
        exportService = new Il2VideoEditExportService(projectStore.Paths);
        currentProject = projectStore.LoadOrCreate(record);
        InitializeComponent();

        positionTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(200),
        };
        positionTimer.Tick += OnPositionTimerTick;

        openTimeoutTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(15),
        };
        openTimeoutTimer.Tick += OnOpenTimeoutTick;

        freezeHoldTimer = new DispatcherTimer();
        freezeHoldTimer.Tick += OnFreezeHoldTimerTick;

        Loaded += OnLoaded;
        Closed += OnClosed;

        Title = $"IL-2 Video Editor - {record.Title}";
        TitleTextBlock.Text = record.Title;
        MetaTextBlock.Text = BuildMetaLine(record);
        SourcePathTextBlock.Text = record.LocalVideoPath;
        ProjectPathTextBlock.Text = $"Project: {projectStore.GetProjectPath(record.VideoId)}";
        ClearAnnotationToolSelection();
        SetPlaybackActive(false);
        CurrentPositionTextBlock.Text = FormatTimestamp(TimeSpan.Zero);
        DurationTextBlock.Text = FormatTimestamp(record.Duration ?? TimeSpan.Zero);
        basePreviewSpeedRatio = 1.0;
        SelectSpeedRatio(1.0);
        SelectFreezeDuration(TimeSpan.FromSeconds(2));
        SelectSlowSpeedRatio(0.5);
        RefreshProjectLists();
        RefreshPendingSlowRange();
        RefreshSelectedFreezePrimitiveList();
    }

    public string SourceVideoPath => record.LocalVideoPath;

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        PlayerElement.Source = new Uri(record.LocalVideoPath, UriKind.Absolute);
        SetViewportStatus("Opening video...");
        StatusTextBlock.Text = "Opening video...";
        BeginBootstrapOpen();
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        Closed -= OnClosed;
        positionTimer.Stop();
        positionTimer.Tick -= OnPositionTimerTick;
        openTimeoutTimer.Stop();
        openTimeoutTimer.Tick -= OnOpenTimeoutTick;
        freezeHoldTimer.Stop();
        freezeHoldTimer.Tick -= OnFreezeHoldTimerTick;
        RemoveActiveCanvasTextEditor();
        PlayerElement.Stop();
        PlayerElement.Close();
    }

    private void OnWindowPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Delete)
        {
            if (Keyboard.FocusedElement is TextBox)
            {
                return;
            }

            if (!TryDeleteSelectedPrimitive())
            {
                return;
            }

            e.Handled = true;
            return;
        }

        if (e.Key != Key.Escape)
        {
            return;
        }

        if (!CancelPendingAnnotationInteraction())
        {
            return;
        }

        e.Handled = true;
    }

    private void OnPlayPauseClicked(object sender, RoutedEventArgs e)
    {
        if (isPlaybackActive)
        {
            OnPauseClicked(sender, e);
            return;
        }

        OnPlayClicked(sender, e);
    }

    private void OnMediaOpened(object sender, RoutedEventArgs e)
    {
        openTimeoutTimer.Stop();
        isMediaReady = true;
        var naturalDuration = PlayerElement.NaturalDuration.HasTimeSpan
            ? PlayerElement.NaturalDuration.TimeSpan
            : record.Duration ?? TimeSpan.Zero;

        isInternalSeekUpdate = true;
        SeekSlider.Minimum = 0;
        SeekSlider.Maximum = Math.Max(1, naturalDuration.TotalSeconds);
        SeekSlider.Value = 0;
        isInternalSeekUpdate = false;

        DurationTextBlock.Text = FormatTimestamp(naturalDuration);
        HideViewportStatus();
        TryMigrateLegacyAnnotationCoordinates();

        if (bootstrapOpenPending)
        {
            bootstrapOpenPending = false;
            PlayerElement.Pause();
            PlayerElement.Position = TimeSpan.Zero;
            PlayerElement.IsMuted = false;
            ResetPlaybackPreviewState(TimeSpan.Zero);
            SetPlaybackActive(false);
            StatusTextBlock.Text = BuildProjectReadyStatus();
            return;
        }

        ApplyPlaybackSpeedForPosition(PlayerElement.Position, updateStatus: true);
        positionTimer.Start();
        SetPlaybackActive(true);
    }

    private void OnMediaEnded(object sender, RoutedEventArgs e)
    {
        positionTimer.Stop();
        freezeHoldTimer.Stop();
        ResetPlaybackPreviewState(TimeSpan.Zero);
        UpdatePositionDisplay(PlayerElement.Position);
        SetPlaybackActive(false);
        SetViewportStatus("Playback ended");
        StatusTextBlock.Text = "Reached the end of the video.";
    }

    private void OnMediaFailed(object sender, ExceptionRoutedEventArgs e)
    {
        openTimeoutTimer.Stop();
        positionTimer.Stop();
        freezeHoldTimer.Stop();
        isMediaReady = false;
        bootstrapOpenPending = false;
        activePreviewFreezeIndex = -1;
        PlayerElement.IsMuted = false;
        AnnotationCanvas.Children.Clear();
        SetPlaybackActive(false);
        SetViewportStatus("Playback failed");
        StatusTextBlock.Text = e.ErrorException?.Message ?? "Playback failed.";
    }

    private void OnPlayClicked(object sender, RoutedEventArgs e)
    {
        if (!isMediaReady)
        {
            StatusTextBlock.Text = "Opening video...";
            SetViewportStatus("Opening video...");
            bootstrapOpenPending = false;
            PlayerElement.IsMuted = false;
            PlayerElement.Play();
            SetPlaybackActive(true);
            return;
        }

        if (freezeHoldTimer.IsEnabled)
        {
            freezeHoldTimer.Stop();
            activePreviewFreezeIndex = -1;
        }
        else
        {
            ResetPlaybackPreviewState(PlayerElement.Position);
        }

        PlayerElement.Play();
        positionTimer.Start();
        HideViewportStatus();
        UpdateAnnotationOverlay();
        ApplyPlaybackSpeedForPosition(PlayerElement.Position, updateStatus: true);
        SetPlaybackActive(true);
    }

    private void OnPauseClicked(object sender, RoutedEventArgs e)
    {
        if (!isMediaReady)
        {
            return;
        }

        if (freezeHoldTimer.IsEnabled)
        {
            freezeHoldTimer.Stop();
            activePreviewFreezeIndex = -1;
            HideViewportStatus();
        }

        PlayerElement.Pause();
        positionTimer.Stop();
        UpdateAnnotationOverlay();
        SetPlaybackActive(false);
        StatusTextBlock.Text = "Playback paused.";
    }

    private void OnStopClicked(object sender, RoutedEventArgs e)
    {
        if (!isMediaReady)
        {
            return;
        }

        positionTimer.Stop();
        freezeHoldTimer.Stop();
        PlayerElement.Stop();
        PlayerElement.SpeedRatio = basePreviewSpeedRatio;
        ResetPlaybackPreviewState(TimeSpan.Zero);
        UpdatePositionDisplay(TimeSpan.Zero);
        SetPlaybackActive(false);
        SetViewportStatus("Playback stopped");
        StatusTextBlock.Text = "Playback stopped.";
    }

    private void OnSeekSliderMouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        isSeekActive = true;
    }

    private void OnSeekSliderMouseUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        isSeekActive = false;
        SeekToSliderPosition();
    }

    private void OnSeekSliderValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!isMediaReady || isInternalSeekUpdate)
        {
            return;
        }

        SeekToSliderPosition();
    }

    private void OnSpeedSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SpeedComboBox.SelectedItem is not ComboBoxItem item)
        {
            return;
        }

        if (!double.TryParse(item.Tag?.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var ratio))
        {
            return;
        }

        basePreviewSpeedRatio = ratio;

        if (!isMediaReady)
        {
            StatusTextBlock.Text = Math.Abs(ratio - 1.0) < 0.001
                ? "Preview speed reset to 1.00x."
                : $"Preview speed set to {ratio:0.00}x.";
            return;
        }

        ApplyPlaybackSpeedForPosition(PlayerElement.Position, updateStatus: true);
    }

    private void OnPositionTimerTick(object? sender, EventArgs e)
    {
        if (!isMediaReady || isSeekActive)
        {
            return;
        }

        var position = PlayerElement.Position;
        UpdatePositionDisplay(position);
        ApplyPlaybackSpeedForPosition(position, updateStatus: true);
        TryStartFreezePreview(position);
    }

    private void SeekToSliderPosition()
    {
        if (!isMediaReady)
        {
            return;
        }

        var position = TimeSpan.FromSeconds(SeekSlider.Value);
        PlayerElement.Position = position;
        ResetPlaybackPreviewState(position);
        UpdatePositionDisplay(position);
        ApplyPlaybackSpeedForPosition(position, updateStatus: positionTimer.IsEnabled);
    }

    private void UpdatePositionDisplay(TimeSpan position)
    {
        isInternalSeekUpdate = true;
        SeekSlider.Value = Math.Clamp(position.TotalSeconds, SeekSlider.Minimum, SeekSlider.Maximum);
        isInternalSeekUpdate = false;
        CurrentPositionTextBlock.Text = FormatTimestamp(position);
    }

    private void HideViewportStatus()
    {
        ViewportStatusBorder.Visibility = Visibility.Collapsed;
    }

    private void SetViewportStatus(string status)
    {
        ViewportStatusTextBlock.Text = status;
        ViewportStatusBorder.Visibility = Visibility.Visible;
    }

    private void SelectSpeedRatio(double ratio)
    {
        foreach (var entry in SpeedComboBox.Items.OfType<ComboBoxItem>())
        {
            if (!double.TryParse(entry.Tag?.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var candidate))
            {
                continue;
            }

            if (Math.Abs(candidate - ratio) < 0.001)
            {
                SpeedComboBox.SelectedItem = entry;
                return;
            }
        }
    }

    private void BeginBootstrapOpen()
    {
        bootstrapOpenPending = true;
        PlayerElement.IsMuted = true;
        openTimeoutTimer.Stop();
        openTimeoutTimer.Start();
        PlayerElement.Play();
    }

    private void OnOpenTimeoutTick(object? sender, EventArgs e)
    {
        openTimeoutTimer.Stop();

        if (isMediaReady)
        {
            return;
        }

        bootstrapOpenPending = false;
        PlayerElement.IsMuted = false;
        positionTimer.Stop();
        SetViewportStatus("Playback timed out");
        StatusTextBlock.Text = "Timed out opening the video in WPF preview. The file may not be opening correctly in MediaElement on this runtime.";
    }

    private async void OnExportClicked(object sender, RoutedEventArgs e)
    {
        if (isExporting)
        {
            return;
        }

        TryMigrateLegacyAnnotationCoordinates();

        var sourceDuration = TryGetSourceDurationForExport();
        if (sourceDuration is null)
        {
            return;
        }

        isExporting = true;
        ExportButton.IsEnabled = false;
        UpdateExportProgress(new Il2VideoEditExportProgress(0.0, "Preparing export", "Resolving FFmpeg and building the render plan."));

        try
        {
            var exportProgress = new Progress<Il2VideoEditExportProgress>(UpdateExportProgress);
            var outputPath = await exportService.ExportAsync(currentProject, sourceDuration.Value, exportProgress);
            HideViewportStatus();
            UpdateExportProgress(new Il2VideoEditExportProgress(1.0, "Export complete", $"Saved edited MP4 to {outputPath}"));
            StatusTextBlock.Text = $"Exported edited video to {outputPath}";
        }
        catch (Exception exception)
        {
            UpdateExportProgress(new Il2VideoEditExportProgress(
                ExportProgressBar.Value / 100.0,
                "Export failed",
                exception.Message));
            SetViewportStatus("Export failed");
            StatusTextBlock.Text = exception.Message;
        }
        finally
        {
            isExporting = false;
            ExportButton.IsEnabled = true;
        }
    }

    private void UpdateExportProgress(Il2VideoEditExportProgress progress)
    {
        ExportProgressPanel.Visibility = Visibility.Visible;
        ExportProgressBar.Value = progress.Percent;
        ExportProgressTextBlock.Text = string.IsNullOrWhiteSpace(progress.Detail)
            ? progress.Stage
            : $"{progress.Stage}: {progress.Detail}";
        ExportProgressPercentTextBlock.Text = $"{progress.Percent}%";
        SetViewportStatus($"{progress.Stage} ({progress.Percent}%)");
        StatusTextBlock.Text = string.IsNullOrWhiteSpace(progress.Detail)
            ? progress.Stage
            : progress.Detail;
    }

    private void OnAddFreezeClicked(object sender, RoutedEventArgs e)
    {
        var currentPosition = TryGetEditorPosition();
        if (currentPosition is null)
        {
            return;
        }

        var holdDuration = GetSelectedFreezeDuration();

        try
        {
            currentProject = currentProject.AddFreezeAnnotation(currentPosition.Value, holdDuration);
            projectStore.Save(currentProject);
            var selectedIndex = FindFreezeAnnotationIndex(currentPosition.Value, holdDuration);
            RefreshProjectLists(selectedIndex);
            ResetPlaybackPreviewState(PlayerElement.Position);
            StatusTextBlock.Text = $"Added freeze marker at {FormatTimestamp(currentPosition.Value)}.";
        }
        catch (Exception exception)
        {
            StatusTextBlock.Text = exception.Message;
        }
    }

    private void OnDeleteFreezeClicked(object sender, RoutedEventArgs e)
    {
        if (!TryGetSelectedFreezeIndex(out var freezeIndex))
        {
            return;
        }

        currentProject = currentProject.RemoveFreezeAnnotation(freezeIndex);
        projectStore.Save(currentProject);
        pendingPrimitiveEdit = null;

        var replacementSelection = currentProject.FreezeAnnotations.Length == 0
            ? -1
            : Math.Min(freezeIndex, currentProject.FreezeAnnotations.Length - 1);
        RefreshProjectLists(replacementSelection);
        RefreshSelectedFreezePrimitiveList();
        ResetPlaybackPreviewState(isMediaReady ? PlayerElement.Position : TimeSpan.Zero);
        StatusTextBlock.Text = "Deleted the selected freeze frame from the project.";
    }

    private void OnMarkSlowStartClicked(object sender, RoutedEventArgs e)
    {
        var currentPosition = TryGetEditorPosition();
        if (currentPosition is null)
        {
            return;
        }

        pendingSlowRangeStart = currentPosition.Value;
        RefreshPendingSlowRange();
        StatusTextBlock.Text = $"Slow range start set to {FormatTimestamp(currentPosition.Value)}.";
    }

    private void OnMarkSlowEndClicked(object sender, RoutedEventArgs e)
    {
        var currentPosition = TryGetEditorPosition();
        if (currentPosition is null)
        {
            return;
        }

        pendingSlowRangeEnd = currentPosition.Value;
        RefreshPendingSlowRange();
        StatusTextBlock.Text = $"Slow range end set to {FormatTimestamp(currentPosition.Value)}.";
    }

    private void OnAddSlowRangeClicked(object sender, RoutedEventArgs e)
    {
        if (pendingSlowRangeStart is null || pendingSlowRangeEnd is null)
        {
            StatusTextBlock.Text = "Mark both a start and end position before adding a slow range.";
            return;
        }

        try
        {
            currentProject = currentProject.AddSlowRange(
                pendingSlowRangeStart.Value,
                pendingSlowRangeEnd.Value,
                GetSelectedSlowSpeedRatio(),
                "PitchCorrected");
            projectStore.Save(currentProject);
            pendingSlowRangeStart = null;
            pendingSlowRangeEnd = null;
            RefreshPendingSlowRange();
            RefreshProjectLists();
            ApplyPlaybackSpeedForPosition(PlayerElement.Position, updateStatus: positionTimer.IsEnabled);
            StatusTextBlock.Text = "Added slow range to the project.";
        }
        catch (Exception exception)
        {
            StatusTextBlock.Text = exception.Message;
        }
    }

    private void OnFreezeAnnotationsSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        pendingPrimitiveEdit = null;
        activeCanvasPrimitive = null;
        activeCanvasHandleRole = null;
        isActiveCanvasTextEditing = false;
        ShowSelectedFreezeFrame();
        UpdateAnnotationOverlay();
        RefreshSelectedFreezePrimitiveList();

        UpdateSelectedFreezeSummary();
    }

    private void ShowSelectedFreezeFrame()
    {
        if (!isMediaReady)
        {
            return;
        }

        var selectedIndex = FreezeAnnotationsListBox.SelectedIndex;
        if (selectedIndex < 0 || selectedIndex >= currentProject.FreezeAnnotations.Length)
        {
            return;
        }

        if (freezeHoldTimer.IsEnabled && activePreviewFreezeIndex == selectedIndex)
        {
            return;
        }

        var selectedFreeze = currentProject.FreezeAnnotations[selectedIndex];
        positionTimer.Stop();
        freezeHoldTimer.Stop();
        PlayerElement.Pause();
        PlayerElement.Position = selectedFreeze.SourceTimestamp;
        UpdatePositionDisplay(selectedFreeze.SourceTimestamp);
        ResetPlaybackPreviewState(selectedFreeze.SourceTimestamp);
        HideViewportStatus();
        SetPlaybackActive(false);
        StatusTextBlock.Text = $"Showing freeze frame at {FormatTimestamp(selectedFreeze.SourceTimestamp)}.";
    }

    private void UpdateSelectedFreezeSummary()
    {

        if (FreezeAnnotationsListBox.SelectedIndex < 0 || FreezeAnnotationsListBox.SelectedIndex >= currentProject.FreezeAnnotations.Length)
        {
            SelectedFreezeTextBlock.Text = "Select a freeze marker to annotate it on the paused frame.";
            return;
        }

        var selectedFreeze = currentProject.FreezeAnnotations[FreezeAnnotationsListBox.SelectedIndex];
        SelectedFreezeTextBlock.Text = $"Selected freeze at {FormatTimestamp(selectedFreeze.SourceTimestamp)} with {selectedFreeze.Shapes.Length + selectedFreeze.TextAnnotations.Length} annotation item(s). Pause playback, then draw on the frame.";
    }

    private void OnAnnotationToolChecked(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton radioButton)
        {
            currentAnnotationTool = radioButton.Content?.ToString() ?? "Line";
            if (pendingPrimitiveEdit is not null)
            {
                StatusTextBlock.Text = pendingPrimitiveEdit.IsText
                    ? "Primitive edit armed. Keep the Text tool and click to place the updated text primitive."
                    : $"Primitive edit armed. Drag on the paused frame to replace the selected {pendingPrimitiveEdit.Descriptor.Tool.ToLowerInvariant()} primitive.";
                return;
            }

            StatusTextBlock.Text = currentAnnotationTool == "Text"
                ? "Text tool selected for the next placement. Enter text, use Enter for a new line, and click on the frame once to place it."
                : $"{currentAnnotationTool} tool selected for the next draw. Pause the video, then drag once on the frame.";
        }
    }

    private void OnFreezePrimitiveSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (FreezePrimitiveListBox.SelectedItem is not FreezePrimitiveListItem item)
        {
            activeCanvasPrimitive = null;
            activeCanvasHandleRole = null;
            activeCanvasTextDraft = null;
            isActiveCanvasTextEditing = false;
            UpdateAnnotationOverlay();
            SelectedPrimitiveTextBlock.Text = "Select a primitive to edit or delete it.";
            return;
        }

        activeCanvasPrimitive = item;
        activeCanvasHandleRole = null;
        isActiveCanvasTextEditing = false;
        if (item.IsText)
        {
            activeCanvasTextDraft = item.Descriptor.Text ?? string.Empty;
            AnnotationTextTextBox.Text = item.Descriptor.Text ?? string.Empty;
        }
        else
        {
            activeCanvasTextDraft = null;
        }
        UpdateAnnotationOverlay();
        SelectedPrimitiveTextBlock.Text = item.IsText
            ? $"Selected text primitive: {item.Descriptor.Text ?? string.Empty}. Click its text to edit, or press Delete to remove it."
            : $"Selected {item.Descriptor.Tool.ToLowerInvariant()} primitive. Drag its canvas handles to edit it, or use Edit Selected Primitive to redraw it.";
    }

    private void OnEditSelectedPrimitiveClicked(object sender, RoutedEventArgs e)
    {
        if (!TryGetSelectedFreezeIndex(out _))
        {
            return;
        }

        if (FreezePrimitiveListBox.SelectedItem is not FreezePrimitiveListItem item)
        {
            StatusTextBlock.Text = "Select a saved primitive before editing it.";
            return;
        }

        pendingPrimitiveEdit = item;
        SelectAnnotationTool(item.IsText ? "Text" : item.Descriptor.Tool);

        if (item.IsText)
        {
            AnnotationTextTextBox.Text = item.Descriptor.Text ?? string.Empty;
            StatusTextBlock.Text = "Primitive edit armed. Click on the paused frame to place the updated text primitive.";
            return;
        }

        StatusTextBlock.Text = $"Primitive edit armed. Drag on the paused frame to replace the selected {item.Descriptor.Tool.ToLowerInvariant()} primitive.";
    }

    private void OnDeleteSelectedPrimitiveClicked(object sender, RoutedEventArgs e)
    {
        if (TryDeleteSelectedPrimitive())
        {
            return;
        }

        StatusTextBlock.Text = "Select a saved primitive before deleting it.";
    }

    private void OnAnnotationCanvasMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (!TryGetSelectedFreezeIndex(out var freezeIndex))
        {
            return;
        }

        if (freezeHoldTimer.IsEnabled)
        {
            freezeHoldTimer.Stop();
            HideViewportStatus();
        }

        if (positionTimer.IsEnabled)
        {
            StatusTextBlock.Text = "Pause playback before annotating a freeze frame.";
            return;
        }

        var position = ClampCanvasPointToAnnotationViewport(e.GetPosition(AnnotationCanvas));
        var selectedTool = currentAnnotationTool;

        if (string.IsNullOrWhiteSpace(selectedTool))
        {
            if (FreezePrimitiveListBox.SelectedIndex >= 0)
            {
                FreezePrimitiveListBox.SelectedIndex = -1;
                StatusTextBlock.Text = "Selection cleared. Select a tool to place the next annotation.";
            }
            else
            {
                StatusTextBlock.Text = "Select a tool before placing a new annotation.";
            }

            e.Handled = true;
            return;
        }

        if (string.Equals(selectedTool, "Text", StringComparison.Ordinal))
        {
            if (pendingPrimitiveEdit is not null && !pendingPrimitiveEdit.IsText)
            {
                StatusTextBlock.Text = "The selected primitive is a shape. Drag on the frame to replace it, or clear edit mode and add text separately.";
                return;
            }

            AddTextAnnotation(freezeIndex, position);
            return;
        }

        if (pendingPrimitiveEdit is not null && pendingPrimitiveEdit.IsText)
        {
            StatusTextBlock.Text = "The selected primitive is text. Keep the Text tool to replace it, or clear edit mode and add a new shape separately.";
            return;
        }

        annotationStartPoint = position;
        annotationPreviewShape = CreatePreviewShape(selectedTool);
        if (annotationPreviewShape is null)
        {
            return;
        }

        AnnotationCanvas.Children.Add(annotationPreviewShape);
        UpdatePreviewShape(annotationPreviewShape, position, position);
        AnnotationCanvas.CaptureMouse();
        e.Handled = true;
    }

    private void OnAnnotationCanvasMouseMove(object sender, MouseEventArgs e)
    {
        if (activeCanvasPrimitive is not null && activeCanvasHandleRole is not null && AnnotationCanvas.IsMouseCaptured)
        {
            UpdateActiveCanvasHandle(ClampCanvasPointToAnnotationViewport(e.GetPosition(AnnotationCanvas)));
            return;
        }

        if (annotationStartPoint is null || annotationPreviewShape is null || !AnnotationCanvas.IsMouseCaptured)
        {
            return;
        }

        UpdatePreviewShape(annotationPreviewShape, annotationStartPoint.Value, ClampCanvasPointToAnnotationViewport(e.GetPosition(AnnotationCanvas)));
    }

    private void OnAnnotationCanvasMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (activeCanvasPrimitive is not null && activeCanvasHandleRole is not null)
        {
            AnnotationCanvas.ReleaseMouseCapture();
            var finishedPrimitive = activeCanvasPrimitive;
            activeCanvasHandleRole = null;
            projectStore.Save(currentProject);
            RefreshSelectedFreezePrimitiveList();
            FreezePrimitiveListBox.SelectedItem = FindPrimitiveListMatch(finishedPrimitive);
            StatusTextBlock.Text = BuildCanvasHandleStatus(finishedPrimitive.Descriptor.Tool, null);
            return;
        }

        if (annotationStartPoint is null || annotationPreviewShape is null)
        {
            return;
        }

        var startPoint = annotationStartPoint.Value;
        var endPoint = ClampCanvasPointToAnnotationViewport(e.GetPosition(AnnotationCanvas));
        AnnotationCanvas.ReleaseMouseCapture();
        AnnotationCanvas.Children.Remove(annotationPreviewShape);
        annotationPreviewShape = null;
        annotationStartPoint = null;

        if (!TryGetSelectedFreezeIndex(out var freezeIndex))
        {
            return;
        }

        var selectedTool = currentAnnotationTool;
        if (string.IsNullOrWhiteSpace(selectedTool))
        {
            StatusTextBlock.Text = "Select a tool before drawing a new annotation.";
            return;
        }

        var descriptor = CreateAnnotationDescriptor(selectedTool, startPoint, endPoint, null);
        var wasEditingPrimitive = pendingPrimitiveEdit is not null;
        currentProject = ApplyShapeAnnotationChange(freezeIndex, descriptor);
        projectStore.Save(currentProject);
        RefreshProjectLists(freezeIndex);
        RefreshSelectedFreezePrimitiveList();
        UpdateAnnotationOverlay();
        UpdateSelectedFreezeSummary();
        ClearAnnotationToolSelection();
        StatusTextBlock.Text = !wasEditingPrimitive
            ? $"Added {descriptor.Tool.ToLowerInvariant()} annotation to the selected freeze frame. Select a tool to draw another one."
            : $"Replaced the selected primitive with a {descriptor.Tool.ToLowerInvariant()} annotation. Select a tool to draw another one.";
        pendingPrimitiveEdit = null;
    }

    private void OnAnnotationCanvasSizeChanged(object sender, SizeChangedEventArgs e)
    {
        TryMigrateLegacyAnnotationCoordinates();
        UpdateAnnotationOverlay();
    }

    private bool CancelPendingAnnotationInteraction()
    {
        var canceledPreviewDraw = false;
        if (annotationPreviewShape is not null)
        {
            if (AnnotationCanvas.Children.Contains(annotationPreviewShape))
            {
                AnnotationCanvas.Children.Remove(annotationPreviewShape);
            }

            annotationPreviewShape = null;
            annotationStartPoint = null;
            canceledPreviewDraw = true;
        }

        if (AnnotationCanvas.IsMouseCaptured && activeCanvasHandleRole is null)
        {
            AnnotationCanvas.ReleaseMouseCapture();
        }

        var clearedTool = !string.IsNullOrWhiteSpace(currentAnnotationTool) || pendingPrimitiveEdit is not null;
        if (!clearedTool && !canceledPreviewDraw)
        {
            return false;
        }

        pendingPrimitiveEdit = null;
        ClearAnnotationToolSelection();
        StatusTextBlock.Text = "Canceled the pending annotation action. Select a tool to start again.";
        return true;
    }

    private bool TryMigrateLegacyAnnotationCoordinates()
    {
        if (hasMigratedLegacyAnnotationCoordinates || !isMediaReady)
        {
            return false;
        }

        if (AnnotationCanvas.ActualWidth <= 1 || AnnotationCanvas.ActualHeight <= 1)
        {
            return false;
        }

        var migratedProject = currentProject;
        var changed = false;
        for (var freezeIndex = 0; freezeIndex < currentProject.FreezeAnnotations.Length; freezeIndex++)
        {
            var freeze = currentProject.FreezeAnnotations[freezeIndex];
            for (var shapeIndex = 0; shapeIndex < freeze.Shapes.Length; shapeIndex++)
            {
                var descriptor = TryDeserializeDescriptor(freeze.Shapes[shapeIndex]);
                if (descriptor is null || descriptor.UsesVideoViewportCoordinates)
                {
                    continue;
                }

                migratedProject = migratedProject.ReplaceFreezeShapeDescriptor(
                    freezeIndex,
                    shapeIndex,
                    SerializeDescriptor(ConvertLegacyCanvasDescriptorToViewportCoordinates(descriptor)));
                changed = true;
            }

            for (var textIndex = 0; textIndex < freeze.TextAnnotations.Length; textIndex++)
            {
                var descriptor = TryDeserializeDescriptor(freeze.TextAnnotations[textIndex]);
                if (descriptor is null || descriptor.UsesVideoViewportCoordinates)
                {
                    continue;
                }

                migratedProject = migratedProject.ReplaceFreezeTextDescriptor(
                    freezeIndex,
                    textIndex,
                    SerializeDescriptor(ConvertLegacyCanvasDescriptorToViewportCoordinates(descriptor)));
                changed = true;
            }
        }

        hasMigratedLegacyAnnotationCoordinates = true;
        if (!changed)
        {
            return false;
        }

        currentProject = migratedProject;
        projectStore.Save(currentProject);
        pendingPrimitiveEdit = null;
        activeCanvasPrimitive = null;
        activeCanvasHandleRole = null;
        activeCanvasTextDraft = null;
        isActiveCanvasTextEditing = false;
        RefreshSelectedFreezePrimitiveList();
        UpdateSelectedFreezeSummary();
        return true;
    }

    private TimeSpan? TryGetEditorPosition()
    {
        if (!isMediaReady)
        {
            StatusTextBlock.Text = "Wait until playback is ready before adding edit markers.";
            return null;
        }

        return PlayerElement.Position;
    }

    private TimeSpan? TryGetSourceDurationForExport()
    {
        if (PlayerElement.NaturalDuration.HasTimeSpan)
        {
            return PlayerElement.NaturalDuration.TimeSpan;
        }

        if (record.Duration is not null && record.Duration.Value > TimeSpan.Zero)
        {
            return record.Duration.Value;
        }

        StatusTextBlock.Text = "Could not determine the source duration for export. Open the video in the editor first.";
        return null;
    }

    private void RefreshProjectLists(int? selectedFreezeIndex = null)
    {
        var desiredFreezeIndex = selectedFreezeIndex ?? FreezeAnnotationsListBox.SelectedIndex;
        FreezeAnnotationsListBox.ItemsSource = currentProject.FreezeAnnotations
            .Select(item => $"{FormatTimestamp(item.SourceTimestamp)} | Hold {FormatTimestamp(item.HoldDuration)}")
            .DefaultIfEmpty("No freeze markers yet.")
            .ToArray();

        SlowRangesListBox.ItemsSource = currentProject.SlowRanges
            .Select(item => $"{FormatTimestamp(item.Start)} - {FormatTimestamp(item.End)} | {item.SpeedFactor:0.00}x | {item.AudioPolicy}")
            .DefaultIfEmpty("No slow ranges yet.")
            .ToArray();

        if (desiredFreezeIndex >= 0 && desiredFreezeIndex < currentProject.FreezeAnnotations.Length)
        {
            FreezeAnnotationsListBox.SelectedIndex = desiredFreezeIndex;
        }
        else if (currentProject.FreezeAnnotations.Length == 0)
        {
            FreezeAnnotationsListBox.SelectedIndex = -1;
            UpdateAnnotationOverlay();
        }

        if (currentProject.FreezeAnnotations.Length == 0)
        {
            RefreshSelectedFreezePrimitiveList();
            UpdateSelectedFreezeSummary();
        }
    }

    private void RefreshPendingSlowRange()
    {
        SlowRangeStartTextBlock.Text = pendingSlowRangeStart is null
            ? "Not set"
            : FormatTimestamp(pendingSlowRangeStart.Value);
        SlowRangeEndTextBlock.Text = pendingSlowRangeEnd is null
            ? "Not set"
            : FormatTimestamp(pendingSlowRangeEnd.Value);
    }

    private string BuildProjectReadyStatus()
    {
        return $"Playback ready. Project loaded with {currentProject.FreezeAnnotations.Length} freeze annotation(s) and {currentProject.SlowRanges.Length} slow range(s).";
    }

    private bool TryGetSelectedFreezeIndex(out int freezeIndex)
    {
        freezeIndex = FreezeAnnotationsListBox.SelectedIndex;
        if (freezeIndex < 0 || freezeIndex >= currentProject.FreezeAnnotations.Length)
        {
            StatusTextBlock.Text = "Select a freeze marker before annotating the paused frame.";
            return false;
        }

        return true;
    }

    private int FindFreezeAnnotationIndex(TimeSpan sourceTimestamp, TimeSpan holdDuration)
    {
        for (var index = currentProject.FreezeAnnotations.Length - 1; index >= 0; index--)
        {
            var item = currentProject.FreezeAnnotations[index];
            if (item.SourceTimestamp == sourceTimestamp && item.HoldDuration == holdDuration)
            {
                return index;
            }
        }

        return -1;
    }

    private TimeSpan GetSelectedFreezeDuration()
    {
        if (FreezeDurationComboBox.SelectedItem is ComboBoxItem item
            && double.TryParse(item.Tag?.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds)
            && seconds > 0)
        {
            return TimeSpan.FromSeconds(seconds);
        }

        return TimeSpan.FromSeconds(2);
    }

    private double GetSelectedSlowSpeedRatio()
    {
        if (SlowSpeedComboBox.SelectedItem is ComboBoxItem item
            && double.TryParse(item.Tag?.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var ratio)
            && ratio > 0)
        {
            return ratio;
        }

        return 0.5;
    }

    private void SelectFreezeDuration(TimeSpan holdDuration)
    {
        foreach (var entry in FreezeDurationComboBox.Items.OfType<ComboBoxItem>())
        {
            if (!double.TryParse(entry.Tag?.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var candidateSeconds))
            {
                continue;
            }

            if (Math.Abs(candidateSeconds - holdDuration.TotalSeconds) < 0.001)
            {
                FreezeDurationComboBox.SelectedItem = entry;
                return;
            }
        }
    }

    private void SelectSlowSpeedRatio(double ratio)
    {
        foreach (var entry in SlowSpeedComboBox.Items.OfType<ComboBoxItem>())
        {
            if (!double.TryParse(entry.Tag?.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var candidate))
            {
                continue;
            }

            if (Math.Abs(candidate - ratio) < 0.001)
            {
                SlowSpeedComboBox.SelectedItem = entry;
                return;
            }
        }
    }

    private void AddTextAnnotation(int freezeIndex, Point position)
    {
        var text = AnnotationTextTextBox.Text?.Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            StatusTextBlock.Text = "Enter text before placing a text annotation.";
            return;
        }

        var descriptor = CreateAnnotationDescriptor("Text", position, position, text);
        var wasEditingPrimitive = pendingPrimitiveEdit is not null;
        currentProject = ApplyTextAnnotationChange(freezeIndex, descriptor);
        projectStore.Save(currentProject);
        RefreshProjectLists(freezeIndex);
        RefreshSelectedFreezePrimitiveList();
        UpdateAnnotationOverlay();
        UpdateSelectedFreezeSummary();
        ClearAnnotationToolSelection();
        StatusTextBlock.Text = !wasEditingPrimitive
            ? "Added text annotation to the selected freeze frame. Select a tool to place another one."
            : "Replaced the selected text primitive. Select a tool to place another annotation.";
        pendingPrimitiveEdit = null;
    }

    private void UpdateActiveCanvasText(string text)
    {
        if (activeCanvasPrimitive is null || !activeCanvasPrimitive.IsText)
        {
            return;
        }

        if (isUpdatingActiveCanvasTextEditor)
        {
            return;
        }

        activeCanvasTextDraft = text;
        SelectedPrimitiveTextBlock.Text = $"Editing text primitive: {text}";
        StatusTextBlock.Text = "Editing text directly on the canvas. Use Enter for new lines and Ctrl+Enter to finish.";
    }

    private void CommitActiveCanvasTextEdit()
    {
        if (activeCanvasPrimitive is null || !activeCanvasPrimitive.IsText)
        {
            return;
        }

        if (!TryGetSelectedFreezeIndex(out var freezeIndex))
        {
            return;
        }

        var committedText = activeCanvasTextDraft ?? activeCanvasPrimitive.Descriptor.Text ?? string.Empty;
        var descriptor = activeCanvasPrimitive.Descriptor with
        {
            Text = committedText,
        };

        currentProject = currentProject.ReplaceFreezeTextDescriptor(freezeIndex, activeCanvasPrimitive.PrimitiveIndex, SerializeDescriptor(descriptor));
        activeCanvasPrimitive.UpdateDescriptor(descriptor, notifyDisplayText: false);
        SyncMatchingPrimitiveListItem(activeCanvasPrimitive, descriptor, notifyDisplayText: true);

        if (!string.Equals(AnnotationTextTextBox.Text, committedText, StringComparison.Ordinal))
        {
            AnnotationTextTextBox.Text = committedText;
        }

        activeCanvasPrimitive.NotifyDisplayTextChanged();
        projectStore.Save(currentProject);
        UpdateSelectedFreezeSummary();
        StatusTextBlock.Text = "Updated the selected text on the canvas.";
    }

    private bool TryDeleteSelectedPrimitive()
    {
        var freezeIndex = FreezeAnnotationsListBox.SelectedIndex;
        if (freezeIndex < 0 || freezeIndex >= currentProject.FreezeAnnotations.Length)
        {
            return false;
        }

        var item = FreezePrimitiveListBox.SelectedItem as FreezePrimitiveListItem ?? activeCanvasPrimitive;
        if (item is null)
        {
            return false;
        }

        currentProject = item.IsText
            ? currentProject.RemoveFreezeTextDescriptor(freezeIndex, item.PrimitiveIndex)
            : currentProject.RemoveFreezeShapeDescriptor(freezeIndex, item.PrimitiveIndex);

        projectStore.Save(currentProject);
        pendingPrimitiveEdit = null;
        activeCanvasPrimitive = null;
        activeCanvasHandleRole = null;
        activeCanvasTextDraft = null;
        isActiveCanvasTextEditing = false;
        RefreshSelectedFreezePrimitiveList();
        FreezePrimitiveListBox.SelectedIndex = -1;
        UpdateAnnotationOverlay();
        UpdateSelectedFreezeSummary();
        StatusTextBlock.Text = "Deleted the selected primitive from the freeze frame.";
        return true;
    }

    private Il2VideoEditProject ApplyShapeAnnotationChange(int freezeIndex, Il2VideoAnnotationDescriptor descriptor)
    {
        var serialized = SerializeDescriptor(descriptor);
        if (pendingPrimitiveEdit is null)
        {
            return currentProject.AddFreezeShapeDescriptor(freezeIndex, serialized);
        }

        if (pendingPrimitiveEdit.IsText)
        {
            throw new InvalidOperationException("The selected primitive is text and cannot be replaced with a shape directly.");
        }

        return currentProject.ReplaceFreezeShapeDescriptor(freezeIndex, pendingPrimitiveEdit.PrimitiveIndex, serialized);
    }

    private Il2VideoEditProject ApplyTextAnnotationChange(int freezeIndex, Il2VideoAnnotationDescriptor descriptor)
    {
        var serialized = SerializeDescriptor(descriptor);
        if (pendingPrimitiveEdit is null)
        {
            return currentProject.AddFreezeTextDescriptor(freezeIndex, serialized);
        }

        if (!pendingPrimitiveEdit.IsText)
        {
            throw new InvalidOperationException("The selected primitive is a shape and cannot be replaced with text directly.");
        }

        return currentProject.ReplaceFreezeTextDescriptor(freezeIndex, pendingPrimitiveEdit.PrimitiveIndex, serialized);
    }

    private void RefreshSelectedFreezePrimitiveList()
    {
        var desiredIndex = FreezePrimitiveListBox.SelectedIndex;

        if (FreezeAnnotationsListBox.SelectedIndex < 0 || FreezeAnnotationsListBox.SelectedIndex >= currentProject.FreezeAnnotations.Length)
        {
            FreezePrimitiveListBox.ItemsSource = Array.Empty<FreezePrimitiveListItem>();
            FreezePrimitiveListBox.SelectedIndex = -1;
            SelectedPrimitiveTextBlock.Text = "Select a primitive to edit or delete it.";
            return;
        }

        var freeze = currentProject.FreezeAnnotations[FreezeAnnotationsListBox.SelectedIndex];
        var items = freeze.Shapes
            .Select(TryDeserializeDescriptor)
            .Select((descriptor, index) => descriptor is null
                ? null
                : new FreezePrimitiveListItem(false, index, descriptor))
            .Concat(
                freeze.TextAnnotations
                    .Select(TryDeserializeDescriptor)
                    .Select((descriptor, index) => descriptor is null
                        ? null
                        : new FreezePrimitiveListItem(true, index, descriptor)))
            .OfType<FreezePrimitiveListItem>()
            .ToArray();

        FreezePrimitiveListBox.ItemsSource = items;
        if (items.Length == 0)
        {
            activeCanvasPrimitive = null;
            FreezePrimitiveListBox.SelectedIndex = -1;
            SelectedPrimitiveTextBlock.Text = "No saved primitives on this freeze frame yet.";
            return;
        }

        FreezePrimitiveListBox.SelectedIndex = Math.Clamp(desiredIndex, 0, items.Length - 1);
    }

    private void SelectAnnotationTool(string tool)
    {
        switch (tool)
        {
            case "Line":
                LineToolRadioButton.IsChecked = true;
                break;
            case "Rectangle":
                RectangleToolRadioButton.IsChecked = true;
                break;
            case "Circle":
                EllipseToolRadioButton.IsChecked = true;
                break;
            case "Text":
                TextToolRadioButton.IsChecked = true;
                break;
        }
    }

    private void ClearAnnotationToolSelection()
    {
        currentAnnotationTool = null;
        LineToolRadioButton.IsChecked = false;
        RectangleToolRadioButton.IsChecked = false;
        EllipseToolRadioButton.IsChecked = false;
        TextToolRadioButton.IsChecked = false;
    }

    private void UpdateAnnotationOverlay()
    {
        AnnotationCanvas.Children.Clear();

        var freezeIndex = GetOverlayFreezeIndex();
        if (freezeIndex < 0)
        {
            RemoveActiveCanvasTextEditor();
            return;
        }

        if (freezeIndex >= currentProject.FreezeAnnotations.Length)
        {
            RemoveActiveCanvasTextEditor();
            return;
        }

        if (AnnotationCanvas.ActualWidth <= 1 || AnnotationCanvas.ActualHeight <= 1)
        {
            RemoveActiveCanvasTextEditor();
            return;
        }

        var freeze = currentProject.FreezeAnnotations[freezeIndex];
        foreach (var primitive in freeze.Shapes
                     .Select(TryDeserializeDescriptor)
                     .Select((descriptor, index) => descriptor is null ? null : new FreezePrimitiveListItem(false, index, descriptor))
                     .OfType<FreezePrimitiveListItem>())
        {
            var element = CreateCanvasElement(primitive);
            if (element is not null)
            {
                AnnotationCanvas.Children.Add(element);
            }
        }

        foreach (var primitive in freeze.TextAnnotations
                     .Select(TryDeserializeDescriptor)
                     .Select((descriptor, index) => descriptor is null ? null : new FreezePrimitiveListItem(true, index, descriptor))
                     .OfType<FreezePrimitiveListItem>())
        {
            var element = CreateCanvasElement(primitive);
            if (element is not null)
            {
                AnnotationCanvas.Children.Add(element);
            }
        }

        if (activeCanvasPrimitive is not null && !activeCanvasPrimitive.IsText && string.Equals(activeCanvasPrimitive.Descriptor.Tool, "Line", StringComparison.Ordinal))
        {
            AddActivePrimitiveHandles(activeCanvasPrimitive);
        }
        else if (activeCanvasPrimitive is not null)
        {
            AddActivePrimitiveHandles(activeCanvasPrimitive);
        }

        UpdateActiveCanvasTextEditor();
    }

    private int GetOverlayFreezeIndex()
    {
        if (activePreviewFreezeIndex >= 0)
        {
            return activePreviewFreezeIndex;
        }

        return positionTimer.IsEnabled
            ? -1
            : FreezeAnnotationsListBox.SelectedIndex;
    }

    private void ResetPlaybackPreviewState(TimeSpan position)
    {
        freezeHoldTimer.Stop();
        activePreviewFreezeIndex = -1;
        nextFreezePreviewIndex = previewPlanner.GetNextFreezeIndex(currentProject, position);
        UpdateAnnotationOverlay();
    }

    private bool TryStartFreezePreview(TimeSpan position)
    {
        var freeze = previewPlanner.GetFreezeToTrigger(currentProject, nextFreezePreviewIndex, position, FreezeTriggerTolerance);
        if (freeze is null)
        {
            return false;
        }

        activePreviewFreezeIndex = nextFreezePreviewIndex;
        nextFreezePreviewIndex++;
        positionTimer.Stop();
        PlayerElement.Pause();
        PlayerElement.Position = freeze.SourceTimestamp;
        UpdatePositionDisplay(freeze.SourceTimestamp);
        FreezeAnnotationsListBox.SelectedIndex = activePreviewFreezeIndex;
        UpdateAnnotationOverlay();
        SetViewportStatus($"Holding frame for {FormatTimestamp(freeze.HoldDuration)}");
        StatusTextBlock.Text = $"Holding freeze frame at {FormatTimestamp(freeze.SourceTimestamp)} for {FormatTimestamp(freeze.HoldDuration)}.";
        freezeHoldTimer.Interval = freeze.HoldDuration;
        freezeHoldTimer.Start();
        return true;
    }

    private void OnFreezeHoldTimerTick(object? sender, EventArgs e)
    {
        freezeHoldTimer.Stop();

        if (!isMediaReady)
        {
            activePreviewFreezeIndex = -1;
            UpdateAnnotationOverlay();
            return;
        }

        activePreviewFreezeIndex = -1;
        HideViewportStatus();
        ApplyPlaybackSpeedForPosition(PlayerElement.Position, updateStatus: false);
        PlayerElement.Play();
        positionTimer.Start();
        SetPlaybackActive(true);
        UpdateAnnotationOverlay();
        StatusTextBlock.Text = BuildPlaybackStatus(PlayerElement.Position);
    }

    private void ApplyPlaybackSpeedForPosition(TimeSpan position, bool updateStatus)
    {
        if (!isMediaReady || freezeHoldTimer.IsEnabled)
        {
            return;
        }

        var effectiveRatio = previewPlanner.GetEffectiveSpeedRatio(currentProject, position, basePreviewSpeedRatio);
        PlayerElement.SpeedRatio = effectiveRatio;

        if (updateStatus)
        {
            StatusTextBlock.Text = BuildPlaybackStatus(position);
        }
    }

    private string BuildPlaybackStatus(TimeSpan position)
    {
        var activeSlowRange = previewPlanner.GetActiveSlowRange(currentProject, position);
        if (activeSlowRange is not null)
        {
            return $"Playing slow-range preview at {activeSlowRange.SpeedFactor:0.00}x between {FormatTimestamp(activeSlowRange.Start)} and {FormatTimestamp(activeSlowRange.End)}. MediaElement audio preview may be limited away from 1.00x.";
        }

        return Math.Abs(basePreviewSpeedRatio - 1.0) < 0.001
            ? "Playing at 1.00x preview speed."
            : $"Playing at {basePreviewSpeedRatio:0.00}x preview speed. MediaElement audio preview may be limited away from 1.00x.";
    }

    private Shape? CreatePreviewShape(string tool)
    {
        return tool switch
        {
            "Line" => new Line
            {
                Stroke = CreateStrokeBrush(AnnotationStrokeHex),
                StrokeThickness = AnnotationStrokeThickness,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
            },
            "Rectangle" => new Rectangle
            {
                Stroke = CreateStrokeBrush(AnnotationStrokeHex),
                StrokeThickness = AnnotationStrokeThickness,
            },
            "Circle" => new Ellipse
            {
                Stroke = CreateStrokeBrush(AnnotationStrokeHex),
                StrokeThickness = AnnotationStrokeThickness,
            },
            _ => null,
        };
    }

    private void UpdatePreviewShape(Shape shape, Point startPoint, Point endPoint)
    {
        switch (shape)
        {
            case Line line:
                line.X1 = startPoint.X;
                line.Y1 = startPoint.Y;
                line.X2 = endPoint.X;
                line.Y2 = endPoint.Y;
                break;

            case Rectangle rectangle:
                Canvas.SetLeft(rectangle, Math.Min(startPoint.X, endPoint.X));
                Canvas.SetTop(rectangle, Math.Min(startPoint.Y, endPoint.Y));
                rectangle.Width = Math.Abs(endPoint.X - startPoint.X);
                rectangle.Height = Math.Abs(endPoint.Y - startPoint.Y);
                break;

            case Ellipse ellipse:
                Canvas.SetLeft(ellipse, Math.Min(startPoint.X, endPoint.X));
                Canvas.SetTop(ellipse, Math.Min(startPoint.Y, endPoint.Y));
                ellipse.Width = Math.Abs(endPoint.X - startPoint.X);
                ellipse.Height = Math.Abs(endPoint.Y - startPoint.Y);
                break;
        }
    }

    private Il2VideoAnnotationDescriptor CreateAnnotationDescriptor(string tool, Point startPoint, Point endPoint, string? text)
    {
        var normalizedStart = NormalizeCanvasPointToAnnotationViewport(startPoint);
        var normalizedEnd = NormalizeCanvasPointToAnnotationViewport(endPoint);
        return new Il2VideoAnnotationDescriptor(
            tool,
            normalizedStart.X,
            normalizedStart.Y,
            normalizedEnd.X,
            normalizedEnd.Y,
            text,
            AnnotationStrokeHex,
            AnnotationStrokeThickness,
            Il2VideoAnnotationDescriptor.VideoViewportCoordinateSpace);
    }

    private string SerializeDescriptor(Il2VideoAnnotationDescriptor descriptor)
    {
        return JsonSerializer.Serialize(descriptor);
    }

    private Il2VideoAnnotationDescriptor? TryDeserializeDescriptor(string serialized)
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

    private UIElement? CreateCanvasElement(FreezePrimitiveListItem primitive)
    {
        var descriptor = primitive.Descriptor;
        var startPoint = GetCanvasPointForDescriptor(descriptor, isStartPoint: true);
        var endPoint = GetCanvasPointForDescriptor(descriptor, isStartPoint: false);
        var isActivePrimitive = primitiveMatchesActive(primitive);

        UIElement? element = descriptor.Tool switch
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
            "Text" when isActivePrimitive && primitive.IsText && isActiveCanvasTextEditing => null,
            "Text" => CreateTextElement(startPoint, descriptor),
            _ => null,
        };

        if (element is null)
        {
            return null;
        }

        element.MouseLeftButtonDown += (_, eventArgs) => OnCanvasPrimitiveClicked(primitive, eventArgs);

        if (isActivePrimitive)
        {
            switch (element)
            {
                case Shape shape:
                    shape.StrokeThickness = descriptor.StrokeThickness + 1.5;
                    break;
                case Border border:
                    border.BorderBrush = Brushes.Gold;
                    border.BorderThickness = new Thickness(2);
                    break;
                case TextBox textBox:
                    textBox.BorderBrush = Brushes.Gold;
                    textBox.BorderThickness = new Thickness(2);
                    break;
            }
        }

        return element;

        bool primitiveMatchesActive(FreezePrimitiveListItem candidate)
        {
            return activeCanvasPrimitive is not null
                && activeCanvasPrimitive.IsText == candidate.IsText
                && activeCanvasPrimitive.PrimitiveIndex == candidate.PrimitiveIndex;
        }
    }

    private void OnCanvasPrimitiveClicked(FreezePrimitiveListItem primitive, MouseButtonEventArgs e)
    {
        var clickedSelectedTextContent = primitive.IsText
            && activeCanvasPrimitive is not null
            && activeCanvasPrimitive.IsText == primitive.IsText
            && activeCanvasPrimitive.PrimitiveIndex == primitive.PrimitiveIndex
            && e.OriginalSource is TextBlock;

        activeCanvasPrimitive = primitive;
        activeCanvasHandleRole = null;
        isActiveCanvasTextEditing = clickedSelectedTextContent;
        activeCanvasTextDraft = primitive.IsText ? primitive.Descriptor.Text ?? string.Empty : null;
        FreezePrimitiveListBox.SelectedItem = FindPrimitiveListMatch(primitive);
        UpdateAnnotationOverlay();
        SelectedPrimitiveTextBlock.Text = primitive.IsText
            ? clickedSelectedTextContent
                ? $"Editing text primitive: {primitive.Descriptor.Text ?? string.Empty}"
                : $"Selected text primitive: {primitive.Descriptor.Text ?? string.Empty}. Click the text to edit, or press Delete to remove it."
            : $"Selected {primitive.Descriptor.Tool.ToLowerInvariant()} primitive on the canvas.";
        e.Handled = true;
    }

    private object? FindPrimitiveListMatch(FreezePrimitiveListItem primitive)
    {
        return FreezePrimitiveListBox.Items
            .OfType<FreezePrimitiveListItem>()
            .FirstOrDefault(item => item.IsText == primitive.IsText && item.PrimitiveIndex == primitive.PrimitiveIndex);
    }

    private void AddActivePrimitiveHandles(FreezePrimitiveListItem primitive)
    {
        var descriptor = primitive.Descriptor;
        var startPoint = GetCanvasPointForDescriptor(descriptor, isStartPoint: true);
        var endPoint = GetCanvasPointForDescriptor(descriptor, isStartPoint: false);

        switch (descriptor.Tool)
        {
            case "Line":
            case "Rectangle":
            case "Circle":
                AnnotationCanvas.Children.Add(CreateCanvasHandle(startPoint, "start", Cursors.SizeAll));
                AnnotationCanvas.Children.Add(CreateCanvasHandle(endPoint, "end", Cursors.SizeAll));
                break;

            case "Text":
                AnnotationCanvas.Children.Add(CreateCanvasHandle(
                    new Point(Math.Max(0, startPoint.X - LineHandleSize - 4), Math.Max(0, startPoint.Y - LineHandleSize - 4)),
                    "move",
                    Cursors.SizeAll));
                break;
        }
    }

    private Rectangle CreateCanvasHandle(Point center, string role, Cursor cursor)
    {
        var handle = new Rectangle
        {
            Width = LineHandleSize,
            Height = LineHandleSize,
            Fill = Brushes.White,
            Stroke = Brushes.Gold,
            StrokeThickness = 2,
            Cursor = cursor,
            Tag = role,
        };

        Canvas.SetLeft(handle, center.X - (LineHandleSize / 2));
        Canvas.SetTop(handle, center.Y - (LineHandleSize / 2));
        handle.MouseLeftButtonDown += (_, e) => OnCanvasHandleMouseLeftButtonDown(role, e);
        return handle;
    }

    private void OnCanvasHandleMouseLeftButtonDown(string role, MouseButtonEventArgs e)
    {
        if (activeCanvasPrimitive is null)
        {
            return;
        }

        if (positionTimer.IsEnabled)
        {
            StatusTextBlock.Text = "Pause playback before editing a primitive on the canvas.";
            return;
        }

        activeCanvasHandleRole = role;
        AnnotationCanvas.CaptureMouse();
        StatusTextBlock.Text = BuildCanvasHandleStatus(activeCanvasPrimitive.Descriptor.Tool, role);
        e.Handled = true;
    }

    private void UpdateActiveCanvasHandle(Point canvasPoint)
    {
        if (activeCanvasPrimitive is null || activeCanvasHandleRole is null)
        {
            return;
        }

        if (!TryGetSelectedFreezeIndex(out var freezeIndex))
        {
            return;
        }

        var descriptor = UpdateDescriptorForCanvasHandle(activeCanvasPrimitive.Descriptor, activeCanvasHandleRole, canvasPoint);

        currentProject = activeCanvasPrimitive.IsText
            ? currentProject.ReplaceFreezeTextDescriptor(freezeIndex, activeCanvasPrimitive.PrimitiveIndex, SerializeDescriptor(descriptor))
            : currentProject.ReplaceFreezeShapeDescriptor(freezeIndex, activeCanvasPrimitive.PrimitiveIndex, SerializeDescriptor(descriptor));
        activeCanvasPrimitive.UpdateDescriptor(descriptor);
        SyncMatchingPrimitiveListItem(activeCanvasPrimitive, descriptor, notifyDisplayText: activeCanvasPrimitive.IsText);
        UpdateAnnotationOverlay();
        SelectedPrimitiveTextBlock.Text = BuildCanvasHandleStatus(descriptor.Tool, activeCanvasHandleRole);
    }

    private void SyncMatchingPrimitiveListItem(FreezePrimitiveListItem sourcePrimitive, Il2VideoAnnotationDescriptor descriptor, bool notifyDisplayText)
    {
        var matchingListItem = FindPrimitiveListMatch(sourcePrimitive) as FreezePrimitiveListItem;
        if (matchingListItem is null || ReferenceEquals(matchingListItem, sourcePrimitive))
        {
            return;
        }

        matchingListItem.UpdateDescriptor(descriptor, notifyDisplayText);
    }

    private Il2VideoAnnotationDescriptor UpdateDescriptorForCanvasHandle(Il2VideoAnnotationDescriptor descriptor, string role, Point canvasPoint)
    {
        var normalizedPoint = NormalizeCanvasPointToAnnotationViewport(canvasPoint);
        var normalizedX = normalizedPoint.X;
        var normalizedY = normalizedPoint.Y;

        return descriptor.Tool switch
        {
            "Line" or "Rectangle" or "Circle" => descriptor with
            {
                StartX = role == "start" ? normalizedX : descriptor.StartX,
                StartY = role == "start" ? normalizedY : descriptor.StartY,
                EndX = role == "end" ? normalizedX : descriptor.EndX,
                EndY = role == "end" ? normalizedY : descriptor.EndY,
                CoordinateSpace = Il2VideoAnnotationDescriptor.VideoViewportCoordinateSpace,
            },
            "Text" => descriptor with
            {
                StartX = normalizedX,
                StartY = normalizedY,
                EndX = normalizedX,
                EndY = normalizedY,
                CoordinateSpace = Il2VideoAnnotationDescriptor.VideoViewportCoordinateSpace,
            },
            _ => descriptor,
        };
    }

    private static string BuildCanvasHandleStatus(string tool, string? role)
    {
        return tool switch
        {
            "Line" when role == "start" => "Dragging the line start point.",
            "Line" when role == "end" => "Dragging the line end point.",
            "Line" => "Updated the selected line on the canvas.",
            "Rectangle" when role == "start" => "Dragging the rectangle first corner.",
            "Rectangle" when role == "end" => "Dragging the rectangle opposite corner.",
            "Rectangle" => "Updated the selected rectangle on the canvas.",
            "Circle" when role == "start" => "Dragging the ellipse first corner.",
            "Circle" when role == "end" => "Dragging the ellipse opposite corner.",
            "Circle" => "Updated the selected ellipse on the canvas.",
            "Text" when role == "move" => "Dragging the text position.",
            "Text" => "Updated the selected text on the canvas.",
            _ => "Updated the selected primitive on the canvas.",
        };
    }

    private Rectangle CreateRectangle(Point startPoint, Point endPoint, Il2VideoAnnotationDescriptor descriptor)
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

    private Ellipse CreateEllipse(Point startPoint, Point endPoint, Il2VideoAnnotationDescriptor descriptor)
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

    private Border CreateTextElement(Point startPoint, Il2VideoAnnotationDescriptor descriptor)
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
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 260,
            },
        };
        Canvas.SetLeft(border, startPoint.X);
        Canvas.SetTop(border, startPoint.Y);
        return border;
    }

    private void UpdateActiveCanvasTextEditor()
    {
        if (activeCanvasPrimitive is null || !activeCanvasPrimitive.IsText || !isActiveCanvasTextEditing)
        {
            RemoveActiveCanvasTextEditor();
            return;
        }

        var descriptor = activeCanvasPrimitive.Descriptor;
        var startPoint = GetCanvasPointForDescriptor(descriptor, isStartPoint: true);
        var created = false;

        if (activeCanvasTextEditor is null)
        {
            activeCanvasTextEditor = new TextBox
            {
                Background = new SolidColorBrush(Color.FromArgb(180, 8, 17, 30)),
                Foreground = Brushes.White,
                FontSize = 18,
                FontWeight = FontWeights.SemiBold,
                Padding = new Thickness(6, 3, 6, 3),
                BorderBrush = Brushes.Gold,
                BorderThickness = new Thickness(2),
                MinWidth = 180,
                MinHeight = 72,
                MaxWidth = 320,
                AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            };
            activeCanvasTextEditor.TextChanged += (_, _) => UpdateActiveCanvasText(activeCanvasTextEditor.Text);
            activeCanvasTextEditor.LostFocus += (_, _) => ExitActiveCanvasTextEditMode(commitChanges: true);
            activeCanvasTextEditor.KeyDown += (_, e) =>
            {
                if (e.Key != Key.Enter || (Keyboard.Modifiers & ModifierKeys.Control) == 0)
                {
                    return;
                }

                ExitActiveCanvasTextEditMode(commitChanges: true);
                Focus();
                e.Handled = true;
            };
            AnnotationEditorLayer.Children.Add(activeCanvasTextEditor);
            created = true;
        }

        isUpdatingActiveCanvasTextEditor = true;
        var desiredText = activeCanvasTextDraft ?? descriptor.Text ?? string.Empty;
        if (!string.Equals(activeCanvasTextEditor.Text, desiredText, StringComparison.Ordinal))
        {
            activeCanvasTextEditor.Text = desiredText;
        }
        Canvas.SetLeft(activeCanvasTextEditor, startPoint.X);
        Canvas.SetTop(activeCanvasTextEditor, startPoint.Y);
        isUpdatingActiveCanvasTextEditor = false;

        if (created)
        {
            activeCanvasTextEditor.Focus();
            activeCanvasTextEditor.CaretIndex = activeCanvasTextEditor.Text.Length;
        }
    }

    private void ExitActiveCanvasTextEditMode(bool commitChanges)
    {
        if (!isActiveCanvasTextEditing)
        {
            return;
        }

        if (commitChanges)
        {
            CommitActiveCanvasTextEdit();
        }

        isActiveCanvasTextEditing = false;
        UpdateAnnotationOverlay();
        if (activeCanvasPrimitive is not null && activeCanvasPrimitive.IsText)
        {
            SelectedPrimitiveTextBlock.Text = $"Selected text primitive: {activeCanvasPrimitive.Descriptor.Text ?? string.Empty}. Click the text to edit, or press Delete to remove it.";
        }
    }

    private void RemoveActiveCanvasTextEditor()
    {
        if (activeCanvasTextEditor is null)
        {
            return;
        }

        AnnotationEditorLayer.Children.Remove(activeCanvasTextEditor);
        activeCanvasTextEditor = null;
        isUpdatingActiveCanvasTextEditor = false;
    }

    private void SetPlaybackActive(bool isActive)
    {
        isPlaybackActive = isActive;
        if (PlayPauseButton is not null)
        {
            PlayPauseButton.Content = isPlaybackActive ? "Pause" : "Play";
        }
    }

    private Rect GetAnnotationViewportRect()
    {
        var sourceWidth = isMediaReady ? PlayerElement.NaturalVideoWidth : 0;
        var sourceHeight = isMediaReady ? PlayerElement.NaturalVideoHeight : 0;
        return Il2VideoViewportMapper.GetViewportRect(AnnotationCanvas.ActualWidth, AnnotationCanvas.ActualHeight, sourceWidth, sourceHeight);
    }

    private Point ClampCanvasPointToAnnotationViewport(Point canvasPoint)
    {
        return Il2VideoViewportMapper.ClampPointToViewport(canvasPoint, GetAnnotationViewportRect());
    }

    private Point NormalizeCanvasPointToAnnotationViewport(Point canvasPoint)
    {
        return Il2VideoViewportMapper.NormalizePoint(canvasPoint, GetAnnotationViewportRect());
    }

    private Point GetCanvasPointForDescriptor(Il2VideoAnnotationDescriptor descriptor, bool isStartPoint)
    {
        var x = isStartPoint ? descriptor.StartX : descriptor.EndX;
        var y = isStartPoint ? descriptor.StartY : descriptor.EndY;
        if (descriptor.UsesVideoViewportCoordinates)
        {
            return Il2VideoViewportMapper.ToCanvasPoint(new Point(x, y), GetAnnotationViewportRect());
        }

        return new Point(x * AnnotationCanvas.ActualWidth, y * AnnotationCanvas.ActualHeight);
    }

    private Il2VideoAnnotationDescriptor ConvertLegacyCanvasDescriptorToViewportCoordinates(Il2VideoAnnotationDescriptor descriptor)
    {
        var legacyStart = new Point(descriptor.StartX * AnnotationCanvas.ActualWidth, descriptor.StartY * AnnotationCanvas.ActualHeight);
        var legacyEnd = new Point(descriptor.EndX * AnnotationCanvas.ActualWidth, descriptor.EndY * AnnotationCanvas.ActualHeight);
        var normalizedStart = NormalizeCanvasPointToAnnotationViewport(legacyStart);
        var normalizedEnd = NormalizeCanvasPointToAnnotationViewport(legacyEnd);
        return descriptor with
        {
            StartX = normalizedStart.X,
            StartY = normalizedStart.Y,
            EndX = normalizedEnd.X,
            EndY = normalizedEnd.Y,
            CoordinateSpace = Il2VideoAnnotationDescriptor.VideoViewportCoordinateSpace,
        };
    }

    private Brush CreateStrokeBrush(string strokeHex)
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

    private static string BuildMetaLine(Il2RawVideoRecord record)
    {
        var parts = new List<string>();

        if (!string.IsNullOrWhiteSpace(record.ChannelTitle))
        {
            parts.Add(record.ChannelTitle);
        }

        if (record.Duration.HasValue)
        {
            parts.Add(FormatTimestamp(record.Duration.Value));
        }

        parts.Add(FormatFileSize(record.FileSizeBytes));
        parts.Add($"Downloaded {record.DownloadedAtUtc.LocalDateTime:yyyy-MM-dd HH:mm}");
        return string.Join(" | ", parts);
    }

    private static string FormatTimestamp(TimeSpan value)
    {
        return value.TotalHours >= 1
            ? $"{(int)value.TotalHours:00}:{value.Minutes:00}:{value.Seconds:00}"
            : $"{value.Minutes:00}:{value.Seconds:00}";
    }

    private static string FormatFileSize(long bytes)
    {
        const double bytesPerMegabyte = 1024 * 1024;
        const double bytesPerGigabyte = bytesPerMegabyte * 1024;

        return bytes >= bytesPerGigabyte
            ? $"{bytes / bytesPerGigabyte:0.00} GB"
            : $"{bytes / bytesPerMegabyte:0.0} MB";
    }

    private sealed class FreezePrimitiveListItem : INotifyPropertyChanged
    {
        private Il2VideoAnnotationDescriptor descriptor;

        public FreezePrimitiveListItem(bool isText, int primitiveIndex, Il2VideoAnnotationDescriptor descriptor)
        {
            IsText = isText;
            PrimitiveIndex = primitiveIndex;
            this.descriptor = descriptor;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public bool IsText { get; }

        public int PrimitiveIndex { get; }

        public Il2VideoAnnotationDescriptor Descriptor => descriptor;

        public string DisplayText => IsText
            ? $"Text: {(Descriptor.Text ?? string.Empty).ReplaceLineEndings(" / ")}"
            : $"{Descriptor.Tool}: {FormatPoint(Descriptor.StartX, Descriptor.StartY)} -> {FormatPoint(Descriptor.EndX, Descriptor.EndY)}";

        public void UpdateDescriptor(Il2VideoAnnotationDescriptor updatedDescriptor, bool notifyDisplayText = true)
        {
            descriptor = updatedDescriptor;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Descriptor)));

            if (notifyDisplayText)
            {
                NotifyDisplayTextChanged();
            }
        }

        public void NotifyDisplayTextChanged()
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DisplayText)));
        }

        private static string FormatPoint(double x, double y)
        {
            return $"({x:0.00}, {y:0.00})";
        }
    }
}