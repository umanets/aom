using System.Collections.ObjectModel;

namespace Aom.Core.Bindings;

public static class BindingCatalog
{
    public static IReadOnlyList<BindingGroup> All { get; } = new ReadOnlyCollection<BindingGroup>(
        new[]
        {
            Group(
                "Preset management",
                "Persist the currently selected preset. Saving Reset creates a new preset that starts unassigned so you can learn a switch key later.",
                (BindingActionIds.SavePreset, "Save current preset", "UpArrow + NumPadPeriod", BindingActivationMode.Press)),
            Group(
                "View controls",
                "Primary flight-view toggles from throttle, stick, and keyboard modifier combos.",
                ("view.global-center-key", "Global center output key", "F7", BindingActivationMode.Press),
                (BindingActionIds.CenterAll, "Center all", "Throttle button 45", BindingActivationMode.Hold),
                (BindingActionIds.GunViewToggle, "Gun view toggle", "Throttle button 4", BindingActivationMode.Press),
                (BindingActionIds.HeadCenter, "Vertical gun center", "F1 + LeftAlt + LeftControl + LeftShift", BindingActivationMode.Press),
                (BindingActionIds.SideViewHold, "Side view toggle", "Throttle button 3", BindingActivationMode.Hold),
                (BindingActionIds.CustomViewHold, "Custom view toggle", "Throttle button 2", BindingActivationMode.Hold)),
            Group(
                "Head position and zoom",
                "Head Y modes and zoom bindings currently hardcoded in the script.",
                (BindingActionIds.HeadHigh, "Head high", "Throttle button 60", BindingActivationMode.Press),
                (BindingActionIds.HeadHighest, "Head highest", "Throttle button 59", BindingActivationMode.Press),
                (BindingActionIds.HeadDynamic, "Dynamic head Y", "F6 + LeftAlt + LeftControl + LeftShift", BindingActivationMode.Press),
                (BindingActionIds.ZoomOut, "Zoom out", "F4 + LeftAlt + LeftControl + LeftShift", BindingActivationMode.Press),
                (BindingActionIds.ZoomIn, "Zoom in", "F5 + LeftAlt + LeftControl + LeftShift", BindingActivationMode.Press),
                (BindingActionIds.ZoomCenter, "Zoom center", "Throttle button 19", BindingActivationMode.Press),
                (BindingActionIds.StickYAxis, "Dynamic Y source", "Stick Y axis", BindingActivationMode.Hold)),
            Group(
                "Aircraft helpers",
                "Timed automation and awareness helpers that still need runtime adapters in the rewrite.",
                (BindingActionIds.OpenFlaps, "Open flaps", "Stick button 1", BindingActivationMode.Press),
                (BindingActionIds.CloseFlaps, "Close flaps", "Stick button 0", BindingActivationMode.Press),
                (BindingActionIds.ToggleCheckSix, "Toggle check six notifier", "F12 + LeftAlt + LeftControl + LeftShift", BindingActivationMode.Press),
                (BindingActionIds.ToggleOverlay, "Toggle overlay", "F8 + LeftAlt + LeftControl + LeftShift", BindingActivationMode.Press)),
            Group(
                "AI Partner",
                "Bindings reserved for the future AI wingman workflow.",
                (BindingActionIds.AiPartnerPushToTalk, "Push-to-talk", string.Empty, BindingActivationMode.Hold)),
            Group(
                "Tune mode",
                "Current tuning workflow that the new desktop app will replace with direct editable controls.",
                (BindingActionIds.ToggleTuneMode, "Toggle tune mode", "ScrollLock", BindingActivationMode.Press),
                (BindingActionIds.CycleTuneMode, "Cycle tune mode", "RightControl + Insert", BindingActivationMode.Press),
                (BindingActionIds.TuneAdjustXPositive, "Adjust X +", "RightShift + LeftArrow", BindingActivationMode.Hold),
                (BindingActionIds.TuneAdjustXNegative, "Adjust X -", "RightShift + RightArrow", BindingActivationMode.Hold),
                (BindingActionIds.TuneAdjustYPositive, "Adjust Y +", "RightShift + UpArrow", BindingActivationMode.Hold),
                (BindingActionIds.TuneAdjustYNegative, "Adjust Y -", "RightShift + DownArrow", BindingActivationMode.Hold),
                (BindingActionIds.TuneAdjustZPositive, "Adjust Z +", "RightShift + PageUp", BindingActivationMode.Hold),
                (BindingActionIds.TuneAdjustZNegative, "Adjust Z -", "RightShift + PageDown", BindingActivationMode.Hold),
                (BindingActionIds.TuneAdjustYawNegative, "Adjust yaw -", "RightShift + Delete", BindingActivationMode.Hold),
                (BindingActionIds.TuneAdjustYawPositive, "Adjust yaw +", "RightShift + End", BindingActivationMode.Hold),
                (BindingActionIds.TuneAdjustPitchNegative, "Adjust pitch -", "RightShift + Insert", BindingActivationMode.Hold),
                (BindingActionIds.TuneAdjustPitchPositive, "Adjust pitch +", "RightShift + Home", BindingActivationMode.Hold)),
        });

    private static BindingGroup Group(string title, string description, params (string ActionId, string Name, string Trigger, BindingActivationMode ActivationMode)[] bindings)
    {
        var items = bindings
            .Select(binding => new BindingDefinition(binding.ActionId, binding.Name, binding.Trigger, binding.ActivationMode))
            .ToArray();

        return new BindingGroup(title, description, items);
    }
}