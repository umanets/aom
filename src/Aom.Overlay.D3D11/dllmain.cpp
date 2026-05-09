#include <Windows.h>

#include "OverlayAutoHook.h"
#include "OverlayRuntime.h"

namespace
{
DWORD WINAPI InitializeOverlayHooks(LPVOID)
{
    aom::overlay::OverlayAutoHook::Initialize();
    return 0;
}
}

BOOL APIENTRY DllMain(HMODULE module, DWORD reasonForCall, LPVOID reserved)
{
    UNREFERENCED_PARAMETER(reserved);

    if (reasonForCall == DLL_PROCESS_ATTACH)
    {
        DisableThreadLibraryCalls(module);

        if (aom::overlay::OverlayAutoHook::ShouldAutoInstall())
        {
            const HANDLE threadHandle = CreateThread(nullptr, 0, InitializeOverlayHooks, nullptr, 0, nullptr);
            if (threadHandle != nullptr)
            {
                CloseHandle(threadHandle);
            }
        }
    }
    else if (reasonForCall == DLL_PROCESS_DETACH)
    {
        aom::overlay::OverlayAutoHook::Shutdown();
        aom::overlay::OverlayRuntime::Instance().Reset();
    }

    return TRUE;
}