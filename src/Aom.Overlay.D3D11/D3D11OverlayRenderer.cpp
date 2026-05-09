#include "D3D11OverlayRenderer.h"

#include <Windows.h>
#include <d2d1helper.h>
#include <d3dcompiler.h>

#include <array>
#include <cstring>
#include <cwchar>
#include <string_view>

#pragma comment(lib, "d3dcompiler.lib")

namespace
{
constexpr DXGI_FORMAT OverlayTextureFormat = DXGI_FORMAT_B8G8R8A8_UNORM;

const char* OverlayVertexShaderSource = R"(
struct VSInput
{
    float2 position : POSITION;
    float2 uv : TEXCOORD0;
};

struct PSInput
{
    float4 position : SV_Position;
    float2 uv : TEXCOORD0;
};

PSInput main(VSInput input)
{
    PSInput output;
    output.position = float4(input.position, 0.0f, 1.0f);
    output.uv = input.uv;
    return output;
}
)";

const char* OverlayPixelShaderSource = R"(
Texture2D overlayTexture : register(t0);
SamplerState overlaySampler : register(s0);

float4 main(float4 position : SV_Position, float2 uv : TEXCOORD0) : SV_Target
{
    return overlayTexture.Sample(overlaySampler, uv);
}
)";

std::wstring FormatDxgiFormat(const DXGI_FORMAT format)
{
    wchar_t buffer[32] {};
    swprintf_s(buffer, L"%u", static_cast<unsigned int>(format));
    return buffer;
}

std::wstring FormatHResult(const HRESULT result)
{
    wchar_t buffer[32] {};
    swprintf_s(buffer, L"0x%08X", static_cast<unsigned int>(result));
    return buffer;
}

void AppendRendererLogLine(const std::wstring_view message)
{
    static std::wstring lastMessage;
    if (lastMessage == message)
    {
        return;
    }

    lastMessage.assign(message);
    OutputDebugStringW((std::wstring(message) + L"\n").c_str());

    std::array<wchar_t, MAX_PATH> tempPath {};
    const DWORD tempPathLength = GetTempPathW(static_cast<DWORD>(tempPath.size()), tempPath.data());
    if (tempPathLength == 0 || tempPathLength >= tempPath.size())
    {
        return;
    }

    std::wstring logPath(tempPath.data(), tempPathLength);
    if (!logPath.empty() && logPath.back() != L'\\')
    {
        logPath.push_back(L'\\');
    }

    logPath.append(L"aom-overlay-d3d11.log");

    const HANDLE handle = CreateFileW(
        logPath.c_str(),
        FILE_APPEND_DATA,
        FILE_SHARE_READ | FILE_SHARE_WRITE,
        nullptr,
        OPEN_ALWAYS,
        FILE_ATTRIBUTE_NORMAL,
        nullptr);

    if (handle == INVALID_HANDLE_VALUE)
    {
        return;
    }

    const std::wstring payload(std::wstring(message) + L"\r\n");
    DWORD bytesWritten = 0;
    WriteFile(handle, payload.data(), static_cast<DWORD>(payload.size() * sizeof(wchar_t)), &bytesWritten, nullptr);
    CloseHandle(handle);
}

bool CompileShader(
    const char* source,
    const char* entryPoint,
    const char* profile,
    ID3DBlob** compiledBlob)
{
    Microsoft::WRL::ComPtr<ID3DBlob> errors;
    const HRESULT result = D3DCompile(
        source,
        std::strlen(source),
        nullptr,
        nullptr,
        nullptr,
        entryPoint,
        profile,
        0,
        0,
        compiledBlob,
        errors.ReleaseAndGetAddressOf());

    if (FAILED(result))
    {
        std::wstring message = L"AOM overlay: shader compilation failed with ";
        message.append(FormatHResult(result));
        AppendRendererLogLine(message);
        return false;
    }

    return true;
}
}

namespace aom::overlay
{
D3D11OverlayRenderer::D3D11OverlayRenderer() = default;

bool D3D11OverlayRenderer::Render(IDXGISwapChain* swapChain, const OverlaySnapshot& snapshot)
{
    if (swapChain == nullptr || !snapshot.isVisible)
    {
        return false;
    }

    if (!EnsureDeviceIndependentResources() || !EnsureTargetResources(swapChain))
    {
        return false;
    }

    if (!UpdateOverlayTexture(snapshot))
    {
        return false;
    }

    return DrawOverlayQuad();
}

void D3D11OverlayRenderer::Reset()
{
    DiscardTargetResources();
    accentBrush_.Reset();
    textBrush_.Reset();
    borderBrush_.Reset();
    panelBrush_.Reset();
    overlayBitmapRenderTarget_.Reset();
    overlayBitmap_.Reset();
    titleTextFormat_.Reset();
    bodyTextFormat_.Reset();
    wicFactory_.Reset();
    dwriteFactory_.Reset();
    d2dFactory_.Reset();
    overlayPixelBuffer_.clear();
    lastUploadedSequence_ = 0;
}

bool D3D11OverlayRenderer::EnsureDeviceIndependentResources()
{
    if (d2dFactory_ == nullptr)
    {
        if (FAILED(D2D1CreateFactory(D2D1_FACTORY_TYPE_SINGLE_THREADED, d2dFactory_.ReleaseAndGetAddressOf())))
        {
            AppendRendererLogLine(L"AOM overlay: failed to create D2D factory.");
            return false;
        }
    }

    if (dwriteFactory_ == nullptr)
    {
        if (FAILED(DWriteCreateFactory(
            DWRITE_FACTORY_TYPE_SHARED,
            __uuidof(IDWriteFactory),
            reinterpret_cast<IUnknown**>(dwriteFactory_.ReleaseAndGetAddressOf()))))
        {
            AppendRendererLogLine(L"AOM overlay: failed to create DirectWrite factory.");
            return false;
        }
    }

    if (wicFactory_ == nullptr)
    {
        const HRESULT initializeResult = CoInitializeEx(nullptr, COINIT_MULTITHREADED);
        if (FAILED(initializeResult) && initializeResult != RPC_E_CHANGED_MODE)
        {
            AppendRendererLogLine(L"AOM overlay: CoInitializeEx failed with " + FormatHResult(initializeResult));
            return false;
        }

        const HRESULT wicResult = CoCreateInstance(
            CLSID_WICImagingFactory,
            nullptr,
            CLSCTX_INPROC_SERVER,
            IID_PPV_ARGS(wicFactory_.ReleaseAndGetAddressOf()));

        if (FAILED(wicResult))
        {
            AppendRendererLogLine(L"AOM overlay: failed to create WIC factory with " + FormatHResult(wicResult));
            return false;
        }
    }

    if (titleTextFormat_ == nullptr)
    {
        if (FAILED(dwriteFactory_->CreateTextFormat(
            L"Segoe UI",
            nullptr,
            DWRITE_FONT_WEIGHT_SEMI_BOLD,
            DWRITE_FONT_STYLE_NORMAL,
            DWRITE_FONT_STRETCH_NORMAL,
            18.0F,
            L"en-us",
            titleTextFormat_.ReleaseAndGetAddressOf())))
        {
            AppendRendererLogLine(L"AOM overlay: failed to create title text format.");
            return false;
        }

        titleTextFormat_->SetWordWrapping(DWRITE_WORD_WRAPPING_NO_WRAP);
    }

    if (bodyTextFormat_ == nullptr)
    {
        if (FAILED(dwriteFactory_->CreateTextFormat(
            L"Segoe UI",
            nullptr,
            DWRITE_FONT_WEIGHT_SEMI_BOLD,
            DWRITE_FONT_STYLE_NORMAL,
            DWRITE_FONT_STRETCH_NORMAL,
            16.0F,
            L"en-us",
            bodyTextFormat_.ReleaseAndGetAddressOf())))
        {
            AppendRendererLogLine(L"AOM overlay: failed to create body text format.");
            return false;
        }

        bodyTextFormat_->SetWordWrapping(DWRITE_WORD_WRAPPING_WRAP);
    }

    return true;
}

bool D3D11OverlayRenderer::EnsureBitmapResources()
{
    if (overlayBitmap_ != nullptr && overlayBitmapRenderTarget_ != nullptr)
    {
        return true;
    }

    const HRESULT bitmapResult = wicFactory_->CreateBitmap(
        OverlayPanelWidth,
        OverlayPanelHeight,
        GUID_WICPixelFormat32bppPBGRA,
        WICBitmapCacheOnLoad,
        overlayBitmap_.ReleaseAndGetAddressOf());

    if (FAILED(bitmapResult))
    {
        AppendRendererLogLine(L"AOM overlay: failed to create WIC bitmap with " + FormatHResult(bitmapResult));
        return false;
    }

    const D2D1_RENDER_TARGET_PROPERTIES properties = D2D1::RenderTargetProperties(
        D2D1_RENDER_TARGET_TYPE_SOFTWARE,
        D2D1::PixelFormat(OverlayTextureFormat, D2D1_ALPHA_MODE_PREMULTIPLIED),
        96.0F,
        96.0F);

    const HRESULT renderTargetResult = d2dFactory_->CreateWicBitmapRenderTarget(
        overlayBitmap_.Get(),
        properties,
        overlayBitmapRenderTarget_.ReleaseAndGetAddressOf());

    if (FAILED(renderTargetResult))
    {
        AppendRendererLogLine(L"AOM overlay: failed to create bitmap render target with " + FormatHResult(renderTargetResult));
        return false;
    }

    overlayBitmapRenderTarget_->CreateSolidColorBrush(D2D1::ColorF(0.03F, 0.07F, 0.12F, 0.82F), panelBrush_.ReleaseAndGetAddressOf());
    overlayBitmapRenderTarget_->CreateSolidColorBrush(D2D1::ColorF(0.31F, 0.82F, 0.77F, 0.90F), borderBrush_.ReleaseAndGetAddressOf());
    overlayBitmapRenderTarget_->CreateSolidColorBrush(D2D1::ColorF(0.95F, 0.97F, 0.98F, 0.98F), textBrush_.ReleaseAndGetAddressOf());
    overlayBitmapRenderTarget_->CreateSolidColorBrush(D2D1::ColorF(0.31F, 0.82F, 0.77F, 1.0F), accentBrush_.ReleaseAndGetAddressOf());

    overlayPixelBuffer_.assign(static_cast<std::size_t>(OverlayPanelWidth) * OverlayPanelHeight * 4, std::byte { 0 });
    lastUploadedSequence_ = 0;
    return true;
}

bool D3D11OverlayRenderer::EnsurePipelineResources()
{
    if (vertexShader_ == nullptr)
    {
        Microsoft::WRL::ComPtr<ID3DBlob> vertexShaderBlob;
        if (!CompileShader(OverlayVertexShaderSource, "main", "vs_4_0", vertexShaderBlob.ReleaseAndGetAddressOf()))
        {
            return false;
        }

        if (FAILED(device_->CreateVertexShader(
            vertexShaderBlob->GetBufferPointer(),
            vertexShaderBlob->GetBufferSize(),
            nullptr,
            vertexShader_.ReleaseAndGetAddressOf())))
        {
            AppendRendererLogLine(L"AOM overlay: failed to create vertex shader.");
            return false;
        }

        const D3D11_INPUT_ELEMENT_DESC inputElements[] =
        {
            { "POSITION", 0, DXGI_FORMAT_R32G32_FLOAT, 0, 0, D3D11_INPUT_PER_VERTEX_DATA, 0 },
            { "TEXCOORD", 0, DXGI_FORMAT_R32G32_FLOAT, 0, 8, D3D11_INPUT_PER_VERTEX_DATA, 0 },
        };

        if (FAILED(device_->CreateInputLayout(
            inputElements,
            static_cast<UINT>(std::size(inputElements)),
            vertexShaderBlob->GetBufferPointer(),
            vertexShaderBlob->GetBufferSize(),
            inputLayout_.ReleaseAndGetAddressOf())))
        {
            AppendRendererLogLine(L"AOM overlay: failed to create input layout.");
            return false;
        }
    }

    if (pixelShader_ == nullptr)
    {
        Microsoft::WRL::ComPtr<ID3DBlob> pixelShaderBlob;
        if (!CompileShader(OverlayPixelShaderSource, "main", "ps_4_0", pixelShaderBlob.ReleaseAndGetAddressOf()))
        {
            return false;
        }

        if (FAILED(device_->CreatePixelShader(
            pixelShaderBlob->GetBufferPointer(),
            pixelShaderBlob->GetBufferSize(),
            nullptr,
            pixelShader_.ReleaseAndGetAddressOf())))
        {
            AppendRendererLogLine(L"AOM overlay: failed to create pixel shader.");
            return false;
        }
    }

    if (vertexBuffer_ == nullptr)
    {
        D3D11_BUFFER_DESC vertexBufferDescription {};
        vertexBufferDescription.ByteWidth = sizeof(OverlayVertex) * 4;
        vertexBufferDescription.Usage = D3D11_USAGE_DYNAMIC;
        vertexBufferDescription.BindFlags = D3D11_BIND_VERTEX_BUFFER;
        vertexBufferDescription.CPUAccessFlags = D3D11_CPU_ACCESS_WRITE;

        if (FAILED(device_->CreateBuffer(&vertexBufferDescription, nullptr, vertexBuffer_.ReleaseAndGetAddressOf())))
        {
            AppendRendererLogLine(L"AOM overlay: failed to create vertex buffer.");
            return false;
        }

        UpdateOverlayGeometry();
    }

    if (samplerState_ == nullptr)
    {
        D3D11_SAMPLER_DESC samplerDescription {};
        samplerDescription.Filter = D3D11_FILTER_MIN_MAG_MIP_LINEAR;
        samplerDescription.AddressU = D3D11_TEXTURE_ADDRESS_CLAMP;
        samplerDescription.AddressV = D3D11_TEXTURE_ADDRESS_CLAMP;
        samplerDescription.AddressW = D3D11_TEXTURE_ADDRESS_CLAMP;
        samplerDescription.MaxLOD = D3D11_FLOAT32_MAX;

        if (FAILED(device_->CreateSamplerState(&samplerDescription, samplerState_.ReleaseAndGetAddressOf())))
        {
            AppendRendererLogLine(L"AOM overlay: failed to create sampler state.");
            return false;
        }
    }

    if (blendState_ == nullptr)
    {
        D3D11_BLEND_DESC blendDescription {};
        auto& renderTarget = blendDescription.RenderTarget[0];
        renderTarget.BlendEnable = TRUE;
        renderTarget.SrcBlend = D3D11_BLEND_ONE;
        renderTarget.DestBlend = D3D11_BLEND_INV_SRC_ALPHA;
        renderTarget.BlendOp = D3D11_BLEND_OP_ADD;
        renderTarget.SrcBlendAlpha = D3D11_BLEND_ONE;
        renderTarget.DestBlendAlpha = D3D11_BLEND_INV_SRC_ALPHA;
        renderTarget.BlendOpAlpha = D3D11_BLEND_OP_ADD;
        renderTarget.RenderTargetWriteMask = D3D11_COLOR_WRITE_ENABLE_ALL;

        if (FAILED(device_->CreateBlendState(&blendDescription, blendState_.ReleaseAndGetAddressOf())))
        {
            AppendRendererLogLine(L"AOM overlay: failed to create blend state.");
            return false;
        }
    }

    if (rasterizerState_ == nullptr)
    {
        D3D11_RASTERIZER_DESC rasterizerDescription {};
        rasterizerDescription.FillMode = D3D11_FILL_SOLID;
        rasterizerDescription.CullMode = D3D11_CULL_NONE;
        rasterizerDescription.DepthClipEnable = TRUE;

        if (FAILED(device_->CreateRasterizerState(&rasterizerDescription, rasterizerState_.ReleaseAndGetAddressOf())))
        {
            AppendRendererLogLine(L"AOM overlay: failed to create rasterizer state.");
            return false;
        }
    }

    if (depthStencilState_ == nullptr)
    {
        D3D11_DEPTH_STENCIL_DESC depthStencilDescription {};
        depthStencilDescription.DepthEnable = FALSE;
        depthStencilDescription.StencilEnable = FALSE;

        if (FAILED(device_->CreateDepthStencilState(&depthStencilDescription, depthStencilState_.ReleaseAndGetAddressOf())))
        {
            AppendRendererLogLine(L"AOM overlay: failed to create depth-stencil state.");
            return false;
        }
    }

    if (overlayTexture_ == nullptr || overlayTextureView_ == nullptr)
    {
        D3D11_TEXTURE2D_DESC textureDescription {};
        textureDescription.Width = OverlayPanelWidth;
        textureDescription.Height = OverlayPanelHeight;
        textureDescription.MipLevels = 1;
        textureDescription.ArraySize = 1;
        textureDescription.Format = OverlayTextureFormat;
        textureDescription.SampleDesc.Count = 1;
        textureDescription.Usage = D3D11_USAGE_DYNAMIC;
        textureDescription.BindFlags = D3D11_BIND_SHADER_RESOURCE;
        textureDescription.CPUAccessFlags = D3D11_CPU_ACCESS_WRITE;

        const HRESULT textureResult = device_->CreateTexture2D(&textureDescription, nullptr, overlayTexture_.ReleaseAndGetAddressOf());
        if (FAILED(textureResult))
        {
            AppendRendererLogLine(L"AOM overlay: failed to create overlay texture with " + FormatHResult(textureResult));
            return false;
        }

        const HRESULT viewResult = device_->CreateShaderResourceView(overlayTexture_.Get(), nullptr, overlayTextureView_.ReleaseAndGetAddressOf());
        if (FAILED(viewResult))
        {
            AppendRendererLogLine(L"AOM overlay: failed to create overlay texture view with " + FormatHResult(viewResult));
            return false;
        }
    }

    return true;
}

bool D3D11OverlayRenderer::EnsureTargetResources(IDXGISwapChain* swapChain)
{
    Microsoft::WRL::ComPtr<ID3D11Texture2D> backBuffer;
    if (FAILED(swapChain->GetBuffer(0, IID_PPV_ARGS(backBuffer.ReleaseAndGetAddressOf()))))
    {
        AppendRendererLogLine(L"AOM overlay: IDXGISwapChain::GetBuffer(0) failed.");
        return false;
    }

    D3D11_TEXTURE2D_DESC backBufferDescription {};
    backBuffer->GetDesc(&backBufferDescription);

    const bool targetChanged = activeSwapChain_.Get() != swapChain
        || renderTargetView_ == nullptr
        || backBufferWidth_ != backBufferDescription.Width
        || backBufferHeight_ != backBufferDescription.Height;

    if (targetChanged)
    {
        activeSwapChain_ = swapChain;

        if (FAILED(swapChain->GetDevice(IID_PPV_ARGS(device_.ReleaseAndGetAddressOf()))))
        {
            AppendRendererLogLine(L"AOM overlay: failed to resolve D3D11 device from swapchain.");
            DiscardTargetResources();
            return false;
        }

        device_->GetImmediateContext(deviceContext_.ReleaseAndGetAddressOf());
        if (deviceContext_ == nullptr)
        {
            AppendRendererLogLine(L"AOM overlay: failed to resolve D3D11 immediate context.");
            DiscardTargetResources();
            return false;
        }

        renderTargetView_.Reset();
        const HRESULT renderTargetViewResult = device_->CreateRenderTargetView(backBuffer.Get(), nullptr, renderTargetView_.ReleaseAndGetAddressOf());
        if (FAILED(renderTargetViewResult))
        {
            AppendRendererLogLine(L"AOM overlay: failed to create backbuffer RTV with " + FormatHResult(renderTargetViewResult) + L", format=" + FormatDxgiFormat(backBufferDescription.Format));
            DiscardTargetResources();
            return false;
        }

        backBufferWidth_ = backBufferDescription.Width;
        backBufferHeight_ = backBufferDescription.Height;
        viewport_.TopLeftX = 0.0F;
        viewport_.TopLeftY = 0.0F;
        viewport_.Width = static_cast<float>(backBufferWidth_);
        viewport_.Height = static_cast<float>(backBufferHeight_);
        viewport_.MinDepth = 0.0F;
        viewport_.MaxDepth = 1.0F;

        overlayTexture_.Reset();
        overlayTextureView_.Reset();
        vertexBuffer_.Reset();

        if (!EnsurePipelineResources())
        {
            DiscardTargetResources();
            return false;
        }

        UpdateOverlayGeometry();

        AppendRendererLogLine(L"AOM overlay: D3D11 compositing target ready for format " + FormatDxgiFormat(backBufferDescription.Format));
    }

    return EnsureBitmapResources() && EnsurePipelineResources();
}

bool D3D11OverlayRenderer::UpdateOverlayTexture(const OverlaySnapshot& snapshot)
{
    if (snapshot.sequence == lastUploadedSequence_)
    {
        return true;
    }

    overlayBitmapRenderTarget_->BeginDraw();
    overlayBitmapRenderTarget_->SetTransform(D2D1::Matrix3x2F::Identity());
    overlayBitmapRenderTarget_->Clear(D2D1::ColorF(0.0F, 0.0F));

    const D2D1_ROUNDED_RECT panel = D2D1::RoundedRect(
        D2D1::RectF(0.0F, 0.0F, static_cast<float>(OverlayPanelWidth), static_cast<float>(OverlayPanelHeight)),
        16.0F,
        16.0F);
    overlayBitmapRenderTarget_->FillRoundedRectangle(panel, panelBrush_.Get());
    overlayBitmapRenderTarget_->DrawRoundedRectangle(panel, borderBrush_.Get(), 1.25F);

    const auto overlayText = BuildOverlayText(snapshot);
    overlayBitmapRenderTarget_->DrawTextW(
        L"AOM / IL-2 overlay",
        18,
        titleTextFormat_.Get(),
        D2D1::RectF(20.0F, 10.0F, static_cast<float>(OverlayPanelWidth - 20), 36.0F),
        accentBrush_.Get());
    overlayBitmapRenderTarget_->DrawTextW(
        overlayText.c_str(),
        static_cast<UINT32>(overlayText.size()),
        bodyTextFormat_.Get(),
        D2D1::RectF(20.0F, 42.0F, static_cast<float>(OverlayPanelWidth - 20), static_cast<float>(OverlayPanelHeight - 18)),
        textBrush_.Get());

    const HRESULT endDrawResult = overlayBitmapRenderTarget_->EndDraw();
    if (FAILED(endDrawResult))
    {
        AppendRendererLogLine(L"AOM overlay: bitmap EndDraw failed with " + FormatHResult(endDrawResult));
        overlayBitmapRenderTarget_.Reset();
        overlayBitmap_.Reset();
        lastUploadedSequence_ = 0;
        return false;
    }

    const UINT stride = OverlayPanelWidth * 4;
    const HRESULT copyPixelsResult = overlayBitmap_->CopyPixels(
        nullptr,
        stride,
        static_cast<UINT>(overlayPixelBuffer_.size()),
        reinterpret_cast<BYTE*>(overlayPixelBuffer_.data()));

    if (FAILED(copyPixelsResult))
    {
        AppendRendererLogLine(L"AOM overlay: WIC CopyPixels failed with " + FormatHResult(copyPixelsResult));
        return false;
    }

    D3D11_MAPPED_SUBRESOURCE mappedResource {};
    const HRESULT mapResult = deviceContext_->Map(overlayTexture_.Get(), 0, D3D11_MAP_WRITE_DISCARD, 0, &mappedResource);
    if (FAILED(mapResult))
    {
        AppendRendererLogLine(L"AOM overlay: failed to map overlay texture with " + FormatHResult(mapResult));
        return false;
    }

    const auto* source = reinterpret_cast<const std::byte*>(overlayPixelBuffer_.data());
    auto* destination = reinterpret_cast<std::byte*>(mappedResource.pData);
    for (UINT row = 0; row < OverlayPanelHeight; ++row)
    {
        std::memcpy(destination + static_cast<std::size_t>(mappedResource.RowPitch) * row, source + static_cast<std::size_t>(stride) * row, stride);
    }

    deviceContext_->Unmap(overlayTexture_.Get(), 0);
    lastUploadedSequence_ = snapshot.sequence;
    return true;
}

bool D3D11OverlayRenderer::DrawOverlayQuad()
{
    Microsoft::WRL::ComPtr<ID3D11RenderTargetView> previousRenderTargetView;
    Microsoft::WRL::ComPtr<ID3D11DepthStencilView> previousDepthStencilView;
    deviceContext_->OMGetRenderTargets(1, previousRenderTargetView.GetAddressOf(), previousDepthStencilView.GetAddressOf());

    std::array<D3D11_VIEWPORT, D3D11_VIEWPORT_AND_SCISSORRECT_OBJECT_COUNT_PER_PIPELINE> previousViewports {};
    UINT previousViewportCount = static_cast<UINT>(previousViewports.size());
    deviceContext_->RSGetViewports(&previousViewportCount, previousViewports.data());

    Microsoft::WRL::ComPtr<ID3D11BlendState> previousBlendState;
    FLOAT previousBlendFactor[4] {};
    UINT previousSampleMask = 0;
    deviceContext_->OMGetBlendState(previousBlendState.GetAddressOf(), previousBlendFactor, &previousSampleMask);

    Microsoft::WRL::ComPtr<ID3D11DepthStencilState> previousDepthStencilState;
    UINT previousStencilReference = 0;
    deviceContext_->OMGetDepthStencilState(previousDepthStencilState.GetAddressOf(), &previousStencilReference);

    Microsoft::WRL::ComPtr<ID3D11RasterizerState> previousRasterizerState;
    deviceContext_->RSGetState(previousRasterizerState.GetAddressOf());

    Microsoft::WRL::ComPtr<ID3D11InputLayout> previousInputLayout;
    deviceContext_->IAGetInputLayout(previousInputLayout.GetAddressOf());

    D3D11_PRIMITIVE_TOPOLOGY previousTopology = D3D11_PRIMITIVE_TOPOLOGY_UNDEFINED;
    deviceContext_->IAGetPrimitiveTopology(&previousTopology);

    Microsoft::WRL::ComPtr<ID3D11Buffer> previousVertexBuffer;
    UINT previousStride = 0;
    UINT previousOffset = 0;
    deviceContext_->IAGetVertexBuffers(0, 1, previousVertexBuffer.GetAddressOf(), &previousStride, &previousOffset);

    Microsoft::WRL::ComPtr<ID3D11VertexShader> previousVertexShader;
    Microsoft::WRL::ComPtr<ID3D11PixelShader> previousPixelShader;
    deviceContext_->VSGetShader(previousVertexShader.GetAddressOf(), nullptr, nullptr);
    deviceContext_->PSGetShader(previousPixelShader.GetAddressOf(), nullptr, nullptr);

    Microsoft::WRL::ComPtr<ID3D11ShaderResourceView> previousShaderResourceView;
    deviceContext_->PSGetShaderResources(0, 1, previousShaderResourceView.GetAddressOf());

    Microsoft::WRL::ComPtr<ID3D11SamplerState> previousSamplerState;
    deviceContext_->PSGetSamplers(0, 1, previousSamplerState.GetAddressOf());

    ID3D11RenderTargetView* renderTargets[] = { renderTargetView_.Get() };
    deviceContext_->OMSetRenderTargets(1, renderTargets, nullptr);
    deviceContext_->RSSetViewports(1, &viewport_);
    deviceContext_->RSSetState(rasterizerState_.Get());
    deviceContext_->OMSetDepthStencilState(depthStencilState_.Get(), 0);

    constexpr FLOAT blendFactor[4] = { 0.0F, 0.0F, 0.0F, 0.0F };
    deviceContext_->OMSetBlendState(blendState_.Get(), blendFactor, 0xFFFFFFFF);

    constexpr UINT stride = sizeof(OverlayVertex);
    constexpr UINT offset = 0;
    ID3D11Buffer* vertexBuffers[] = { vertexBuffer_.Get() };
    deviceContext_->IASetInputLayout(inputLayout_.Get());
    deviceContext_->IASetPrimitiveTopology(D3D11_PRIMITIVE_TOPOLOGY_TRIANGLESTRIP);
    deviceContext_->IASetVertexBuffers(0, 1, vertexBuffers, &stride, &offset);
    deviceContext_->VSSetShader(vertexShader_.Get(), nullptr, 0);
    deviceContext_->PSSetShader(pixelShader_.Get(), nullptr, 0);

    ID3D11ShaderResourceView* shaderResources[] = { overlayTextureView_.Get() };
    ID3D11SamplerState* samplers[] = { samplerState_.Get() };
    deviceContext_->PSSetShaderResources(0, 1, shaderResources);
    deviceContext_->PSSetSamplers(0, 1, samplers);

    deviceContext_->Draw(4, 0);

    ID3D11ShaderResourceView* nullShaderResources[] = { nullptr };
    deviceContext_->PSSetShaderResources(0, 1, nullShaderResources);

    ID3D11RenderTargetView* previousRenderTargets[] = { previousRenderTargetView.Get() };
    deviceContext_->OMSetRenderTargets(1, previousRenderTargets, previousDepthStencilView.Get());
    deviceContext_->RSSetViewports(previousViewportCount, previousViewports.data());
    deviceContext_->OMSetBlendState(previousBlendState.Get(), previousBlendFactor, previousSampleMask);
    deviceContext_->OMSetDepthStencilState(previousDepthStencilState.Get(), previousStencilReference);
    deviceContext_->RSSetState(previousRasterizerState.Get());
    deviceContext_->IASetInputLayout(previousInputLayout.Get());
    deviceContext_->IASetPrimitiveTopology(previousTopology);
    ID3D11Buffer* previousVertexBuffers[] = { previousVertexBuffer.Get() };
    deviceContext_->IASetVertexBuffers(0, 1, previousVertexBuffers, &previousStride, &previousOffset);
    deviceContext_->VSSetShader(previousVertexShader.Get(), nullptr, 0);
    deviceContext_->PSSetShader(previousPixelShader.Get(), nullptr, 0);
    ID3D11ShaderResourceView* restoredShaderResources[] = { previousShaderResourceView.Get() };
    ID3D11SamplerState* restoredSamplers[] = { previousSamplerState.Get() };
    deviceContext_->PSSetShaderResources(0, 1, restoredShaderResources);
    deviceContext_->PSSetSamplers(0, 1, restoredSamplers);
    return true;
}

void D3D11OverlayRenderer::UpdateOverlayGeometry()
{
    if (vertexBuffer_ == nullptr || deviceContext_ == nullptr || backBufferWidth_ == 0 || backBufferHeight_ == 0)
    {
        return;
    }

    const float left = (OverlayOriginX / static_cast<float>(backBufferWidth_)) * 2.0F - 1.0F;
    const float right = ((OverlayOriginX + static_cast<float>(OverlayPanelWidth)) / static_cast<float>(backBufferWidth_)) * 2.0F - 1.0F;
    const float top = 1.0F - (OverlayOriginY / static_cast<float>(backBufferHeight_)) * 2.0F;
    const float bottom = 1.0F - ((OverlayOriginY + static_cast<float>(OverlayPanelHeight)) / static_cast<float>(backBufferHeight_)) * 2.0F;

    const OverlayVertex vertices[] =
    {
        { { left, top }, { 0.0F, 0.0F } },
        { { right, top }, { 1.0F, 0.0F } },
        { { left, bottom }, { 0.0F, 1.0F } },
        { { right, bottom }, { 1.0F, 1.0F } },
    };

    D3D11_MAPPED_SUBRESOURCE mappedResource {};
    if (FAILED(deviceContext_->Map(vertexBuffer_.Get(), 0, D3D11_MAP_WRITE_DISCARD, 0, &mappedResource)))
    {
        AppendRendererLogLine(L"AOM overlay: failed to map vertex buffer.");
        return;
    }

    std::memcpy(mappedResource.pData, vertices, sizeof(vertices));
    deviceContext_->Unmap(vertexBuffer_.Get(), 0);
}

void D3D11OverlayRenderer::DiscardTargetResources()
{
    renderTargetView_.Reset();
    overlayTextureView_.Reset();
    overlayTexture_.Reset();
    samplerState_.Reset();
    blendState_.Reset();
    rasterizerState_.Reset();
    depthStencilState_.Reset();
    vertexShader_.Reset();
    pixelShader_.Reset();
    inputLayout_.Reset();
    vertexBuffer_.Reset();
    deviceContext_.Reset();
    device_.Reset();
    activeSwapChain_.Reset();
    backBufferWidth_ = 0;
    backBufferHeight_ = 0;
    lastUploadedSequence_ = 0;
}

std::wstring D3D11OverlayRenderer::BuildOverlayText(const OverlaySnapshot& snapshot) const
{
    std::wstring text;
    text.reserve(512);

    text.append(L"Preset: ").append(snapshot.currentPresetDisplayName.empty() ? L"-" : snapshot.currentPresetDisplayName).append(L"\n");
    text.append(L"TrackIR: ").append(snapshot.liveTrackIrStatus.empty() ? L"-" : snapshot.liveTrackIrStatus).append(L"\n");
    text.append(L"UDP: ").append(snapshot.udpStreamingStatus.empty() ? L"-" : snapshot.udpStreamingStatus).append(L"\n");
    text.append(L"\nPose\n").append(snapshot.outputPoseSummary.empty() ? L"No pose yet." : snapshot.outputPoseSummary).append(L"\n\n");
    text.append(L"Runtime\n").append(snapshot.runtimeStateSummary.empty() ? L"No runtime state yet." : snapshot.runtimeStateSummary).append(L"\n\n");
    text.append(snapshot.trackIrRateSummary.empty() ? L"" : snapshot.trackIrRateSummary).append(L"\n");
    text.append(snapshot.udpRateSummary.empty() ? L"" : snapshot.udpRateSummary);

    return text;
}
}