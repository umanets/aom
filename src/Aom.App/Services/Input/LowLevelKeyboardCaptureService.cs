using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Input;

namespace Aom.App.Services.Input;

public sealed class LowLevelKeyboardCaptureService : IDisposable
{
    private const int WhKeyboardLl = 13;
    private const int WmKeyDown = 0x0100;
    private const int WmKeyUp = 0x0101;
    private const int WmSysKeyDown = 0x0104;
    private const int WmSysKeyUp = 0x0105;
    private const int VkShift = 0x10;
    private const int VkControl = 0x11;
    private const int VkMenu = 0x12;
    private const int VkLeftShift = 0xA0;
    private const int VkRightShift = 0xA1;
    private const int VkLeftControl = 0xA2;
    private const int VkRightControl = 0xA3;
    private const int VkLeftMenu = 0xA4;
    private const int VkRightMenu = 0xA5;

    private static readonly Dictionary<int, string> FriendlyNames = new()
    {
        [VkLeftShift] = "LeftShift",
        [VkRightShift] = "RightShift",
        [VkLeftControl] = "LeftControl",
        [VkRightControl] = "RightControl",
        [VkLeftMenu] = "LeftAlt",
        [VkRightMenu] = "RightAlt",
        [0x26] = "UpArrow",
        [0x28] = "DownArrow",
        [0x25] = "LeftArrow",
        [0x27] = "RightArrow",
        [0x2E] = "Delete",
        [0x23] = "End",
        [0x2D] = "Insert",
        [0x24] = "Home",
        [0x21] = "PageUp",
        [0x22] = "PageDown",
        [0x91] = "ScrollLock",
        [0x6E] = "NumPadPeriod",
    };

    private readonly object sync = new();
    private readonly LowLevelKeyboardProc hookCallback;
    private readonly KeyboardLearnTriggerBuffer learnTriggerBuffer = new();
    private readonly HashSet<int> pressedVirtualKeys = new();
    private nint hookHandle;

    public LowLevelKeyboardCaptureService()
    {
        hookCallback = HookProcedure;
    }

    public void Start()
    {
        if (hookHandle != nint.Zero)
        {
            return;
        }

        using var currentProcess = Process.GetCurrentProcess();
        using var currentModule = currentProcess.MainModule;
        var moduleHandle = currentModule is null ? nint.Zero : GetModuleHandle(currentModule.ModuleName);
        hookHandle = SetWindowsHookEx(WhKeyboardLl, hookCallback, moduleHandle, 0);
        if (hookHandle == nint.Zero)
        {
            throw new InvalidOperationException("Failed to install the global keyboard hook.");
        }
    }

    public KeyboardInputSnapshot GetSnapshot()
    {
        lock (sync)
        {
            return new KeyboardInputSnapshot(pressedVirtualKeys.ToArray());
        }
    }

    public bool TryConsumeLearnTrigger(out string trigger)
    {
        lock (sync)
        {
            return learnTriggerBuffer.TryConsume(DateTimeOffset.UtcNow, out trigger);
        }
    }

    public void ClearPendingLearnTrigger()
    {
        lock (sync)
        {
            learnTriggerBuffer.Clear();
        }
    }

    public void Dispose()
    {
        if (hookHandle == nint.Zero)
        {
            return;
        }

        UnhookWindowsHookEx(hookHandle);
        hookHandle = nint.Zero;
    }

    private nint HookProcedure(int code, nint wParam, nint lParam)
    {
        if (code >= 0)
        {
            var keyboardData = Marshal.PtrToStructure<KbdLlHookStruct>(lParam);
            var virtualKey = NormalizeVirtualKey(keyboardData);
            var message = unchecked((int)wParam);

            lock (sync)
            {
                if (message is WmKeyDown or WmSysKeyDown)
                {
                    var added = pressedVirtualKeys.Add(virtualKey);
                    if (added)
                    {
                        var trigger = BuildFriendlyTrigger(virtualKey);
                        if (!string.IsNullOrWhiteSpace(trigger))
                        {
                            learnTriggerBuffer.Queue(trigger, DateTimeOffset.UtcNow);
                        }
                    }
                }
                else if (message is WmKeyUp or WmSysKeyUp)
                {
                    pressedVirtualKeys.Remove(virtualKey);
                }
            }
        }

        return CallNextHookEx(hookHandle, code, wParam, lParam);
    }

    private string? BuildFriendlyTrigger(int latestVirtualKey)
    {
        var tokens = pressedVirtualKeys
            .Select(TryGetFriendlyName)
            .Where(token => !string.IsNullOrWhiteSpace(token))
            .Cast<string>()
            .OrderBy(GetTokenSortOrder)
            .ThenBy(token => token, StringComparer.Ordinal)
            .ToArray();

        if (tokens.Length == 0)
        {
            return null;
        }

        var hasNonModifier = pressedVirtualKeys.Any(key => !IsModifierKey(key));
        if (!hasNonModifier && IsModifierKey(latestVirtualKey))
        {
            return null;
        }

        return string.Join(" + ", tokens);
    }

    private static string? TryGetFriendlyName(int virtualKey)
    {
        if (FriendlyNames.TryGetValue(virtualKey, out var friendlyName))
        {
            return friendlyName;
        }

        var key = KeyInterop.KeyFromVirtualKey(virtualKey);
        return key switch
        {
            Key.None => null,
            Key.System => null,
            _ => key.ToString(),
        };
    }

    private static bool IsModifierKey(int virtualKey) => virtualKey is VkLeftShift or VkRightShift or VkLeftControl or VkRightControl or VkLeftMenu or VkRightMenu;

    private static int NormalizeVirtualKey(KbdLlHookStruct keyboardData)
    {
        return KeyboardVirtualKeyNormalizer.Normalize(
            unchecked((int)keyboardData.VirtualKeyCode),
            keyboardData.ScanCode,
            keyboardData.Flags);
    }

    private static int GetTokenSortOrder(string token) => token switch
    {
        "LeftAlt" => 0,
        "RightAlt" => 1,
        "LeftControl" => 2,
        "RightControl" => 3,
        "LeftShift" => 4,
        "RightShift" => 5,
        _ => 10,
    };

    private delegate nint LowLevelKeyboardProc(int code, nint wParam, nint lParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern nint SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, nint hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnhookWindowsHookEx(nint hhk);

    [DllImport("user32.dll")]
    private static extern nint CallNextHookEx(nint hhk, int nCode, nint wParam, nint lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern nint GetModuleHandle(string? lpModuleName);

    [StructLayout(LayoutKind.Sequential)]
    private struct KbdLlHookStruct
    {
        public uint VirtualKeyCode;
        public uint ScanCode;
        public uint Flags;
        public uint Time;
        public nint ExtraInfo;
    }
}