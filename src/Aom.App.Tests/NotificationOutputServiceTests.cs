using Aom.App.Services.Output;
using Aom.Core.Runtime;
using Xunit;

namespace Aom.App.Tests;

public sealed class NotificationOutputServiceTests
{
    [Fact]
    public void BuildPresetLoadedMessage_FormatsFriendlyAnnouncement()
    {
        var message = NotificationOutputService.BuildPresetLoadedMessage("Yakodin and B");

        Assert.Equal("Yakodin and B preset loaded", message);
    }

    [Fact]
    public void BuildPresetLoadedMessage_FallsBackWhenNameIsBlank()
    {
        var message = NotificationOutputService.BuildPresetLoadedMessage("   ");

        Assert.Equal("Preset loaded", message);
    }

    [Fact]
    public void BuildFlapsReminderMessage_ReturnsFriendlyReminder()
    {
        var message = NotificationOutputService.BuildFlapsReminderMessage();

        Assert.Equal("Flaps.", message);
    }

    [Fact]
    public void BuildCheckSixSpeedCueMessage_ReturnsExpectedSpeech()
    {
        Assert.Equal("low", NotificationOutputService.BuildCheckSixSpeedCueMessage(CheckSixSpeedCue.Low));
        Assert.Equal("optimal", NotificationOutputService.BuildCheckSixSpeedCueMessage(CheckSixSpeedCue.Optimal));
        Assert.Equal("danger", NotificationOutputService.BuildCheckSixSpeedCueMessage(CheckSixSpeedCue.Danger));
        Assert.Null(NotificationOutputService.BuildCheckSixSpeedCueMessage(CheckSixSpeedCue.None));
    }

    [Fact]
    public void BuildTuneModeStatusMessage_TrimsValidStatus()
    {
        var message = NotificationOutputService.BuildTuneModeStatusMessage("  Game mode.  ");

        Assert.Equal("Game mode.", message);
    }

    [Fact]
    public void BuildTuneModeStatusMessage_AppendsPromptWithCycleBinding()
    {
        var message = NotificationOutputService.BuildTuneModeStatusMessage("TuneMode: Please select tune mode.", "RightControl + Insert");

        Assert.Equal("Please select tune mode by pressing Right Control plus Insert.", message);
    }

    [Fact]
    public void BuildTuneModeStatusMessage_AppendsPeriodToFriendlyModeName()
    {
        var message = NotificationOutputService.BuildTuneModeStatusMessage("Auto");

        Assert.Equal("Auto.", message);
    }

    [Fact]
    public void BuildTuneModeStatusMessage_ReturnsNullForBlankStatus()
    {
        var message = NotificationOutputService.BuildTuneModeStatusMessage("   ");

        Assert.Null(message);
    }

    [Fact]
    public void SpeakAiPartnerReply_IgnoresBlankInputViaBuilderContract()
    {
        var service = new NotificationOutputService();

        service.SpeakAiPartnerReply("   ");

        service.Dispose();
    }
}