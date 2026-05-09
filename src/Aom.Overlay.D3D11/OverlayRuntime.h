#pragma once

#include <Unknwn.h>

#include "D3D11OverlayRenderer.h"
#include "OverlaySharedStateReader.h"

namespace aom::overlay
{
class OverlayRuntime
{
public:
    static OverlayRuntime& Instance();

    bool RenderFrame(IUnknown* swapChainUnknown);
    void Reset();

private:
    OverlayRuntime() = default;

    OverlaySharedStateReader reader_;
    D3D11OverlayRenderer renderer_;
    OverlaySnapshot latestSnapshot_;
};
}