#include <Windows.h>

#include <d3d11.h>
#include <dxgi.h>
#include <shellapi.h>
#include <wrl/client.h>

#include <chrono>
#include <filesystem>
#include <string>
#include <vector>

#include "OverlayExports.h"

using Microsoft::WRL::ComPtr;

namespace
{
constexpr wchar_t WindowClassName[] = L"AomOverlayPocHostWindow";
constexpr wchar_t BaseWindowTitle[] = L"AOM Overlay POC";
constexpr wchar_t ContinueEventName[] = L"Local\\AomOverlayPocContinue";
constexpr wchar_t HookHitEventName[] = L"Local\\AomOverlayPresentHookHit";

std::wstring FormatHResult(const HRESULT value)
{
    wchar_t buffer[16] {};
    swprintf_s(buffer, L"0x%08X", static_cast<unsigned int>(value));
    return buffer;
}

std::wstring BuildDllPath()
{
    std::wstring executablePath(MAX_PATH, L'\0');
    const auto length = GetModuleFileNameW(nullptr, executablePath.data(), static_cast<DWORD>(executablePath.size()));
    executablePath.resize(length);

    auto path = std::filesystem::path(executablePath).parent_path();
    path /= LR"(..\..\..\..\Aom.Overlay.D3D11\bin\x64\Debug\AomOverlayD3D11.dll)";
    return std::filesystem::weakly_canonical(path).wstring();
}

struct OverlayDll
{
    HMODULE module = nullptr;
    AomOverlayRenderFrameFn renderFrame = nullptr;
    AomOverlayResetFn reset = nullptr;
    AomOverlayGetProtocolDescriptionFn describe = nullptr;

    bool Load(std::wstring& error)
    {
        const auto dllPath = BuildDllPath();
        module = LoadLibraryW(dllPath.c_str());
        if (module == nullptr)
        {
            error = L"Failed to load overlay DLL from " + dllPath;
            return false;
        }

        renderFrame = reinterpret_cast<AomOverlayRenderFrameFn>(GetProcAddress(module, "AomOverlayRenderFrame"));
        reset = reinterpret_cast<AomOverlayResetFn>(GetProcAddress(module, "AomOverlayReset"));
        describe = reinterpret_cast<AomOverlayGetProtocolDescriptionFn>(GetProcAddress(module, "AomOverlayGetProtocolDescription"));

        if (renderFrame == nullptr || reset == nullptr || describe == nullptr)
        {
            error = L"Overlay DLL exports were not resolved.";
            return false;
        }

        return true;
    }

    void Unload()
    {
        if (module != nullptr)
        {
            FreeLibrary(module);
            module = nullptr;
        }

        renderFrame = nullptr;
        reset = nullptr;
        describe = nullptr;
    }
};

class UniqueHandle
{
public:
    UniqueHandle() = default;
    ~UniqueHandle()
    {
        Reset();
    }

    UniqueHandle(const UniqueHandle&) = delete;
    UniqueHandle& operator=(const UniqueHandle&) = delete;

    HANDLE Get() const
    {
        return handle_;
    }

    void Attach(HANDLE handle)
    {
        Reset();
        handle_ = handle;
    }

    void Reset()
    {
        if (handle_ != nullptr)
        {
            CloseHandle(handle_);
            handle_ = nullptr;
        }
    }

private:
    HANDLE handle_ = nullptr;
};

class PocHostApp
{
public:
    explicit PocHostApp(HINSTANCE instance, bool disableDirectOverlayCall, bool waitForAttach)
        : instance_(instance),
          disableDirectOverlayCall_(disableDirectOverlayCall),
          waitForAttach_(waitForAttach)
    {
    }

    int Run(int showCommand)
    {
        if (!disableDirectOverlayCall_)
        {
            std::wstring loadError;
            if (!overlayDll_.Load(loadError))
            {
                MessageBoxW(nullptr, loadError.c_str(), L"AOM Overlay POC", MB_ICONERROR | MB_OK);
                return 1;
            }
        }

        if (!CreateWindowClass() || !CreateMainWindow(showCommand))
        {
            overlayDll_.Unload();
            return 1;
        }

        if (waitForAttach_ && !WaitForAttachSignal())
        {
            overlayDll_.Unload();
            return 1;
        }

        if (!CreateDeviceResources())
        {
            overlayDll_.Unload();
            return 1;
        }

        UpdateWindowTitle(false);

        MSG message {};
        while (message.message != WM_QUIT)
        {
            if (PeekMessageW(&message, nullptr, 0, 0, PM_REMOVE))
            {
                TranslateMessage(&message);
                DispatchMessageW(&message);
                continue;
            }

            RenderFrame();
        }

        overlayDll_.Unload();
        return static_cast<int>(message.wParam);
    }

private:
    static LRESULT CALLBACK WindowProc(HWND windowHandle, UINT message, WPARAM wParam, LPARAM lParam)
    {
        auto* app = reinterpret_cast<PocHostApp*>(GetWindowLongPtrW(windowHandle, GWLP_USERDATA));
        if (message == WM_NCCREATE)
        {
            auto* createStruct = reinterpret_cast<CREATESTRUCTW*>(lParam);
            app = static_cast<PocHostApp*>(createStruct->lpCreateParams);
            SetWindowLongPtrW(windowHandle, GWLP_USERDATA, reinterpret_cast<LONG_PTR>(app));
        }

        if (app != nullptr)
        {
            return app->HandleMessage(windowHandle, message, wParam, lParam);
        }

        return DefWindowProcW(windowHandle, message, wParam, lParam);
    }

    bool CreateWindowClass()
    {
        WNDCLASSEXW windowClass {};
        windowClass.cbSize = sizeof(windowClass);
        windowClass.lpfnWndProc = WindowProc;
        windowClass.hInstance = instance_;
        windowClass.lpszClassName = WindowClassName;
        windowClass.hCursor = LoadCursorW(nullptr, IDC_ARROW);
        windowClass.hbrBackground = reinterpret_cast<HBRUSH>(COLOR_WINDOW + 1);

        return RegisterClassExW(&windowClass) != 0;
    }

    bool CreateMainWindow(int showCommand)
    {
        constexpr DWORD windowStyle = WS_OVERLAPPED | WS_CAPTION | WS_SYSMENU | WS_MINIMIZEBOX;
        RECT bounds { 0, 0, 1600, 900 };
        AdjustWindowRect(&bounds, windowStyle, FALSE);

        windowHandle_ = CreateWindowExW(
            0,
            WindowClassName,
            BaseWindowTitle,
            windowStyle,
            CW_USEDEFAULT,
            CW_USEDEFAULT,
            bounds.right - bounds.left,
            bounds.bottom - bounds.top,
            nullptr,
            nullptr,
            instance_,
            this);

        if (windowHandle_ == nullptr)
        {
            MessageBoxW(nullptr, L"Failed to create POC host window.", BaseWindowTitle, MB_ICONERROR | MB_OK);
            return false;
        }

        ShowWindow(windowHandle_, showCommand);
        return true;
    }

    bool WaitForAttachSignal()
    {
        continueEvent_.Attach(CreateEventW(nullptr, TRUE, FALSE, ContinueEventName));
        if (continueEvent_.Get() == nullptr)
        {
            MessageBoxW(windowHandle_, L"Failed to create the attach synchronization event for the POC host.", BaseWindowTitle, MB_ICONERROR | MB_OK);
            return false;
        }

        SetWindowTextW(windowHandle_, L"AOM Overlay POC | waiting for injected overlay attach");

        const DWORD waitResult = WaitForSingleObject(continueEvent_.Get(), 15000);
        if (waitResult == WAIT_OBJECT_0)
        {
            return true;
        }

        MessageBoxW(windowHandle_, L"Timed out waiting for the injected overlay to attach to the POC host.", BaseWindowTitle, MB_ICONERROR | MB_OK);
        return false;
    }

    bool CreateDeviceResources()
    {
        DXGI_SWAP_CHAIN_DESC swapChainDescription {};
        swapChainDescription.BufferDesc.Width = 1600;
        swapChainDescription.BufferDesc.Height = 900;
        swapChainDescription.BufferDesc.Format = DXGI_FORMAT_B8G8R8A8_UNORM;
        swapChainDescription.SampleDesc.Count = 1;
        swapChainDescription.BufferUsage = DXGI_USAGE_RENDER_TARGET_OUTPUT;
        swapChainDescription.BufferCount = 2;
        swapChainDescription.OutputWindow = windowHandle_;
        swapChainDescription.Windowed = TRUE;
        swapChainDescription.SwapEffect = DXGI_SWAP_EFFECT_DISCARD;

        const UINT baseCreationFlags = D3D11_CREATE_DEVICE_BGRA_SUPPORT;
        UINT debugCreationFlags = baseCreationFlags;
#if defined(_DEBUG)
        debugCreationFlags |= D3D11_CREATE_DEVICE_DEBUG;
#endif

        D3D_FEATURE_LEVEL featureLevel = D3D_FEATURE_LEVEL_11_0;
        const D3D_FEATURE_LEVEL requestedLevels[] =
        {
            D3D_FEATURE_LEVEL_11_1,
            D3D_FEATURE_LEVEL_11_0,
            D3D_FEATURE_LEVEL_10_1,
            D3D_FEATURE_LEVEL_10_0,
        };

        const D3D_FEATURE_LEVEL fallbackLevels[] =
        {
            D3D_FEATURE_LEVEL_11_0,
            D3D_FEATURE_LEVEL_10_1,
            D3D_FEATURE_LEVEL_10_0,
        };

        HRESULT lastResult = E_FAIL;
        auto tryCreateDevice = [&](const D3D_DRIVER_TYPE driverType,
                                   const UINT creationFlags,
                                   const D3D_FEATURE_LEVEL* levels,
                                   const UINT levelCount,
                                   const wchar_t* backendLabel) -> bool
        {
            swapChain_.Reset();
            device_.Reset();
            context_.Reset();
            renderTargetView_.Reset();
            featureLevel = D3D_FEATURE_LEVEL_11_0;

            lastResult = D3D11CreateDeviceAndSwapChain(
                nullptr,
                driverType,
                nullptr,
                creationFlags,
                levels,
                levelCount,
                D3D11_SDK_VERSION,
                &swapChainDescription,
                swapChain_.ReleaseAndGetAddressOf(),
                device_.ReleaseAndGetAddressOf(),
                &featureLevel,
                context_.ReleaseAndGetAddressOf());

            if (FAILED(lastResult))
            {
                return false;
            }

            deviceBackend_ = backendLabel;
            return true;
        };

        bool created = false;
#if defined(_DEBUG)
        created = tryCreateDevice(
            D3D_DRIVER_TYPE_HARDWARE,
            debugCreationFlags,
            requestedLevels,
            static_cast<UINT>(std::size(requestedLevels)),
            L"hardware + debug layer");

        if (!created)
        {
            created = tryCreateDevice(
                D3D_DRIVER_TYPE_HARDWARE,
                debugCreationFlags,
                fallbackLevels,
                static_cast<UINT>(std::size(fallbackLevels)),
                L"hardware + debug layer");
        }
#endif

        if (!created)
        {
            created = tryCreateDevice(
                D3D_DRIVER_TYPE_HARDWARE,
                baseCreationFlags,
                requestedLevels,
                static_cast<UINT>(std::size(requestedLevels)),
                L"hardware");
        }

        if (!created)
        {
            created = tryCreateDevice(
                D3D_DRIVER_TYPE_HARDWARE,
                baseCreationFlags,
                fallbackLevels,
                static_cast<UINT>(std::size(fallbackLevels)),
                L"hardware");
        }

        if (!created)
        {
            created = tryCreateDevice(
                D3D_DRIVER_TYPE_WARP,
                baseCreationFlags,
                fallbackLevels,
                static_cast<UINT>(std::size(fallbackLevels)),
                L"WARP fallback");
        }

        if (!created)
        {
            std::wstring message = L"Failed to create D3D11 device and swap chain for the POC host.\nLast HRESULT: ";
            message += FormatHResult(lastResult);

            if (lastResult == DXGI_ERROR_SDK_COMPONENT_MISSING)
            {
                message += L"\nThe Direct3D debug layer is not installed on this machine.";
            }
            else if (lastResult == E_INVALIDARG)
            {
                message += L"\nThis usually means the runtime rejected the requested feature-level set.";
            }

            MessageBoxW(windowHandle_, message.c_str(), BaseWindowTitle, MB_ICONERROR | MB_OK);
            return false;
        }

        ComPtr<ID3D11Texture2D> backBuffer;
        if (FAILED(swapChain_->GetBuffer(0, IID_PPV_ARGS(backBuffer.ReleaseAndGetAddressOf())))
            || FAILED(device_->CreateRenderTargetView(backBuffer.Get(), nullptr, renderTargetView_.ReleaseAndGetAddressOf())))
        {
            MessageBoxW(windowHandle_, L"Failed to create the back-buffer render target view.", BaseWindowTitle, MB_ICONERROR | MB_OK);
            return false;
        }

        return true;
    }

    void RenderFrame()
    {
        const float clearColor[4] = { 0.06f, 0.09f, 0.14f, 1.0f };
        context_->OMSetRenderTargets(1, renderTargetView_.GetAddressOf(), nullptr);
        context_->ClearRenderTargetView(renderTargetView_.Get(), clearColor);

        const bool overlayRendered = !disableDirectOverlayCall_
            && overlayDll_.renderFrame != nullptr
            && overlayDll_.renderFrame(swapChain_.Get()) == TRUE;

        UpdateInjectedHookState();

        swapChain_->Present(1, 0);
        UpdateWindowTitle(overlayRendered);
    }

    void UpdateInjectedHookState()
    {
        if (injectedHookObserved_)
        {
            return;
        }

        if (hookHitEvent_.Get() == nullptr)
        {
            hookHitEvent_.Attach(OpenEventW(SYNCHRONIZE, FALSE, HookHitEventName));
        }

        if (hookHitEvent_.Get() != nullptr && WaitForSingleObject(hookHitEvent_.Get(), 0) == WAIT_OBJECT_0)
        {
            injectedHookObserved_ = true;
        }
    }

    void UpdateWindowTitle(const bool overlayRendered)
    {
        const auto now = std::chrono::steady_clock::now();
        if (now - lastTitleUpdateAt_ < std::chrono::milliseconds(500))
        {
            return;
        }

        lastTitleUpdateAt_ = now;

        std::wstring title = BaseWindowTitle;
        title += L" | backend: ";
        title += deviceBackend_.empty() ? L"unknown" : deviceBackend_;

        if (injectedHookObserved_)
        {
            title += L" | injected Present hook active";
        }
        else if (overlayRendered)
        {
            title += L" | overlay rendered from AOM feed";
        }
        else if (disableDirectOverlayCall_)
        {
            title += L" | waiting for injected Present hook";
        }
        else
        {
            title += L" | waiting for AOM app + visible overlay toggle";
        }

        if (!disableDirectOverlayCall_ && overlayDll_.describe != nullptr)
        {
            title += L" | ";
            title += overlayDll_.describe();
        }

        SetWindowTextW(windowHandle_, title.c_str());
    }

    LRESULT HandleMessage(HWND windowHandle, UINT message, WPARAM wParam, LPARAM lParam)
    {
        switch (message)
        {
        case WM_DESTROY:
            PostQuitMessage(0);
            return 0;
        case WM_KEYDOWN:
            if (wParam == VK_ESCAPE)
            {
                DestroyWindow(windowHandle);
                return 0;
            }

            break;
        default:
            break;
        }

        return DefWindowProcW(windowHandle, message, wParam, lParam);
    }

    HINSTANCE instance_ = nullptr;
    HWND windowHandle_ = nullptr;
    OverlayDll overlayDll_;
    ComPtr<ID3D11Device> device_;
    ComPtr<ID3D11DeviceContext> context_;
    ComPtr<IDXGISwapChain> swapChain_;
    ComPtr<ID3D11RenderTargetView> renderTargetView_;
    std::chrono::steady_clock::time_point lastTitleUpdateAt_ {};
    std::wstring deviceBackend_;
    bool disableDirectOverlayCall_ = false;
    bool waitForAttach_ = false;
    bool injectedHookObserved_ = false;
    UniqueHandle continueEvent_;
    UniqueHandle hookHitEvent_;
};
}

int WINAPI wWinMain(HINSTANCE instance, HINSTANCE, PWSTR, int showCommand)
{
    int argumentCount = 0;
    PWSTR* arguments = CommandLineToArgvW(GetCommandLineW(), &argumentCount);
    bool disableDirectOverlayCall = false;
    bool waitForAttach = false;

    for (int index = 1; index < argumentCount; ++index)
    {
        if (std::wstring_view(arguments[index]) == L"--no-direct-overlay-call")
        {
            disableDirectOverlayCall = true;
        }
        else if (std::wstring_view(arguments[index]) == L"--wait-for-attach")
        {
            waitForAttach = true;
        }
    }

    if (arguments != nullptr)
    {
        LocalFree(arguments);
    }

    PocHostApp app(instance, disableDirectOverlayCall, waitForAttach);
    return app.Run(showCommand);
}