using Aom.App.Services.Input;
using Aom.Core.Bindings;
using Xunit;

namespace Aom.App.Tests;

public sealed class BindingInputResolverTests
{
    [Fact]
    public void LearnAxisFilter_DoesNotTriggerForSmallAxisNoise()
    {
        var filter = new LearnAxisCaptureFilter(0.18);

        filter.Reset(new[] { CreateJoystickState(y: 32768) });

        var trigger = filter.TryCapture(new[] { CreateJoystickState(y: 34500) });

        Assert.Null(trigger);
    }

    [Fact]
    public void LearnAxisFilter_TriggersAfterMeaningfulAxisTravel()
    {
        var filter = new LearnAxisCaptureFilter(0.18);

        filter.Reset(new[] { CreateJoystickState(y: 32768) });

        var trigger = filter.TryCapture(new[] { CreateJoystickState(y: 47000) });

        Assert.Equal("Joystick 0 Axis Y", trigger);
    }

    [Fact]
    public void Evaluate_MatchesKeyboardShortcutBinding()
    {
        var resolver = new BindingInputResolver();
        var keyboardState = new KeyboardInputSnapshot(
            new[]
            {
                0xA1,
                0x25,
            });

        var snapshot = resolver.Evaluate(
            new[] { new BindingEvaluationRequest(BindingActionIds.TuneAdjustXPositive, "RightShift + LeftArrow", BindingActivationMode.Hold) },
            Array.Empty<JoystickLiveState>(),
            keyboardState);

        Assert.True(snapshot.IsActive(BindingActionIds.TuneAdjustXPositive));
    }

    [Fact]
    public void Evaluate_MatchesJoystickButtonBinding()
    {
        var resolver = new BindingInputResolver();
        var joystickState = new JoystickLiveState(
            DeviceId: 0,
            Name: "Test",
            X: 0,
            Y: 0,
            Z: 0,
            R: 0,
            U: 0,
            V: 0,
            Pov: 0xFFFF,
            Buttons: 0,
            PressedButtons: new[] { 6 },
            Summary: "buttons 6");

        var snapshot = resolver.Evaluate(
            new[] { new BindingEvaluationRequest(BindingActionIds.CenterAll, "Joystick 0 Button 6", BindingActivationMode.Hold) },
            new[] { joystickState },
            KeyboardInputSnapshot.Empty);

        Assert.True(snapshot.IsActive(BindingActionIds.CenterAll));
    }

    [Fact]
    public void Evaluate_MatchesHighNumberJoystickButtonBinding()
    {
        var resolver = new BindingInputResolver();
        var joystickState = new JoystickLiveState(
            DeviceId: 0,
            Name: "Test",
            X: 0,
            Y: 0,
            Z: 0,
            R: 0,
            U: 0,
            V: 0,
            Pov: 0xFFFF,
            Buttons: 0,
            PressedButtons: new[] { 45 },
            Summary: "buttons 45");

        var snapshot = resolver.Evaluate(
            new[] { new BindingEvaluationRequest(BindingActionIds.CenterAll, "Joystick 0 Button 45", BindingActivationMode.Hold) },
            new[] { joystickState },
            KeyboardInputSnapshot.Empty);

        Assert.True(snapshot.IsActive(BindingActionIds.CenterAll));
    }

    [Fact]
    public void Evaluate_PressBindingFiresOnlyOnFirstKeyboardFrame()
    {
        var resolver = new BindingInputResolver();
        var keyboardState = new KeyboardInputSnapshot(new[] { 0x91 });
        var binding = new BindingEvaluationRequest(BindingActionIds.ToggleTuneMode, "ScrollLock", BindingActivationMode.Press);

        var first = resolver.Evaluate(new[] { binding }, Array.Empty<JoystickLiveState>(), keyboardState);
        var second = resolver.Evaluate(new[] { binding }, Array.Empty<JoystickLiveState>(), keyboardState);

        Assert.True(first.WasPressed(BindingActionIds.ToggleTuneMode));
        Assert.False(second.WasPressed(BindingActionIds.ToggleTuneMode));
    }

    private static JoystickLiveState CreateJoystickState(uint x = 0, uint y = 0, uint z = 0) =>
        new(
            DeviceId: 0,
            Name: "Test",
            X: x,
            Y: y,
            Z: z,
            R: 0,
            U: 0,
            V: 0,
            Pov: 0xFFFF,
            Buttons: 0,
            PressedButtons: Array.Empty<int>(),
            Summary: "test");
}