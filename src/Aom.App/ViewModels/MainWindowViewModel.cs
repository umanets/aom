using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using Aom.App.Services.Ai;
using Aom.App.Services.Il2;
using Aom.App.Services.Input;
using Aom.App.Services.OpenTrack;
using Aom.App.Services.Overlay;
using Aom.App.Services.Output;
using Aom.App.Services.Runtime;
using Aom.App.Services.Settings;
using Aom.App.Services.TrackIr;
using Aom.App.Services.Videos;
using Aom.Core.Bindings;
using Aom.Core.Presets;
using Aom.Core.Runtime;
using Aom.Core.Tuning;

namespace Aom.App.ViewModels;

public sealed class MainWindowViewModel : INotifyPropertyChanged
{
    private const double LearnAxisRangeThresholdNormalized = 0.18;
    private readonly TimeSpan il2TelemetryStaleThreshold = TimeSpan.FromMilliseconds(500);
    private readonly TimeSpan trackIrStaleThreshold = TimeSpan.FromMilliseconds(100);
    private readonly TimeSpan uiRefreshInterval = TimeSpan.FromMilliseconds(33);
    private const string CustomPresetActionIdPrefix = "preset.custom.";
    private static readonly Dictionary<string, string> BuiltInPresetActionIds = new(StringComparer.Ordinal)
    {
        ["reset"] = BindingActionIds.ResetPreset,
        ["lagg"] = BindingActionIds.LaggPreset,
        ["f4"] = BindingActionIds.F4Preset,
        ["yakodin"] = BindingActionIds.YakodinPreset,
        ["yakodin-and-b"] = BindingActionIds.YakodinAndBPreset,
        ["la-five"] = BindingActionIds.LaFivePreset,
    };
    private static readonly Dictionary<string, string> DefaultPresetSelectionTriggers = new(StringComparer.Ordinal)
    {
        ["reset"] = "UpArrow + NumPad0",
        ["lagg"] = "UpArrow + NumPad1",
        ["f4"] = "UpArrow + NumPad2",
        ["yakodin"] = "UpArrow + NumPad3",
        ["yakodin-and-b"] = "UpArrow + NumPad4",
        ["la-five"] = string.Empty,
    };
    private readonly TimeSpan telemetryInterval = TimeSpan.FromMilliseconds(8);
    private readonly JoystickDiscoverySummary noJoystickSummary = new("No DirectInput joystick devices were detected during startup.");
    private readonly PoseCalculator poseCalculator = new();
    private readonly CheckSixNotifierController checkSixNotifierController = new();
    private readonly FlapAutomationController flapAutomationController = new();
    private readonly TuneModeController tuneModeController = new();
    private readonly TuneModeRuntimePlanner tuneModeRuntimePlanner = new();
    private readonly RuntimeViewStateReducer runtimeViewStateReducer = new();
    private readonly RuntimeSyncPlanner runtimeSyncPlanner = new();
    private readonly TrackIrDiagnosticsService trackIrDiagnosticsService = new();
    private readonly DirectInputJoystickDiscoveryService joystickDiscoveryService = new();
    private readonly LowLevelKeyboardCaptureService keyboardCaptureService = new();
    private readonly LearnAxisCaptureFilter learnAxisCaptureFilter = new(LearnAxisRangeThresholdNormalized);
    private readonly KeyboardOutputService keyboardOutputService = new();
    private readonly NotificationOutputService notificationOutputService = new();
    private readonly BindingInputResolver bindingInputResolver = new();
    private readonly OpenAiApiKeyResolver openAiApiKeyResolver = new();
    private readonly OpenAiWingmanService openAiWingmanService = new();
    private readonly Il2WindowCaptureService il2WindowCaptureService = new();
    private readonly PushToTalkAudioCaptureService aiPartnerAudioCaptureService = new();
    private readonly AiPartnerDiagnosticsLog aiPartnerDiagnosticsLog = new();
    private readonly JsonAppSettingsStore settingsStore = new();
    private readonly Il2RawVideoLibraryViewModel il2RawVideoLibrary;
    private MicrophoneLoopbackService? microphoneLoopbackService;
    private readonly RelayCommand<BindingEntryViewModel> beginLearnModeCommand;
    private readonly RelayCommand<object> cancelLearnModeCommand;
    private readonly RelayCommand<object> toggleOverlayCommand;
    private readonly RelayCommand<object> toggleUdpStreamingCommand;
    private readonly Il2TelemetryReceiver? il2TelemetryReceiver;
    private readonly OverlaySharedStateWriter? overlayStateWriter;
    private readonly object udpSync = new();
    private List<BindingGroupViewModel> bindingGroups = new();
    private List<PresetListItemViewModel> presetItems = new();
    private CameraPreset selectedPreset = PresetCatalog.Default;
    private readonly TrackIrInputProfile trackIrInputProfile;
    private PresetListItemViewModel? selectedPresetItem;
    private CameraPreset? tunedPreset;
    private TrackIrProbeResult trackIrProbe = TrackIrProbeResult.NotFound(Array.Empty<string>());
    private JoystickDiscoverySummary joystickSummary;
    private TrackIrLiveSession? trackIrSession;
    private HighResolutionTimerScope? highResolutionTimerScope;
    private CancellationTokenSource? telemetryCancellation;
    private Task? telemetryTask;
    private DateTimeOffset lastIl2TelemetryFrameAt = DateTimeOffset.MinValue;
    private DateTimeOffset lastUiRefreshAt = DateTimeOffset.MinValue;
    private DateTimeOffset lastTrackIrFrameAt = DateTimeOffset.MinValue;
    private DateTimeOffset trackIrRateWindowStartedAt = DateTimeOffset.MinValue;
    private DateTimeOffset udpRateWindowStartedAt = DateTimeOffset.MinValue;
    private float? lastIl2SpeedMetersPerSecond;
    private float? lastIl2FlapsPosition;
    private string il2TelemetryStatus = "waiting on UDP 4322";
    private string il2SpeedSummary = "Speed: waiting for IL-2";
    private string il2FlapsSummary = "Flaps: waiting for IL-2";
    private string liveTrackIrStatus = "idle";
    private string liveTrackIrPoseSummary = "Telemetry not started.";
    private string liveTrackIrTimestamp = "No live frame yet.";
    private int trackIrFreshFramesInWindow;
    private int udpPacketsInWindow;
    private double trackIrFreshRateHz;
    private double udpSendRateHz;
    private IReadOnlyList<JoystickLiveState> liveJoystickStates = Array.Empty<JoystickLiveState>();
    private BindingEntryViewModel? activeLearningBinding;
    private bool activeLearningAllowsAxisCapture;
    private string learnModeStatus = "Select any binding row and press Learn to capture the next joystick button or major axis move.";
    private OpenTrackUdpSender? udpSender;
    private string udpStreamingStatus = "stopped";
    private string udpPacketSummary = "UDP streaming is currently off.";
    private string udpLastSentAt = "No packets sent yet.";
    private long udpPacketCount;
    private HeadPose lastTrackIrPose;
    private bool hasTrackIrPose;
    private double runtimeYOffset;
    private double lastStickY = 500;
    private double trackIrYawMultiplier = TrackIrScaling.DefaultYawMultiplier;
    private int selectedMainTabIndex;
    private bool isAiPartnerEnabled;
    private bool isAiPartnerMicrophoneTestModeEnabled;
    private string aiPartnerBriefing = string.Empty;
    private int aiPartnerScreenshotCadenceSeconds = 3;
    private readonly OpenAiApiKeyResolution openAiApiKeyResolution;
    private string aiPartnerApiStatus = "OpenAI API key status not checked yet.";
    private string aiPartnerTranscript = "Hold push-to-talk, speak, and release to send the first AI turn.";
    private string aiPartnerLastScreenshotStatus = "A fresh IL-2 screenshot will be captured when you release push-to-talk.";
    private string aiPartnerMicrophoneStatus = "Microphone test mode is off.";
    private string aiPartnerConversationStatus = "Ready. Hold push-to-talk, speak, and release to send a voice turn.";
    private string? aiPartnerMicrophoneFailureStatus;
    private bool isAiPartnerPushToTalkActive;
    private bool aiPartnerMicrophoneStartBlocked;
    private bool isAiPartnerRecording;
    private bool isAiPartnerRequestInFlight;
    private Task<AiPartnerVoiceTurnResult>? aiPartnerTurnTask;
    private AiPartnerScreenshotFrame? latestAiPartnerScreenshot;
    private CheckSixNotifierState checkSixNotifierState = new CheckSixNotifierState
    {
        LastSpeechAt = DateTimeOffset.UtcNow,
        LastYawBeyondThresholdAt = DateTimeOffset.UtcNow,
    };
    private SpeechCalloutState speechCalloutState = new();
    private FlapAutomationState flapAutomationState = new();
    private TuneModeState tuneModeState = new();
    private RuntimeViewState runtimeState = new();
    private string runtimeStateSummary = "Runtime reducer is idle.";
    private string outputPoseSummary = "No output pose yet.";
    private string overlayTransportStatus = "DX overlay feed is not initialized.";
    private bool isOverlayVisible;
    private bool overlayTransportFaulted;

    public MainWindowViewModel()
    {
        var settings = settingsStore.Load();
        il2RawVideoLibrary = new Il2RawVideoLibraryViewModel(settings.Il2RawVideoLibraryPath);
        il2RawVideoLibrary.PropertyChanged += OnIl2RawVideoLibraryPropertyChanged;
        selectedMainTabIndex = Math.Clamp(settings.SelectedMainTabIndex, 0, 2);
        isAiPartnerEnabled = settings.AiPartnerEnabled;
        isAiPartnerMicrophoneTestModeEnabled = settings.AiPartnerMicrophoneTestModeEnabled;
        aiPartnerBriefing = settings.AiPartnerBriefing ?? string.Empty;
        aiPartnerScreenshotCadenceSeconds = Math.Clamp(settings.AiPartnerScreenshotCadenceSeconds, 2, 5);
        trackIrInputProfile = TrackIrInputProfileCatalog.Resolve(settings.TrackIrInputProfileId);
        trackIrYawMultiplier = TrackIrScaling.ClampYawMultiplier(settings.TrackIrYawMultiplier);
        runtimeState = ApplyTrackIrInputProfile(runtimeState, trackIrInputProfile, trackIrYawMultiplier);

        presetItems = PersistedPresetCatalog.Load(settings)
            .Select(CreatePresetItem)
            .ToList();

        selectedPresetItem = presetItems.FirstOrDefault(item => string.Equals(item.Id, settings.SelectedPresetId, StringComparison.Ordinal))
            ?? presetItems.FirstOrDefault(item => string.Equals(item.Id, "lagg", StringComparison.Ordinal))
            ?? presetItems.FirstOrDefault();
        selectedPreset = selectedPresetItem?.Preset ?? presetItems.FirstOrDefault()?.Preset ?? PresetCatalog.Default;
        joystickSummary = noJoystickSummary;
        bindingGroups = BuildBindingGroups(settings.BindingTriggers);

        beginLearnModeCommand = new RelayCommand<BindingEntryViewModel>(BeginLearnMode);
        cancelLearnModeCommand = new RelayCommand<object>(_ => CancelLearnMode());
        toggleOverlayCommand = new RelayCommand<object>(_ => ToggleOverlay());
        toggleUdpStreamingCommand = new RelayCommand<object>(_ => ToggleUdpStreaming());

        openAiApiKeyResolution = openAiApiKeyResolver.Resolve();
        aiPartnerApiStatus = openAiApiKeyResolution.SourceDescription;
        aiPartnerDiagnosticsLog.WriteInfo($"AI Partner initialized. API key configured: {openAiApiKeyResolution.IsConfigured}. Source: {openAiApiKeyResolution.SourceDescription}");
        if (!openAiApiKeyResolution.IsConfigured)
        {
            aiPartnerConversationStatus = "OpenAI API key is missing. Set OPENAI_API_KEY before testing AI voice reply.";
            aiPartnerDiagnosticsLog.WriteInfo("AI Partner starts without OpenAI API key configuration.");
        }

        try
        {
            il2TelemetryReceiver = new Il2TelemetryReceiver();
            il2TelemetryStatus = $"waiting on UDP {il2TelemetryReceiver.LocalPort}";
        }
        catch (Exception exception)
        {
            il2TelemetryStatus = $"unavailable ({exception.Message})";
            il2SpeedSummary = "Speed: IL-2 telemetry unavailable";
            il2FlapsSummary = "Flaps: IL-2 telemetry unavailable";
        }

        try
        {
            overlayStateWriter = new OverlaySharedStateWriter();
            overlayTransportStatus = $"DX feed ready via {overlayStateWriter.MappingName} and {overlayStateWriter.UpdatedEventName}.";
        }
        catch (Exception exception)
        {
            overlayTransportStatus = $"DX feed unavailable: {exception.Message}";
        }

        if (settings.AutoStartUdpStreaming)
        {
            StartUdpStreaming(saveSettings: false);
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public event Action<bool>? OverlayVisibilityChanged;

    public string AppTitle => "AOM Desktop";

    public string Subtitle => "Standalone TrackIR/OpenTrack shell with persisted bindings and runtime state.";

    public string OutputTarget => "127.0.0.1:5555";

    public string Footer => "Next step: replace the remaining tune workflow with direct editable controls in the UI.";

    public IReadOnlyList<int> AiPartnerScreenshotCadenceOptions { get; } = new[] { 2, 3, 4, 5 };

    public Il2RawVideoLibraryViewModel Il2RawVideoLibrary => il2RawVideoLibrary;

    public IReadOnlyList<PresetListItemViewModel> Presets => presetItems;

    public IReadOnlyList<BindingGroupViewModel> BindingGroups => bindingGroups;

    public IReadOnlyList<TuneModeDefinition> TuneModes { get; } = TuneModeCatalog.All;

    public ICommand BeginLearnModeCommand => beginLearnModeCommand;

    public ICommand CancelLearnModeCommand => cancelLearnModeCommand;

    public ICommand ToggleOverlayCommand => toggleOverlayCommand;

    public ICommand ToggleUdpStreamingCommand => toggleUdpStreamingCommand;

    public int SelectedMainTabIndex
    {
        get => selectedMainTabIndex;
        set
        {
            var normalized = Math.Clamp(value, 0, 2);
            if (selectedMainTabIndex == normalized)
            {
                return;
            }

            selectedMainTabIndex = normalized;
            OnPropertyChanged();
            SaveSettings();
        }
    }

    public bool IsAiPartnerEnabled
    {
        get => isAiPartnerEnabled;
        set
        {
            if (isAiPartnerEnabled == value)
            {
                return;
            }

            isAiPartnerEnabled = value;
            OnPropertyChanged();
            SaveSettings();
        }
    }

    public bool IsAiPartnerMicrophoneTestModeEnabled
    {
        get => isAiPartnerMicrophoneTestModeEnabled;
        set
        {
            if (isAiPartnerMicrophoneTestModeEnabled == value)
            {
                return;
            }

            isAiPartnerMicrophoneTestModeEnabled = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(AiPartnerPushToTalkStatus));
            SaveSettings();
        }
    }

    public string AiPartnerBriefing
    {
        get => aiPartnerBriefing;
        set
        {
            var normalized = value ?? string.Empty;
            if (string.Equals(aiPartnerBriefing, normalized, StringComparison.Ordinal))
            {
                return;
            }

            aiPartnerBriefing = normalized;
            OnPropertyChanged();
            SaveSettings();
        }
    }

    public int AiPartnerScreenshotCadenceSeconds
    {
        get => aiPartnerScreenshotCadenceSeconds;
        set
        {
            var normalized = Math.Clamp(value, 2, 5);
            if (aiPartnerScreenshotCadenceSeconds == normalized)
            {
                return;
            }

            aiPartnerScreenshotCadenceSeconds = normalized;
            OnPropertyChanged();
            SaveSettings();
        }
    }

    public string AiPartnerApiStatus => aiPartnerApiStatus;

    public string AiPartnerTelemetrySummary => $"Preset {CurrentPresetDisplayName} | {Il2SpeedSummary} | {Il2FlapsSummary}";

    public string AiPartnerTranscript => aiPartnerTranscript;

    public string AiPartnerLastScreenshotStatus => aiPartnerLastScreenshotStatus;

    public string AiPartnerLogPath => aiPartnerDiagnosticsLog.LogFilePath;

    public string AiPartnerConversationStatus
    {
        get => aiPartnerConversationStatus;
        private set
        {
            if (string.Equals(aiPartnerConversationStatus, value, StringComparison.Ordinal))
            {
                return;
            }

            aiPartnerConversationStatus = value;
            OnPropertyChanged();
        }
    }

    public string AiPartnerMicrophoneStatus
    {
        get => aiPartnerMicrophoneStatus;
        private set
        {
            if (string.Equals(aiPartnerMicrophoneStatus, value, StringComparison.Ordinal))
            {
                return;
            }

            aiPartnerMicrophoneStatus = value;
            OnPropertyChanged();
        }
    }

    public BindingEntryViewModel? AiPartnerPushToTalkBinding =>
        bindingGroups
            .SelectMany(group => group.Bindings)
            .FirstOrDefault(binding => string.Equals(binding.ActionId, BindingActionIds.AiPartnerPushToTalk, StringComparison.Ordinal));

    public string AiPartnerPushToTalkStatus => IsAiPartnerMicrophoneTestModeEnabled
        ? isAiPartnerPushToTalkActive
            ? "Push-to-talk is active."
            : "Hold push-to-talk to route microphone input to the current output device."
        : isAiPartnerRequestInFlight
            ? "AI request in progress."
            : isAiPartnerRecording || isAiPartnerPushToTalkActive
                ? "Recording pilot audio. Release push-to-talk to send."
                : "Hold push-to-talk, speak, and release to send audio to the AI wingman.";

    public bool IsOverlayVisible
    {
        get => isOverlayVisible;
        private set
        {
            if (isOverlayVisible == value)
            {
                return;
            }

            isOverlayVisible = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(OverlayStatus));
            OnPropertyChanged(nameof(OverlayToggleLabel));
        }
    }

    public TrackIrProbeResult TrackIrProbe
    {
        get => trackIrProbe;
        private set
        {
            trackIrProbe = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(TrackIrCandidateSummary));
            OnPropertyChanged(nameof(TrackIrSampleSummary));
        }
    }

    public string LiveTrackIrStatus
    {
        get => liveTrackIrStatus;
        private set
        {
            liveTrackIrStatus = value;
            OnPropertyChanged();
        }
    }

    public string LiveTrackIrPoseSummary
    {
        get => liveTrackIrPoseSummary;
        private set
        {
            liveTrackIrPoseSummary = value;
            OnPropertyChanged();
        }
    }

    public string LiveTrackIrTimestamp
    {
        get => liveTrackIrTimestamp;
        private set
        {
            liveTrackIrTimestamp = value;
            OnPropertyChanged();
        }
    }

    public IReadOnlyList<JoystickLiveState> LiveJoystickStates
    {
        get => liveJoystickStates;
        private set
        {
            liveJoystickStates = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(LiveJoystickSummary));
        }
    }

    public JoystickDiscoverySummary JoystickSummary
    {
        get => joystickSummary;
        private set
        {
            joystickSummary = value;
            OnPropertyChanged();
        }
    }

    public string TrackIrCandidateSummary =>
        TrackIrProbe.Candidates.Count == 0
            ? "No candidate paths checked yet."
            : string.Join(Environment.NewLine, TrackIrProbe.Candidates);

    public string TrackIrSampleSummary =>
        TrackIrProbe.SamplePose is null
            ? "No live pose sample collected during probe."
            : FormatPose(TrackIrProbe.SamplePose.Value);

    public string TrackIrInputProfileSummary => $"Profile {trackIrInputProfile.DisplayName}";

    public double TrackIrYawMultiplier
    {
        get => trackIrYawMultiplier;
        set
        {
            var clampedValue = TrackIrScaling.ClampYawMultiplier(value);
            if (Math.Abs(trackIrYawMultiplier - clampedValue) < 0.0001)
            {
                return;
            }

            trackIrYawMultiplier = clampedValue;
            runtimeState = ApplyTrackIrInputProfile(runtimeState, trackIrInputProfile, trackIrYawMultiplier);
            OnPropertyChanged();
            OnPropertyChanged(nameof(TrackIrYawMultiplierSummary));
            SaveSettings();
        }
    }

    public string TrackIrYawMultiplierSummary => $"Yaw multiplier {trackIrYawMultiplier:0.00}x";

    public string TrackIrRateSummary => trackIrFreshRateHz > 0
        ? $"Fresh {trackIrFreshRateHz:0} Hz"
        : "Fresh rate warming up...";

    public string Il2TelemetryStatus
    {
        get => il2TelemetryStatus;
        private set
        {
            il2TelemetryStatus = value;
            OnPropertyChanged();
        }
    }

    public string Il2SpeedSummary
    {
        get => il2SpeedSummary;
        private set
        {
            il2SpeedSummary = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(AiPartnerTelemetrySummary));
        }
    }

    public string Il2FlapsSummary
    {
        get => il2FlapsSummary;
        private set
        {
            il2FlapsSummary = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(AiPartnerTelemetrySummary));
        }
    }

    public string UdpRateSummary => udpSendRateHz > 0
        ? $"UDP send {udpSendRateHz:0} Hz"
        : "UDP send rate warming up...";

    public string LiveJoystickSummary =>
        LiveJoystickStates.Count == 0
            ? "No live joystick samples yet."
            : $"Live samples: {LiveJoystickStates.Count} device(s).";

    public string LearnModeStatus
    {
        get => learnModeStatus;
        private set
        {
            learnModeStatus = value;
            OnPropertyChanged();
        }
    }

    public string RuntimeStateSummary
    {
        get => runtimeStateSummary;
        private set
        {
            runtimeStateSummary = value;
            OnPropertyChanged();
        }
    }

    public string OutputPoseSummary
    {
        get => outputPoseSummary;
        private set
        {
            outputPoseSummary = value;
            OnPropertyChanged();
        }
    }

    public string UdpStreamingStatus
    {
        get => udpStreamingStatus;
        private set
        {
            udpStreamingStatus = value;
            OnPropertyChanged();
        }
    }

    public string UdpPacketSummary
    {
        get => udpPacketSummary;
        private set
        {
            udpPacketSummary = value;
            OnPropertyChanged();
        }
    }

    public string UdpLastSentAt
    {
        get => udpLastSentAt;
        private set
        {
            udpLastSentAt = value;
            OnPropertyChanged();
        }
    }

    public long UdpPacketCount
    {
        get => udpPacketCount;
        private set
        {
            udpPacketCount = value;
            OnPropertyChanged();
        }
    }

    public string UdpToggleLabel => udpSender is null ? "Start" : "Stop";

    public string OverlayStatus => IsOverlayVisible ? "visible" : "hidden";

    public string OverlayToggleLabel => IsOverlayVisible ? "Hide overlay" : "Show overlay";

    public string OverlayBindingSummary
    {
        get
        {
            var trigger = GetBindingTrigger(BindingActionIds.ToggleOverlay);
            return string.IsNullOrWhiteSpace(trigger)
                ? "Overlay hotkey is not assigned yet."
                : $"Overlay hotkey: {trigger}";
        }
    }

    public string OverlaySupportSummary => $"{overlayTransportStatus} Current window is only a desktop preview. RTSS-style DirectX overlay still needs an injected D3D presenter that reads this feed inside the game process.";

    public PresetListItemViewModel? SelectedPresetItem
    {
        get => selectedPresetItem;
        set
        {
            if (value is null || ReferenceEquals(selectedPresetItem, value))
            {
                return;
            }

            SetSelectedPreset(value.Preset, clearTuneOverrides: true, persistSelection: true);
        }
    }

    public string CurrentPresetDisplayName => GetPresetDisplayName(CurrentPreset);

    public string CurrentPresetId => CurrentPreset.Id;

    public IReadOnlyList<PresetParameter> SelectedPresetParameters => CurrentPreset.Parameters;

    public IReadOnlyList<PoseScenarioPreview> PosePreviews =>
        new[]
        {
            BuildPreview(
                "Neutral hold",
                new RuntimeViewState(),
                new HeadPose(Yaw: 0, Pitch: 0.4, Roll: 0, X: 0.1, Y: 0.0, Z: 0.2),
                stickY: 500,
                "Baseline output with all toggles off."),
            BuildPreview(
                "Side look",
                new RuntimeViewState { IsSideView = true },
                new HeadPose(Yaw: 55, Pitch: 2.0, Roll: 0, X: 0.0, Y: 0.1, Z: 0.2),
                stickY: 500,
                "Uses the manual X shift branch for a right-hand look."),
            BuildPreview(
                "Dynamic zoom",
                new RuntimeViewState { IsHeadDynamic = true, IsZoomIn = true, IsGunViewAtCenter = true },
                new HeadPose(Yaw: 12, Pitch: 1.2, Roll: 0, X: 0.3, Y: 0.15, Z: 0.6),
                stickY: 650,
                "Combines dynamic head Y, gun-center offsets, and zoom-in delta."),
        };

    public async Task InitializeHardwareDiagnosticsAsync(nint windowHandle)
    {
        var trackIrTask = Task.Run(() => trackIrDiagnosticsService.Probe(windowHandle));
        var joystickTask = Task.Run(() => joystickDiscoveryService.Discover(windowHandle));

        TrackIrProbe = await trackIrTask;

        var joysticks = await joystickTask;
        JoystickSummary = joysticks.Count == 0
            ? noJoystickSummary
            : new JoystickDiscoverySummary($"Detected {joysticks.Count} DirectInput joystick device(s).", joysticks);

        keyboardCaptureService.Start();

        StartLiveTelemetry(windowHandle);
    }

    public async ValueTask DisposeAsync()
    {
        il2RawVideoLibrary.PropertyChanged -= OnIl2RawVideoLibraryPropertyChanged;
        await il2RawVideoLibrary.DisposeAsync();

        if (telemetryCancellation is not null)
        {
            telemetryCancellation.Cancel();
        }

        if (telemetryTask is not null)
        {
            try
            {
                await telemetryTask;
            }
            catch (OperationCanceledException)
            {
            }
        }

        if (aiPartnerTurnTask is not null)
        {
            try
            {
                await aiPartnerTurnTask;
            }
            catch
            {
            }
        }

        telemetryCancellation?.Dispose();
        trackIrSession?.Dispose();
        highResolutionTimerScope?.Dispose();
        trackIrSession = null;
        highResolutionTimerScope = null;

        lock (udpSync)
        {
            udpSender?.Dispose();
            udpSender = null;
        }

        microphoneLoopbackService?.Dispose();
        microphoneLoopbackService = null;
        aiPartnerAudioCaptureService.Dispose();
        openAiWingmanService.Dispose();
        il2TelemetryReceiver?.Dispose();
        overlayStateWriter?.Dispose();
        keyboardOutputService.ReleaseAutomationKeys();
        keyboardCaptureService.Dispose();
        notificationOutputService.Dispose();
        joystickDiscoveryService.Dispose();
    }

    private PoseScenarioPreview BuildPreview(string title, RuntimeViewState state, HeadPose inputPose, double stickY, string note)
    {
        var result = poseCalculator.ComputeGamePose(CurrentPreset, state, inputPose, stickY);
        return new PoseScenarioPreview(
            title,
            $"Input -> {FormatPose(inputPose)} | stickY {stickY:0}",
            $"Output -> {FormatPose(result.Pose)} | next yOffset {result.NextYOffset:0.00}",
            note);
    }

    private static string FormatPose(HeadPose pose) =>
        $"yaw {pose.Yaw:0.00}, pitch {pose.Pitch:0.00}, roll {pose.Roll:0.00}, x {pose.X:0.00}, y {pose.Y:0.00}, z {pose.Z:0.00}";

    private void StartLiveTelemetry(nint windowHandle)
    {
        telemetryCancellation = new CancellationTokenSource();
        var token = telemetryCancellation.Token;
        highResolutionTimerScope ??= new HighResolutionTimerScope();

        if (TrackIrProbe.IsInitialized && !string.IsNullOrWhiteSpace(TrackIrProbe.DllPath))
        {
            try
            {
                trackIrSession = new TrackIrLiveSession(TrackIrProbe.DllPath, windowHandle);
                LiveTrackIrStatus = "connected";
            }
            catch (Exception exception)
            {
                LiveTrackIrStatus = "startup failed";
                LiveTrackIrPoseSummary = exception.Message;
            }
        }
        else
        {
            LiveTrackIrStatus = "unavailable";
            LiveTrackIrPoseSummary = "TrackIR startup prerequisites were not met during probe.";
        }

        telemetryTask = RunTelemetryLoopAsync(token);
    }

    private async Task RunTelemetryLoopAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(telemetryInterval);

        while (await timer.WaitForNextTickAsync(cancellationToken))
        {
            var sampledAt = DateTimeOffset.UtcNow;
            var nextJoystickStates = ReadJoystickStates();
            var keyboardState = keyboardCaptureService.GetSnapshot();
            HeadPose? nextTrackIrPose = null;
            string? nextTrackIrStatus = null;
            string? trackIrReadErrorMessage = null;
            string? learnedTrigger = null;
            BindingEntryViewModel? capturedBinding = null;
            CameraPreset? presetFromActions = null;
            CameraPreset? tunedPresetFromMode = null;
            RuntimeViewState? nextRuntimeState = null;
            HeadPose? outgoingPose = null;
            double nextStickY = lastStickY;
            string? udpError = null;
            string? centerPulseError = null;
            string? flapOutputError = null;
            string? checkSixOutputError = null;
            string? aiPartnerMicrophoneError = null;
            string? helperStatusMessage = null;
            string? il2TelemetryErrorMessage = null;
            string? tuneModeSpeechMessage = null;
            string? tuneModeSpeechTrigger = null;
            string? nextAiPartnerScreenshotStatus = null;
            string? nextAiPartnerConversationStatus = null;
            bool shouldSavePreset = false;
            bool shouldToggleOverlay = false;
            bool shouldStopUdp = false;
            bool shouldSpeakFlapReminder = false;
            bool nextAiPartnerPushToTalkActive = false;
            string? nextAiPartnerMicrophoneStatus = null;
            bool nextAiPartnerRecording = isAiPartnerRecording;
            bool nextAiPartnerRequestInFlight = isAiPartnerRequestInFlight;
            AiPartnerScreenshotFrame? nextAiPartnerScreenshotFrame = null;
            AiPartnerVoiceTurnResult? completedAiTurn = null;
            CheckSixSpeedCue speedCueToSpeak = CheckSixSpeedCue.None;
            FlapAutomationState? nextFlapState = null;
            CheckSixNotifierState? nextCheckSixState = null;
            Il2TelemetrySnapshot? nextIl2TelemetrySnapshot = null;
            string? nextIl2TelemetryStatus = null;
            SpeechCalloutState? nextSpeechCalloutState = null;
            SpeechCallout nextSpeechCallout = SpeechCallout.None;
            TuneModeState? nextTuneModeState = null;
            string? overlayStatusMessage = null;

            if (il2TelemetryReceiver is not null)
            {
                try
                {
                    if (il2TelemetryReceiver.TryReadLatest(out var liveTelemetry))
                    {
                        nextIl2TelemetrySnapshot = liveTelemetry;
                        nextIl2TelemetryStatus = "live";
                        lastIl2TelemetryFrameAt = liveTelemetry.ReceivedAtUtc;

                        if (liveTelemetry.EasMetersPerSecond is not null)
                        {
                            lastIl2SpeedMetersPerSecond = liveTelemetry.EasMetersPerSecond;
                        }

                        if (liveTelemetry.FlapsPosition is not null)
                        {
                            lastIl2FlapsPosition = liveTelemetry.FlapsPosition;
                        }
                    }
                    else if (lastIl2TelemetryFrameAt == DateTimeOffset.MinValue)
                    {
                        nextIl2TelemetryStatus = $"waiting on UDP {il2TelemetryReceiver.LocalPort}";
                    }
                    else if (sampledAt - lastIl2TelemetryFrameAt <= il2TelemetryStaleThreshold)
                    {
                        nextIl2TelemetryStatus = "live";
                    }
                    else
                    {
                        nextIl2TelemetryStatus = "stale";
                    }
                }
                catch (Exception exception)
                {
                    nextIl2TelemetryStatus = "receive failed";
                    il2TelemetryErrorMessage = exception.Message;
                }
            }

            if (trackIrSession is not null)
            {
                try
                {
                    if (trackIrSession.TryReadPose(out var livePose))
                    {
                        nextTrackIrPose = livePose;
                        lastTrackIrPose = livePose;
                        hasTrackIrPose = true;
                        lastTrackIrFrameAt = sampledAt;
                        RegisterRateSample(sampledAt, ref trackIrRateWindowStartedAt, ref trackIrFreshFramesInWindow, ref trackIrFreshRateHz);
                        nextTrackIrStatus = TrackIrStatusResolver.Resolve(
                            receivedFreshFrame: true,
                            hasTrackIrPose,
                            sampledAt,
                            lastTrackIrFrameAt,
                            trackIrStaleThreshold);
                    }
                    else
                    {
                        nextTrackIrStatus = TrackIrStatusResolver.Resolve(
                            receivedFreshFrame: false,
                            hasTrackIrPose,
                            sampledAt,
                            lastTrackIrFrameAt,
                            trackIrStaleThreshold);
                    }
                }
                catch (Exception exception)
                {
                    nextTrackIrStatus = "read failed";
                    trackIrReadErrorMessage = exception.Message;
                }
            }

            if (aiPartnerTurnTask is not null && aiPartnerTurnTask.IsCompleted)
            {
                try
                {
                    completedAiTurn = aiPartnerTurnTask.GetAwaiter().GetResult();
                    aiPartnerDiagnosticsLog.WriteInfo($"AI turn task completed. Success: {completedAiTurn.Succeeded}. Status: {completedAiTurn.StatusMessage}");
                    nextAiPartnerRequestInFlight = false;
                    nextAiPartnerConversationStatus = completedAiTurn.StatusMessage;
                    nextAiPartnerScreenshotFrame = completedAiTurn.Screenshot;
                    nextAiPartnerScreenshotStatus = completedAiTurn.ScreenshotStatusMessage;

                    if (completedAiTurn.Succeeded && !string.IsNullOrWhiteSpace(completedAiTurn.AiReply))
                    {
                        notificationOutputService.SpeakAiPartnerReply(
                            completedAiTurn.AiReply,
                            completedAiTurn.AiSpeechAudioBytes,
                            completedAiTurn.AiSpeechAudioFormat);
                    }
                }
                catch (Exception exception)
                {
                    aiPartnerDiagnosticsLog.WriteError("AI turn task completion failed.", exception);
                    nextAiPartnerRequestInFlight = false;
                    nextAiPartnerConversationStatus = $"AI turn failed: {exception.Message}";
                }
                finally
                {
                    aiPartnerTurnTask = null;
                }
            }

            if (activeLearningBinding is not null && TryCaptureLearnTrigger(nextJoystickStates, out var trigger))
            {
                learnedTrigger = trigger;
                capturedBinding = activeLearningBinding;
            }
            else if (activeLearningBinding is not null && keyboardCaptureService.TryConsumeLearnTrigger(out var keyboardTrigger))
            {
                learnedTrigger = keyboardTrigger;
                capturedBinding = activeLearningBinding;
            }

            var actionSnapshot = bindingInputResolver.Evaluate(BuildBindingRequests(), nextJoystickStates, keyboardState);
            presetFromActions = ResolvePresetFromActions(actionSnapshot);
            shouldToggleOverlay = actionSnapshot.WasPressed(BindingActionIds.ToggleOverlay);
            nextAiPartnerPushToTalkActive = actionSnapshot.IsActive(BindingActionIds.AiPartnerPushToTalk);

            var activePreset = presetFromActions ?? CurrentPreset;
            var trackIrPose = hasTrackIrPose ? lastTrackIrPose : default;
            nextStickY = actionSnapshot.GetAxisValueOrDefault(BindingActionIds.StickYAxis, GetDefaultStickY(nextJoystickStates));
            var hasFreshIl2FlapTelemetry = Il2TelemetryFlapMonitor.HasFreshFlapTelemetry(lastIl2FlapsPosition, lastIl2TelemetryFrameAt, sampledAt, il2TelemetryStaleThreshold);
            FlapAutomationResult? telemetryFlapResult = null;
            if (hasFreshIl2FlapTelemetry && lastIl2FlapsPosition is not null)
            {
                telemetryFlapResult = flapAutomationController.ObserveFlapState(
                    flapAutomationState,
                    sampledAt,
                    Il2TelemetryFlapMonitor.IsFlapOpen(lastIl2FlapsPosition.Value));
                shouldSpeakFlapReminder = telemetryFlapResult.ShouldSpeakReminder;
            }

            var monitoredFlapState = telemetryFlapResult?.NextState ?? flapAutomationState;
            double? currentIl2SpeedKilometersPerHour =
                lastIl2SpeedMetersPerSecond is not null && sampledAt - lastIl2TelemetryFrameAt <= il2TelemetryStaleThreshold
                    ? lastIl2SpeedMetersPerSecond.Value * 3.6d
                    : null;

            if (hasFreshIl2FlapTelemetry && flapAutomationState.ActiveOperation != FlapOperation.None)
            {
                try
                {
                    keyboardOutputService.ReleaseAutomationKeys();
                }
                catch (Exception exception)
                {
                    flapOutputError = exception.Message;
                }
            }

            try
            {
                var pushToTalkPressed = !isAiPartnerPushToTalkActive && nextAiPartnerPushToTalkActive;
                var pushToTalkReleased = isAiPartnerPushToTalkActive && !nextAiPartnerPushToTalkActive;
                var hasActiveAiPartnerRecording = isAiPartnerRecording || aiPartnerAudioCaptureService.IsRecording;

                if (IsAiPartnerEnabled && IsAiPartnerMicrophoneTestModeEnabled)
                {
                    if (aiPartnerAudioCaptureService.IsRecording)
                    {
                        aiPartnerAudioCaptureService.Stop();
                        nextAiPartnerRecording = false;
                    }

                    if (nextAiPartnerPushToTalkActive)
                    {
                        if (aiPartnerMicrophoneStartBlocked)
                        {
                            nextAiPartnerMicrophoneStatus = aiPartnerMicrophoneFailureStatus ?? "Microphone test failed.";
                        }
                        else
                        {
                            microphoneLoopbackService ??= new MicrophoneLoopbackService();
                            microphoneLoopbackService.Start();
                            nextAiPartnerMicrophoneStatus = "Push-to-talk active. Microphone is routed to output.";
                        }
                    }
                    else
                    {
                        microphoneLoopbackService?.Stop();
                        aiPartnerMicrophoneStartBlocked = false;
                        aiPartnerMicrophoneFailureStatus = null;
                        nextAiPartnerMicrophoneStatus = "Test mode ready. Hold push-to-talk to hear microphone output.";
                        nextAiPartnerConversationStatus ??= "Microphone test mode is active. AI requests are paused.";
                    }
                }
                else if (IsAiPartnerEnabled)
                {
                    microphoneLoopbackService?.Stop();
                    aiPartnerMicrophoneStartBlocked = false;
                    aiPartnerMicrophoneFailureStatus = null;

                    if (pushToTalkPressed)
                    {
                        if (!openAiApiKeyResolution.IsConfigured)
                        {
                            aiPartnerDiagnosticsLog.WriteInfo("Push-to-talk press ignored because OpenAI API key is missing.");
                            nextAiPartnerMicrophoneStatus = "OpenAI API key is missing. Set OPENAI_API_KEY before testing AI voice reply.";
                            nextAiPartnerConversationStatus = "Voice turn cancelled: missing OpenAI API key.";
                        }
                        else if (aiPartnerTurnTask is not null || isAiPartnerRequestInFlight)
                        {
                            aiPartnerDiagnosticsLog.WriteInfo("Push-to-talk press ignored because an AI request is already in progress.");
                            nextAiPartnerMicrophoneStatus = "AI request already in progress. Wait for the current reply.";
                            nextAiPartnerConversationStatus = "Previous AI request is still running.";
                        }
                        else
                        {
                            aiPartnerDiagnosticsLog.WriteInfo("Push-to-talk pressed. Starting microphone capture.");
                            aiPartnerAudioCaptureService.Start();
                            nextAiPartnerRecording = true;
                            nextAiPartnerMicrophoneStatus = "Recording pilot audio. Release push-to-talk to send.";
                            nextAiPartnerConversationStatus = "Recording pilot audio...";
                        }
                    }
                    else if (pushToTalkReleased && hasActiveAiPartnerRecording)
                    {
                        aiPartnerDiagnosticsLog.WriteInfo("Push-to-talk released. Stopping microphone capture.");
                        var audioWaveBytes = aiPartnerAudioCaptureService.Stop();
                        if (aiPartnerAudioCaptureService.LastStopTimedOut)
                        {
                            aiPartnerDiagnosticsLog.WriteInfo("Microphone stop confirmation timed out. Continuing with buffered audio.");
                        }

                        nextAiPartnerRecording = false;

                        if (audioWaveBytes is null || audioWaveBytes.Length <= 44)
                        {
                            aiPartnerDiagnosticsLog.WriteInfo("Microphone capture completed without usable audio bytes.");
                            nextAiPartnerMicrophoneStatus = "No microphone audio captured. Try again.";
                            nextAiPartnerConversationStatus = "Voice turn cancelled: no audio captured.";
                        }
                        else if (!openAiApiKeyResolution.IsConfigured)
                        {
                            aiPartnerDiagnosticsLog.WriteInfo("Captured microphone audio but OpenAI API key is missing.");
                            nextAiPartnerMicrophoneStatus = "OpenAI API key is missing. Set OPENAI_API_KEY before testing AI voice reply.";
                            nextAiPartnerConversationStatus = "Voice turn cancelled: missing OpenAI API key.";
                        }
                        else
                        {
                            aiPartnerDiagnosticsLog.WriteInfo($"Microphone capture completed with {audioWaveBytes.Length} WAV bytes. Preparing AI turn.");
                            var briefingSnapshot = AiPartnerBriefing;
                            var telemetrySnapshot = AiPartnerTelemetrySummary;

                            aiPartnerTurnTask = Task.Run(async () =>
                            {
                                aiPartnerDiagnosticsLog.WriteInfo("AI turn task started. Capturing IL-2 screenshot.");
                                var screenshotCapture = il2WindowCaptureService.CaptureLatest();
                                var screenshotStatus = FormatAiPartnerScreenshotStatus(screenshotCapture);
                                aiPartnerDiagnosticsLog.WriteInfo($"Screenshot capture finished. {screenshotStatus}");
                                var request = new AiPartnerTurnRequest(
                                    briefingSnapshot,
                                    telemetrySnapshot,
                                    audioWaveBytes,
                                    screenshotCapture.Frame,
                                    screenshotStatus);

                                try
                                {
                                    aiPartnerDiagnosticsLog.WriteInfo("Sending pilot audio and context to OpenAI.");
                                    var reply = await openAiWingmanService.ExecuteTurnAsync(openAiApiKeyResolution.ApiKey!, request, cancellationToken);
                                    if (!string.IsNullOrWhiteSpace(reply.AiSpeechErrorMessage))
                                    {
                                        aiPartnerDiagnosticsLog.WriteInfo($"OpenAI voice synthesis unavailable. Falling back to local voice. {reply.AiSpeechErrorMessage}");
                                    }

                                    aiPartnerDiagnosticsLog.WriteInfo($"OpenAI turn completed. Pilot transcript length: {reply.PilotTranscript.Length}. AI reply length: {reply.AiReply.Length}. AI speech format: {reply.AiSpeechAudioFormat ?? "none"}. AI speech bytes: {reply.AiSpeechAudioBytes?.Length ?? 0}.");
                                    return AiPartnerVoiceTurnResult.Success(reply.PilotTranscript, reply.AiReply, reply.AiSpeechAudioBytes, reply.AiSpeechAudioFormat, reply.AiSpeechErrorMessage, screenshotStatus, screenshotCapture.Frame);
                                }
                                catch (Exception exception)
                                {
                                    aiPartnerDiagnosticsLog.WriteError("OpenAI turn failed.", exception);
                                    return AiPartnerVoiceTurnResult.Failure($"AI turn failed: {exception.Message}", screenshotStatus, screenshotCapture.Frame);
                                }
                            }, cancellationToken);

                            nextAiPartnerRequestInFlight = true;
                            nextAiPartnerMicrophoneStatus = "Sending pilot audio to OpenAI...";
                            nextAiPartnerConversationStatus = "Transcribing pilot audio and generating AI reply...";
                            nextAiPartnerScreenshotStatus = "Capturing one fresh IL-2 screenshot for this AI turn...";
                        }
                    }
                    else if (hasActiveAiPartnerRecording && nextAiPartnerPushToTalkActive)
                    {
                        nextAiPartnerMicrophoneStatus = "Recording pilot audio. Release push-to-talk to send.";
                    }
                    else if (aiPartnerTurnTask is not null || isAiPartnerRequestInFlight)
                    {
                        nextAiPartnerMicrophoneStatus = "AI request in progress. Waiting for reply.";
                    }
                    else
                    {
                        nextAiPartnerMicrophoneStatus = openAiApiKeyResolution.IsConfigured
                            ? "Ready. Hold push-to-talk, speak, and release to send audio to the AI wingman."
                            : "OpenAI API key is missing. Set OPENAI_API_KEY before testing AI voice reply.";
                        nextAiPartnerConversationStatus ??= openAiApiKeyResolution.IsConfigured
                            ? "Ready for the next voice turn."
                            : "OpenAI API key is missing. Set OPENAI_API_KEY before testing AI voice reply.";
                    }
                }
                else
                {
                    microphoneLoopbackService?.Stop();
                    if (aiPartnerAudioCaptureService.IsRecording)
                    {
                        aiPartnerAudioCaptureService.Stop();
                    }

                    nextAiPartnerRecording = false;
                    aiPartnerMicrophoneStartBlocked = false;
                    aiPartnerMicrophoneFailureStatus = null;
                    nextAiPartnerMicrophoneStatus = "Enable AI Partner to use push-to-talk voice turns.";
                    nextAiPartnerConversationStatus ??= "AI Partner is off.";
                }
            }
            catch (Exception exception)
            {
                if (aiPartnerAudioCaptureService.IsRecording)
                {
                    aiPartnerAudioCaptureService.Stop();
                }

                microphoneLoopbackService?.Stop();
                aiPartnerDiagnosticsLog.WriteError("AI voice turn flow failed.", exception);
                nextAiPartnerRecording = false;
                aiPartnerMicrophoneStartBlocked = true;
                aiPartnerMicrophoneFailureStatus = $"Microphone test failed: {exception.Message}";
                nextAiPartnerMicrophoneStatus = $"AI voice turn failed: {exception.Message}";
                nextAiPartnerConversationStatus = $"AI voice turn failed: {exception.Message}";
                aiPartnerMicrophoneError = exception.Message;
            }

            var reducedState = runtimeViewStateReducer.Apply(runtimeState with { YOffset = runtimeYOffset }, actionSnapshot);
            var tuneModeUpdate = tuneModeController.Apply(tuneModeState, activePreset, actionSnapshot, reducedState);

            nextTuneModeState = tuneModeUpdate.NextState;
            activePreset = tuneModeUpdate.NextPreset;
            if (!ReferenceEquals(activePreset, presetFromActions ?? CurrentPreset))
            {
                tunedPresetFromMode = activePreset;
            }

            if (tuneModeUpdate.RequestCenterSync)
            {
                reducedState = reducedState with { CenterPendingFrameTimer = 5 };
            }

            if (!string.IsNullOrWhiteSpace(tuneModeUpdate.StatusMessage))
            {
                helperStatusMessage = tuneModeUpdate.StatusMessage;

                if (actionSnapshot.WasPressed(BindingActionIds.ToggleTuneMode))
                {
                    tuneModeSpeechMessage = tuneModeUpdate.StatusMessage;
                    if (nextTuneModeState.IsEnabled)
                    {
                        tuneModeSpeechTrigger = GetBindingTrigger(BindingActionIds.CycleTuneMode);
                    }
                }
                else if (actionSnapshot.WasPressed(BindingActionIds.CycleTuneMode))
                {
                    tuneModeSpeechMessage = tuneModeUpdate.StatusMessage;
                }
            }

            var syncResult = runtimeSyncPlanner.Apply(reducedState);

            if (syncResult.IsSyncFrame)
            {
                nextRuntimeState = syncResult.NextState;
                outgoingPose = syncResult.Pose;
                nextFlapState = monitoredFlapState;
                nextCheckSixState = checkSixNotifierState;

                if (syncResult.SendGlobalCenterPulse)
                {
                    try
                    {
                        keyboardOutputService.TapGlobalCenterKey();
                    }
                    catch (Exception exception)
                    {
                        centerPulseError = exception.Message;
                    }
                }
            }
            else
            {
                if (nextTuneModeState.IsEnabled)
                {
                    nextFlapState = monitoredFlapState;
                    nextCheckSixState = checkSixNotifierState;

                    var tunePlan = tuneModeRuntimePlanner.CreatePlan(nextTuneModeState, activePreset, reducedState with { YOffset = runtimeYOffset });
                    if (tunePlan is not null)
                    {
                        nextStickY = tunePlan.StickY;
                        var result = poseCalculator.ComputeGamePose(activePreset, tunePlan.ViewState, tunePlan.TrackIrPose, tunePlan.StickY);
                        runtimeYOffset = result.NextYOffset;
                        nextRuntimeState = tunePlan.ViewState with { YOffset = result.NextYOffset };
                        outgoingPose = result.Pose;
                    }
                }
                else
                {
                    if (hasFreshIl2FlapTelemetry)
                    {
                        nextFlapState = monitoredFlapState;
                    }
                    else
                    {
                        var flapResult = flapAutomationController.Apply(
                            flapAutomationState,
                            DateTimeOffset.UtcNow,
                            openRequested: actionSnapshot.WasPressed(BindingActionIds.OpenFlaps),
                            closeRequested: actionSnapshot.WasPressed(BindingActionIds.CloseFlaps));

                        nextFlapState = flapResult.NextState;

                        try
                        {
                            keyboardOutputService.ApplyFlapOutput(flapResult.Output);
                        }
                        catch (Exception exception)
                        {
                            flapOutputError = exception.Message;
                        }

                        shouldSpeakFlapReminder = flapResult.ShouldSpeakReminder;
                    }

                    var result = poseCalculator.ComputeGamePose(activePreset, reducedState, trackIrPose, nextStickY);

                    var checkSixResult = checkSixNotifierController.Apply(
                        checkSixNotifierState,
                        DateTimeOffset.UtcNow,
                        currentIl2SpeedKilometersPerHour,
                        toggleRequested: actionSnapshot.WasPressed(BindingActionIds.ToggleCheckSix));

                    nextCheckSixState = checkSixResult.NextState;

                    if (checkSixResult.Toggled)
                    {
                        helperStatusMessage = checkSixResult.NextState.IsActivated
                            ? "Check six notifier activated."
                            : "Check six notifier disabled.";
                    }

                    speedCueToSpeak = checkSixResult.ShouldNotify
                        ? checkSixResult.Cue
                        : CheckSixSpeedCue.None;

                    runtimeYOffset = result.NextYOffset;
                    nextRuntimeState = reducedState with { YOffset = result.NextYOffset };
                    outgoingPose = result.Pose;
                }
            }

            var calloutDecision = SpeechCalloutCoordinator.Select(speechCalloutState, shouldSpeakFlapReminder, speedCueToSpeak);
            nextSpeechCalloutState = calloutDecision.NextState;
            nextSpeechCallout = calloutDecision.Callout;

            if (nextSpeechCallout != SpeechCallout.None)
            {
                try
                {
                    SpeakSpeechCallout(nextSpeechCallout);
                }
                catch (Exception exception)
                {
                    checkSixOutputError = exception.Message;
                }
            }

            var sender = GetUdpSender();
            if (sender is not null)
            {
                try
                {
                    sender.Send(outgoingPose ?? default);
                    RegisterRateSample(sampledAt, ref udpRateWindowStartedAt, ref udpPacketsInWindow, ref udpSendRateHz);
                }
                catch (Exception exception)
                {
                    udpError = exception.Message;
                    shouldStopUdp = true;
                }
            }

            if (nextRuntimeState is not null)
            {
                runtimeState = nextRuntimeState;
                if (nextFlapState is not null)
                {
                    flapAutomationState = nextFlapState;
                }

                if (nextCheckSixState is not null)
                {
                    checkSixNotifierState = nextCheckSixState;
                }

                if (nextSpeechCalloutState is not null)
                {
                    speechCalloutState = nextSpeechCalloutState;
                }

                if (nextTuneModeState is not null)
                {
                    tuneModeState = nextTuneModeState;
                }

                lastStickY = nextStickY;
            }

            shouldSavePreset = actionSnapshot.WasPressed(BindingActionIds.SavePreset);

            if (tuneModeSpeechMessage is not null)
            {
                notificationOutputService.SpeakTuneModeStatus(tuneModeSpeechMessage, tuneModeSpeechTrigger);
            }

            var forceUiRefresh = presetFromActions is not null
                || tunedPresetFromMode is not null
                || shouldSavePreset
                || shouldToggleOverlay
                || capturedBinding is not null
                || shouldStopUdp
                || udpError is not null
                || centerPulseError is not null
                || flapOutputError is not null
                || checkSixOutputError is not null
                || aiPartnerMicrophoneError is not null
                || helperStatusMessage is not null
                || trackIrReadErrorMessage is not null
                || nextAiPartnerPushToTalkActive != isAiPartnerPushToTalkActive
                || !string.Equals(nextAiPartnerMicrophoneStatus, aiPartnerMicrophoneStatus, StringComparison.Ordinal)
                || nextAiPartnerRecording != isAiPartnerRecording
                || nextAiPartnerRequestInFlight != isAiPartnerRequestInFlight
                || nextAiPartnerScreenshotFrame is not null
                || !string.Equals(nextAiPartnerScreenshotStatus, aiPartnerLastScreenshotStatus, StringComparison.Ordinal)
                || completedAiTurn is not null
                || !string.Equals(nextAiPartnerConversationStatus, aiPartnerConversationStatus, StringComparison.Ordinal);

            var refreshStartedAt = DateTimeOffset.UtcNow;
            if (!forceUiRefresh && refreshStartedAt - lastUiRefreshAt < uiRefreshInterval)
            {
                continue;
            }

            lastUiRefreshAt = refreshStartedAt;

            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                LiveJoystickStates = nextJoystickStates;

                if (nextTrackIrStatus is not null)
                {
                    LiveTrackIrStatus = nextTrackIrStatus;
                }

                if (nextIl2TelemetryStatus is not null)
                {
                    Il2TelemetryStatus = nextIl2TelemetryStatus;
                }

                if (isAiPartnerPushToTalkActive != nextAiPartnerPushToTalkActive)
                {
                    isAiPartnerPushToTalkActive = nextAiPartnerPushToTalkActive;
                    OnPropertyChanged(nameof(AiPartnerPushToTalkStatus));
                }

                if (isAiPartnerRecording != nextAiPartnerRecording)
                {
                    isAiPartnerRecording = nextAiPartnerRecording;
                    OnPropertyChanged(nameof(AiPartnerPushToTalkStatus));
                }

                if (isAiPartnerRequestInFlight != nextAiPartnerRequestInFlight)
                {
                    isAiPartnerRequestInFlight = nextAiPartnerRequestInFlight;
                    OnPropertyChanged(nameof(AiPartnerPushToTalkStatus));
                }

                if (nextAiPartnerMicrophoneStatus is not null)
                {
                    AiPartnerMicrophoneStatus = nextAiPartnerMicrophoneStatus;
                }

                if (nextAiPartnerConversationStatus is not null)
                {
                    AiPartnerConversationStatus = nextAiPartnerConversationStatus;
                }

                if (nextAiPartnerScreenshotFrame is not null)
                {
                    latestAiPartnerScreenshot = nextAiPartnerScreenshotFrame;
                }

                if (completedAiTurn is not null)
                {
                    aiPartnerTranscript = AppendAiPartnerTranscript(
                        aiPartnerTranscript,
                        completedAiTurn.PilotTranscript,
                        completedAiTurn.AiReply,
                        completedAiTurn.Succeeded ? null : completedAiTurn.StatusMessage);
                    OnPropertyChanged(nameof(AiPartnerTranscript));
                }

                if (nextAiPartnerScreenshotStatus is not null && !string.Equals(aiPartnerLastScreenshotStatus, nextAiPartnerScreenshotStatus, StringComparison.Ordinal))
                {
                    aiPartnerLastScreenshotStatus = nextAiPartnerScreenshotStatus;
                    OnPropertyChanged(nameof(AiPartnerLastScreenshotStatus));
                }

                OnPropertyChanged(nameof(TrackIrInputProfileSummary));
                OnPropertyChanged(nameof(TrackIrRateSummary));
                OnPropertyChanged(nameof(UdpRateSummary));

                if (nextTrackIrPose is not null)
                {
                    LiveTrackIrPoseSummary = FormatPose(nextTrackIrPose.Value);
                    LiveTrackIrTimestamp = $"Last frame {DateTime.Now:HH:mm:ss.fff}";
                }

                if (trackIrReadErrorMessage is not null)
                {
                    LiveTrackIrPoseSummary = trackIrReadErrorMessage;
                }

                if (nextIl2TelemetrySnapshot is not null)
                {
                    Il2SpeedSummary = FormatIl2SpeedSummary(lastIl2SpeedMetersPerSecond);
                    Il2FlapsSummary = FormatIl2FlapsSummary(lastIl2FlapsPosition);
                }

                if (il2TelemetryErrorMessage is not null)
                {
                    Il2TelemetryStatus = $"receive failed ({il2TelemetryErrorMessage})";
                }

                if (presetFromActions is not null)
                {
                    SetSelectedPreset(presetFromActions, clearTuneOverrides: true, persistSelection: true);
                    notificationOutputService.SpeakPresetLoaded(GetPresetDisplayName(presetFromActions));
                }

                if (shouldToggleOverlay)
                {
                    ToggleOverlay();
                    overlayStatusMessage = IsOverlayVisible ? "Overlay shown." : "Overlay hidden.";
                }

                if (tunedPresetFromMode is not null)
                {
                    SetTunedPreset(tunedPresetFromMode);
                }

                if (shouldSavePreset)
                {
                    LearnModeStatus = SaveCurrentPreset();
                }

                if (capturedBinding is not null && learnedTrigger is not null)
                {
                    capturedBinding.SetTrigger(learnedTrigger);
                    capturedBinding.SetLearning(false);
                    activeLearningBinding = null;
                    activeLearningAllowsAxisCapture = false;
                    learnAxisCaptureFilter.Clear();
                    LearnModeStatus = $"Captured {capturedBinding.Name}: {learnedTrigger}";
                    OnPropertyChanged(nameof(OverlayBindingSummary));
                    SaveSettings();
                }

                if (nextRuntimeState is not null)
                {
                    RuntimeStateSummary = FormatRuntimeState(runtimeState, flapAutomationState, checkSixNotifierState, tuneModeState, CurrentPresetDisplayName, nextStickY);
                }

                if (outgoingPose is not null)
                {
                    OutputPoseSummary = $"Preset {CurrentPresetDisplayName} -> {FormatPose(outgoingPose.Value)}";

                    if (sender is not null && udpError is null)
                    {
                        UdpStreamingStatus = "streaming";
                        UdpPacketCount += 1;
                        UdpPacketSummary = FormatPose(outgoingPose.Value);
                        UdpLastSentAt = $"Last packet {DateTime.Now:HH:mm:ss.fff}";
                    }
                }

                if (shouldStopUdp)
                {
                    StopUdpStreaming(saveSettings: true);
                }

                if (udpError is not null)
                {
                    UdpStreamingStatus = "send failed";
                    UdpPacketSummary = udpError;
                }

                if (centerPulseError is not null)
                {
                    LearnModeStatus = $"Global center pulse failed: {centerPulseError}";
                }

                if (flapOutputError is not null)
                {
                    LearnModeStatus = $"Flap automation failed: {flapOutputError}";
                }

                if (checkSixOutputError is not null)
                {
                    LearnModeStatus = $"Check-six notifier failed: {checkSixOutputError}";
                }

                if (overlayStatusMessage is not null)
                {
                    LearnModeStatus = overlayStatusMessage;
                }

                if (helperStatusMessage is not null)
                {
                    LearnModeStatus = helperStatusMessage;
                }

                PublishOverlayState();
            });
        }
    }

    private IReadOnlyList<JoystickLiveState> ReadJoystickStates()
    {
        if (JoystickSummary.Devices is null || JoystickSummary.Devices.Count == 0)
        {
            return Array.Empty<JoystickLiveState>();
        }

        return JoystickSummary.Devices
            .Select(device => joystickDiscoveryService.TryReadState(device, out var state) ? state : null)
            .Where(state => state is not null)
            .Cast<JoystickLiveState>()
            .ToArray();
    }

    private void BeginLearnMode(BindingEntryViewModel? binding)
    {
        if (binding is null)
        {
            return;
        }

        if (activeLearningBinding is not null)
        {
            activeLearningBinding.SetLearning(false);
        }

        activeLearningBinding = binding;
        activeLearningAllowsAxisCapture = AllowsAxisLearn(binding.ActionId);
        binding.SetLearning(true);
        keyboardCaptureService.ClearPendingLearnTrigger();
        learnAxisCaptureFilter.Reset(LiveJoystickStates);
        LearnModeStatus = activeLearningAllowsAxisCapture
            ? $"Listening for the next joystick button, axis move, or keyboard shortcut for {binding.Name}."
            : $"Listening for the next joystick button or keyboard shortcut for {binding.Name}.";
    }

    private void CancelLearnMode()
    {
        if (activeLearningBinding is null)
        {
            LearnModeStatus = "No active learn mode to cancel.";
            return;
        }

        activeLearningBinding.SetLearning(false);
        activeLearningAllowsAxisCapture = false;
        learnAxisCaptureFilter.Clear();
        LearnModeStatus = $"Learn mode cancelled for {activeLearningBinding.Name}.";
        activeLearningBinding = null;
    }

    private bool TryCaptureLearnTrigger(IReadOnlyList<JoystickLiveState> joystickStates, out string trigger)
    {
        foreach (var state in joystickStates)
        {
            if (state.PressedButtons.Count > 0)
            {
                trigger = $"Joystick {state.DeviceId} Button {state.PressedButtons[0]}";
                return true;
            }
        }

        if (activeLearningAllowsAxisCapture)
        {
            var axisTrigger = learnAxisCaptureFilter.TryCapture(joystickStates);
            if (axisTrigger is not null)
            {
                trigger = axisTrigger;
                return true;
            }
        }

        trigger = string.Empty;
        return false;
    }

    private void ToggleUdpStreaming()
    {
        if (GetUdpSender() is null)
        {
            StartUdpStreaming();
            return;
        }

        StopUdpStreaming(saveSettings: true);
    }

    private IEnumerable<BindingEvaluationRequest> BuildBindingRequests() =>
        bindingGroups
            .SelectMany(group => group.Bindings)
            .Select(binding => new BindingEvaluationRequest(binding.ActionId, binding.Trigger, binding.ActivationMode))
            .ToArray();

    private string? GetBindingTrigger(string actionId)
    {
        return bindingGroups
            .SelectMany(group => group.Bindings)
            .FirstOrDefault(binding => string.Equals(binding.ActionId, actionId, StringComparison.Ordinal))
            ?.Trigger;
    }

    private CameraPreset? ResolvePresetFromActions(RuntimeActionSnapshot actionSnapshot)
    {
        foreach (var presetItem in presetItems)
        {
            if (actionSnapshot.WasPressed(GetPresetSelectionActionId(presetItem.Id)))
            {
                return presetItem.Preset;
            }
        }

        return null;
    }

    private CameraPreset CurrentPreset => tunedPreset ?? selectedPreset;

    private string GetPresetDisplayName(CameraPreset preset)
    {
        var matchingItem = presetItems.FirstOrDefault(item => string.Equals(item.Id, preset.Id, StringComparison.Ordinal));
        return matchingItem?.DisplayName ?? preset.DisplayName;
    }

    private PresetListItemViewModel CreatePresetItem(CameraPreset preset)
    {
        return new PresetListItemViewModel(
            preset,
            displayNameOverride: null,
            renameCommitted: OnPresetDisplayNameCommitted,
            deleteRequested: OnPresetDeleteRequested,
            canDelete: !BuiltInPresetActionIds.ContainsKey(preset.Id));
    }

    private List<BindingGroupViewModel> BuildBindingGroups(IReadOnlyDictionary<string, string> storedTriggers)
    {
        var groups = new List<BindingGroupViewModel>
        {
            new(
                "Preset selection",
                "Preset hotkeys imported from the legacy profile. New presets start unassigned so you can learn a switch key after saving them.",
                presetItems
                    .Select(preset => new BindingEntryViewModel(
                        GetPresetSelectionActionId(preset.Id),
                        $"{preset.DisplayName} preset",
                        ResolveBindingTrigger(storedTriggers, GetPresetSelectionActionId(preset.Id), GetDefaultPresetSelectionTrigger(preset.Id)),
                        BindingActivationMode.Press))
                    .ToArray())
        };

        groups.AddRange(BindingCatalog.All.Select(group => new BindingGroupViewModel(
            group.Title,
            group.Description,
            group.Bindings.Select(binding => new BindingEntryViewModel(
                binding.ActionId,
                binding.Name,
                ResolveBindingTrigger(storedTriggers, binding.ActionId, binding.Trigger),
                binding.ActivationMode)).ToArray())));

        return groups;
    }

    private static string ResolveBindingTrigger(IReadOnlyDictionary<string, string> storedTriggers, string actionId, string defaultTrigger)
    {
        return storedTriggers.TryGetValue(actionId, out var overrideTrigger)
            ? overrideTrigger
            : defaultTrigger;
    }

    private static string GetPresetSelectionActionId(string presetId)
    {
        return BuiltInPresetActionIds.TryGetValue(presetId, out var actionId)
            ? actionId
            : $"{CustomPresetActionIdPrefix}{presetId}";
    }

    private static string GetDefaultPresetSelectionTrigger(string presetId)
    {
        return DefaultPresetSelectionTriggers.TryGetValue(presetId, out var trigger)
            ? trigger
            : string.Empty;
    }

    private void RebuildBindingGroups()
    {
        var existingTriggers = bindingGroups
            .SelectMany(group => group.Bindings)
            .ToDictionary(binding => binding.ActionId, binding => binding.Trigger, StringComparer.Ordinal);

        if (activeLearningBinding is not null)
        {
            activeLearningBinding.SetLearning(false);
            activeLearningBinding = null;
            activeLearningAllowsAxisCapture = false;
            learnAxisCaptureFilter.Clear();
            LearnModeStatus = "Learn mode cancelled because the preset bindings changed.";
        }

        bindingGroups = BuildBindingGroups(existingTriggers);
        OnPropertyChanged(nameof(BindingGroups));
        OnPropertyChanged(nameof(AiPartnerPushToTalkBinding));
        OnPropertyChanged(nameof(OverlayBindingSummary));
    }

    private void ToggleOverlay()
    {
        SetOverlayVisible(!IsOverlayVisible);
    }

    private void SetOverlayVisible(bool isVisible)
    {
        if (IsOverlayVisible == isVisible)
        {
            return;
        }

        IsOverlayVisible = isVisible;
        OverlayVisibilityChanged?.Invoke(isVisible);
        PublishOverlayState();
    }

    private void PublishOverlayState()
    {
        if (overlayStateWriter is null || overlayTransportFaulted)
        {
            return;
        }

        try
        {
            overlayStateWriter.Publish(new OverlaySharedStateSnapshot(
                IsVisible: IsOverlayVisible,
                CurrentPresetDisplayName: CurrentPresetDisplayName,
                LiveTrackIrStatus: LiveTrackIrStatus,
                UdpStreamingStatus: UdpStreamingStatus,
                OutputPoseSummary: OutputPoseSummary,
                RuntimeStateSummary: RuntimeStateSummary,
                TrackIrRateSummary: TrackIrRateSummary,
                UdpRateSummary: UdpRateSummary,
                UpdatedAtUtc: DateTimeOffset.UtcNow));
        }
        catch (Exception exception)
        {
            overlayTransportFaulted = true;
            overlayTransportStatus = $"DX feed failed: {exception.Message}";
            OnPropertyChanged(nameof(OverlaySupportSummary));
        }
    }

    private void SetSelectedPreset(CameraPreset? value, bool clearTuneOverrides, bool persistSelection)
    {
        if (value is null)
        {
            return;
        }

        var matchingItem = presetItems.FirstOrDefault(item => string.Equals(item.Id, value.Id, StringComparison.Ordinal));
        if (!ReferenceEquals(selectedPresetItem, matchingItem))
        {
            selectedPresetItem = matchingItem;
            OnPropertyChanged(nameof(SelectedPresetItem));
        }

        if (ReferenceEquals(selectedPreset, value) && (!clearTuneOverrides || tunedPreset is null))
        {
            return;
        }

        selectedPreset = value;
        if (clearTuneOverrides)
        {
            tunedPreset = null;
        }

        OnPropertyChanged(nameof(CurrentPresetDisplayName));
        OnPropertyChanged(nameof(CurrentPresetId));
        OnPropertyChanged(nameof(SelectedPresetParameters));
        OnPropertyChanged(nameof(PosePreviews));
        OnPropertyChanged(nameof(AiPartnerTelemetrySummary));

        if (persistSelection)
        {
            SaveSettings();
        }
    }

    private void SetTunedPreset(CameraPreset value)
    {
        tunedPreset = value;
        OnPropertyChanged(nameof(CurrentPresetDisplayName));
        OnPropertyChanged(nameof(CurrentPresetId));
        OnPropertyChanged(nameof(SelectedPresetParameters));
        OnPropertyChanged(nameof(PosePreviews));
        OnPropertyChanged(nameof(AiPartnerTelemetrySummary));
    }

    private void OnPresetDisplayNameCommitted(PresetListItemViewModel presetItem)
    {
        var updatedPreset = presetItem.Preset with { DisplayName = presetItem.DisplayName };
        presetItem.UpdatePreset(updatedPreset);

        if (string.Equals(selectedPreset.Id, presetItem.Id, StringComparison.Ordinal))
        {
            selectedPreset = selectedPreset with { DisplayName = presetItem.DisplayName };
            OnPropertyChanged(nameof(AiPartnerTelemetrySummary));
        }

        if (tunedPreset is not null && string.Equals(tunedPreset.Id, presetItem.Id, StringComparison.Ordinal))
        {
            tunedPreset = tunedPreset with { DisplayName = presetItem.DisplayName };
        }

        RebuildBindingGroups();
        SaveSettings();

        if (string.Equals(CurrentPreset.Id, presetItem.Id, StringComparison.Ordinal))
        {
            OnPropertyChanged(nameof(CurrentPresetDisplayName));
        }
    }

    private void OnPresetDeleteRequested(PresetListItemViewModel presetItem)
    {
        if (!presetItem.CanDelete)
        {
            return;
        }

        presetItems = presetItems
            .Where(item => !string.Equals(item.Id, presetItem.Id, StringComparison.Ordinal))
            .ToList();
        OnPropertyChanged(nameof(Presets));

        var fallbackPreset = presetItems.FirstOrDefault(item => string.Equals(item.Id, selectedPreset.Id, StringComparison.Ordinal))?.Preset
            ?? presetItems.FirstOrDefault(item => string.Equals(item.Id, "lagg", StringComparison.Ordinal))?.Preset
            ?? presetItems.FirstOrDefault()?.Preset
            ?? PresetCatalog.Default;

        SetSelectedPreset(fallbackPreset, clearTuneOverrides: true, persistSelection: false);
        RebuildBindingGroups();
        SaveSettings();
        LearnModeStatus = $"Deleted preset {presetItem.DisplayName}.";
    }

    private string SaveCurrentPreset()
    {
        var persistedDisplayName = CurrentPresetDisplayName;
        var presetToSave = CurrentPreset with { DisplayName = persistedDisplayName };

        if (string.Equals(selectedPreset.Id, "reset", StringComparison.Ordinal))
        {
            var newPresetName = PresetNameAllocator.AllocateUnknownDisplayName(presetItems.Select(item => item.DisplayName));
            var newPreset = new CameraPreset(
                $"custom-{Guid.NewGuid():N}",
                newPresetName,
                presetToSave.Parameters.ToArray());

            presetItems = presetItems
                .Concat(new[] { CreatePresetItem(newPreset) })
                .ToList();
            OnPropertyChanged(nameof(Presets));
            SetSelectedPreset(newPreset, clearTuneOverrides: true, persistSelection: false);
            RebuildBindingGroups();
            SaveSettings();

            return $"Saved new preset {newPresetName}.";
        }

        var presetItem = presetItems.FirstOrDefault(item => string.Equals(item.Id, selectedPreset.Id, StringComparison.Ordinal));
        if (presetItem is null)
        {
            return "Save preset failed: current preset was not found.";
        }

        presetItem.UpdatePreset(presetToSave);
        SetSelectedPreset(presetToSave, clearTuneOverrides: true, persistSelection: false);
        RebuildBindingGroups();
        SaveSettings();

        return $"Saved preset {persistedDisplayName}.";
    }

    private void StartUdpStreaming(bool saveSettings = true)
    {
        lock (udpSync)
        {
            if (udpSender is not null)
            {
                return;
            }

            udpSender = new OpenTrackUdpSender();
            runtimeYOffset = 0;
            UdpStreamingStatus = "armed";
            UdpPacketSummary = $"Streaming to {udpSender.Destination}.";
            OnPropertyChanged(nameof(UdpToggleLabel));
        }

        if (saveSettings)
        {
            SaveSettings();
        }
    }

    private void StopUdpStreaming(bool saveSettings)
    {
        lock (udpSync)
        {
            udpSender?.Dispose();
            udpSender = null;
            UdpStreamingStatus = "stopped";
            UdpPacketSummary = "UDP streaming stopped.";
            OnPropertyChanged(nameof(UdpToggleLabel));
        }

        if (saveSettings)
        {
            SaveSettings();
        }
    }

    private OpenTrackUdpSender? GetUdpSender()
    {
        lock (udpSync)
        {
            return udpSender;
        }
    }

    private void OnIl2RawVideoLibraryPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.Equals(e.PropertyName, nameof(Il2RawVideoLibraryViewModel.LibraryPath), StringComparison.Ordinal))
        {
            SaveSettings();
        }
    }

    private void SaveSettings()
    {
        settingsStore.Save(new AppSettingsDocument
        {
            SelectedPresetId = selectedPreset.Id,
            TrackIrInputProfileId = trackIrInputProfile.Id,
            SelectedMainTabIndex = SelectedMainTabIndex,
            TrackIrYawMultiplier = trackIrYawMultiplier,
            AutoStartUdpStreaming = GetUdpSender() is not null,
            AiPartnerEnabled = IsAiPartnerEnabled,
            AiPartnerMicrophoneTestModeEnabled = IsAiPartnerMicrophoneTestModeEnabled,
            AiPartnerBriefing = AiPartnerBriefing,
            AiPartnerScreenshotCadenceSeconds = AiPartnerScreenshotCadenceSeconds,
            Il2RawVideoLibraryPath = Il2RawVideoLibrary.LibraryPath,
            BindingTriggers = bindingGroups
                .SelectMany(group => group.Bindings)
                .ToDictionary(binding => binding.ActionId, binding => binding.Trigger, StringComparer.Ordinal),
            PresetDisplayNames = new Dictionary<string, string>(StringComparer.Ordinal),
            Presets = PersistedPresetCatalog.Save(presetItems.Select(item => item.Preset)).ToList(),
        });
    }

    private static RuntimeViewState ApplyTrackIrInputProfile(RuntimeViewState state, TrackIrInputProfile profile, double yawMultiplier)
    {
        return state with
        {
            AutoCornerStart = profile.AutoCornerStart,
            AutoCornerEnd = profile.AutoCornerEnd,
            AutoCornerXEnd = profile.AutoCornerXEnd,
            TrackIrYawScale = TrackIrScaling.ApplyYawMultiplier(profile.YawScale, yawMultiplier),
            TrackIrPitchScale = profile.PitchScale,
            TrackIrXDeadZone = profile.XDeadZone,
            TrackIrYEngageYawThreshold = profile.YEngageYawThreshold,
        };
    }

    private static void RegisterRateSample(DateTimeOffset sampledAt, ref DateTimeOffset windowStartedAt, ref int samplesInWindow, ref double rateHz)
    {
        if (windowStartedAt == DateTimeOffset.MinValue)
        {
            windowStartedAt = sampledAt;
        }

        samplesInWindow += 1;
        var elapsedSeconds = (sampledAt - windowStartedAt).TotalSeconds;
        if (elapsedSeconds < 1.0)
        {
            return;
        }

        rateHz = samplesInWindow / elapsedSeconds;
        windowStartedAt = sampledAt;
        samplesInWindow = 0;
    }

    private static double GetDefaultStickY(IReadOnlyList<JoystickLiveState> states)
    {
        var state = ResolveJoystickByAlias(states, "Stick") ?? states.FirstOrDefault();
        if (state is null)
        {
            return 500;
        }

        return NormalizeAxis(state.Y) * 1000.0;
    }

    private static JoystickLiveState? ResolveJoystickByAlias(IReadOnlyList<JoystickLiveState> joystickStates, string alias)
    {
        if (joystickStates.Count == 0)
        {
            return null;
        }

        if (alias.Equals("Throttle", StringComparison.OrdinalIgnoreCase))
        {
            return joystickStates.FirstOrDefault(candidate => candidate.Name.Contains("throttle", StringComparison.OrdinalIgnoreCase))
                ?? joystickStates.FirstOrDefault();
        }

        return joystickStates.FirstOrDefault(candidate =>
                (candidate.Name.Contains("stick", StringComparison.OrdinalIgnoreCase) || candidate.Name.Contains("joystick", StringComparison.OrdinalIgnoreCase)) &&
                !candidate.Name.Contains("throttle", StringComparison.OrdinalIgnoreCase))
            ?? joystickStates.Skip(1).FirstOrDefault()
            ?? joystickStates.FirstOrDefault();
    }

    private static string FormatRuntimeState(RuntimeViewState state, FlapAutomationState flapState, CheckSixNotifierState checkSixState, TuneModeState tuneModeState, string presetDisplayName, double stickY)
    {
        var flags = new List<string>();

        if (state.IsSideView)
        {
            flags.Add("side");
        }

        if (state.IsCustomView)
        {
            flags.Add("custom");
        }

        if (state.IsGunViewAtCenter)
        {
            flags.Add("gun-center");
        }

        if (state.IsHeadCenter)
        {
            flags.Add("head-center");
        }

        if (state.IsHeadHigh)
        {
            flags.Add("head-high");
        }

        if (state.IsHeadHighest)
        {
            flags.Add("head-highest");
        }

        if (state.IsHeadDynamic)
        {
            flags.Add("head-dynamic");
        }

        if (state.IsZoomIn)
        {
            flags.Add("zoom-in");
        }

        if (state.IsZoomOut)
        {
            flags.Add("zoom-out");
        }

        if (flags.Count == 0)
        {
            flags.Add("neutral");
        }

        if (state.CenterPendingFrameTimer is not null)
        {
            flags.Add($"sync={state.CenterPendingFrameTimer}");
        }

        if (flapState.ActiveOperation != FlapOperation.None)
        {
            flags.Add($"flaps-{flapState.ActiveOperation.ToString().ToLowerInvariant()}");
        }
        else if (flapState.IsFlapOpen)
        {
            flags.Add("flaps-open");
        }

        if (checkSixState.IsActivated)
        {
            flags.Add("check-six-on");
        }

        if (tuneModeState.IsEnabled)
        {
            flags.Add(string.IsNullOrWhiteSpace(tuneModeState.SelectedModeName) ? "tune-waiting" : $"tune={tuneModeState.SelectedModeName}");
        }

        return $"Preset {presetDisplayName} | flags: {string.Join(", ", flags)} | stickY {stickY:0} | yOffset {state.YOffset:0.00}";
    }

    private static string FormatIl2SpeedSummary(float? easMetersPerSecond)
    {
        if (easMetersPerSecond is null)
        {
            return "Speed: n/a";
        }

        var kilometersPerHour = easMetersPerSecond.Value * 3.6d;
        return $"Speed: {kilometersPerHour:0} km/h EAS";
    }

    private static string FormatIl2FlapsSummary(float? flapsPosition)
    {
        if (flapsPosition is null)
        {
            return "Flaps: n/a";
        }

        var clamped = Math.Clamp(flapsPosition.Value, 0f, 1f);
        if (clamped <= 0.01f)
        {
            return "Flaps: closed";
        }

        if (clamped >= 0.99f)
        {
            return "Flaps: full";
        }

        return $"Flaps: {clamped * 100f:0}%";
    }

    private void SpeakSpeechCallout(SpeechCallout callout)
    {
        switch (callout)
        {
            case SpeechCallout.Flaps:
                notificationOutputService.SpeakFlapsReminder();
                break;
            case SpeechCallout.CheckSixSpeedLow:
                notificationOutputService.SpeakCheckSixSpeedCue(CheckSixSpeedCue.Low);
                break;
            case SpeechCallout.CheckSixSpeedOptimal:
                notificationOutputService.SpeakCheckSixSpeedCue(CheckSixSpeedCue.Optimal);
                break;
            case SpeechCallout.CheckSixSpeedDanger:
                notificationOutputService.SpeakCheckSixSpeedCue(CheckSixSpeedCue.Danger);
                break;
        }
    }

    private static double NormalizeAxis(uint value) => Math.Clamp(value / 65535.0, 0, 1);

    private static string FormatAiPartnerScreenshotStatus(AiPartnerScreenshotCaptureResult captureResult)
    {
        return captureResult.Frame is null
            ? captureResult.StatusMessage
            : $"{captureResult.StatusMessage} Captured on push-to-talk release at {captureResult.Frame.CapturedAtUtc.ToLocalTime():HH:mm:ss}.";
    }

    private static string AppendAiPartnerTranscript(string currentTranscript, string? pilotTranscript, string? aiReply, string? systemMessage)
    {
        var entries = new List<string>();

        if (!string.IsNullOrWhiteSpace(currentTranscript) &&
            !currentTranscript.StartsWith("Hold push-to-talk", StringComparison.Ordinal))
        {
            entries.Add(currentTranscript.Trim());
        }

        if (!string.IsNullOrWhiteSpace(pilotTranscript))
        {
            entries.Add($"Pilot: {pilotTranscript.Trim()}");
        }

        if (!string.IsNullOrWhiteSpace(aiReply))
        {
            entries.Add($"AI: {aiReply.Trim()}");
        }

        if (!string.IsNullOrWhiteSpace(systemMessage))
        {
            entries.Add($"System: {systemMessage.Trim()}");
        }

        var combined = string.Join(Environment.NewLine + Environment.NewLine, entries);
        const int maxCharacters = 5000;
        if (combined.Length <= maxCharacters)
        {
            return combined;
        }

        return combined[^maxCharacters..];
    }

    private static bool AllowsAxisLearn(string actionId) => string.Equals(actionId, BindingActionIds.StickYAxis, StringComparison.Ordinal);

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public sealed record PoseScenarioPreview(string Title, string InputSummary, string OutputSummary, string Note);

public sealed record JoystickDiscoverySummary(string Summary, IReadOnlyList<JoystickDeviceInfo>? Devices = null);