#include "OverlayRuntime.h"

#include <dxgi.h>
#include <wrl/client.h>

namespace aom::overlay
{
OverlayRuntime& OverlayRuntime::Instance()
{
    static OverlayRuntime instance;
    return instance;
}

bool OverlayRuntime::RenderFrame(IUnknown* swapChainUnknown)
{
    if (swapChainUnknown == nullptr)
    {
        return false;
    }

    Microsoft::WRL::ComPtr<IDXGISwapChain> swapChain;
    if (FAILED(swapChainUnknown->QueryInterface(IID_PPV_ARGS(swapChain.ReleaseAndGetAddressOf()))))
    {
        return false;
    }

    OverlaySnapshot nextSnapshot;
    if (reader_.TryReadLatest(nextSnapshot))
    {
        latestSnapshot_ = std::move(nextSnapshot);
    }

    if (!latestSnapshot_.isFresh)
    {
        return false;
    }

    return renderer_.Render(swapChain.Get(), latestSnapshot_);
}

void OverlayRuntime::Reset()
{
    reader_.Reset();
    renderer_.Reset();
    latestSnapshot_ = OverlaySnapshot {};
}
}