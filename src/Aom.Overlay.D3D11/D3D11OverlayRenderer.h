#pragma once

#include <d2d1.h>
#include <d3d11.h>
#include <dwrite.h>
#include <dxgi.h>
#include <wincodec.h>
#include <wrl/client.h>

#include <string>
#include <vector>

#include "OverlaySnapshot.h"

namespace aom::overlay
{
class D3D11OverlayRenderer
{
public:
    D3D11OverlayRenderer();

    bool Render(IDXGISwapChain* swapChain, const OverlaySnapshot& snapshot);
    void Reset();

private:
    struct OverlayVertex
    {
        float position[2];
        float uv[2];
    };

    bool EnsureDeviceIndependentResources();
    bool EnsureTargetResources(IDXGISwapChain* swapChain);
    bool EnsureBitmapResources();
    bool EnsurePipelineResources();
    bool UpdateOverlayTexture(const OverlaySnapshot& snapshot);
    bool DrawOverlayQuad();
    void UpdateOverlayGeometry();
    void DiscardTargetResources();
    std::wstring BuildOverlayText(const OverlaySnapshot& snapshot) const;

    static constexpr UINT OverlayPanelWidth = 432;
    static constexpr UINT OverlayPanelHeight = 252;
    static constexpr float OverlayOriginX = 28.0F;
    static constexpr float OverlayOriginY = 28.0F;

    Microsoft::WRL::ComPtr<IDXGISwapChain> activeSwapChain_;
    Microsoft::WRL::ComPtr<ID3D11Device> device_;
    Microsoft::WRL::ComPtr<ID3D11DeviceContext> deviceContext_;
    Microsoft::WRL::ComPtr<ID3D11RenderTargetView> renderTargetView_;
    Microsoft::WRL::ComPtr<ID3D11Texture2D> overlayTexture_;
    Microsoft::WRL::ComPtr<ID3D11ShaderResourceView> overlayTextureView_;
    Microsoft::WRL::ComPtr<ID3D11SamplerState> samplerState_;
    Microsoft::WRL::ComPtr<ID3D11BlendState> blendState_;
    Microsoft::WRL::ComPtr<ID3D11RasterizerState> rasterizerState_;
    Microsoft::WRL::ComPtr<ID3D11DepthStencilState> depthStencilState_;
    Microsoft::WRL::ComPtr<ID3D11VertexShader> vertexShader_;
    Microsoft::WRL::ComPtr<ID3D11PixelShader> pixelShader_;
    Microsoft::WRL::ComPtr<ID3D11InputLayout> inputLayout_;
    Microsoft::WRL::ComPtr<ID3D11Buffer> vertexBuffer_;
    D3D11_VIEWPORT viewport_ {};
    UINT backBufferWidth_ = 0;
    UINT backBufferHeight_ = 0;

    Microsoft::WRL::ComPtr<ID2D1Factory> d2dFactory_;
    Microsoft::WRL::ComPtr<IWICImagingFactory> wicFactory_;
    Microsoft::WRL::ComPtr<IDWriteFactory> dwriteFactory_;
    Microsoft::WRL::ComPtr<IDWriteTextFormat> titleTextFormat_;
    Microsoft::WRL::ComPtr<IDWriteTextFormat> bodyTextFormat_;
    Microsoft::WRL::ComPtr<IWICBitmap> overlayBitmap_;
    Microsoft::WRL::ComPtr<ID2D1RenderTarget> overlayBitmapRenderTarget_;
    Microsoft::WRL::ComPtr<ID2D1SolidColorBrush> panelBrush_;
    Microsoft::WRL::ComPtr<ID2D1SolidColorBrush> borderBrush_;
    Microsoft::WRL::ComPtr<ID2D1SolidColorBrush> textBrush_;
    Microsoft::WRL::ComPtr<ID2D1SolidColorBrush> accentBrush_;
    std::vector<std::byte> overlayPixelBuffer_;
    std::uint64_t lastUploadedSequence_ = 0;
};
}