using Aom.Core.Bindings;
using Aom.Core.Runtime;
using System.Windows.Input;

namespace Aom.App.Services.Input;

public sealed class BindingInputResolver
{
    private const double AxisActiveThresholdNormalized = 0.18;
    private const int VkShift = 0x10;
    private const int VkControl = 0x11;
    private const int VkMenu = 0x12;
    private static readonly Dictionary<string, int> KeyboardTokens = new(StringComparer.OrdinalIgnoreCase)
    {
        ["LeftAlt"] = 0xA4,
        ["RightAlt"] = 0xA5,
        ["LeftControl"] = 0xA2,
        ["RightControl"] = 0xA3,
        ["LeftShift"] = 0xA0,
        ["RightShift"] = 0xA1,
        ["UpArrow"] = 0x26,
        ["DownArrow"] = 0x28,
        ["LeftArrow"] = 0x25,
        ["RightArrow"] = 0x27,
        ["Delete"] = 0x2E,
        ["End"] = 0x23,
        ["Insert"] = 0x2D,
        ["Home"] = 0x24,
        ["PageUp"] = 0x21,
        ["PageDown"] = 0x22,
        ["ScrollLock"] = 0x91,
        ["NumPadPeriod"] = 0x6E,
    };
    private readonly Dictionary<string, bool> previousStates = new(StringComparer.Ordinal);

    public RuntimeActionSnapshot Evaluate(IEnumerable<BindingEvaluationRequest> bindings, IReadOnlyList<JoystickLiveState> joystickStates, KeyboardInputSnapshot? keyboardState = null)
    {
        var activeActionIds = new HashSet<string>(StringComparer.Ordinal);
        var pressedActionIds = new HashSet<string>(StringComparer.Ordinal);
        var axisValues = new Dictionary<string, double>(StringComparer.Ordinal);
        var resolvedKeyboardState = keyboardState ?? KeyboardInputSnapshot.Empty;

        foreach (var binding in bindings)
        {
            var isActive = TryEvaluate(binding.Trigger, joystickStates, resolvedKeyboardState, out var axisValue);
            var wasActive = previousStates.TryGetValue(binding.ActionId, out var previous) && previous;

            if (isActive)
            {
                activeActionIds.Add(binding.ActionId);

                if (binding.ActivationMode == BindingActivationMode.Press && !wasActive)
                {
                    pressedActionIds.Add(binding.ActionId);
                }
            }

            if (axisValue is not null)
            {
                axisValues[binding.ActionId] = axisValue.Value;
            }

            previousStates[binding.ActionId] = isActive;
        }

        return new RuntimeActionSnapshot(activeActionIds, pressedActionIds, axisValues);
    }

    private static bool TryEvaluate(string trigger, IReadOnlyList<JoystickLiveState> joystickStates, KeyboardInputSnapshot keyboardState, out double? axisValue)
    {
        axisValue = null;

        if (string.IsNullOrWhiteSpace(trigger))
        {
            return false;
        }

        if (TryParseButtonTrigger(trigger, joystickStates, out var buttonState, out var buttonIndex))
        {
            return buttonState.PressedButtons.Contains(buttonIndex);
        }

        if (TryParseAxisTrigger(trigger, joystickStates, out var axisState, out var axisName))
        {
            var normalizedValue = GetAxisNormalized(axisState, axisName);
            axisValue = normalizedValue * 1000.0;
            return Math.Abs(normalizedValue - 0.5) >= AxisActiveThresholdNormalized;
        }

        return TryEvaluateKeyboardTrigger(trigger, keyboardState);
    }

    private static bool TryEvaluateKeyboardTrigger(string trigger, KeyboardInputSnapshot keyboardState)
    {
        if (keyboardState.PressedVirtualKeys.Count == 0)
        {
            return false;
        }

        var tokens = trigger.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (tokens.Length == 0)
        {
            return false;
        }

        var parsedVirtualKeys = new List<int>(tokens.Length);
        foreach (var token in tokens)
        {
            if (!TryGetVirtualKeyForToken(token, out var virtualKey))
            {
                return false;
            }

            parsedVirtualKeys.Add(virtualKey);
        }

        return parsedVirtualKeys.All(virtualKey => MatchesVirtualKey(keyboardState, virtualKey));
    }

    private static bool TryParseButtonTrigger(string trigger, IReadOnlyList<JoystickLiveState> joystickStates, out JoystickLiveState state, out int buttonIndex)
    {
        state = default!;
        buttonIndex = -1;

        if (trigger.StartsWith("Joystick ", StringComparison.OrdinalIgnoreCase))
        {
            var parts = trigger.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length == 4 &&
                int.TryParse(parts[1], out var deviceId) &&
                parts[2].Equals("Button", StringComparison.OrdinalIgnoreCase) &&
                int.TryParse(parts[3], out var parsedButtonIndex))
            {
                var resolvedState = joystickStates.FirstOrDefault(candidate => candidate.DeviceId == deviceId);
                if (resolvedState is not null)
                {
                    state = resolvedState;
                    buttonIndex = parsedButtonIndex;
                    return true;
                }
            }
        }

        if (trigger.StartsWith("Throttle button ", StringComparison.OrdinalIgnoreCase) &&
            int.TryParse(trigger[16..], out var throttleButtonIndex) &&
            TryResolveAliasState("Throttle", joystickStates, out var throttleState))
        {
            state = throttleState;
            buttonIndex = throttleButtonIndex;
            return true;
        }

        if (trigger.StartsWith("Stick button ", StringComparison.OrdinalIgnoreCase) &&
            int.TryParse(trigger[13..], out var stickButtonIndex) &&
            TryResolveAliasState("Stick", joystickStates, out var stickState))
        {
            state = stickState;
            buttonIndex = stickButtonIndex;
            return true;
        }

        return false;
    }

    private static bool TryParseAxisTrigger(string trigger, IReadOnlyList<JoystickLiveState> joystickStates, out JoystickLiveState state, out string axisName)
    {
        state = default!;
        axisName = string.Empty;

        if (trigger.StartsWith("Joystick ", StringComparison.OrdinalIgnoreCase))
        {
            var parts = trigger.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length == 4 &&
                int.TryParse(parts[1], out var deviceId) &&
                parts[2].Equals("Axis", StringComparison.OrdinalIgnoreCase))
            {
                var resolvedState = joystickStates.FirstOrDefault(candidate => candidate.DeviceId == deviceId);
                if (resolvedState is not null)
                {
                    state = resolvedState;
                    axisName = parts[3];
                    return true;
                }
            }
        }

        if (trigger.StartsWith("Throttle ", StringComparison.OrdinalIgnoreCase) &&
            trigger.EndsWith(" axis", StringComparison.OrdinalIgnoreCase) &&
            TryResolveAliasState("Throttle", joystickStates, out var throttleState))
        {
            state = throttleState;
            axisName = trigger[9..^5].Trim();
            return true;
        }

        if (trigger.StartsWith("Stick ", StringComparison.OrdinalIgnoreCase) &&
            trigger.EndsWith(" axis", StringComparison.OrdinalIgnoreCase) &&
            TryResolveAliasState("Stick", joystickStates, out var stickState))
        {
            state = stickState;
            axisName = trigger[6..^5].Trim();
            return true;
        }

        return false;
    }

    private static bool TryResolveAliasState(string alias, IReadOnlyList<JoystickLiveState> joystickStates, out JoystickLiveState state)
    {
        state = default!;

        var namedMatch = alias.Equals("Throttle", StringComparison.OrdinalIgnoreCase)
            ? joystickStates.FirstOrDefault(candidate => candidate.Name.Contains("throttle", StringComparison.OrdinalIgnoreCase))
            : joystickStates.FirstOrDefault(candidate =>
                (candidate.Name.Contains("stick", StringComparison.OrdinalIgnoreCase) || candidate.Name.Contains("joystick", StringComparison.OrdinalIgnoreCase)) &&
                !candidate.Name.Contains("throttle", StringComparison.OrdinalIgnoreCase));

        if (namedMatch is not null)
        {
            state = namedMatch;
            return true;
        }

        state = alias.Equals("Throttle", StringComparison.OrdinalIgnoreCase)
            ? joystickStates.FirstOrDefault()!
            : joystickStates.Skip(1).FirstOrDefault() ?? joystickStates.FirstOrDefault()!;

        return state is not null;
    }

    private static double GetAxisNormalized(JoystickLiveState state, string axisName) =>
        NormalizeAxis(axisName switch
        {
            "X" => state.X,
            "Y" => state.Y,
            "Z" => state.Z,
            "R" => state.R,
            "U" => state.U,
            "V" => state.V,
            _ => state.Y,
        });

    private static bool TryGetVirtualKeyForToken(string token, out int virtualKey)
    {
        if (KeyboardTokens.TryGetValue(token, out virtualKey))
        {
            return true;
        }

        if (Enum.TryParse<Key>(NormalizeKeyToken(token), ignoreCase: true, out var key))
        {
            virtualKey = KeyInterop.VirtualKeyFromKey(key);
            return virtualKey != 0;
        }

        virtualKey = 0;
        return false;
    }

    private static string NormalizeKeyToken(string token) => token switch
    {
        "LeftControl" => nameof(Key.LeftCtrl),
        "RightControl" => nameof(Key.RightCtrl),
        "UpArrow" => nameof(Key.Up),
        "DownArrow" => nameof(Key.Down),
        "LeftArrow" => nameof(Key.Left),
        "RightArrow" => nameof(Key.Right),
        "ScrollLock" => nameof(Key.Scroll),
        "NumPadPeriod" => nameof(Key.Decimal),
        _ => token,
    };

    private static bool MatchesVirtualKey(KeyboardInputSnapshot keyboardState, int virtualKey)
    {
        if (keyboardState.Contains(virtualKey))
        {
            return true;
        }

        return virtualKey switch
        {
            0xA0 or 0xA1 => keyboardState.Contains(VkShift),
            0xA2 or 0xA3 => keyboardState.Contains(VkControl),
            0xA4 or 0xA5 => keyboardState.Contains(VkMenu),
            _ => false,
        };
    }

    private static double NormalizeAxis(uint value) => Math.Clamp(value / 65535.0, 0, 1);
}