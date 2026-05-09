#pragma once

namespace aom::overlay
{
class OverlayAutoHook
{
public:
    static bool ShouldAutoInstall();
    static void Initialize();
    static void Shutdown();
};
}