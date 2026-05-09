#pragma once

#include <Unknwn.h>
#include <Windows.h>

#ifdef AOM_OVERLAY_D3D11_EXPORTS
#define AOM_OVERLAY_API __declspec(dllexport)
#else
#define AOM_OVERLAY_API __declspec(dllimport)
#endif

extern "C"
{
using AomOverlayRenderFrameFn = BOOL(WINAPI*)(IUnknown* swapChain);
using AomOverlayResetFn = void(WINAPI*)();
using AomOverlayGetProtocolDescriptionFn = const wchar_t*(WINAPI*)();

AOM_OVERLAY_API BOOL WINAPI AomOverlayRenderFrame(IUnknown* swapChain);
AOM_OVERLAY_API void WINAPI AomOverlayReset();
AOM_OVERLAY_API const wchar_t* WINAPI AomOverlayGetProtocolDescription();
}