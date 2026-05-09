using Aom.App.Services.Il2;
using Aom.Core.Runtime;
using Xunit;

namespace Aom.App.Tests;

public sealed class Il2TelemetryFlapMonitorTests
{
    [Fact]
    public void HasFreshFlapTelemetry_ReturnsTrueWhenFlapsAreFresh()
    {
        var now = DateTimeOffset.UtcNow;

        var result = Il2TelemetryFlapMonitor.HasFreshFlapTelemetry(0.4f, now.AddMilliseconds(-120), now, TimeSpan.FromMilliseconds(500));

        Assert.True(result);
    }

    [Fact]
    public void HasFreshFlapTelemetry_ReturnsFalseWhenFlapsAreMissingOrStale()
    {
        var now = DateTimeOffset.UtcNow;

        Assert.False(Il2TelemetryFlapMonitor.HasFreshFlapTelemetry(null, now, now, TimeSpan.FromMilliseconds(500)));
        Assert.False(Il2TelemetryFlapMonitor.HasFreshFlapTelemetry(0.4f, now.AddMilliseconds(-800), now, TimeSpan.FromMilliseconds(500)));
    }

    [Fact]
    public void CreateTrackedState_ResetsAutomationAndUsesTelemetryPosition()
    {
        var currentState = new FlapAutomationState
        {
            IsFlapOpen = false,
            ActiveOperation = FlapOperation.Opening,
            OperationStartedAt = DateTimeOffset.UtcNow,
            LastReminderAt = DateTimeOffset.UtcNow.AddSeconds(-1),
        };

        var nextState = Il2TelemetryFlapMonitor.CreateTrackedState(currentState, 0.8f);

        Assert.True(nextState.IsFlapOpen);
        Assert.Equal(FlapOperation.None, nextState.ActiveOperation);
        Assert.Null(nextState.OperationStartedAt);
        Assert.Equal(currentState.LastReminderAt, nextState.LastReminderAt);
    }
}