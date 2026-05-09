#include "OverlayExports.h"
#include "OverlayRuntime.h"

extern "C"
{
AOM_OVERLAY_API BOOL WINAPI AomOverlayRenderFrame(IUnknown* swapChain)
{
    return aom::overlay::OverlayRuntime::Instance().RenderFrame(swapChain) ? TRUE : FALSE;
}

AOM_OVERLAY_API void WINAPI AomOverlayReset()
{
    aom::overlay::OverlayRuntime::Instance().Reset();
}

AOM_OVERLAY_API const wchar_t* WINAPI AomOverlayGetProtocolDescription()
{
    return L"D3D11 overlay scaffold. Input mapping: Local\\AomDesktop.OverlayState, event: Local\\AomDesktop.OverlayUpdated.";
}
}