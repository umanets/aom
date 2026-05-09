namespace Aom.App.Services.Input;

public static class KeyboardVirtualKeyNormalizer
{
    private const int VkShift = 0x10;
    private const int VkControl = 0x11;
    private const int VkMenu = 0x12;
    private const int VkLeftShift = 0xA0;
    private const int VkRightShift = 0xA1;
    private const int VkLeftControl = 0xA2;
    private const int VkRightControl = 0xA3;
    private const int VkLeftMenu = 0xA4;
    private const int VkRightMenu = 0xA5;
    private const uint LlkhfExtended = 0x01;
    private const uint MapvkVscToVkEx = 3;

    private static readonly IReadOnlyDictionary<uint, int> NonExtendedNavigationScanCodeMap = new Dictionary<uint, int>
    {
        [0x47] = 0x67,
        [0x48] = 0x68,
        [0x49] = 0x69,
        [0x4B] = 0x64,
        [0x4C] = 0x65,
        [0x4D] = 0x66,
        [0x4F] = 0x61,
        [0x50] = 0x62,
        [0x51] = 0x63,
        [0x52] = 0x60,
        [0x53] = 0x6E,
    };

    public static int Normalize(int virtualKey, uint scanCode, uint flags)
    {
        return virtualKey switch
        {
            VkShift => unchecked((int)MapVirtualKey(scanCode, MapvkVscToVkEx)),
            VkControl => (flags & LlkhfExtended) != 0 ? VkRightControl : VkLeftControl,
            VkMenu => (flags & LlkhfExtended) != 0 ? VkRightMenu : VkLeftMenu,
            _ => TryNormalizeNavigationNumpadKey(virtualKey, scanCode, flags),
        };
    }

    private static int TryNormalizeNavigationNumpadKey(int virtualKey, uint scanCode, uint flags)
    {
        if ((flags & LlkhfExtended) == 0 && NonExtendedNavigationScanCodeMap.TryGetValue(scanCode, out var numpadVirtualKey))
        {
            return numpadVirtualKey;
        }

        return virtualKey;
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern uint MapVirtualKey(uint uCode, uint uMapType);
}