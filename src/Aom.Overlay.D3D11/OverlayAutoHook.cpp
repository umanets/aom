#include "OverlayAutoHook.h"

#include <Windows.h>
#include <Psapi.h>

#include <d3d11.h>
#include <dxgi1_2.h>

#include <algorithm>
#include <array>
#include <atomic>
#include <cstdint>
#include <string>
#include <vector>

#include "OverlayRuntime.h"

#pragma comment(lib, "Psapi.lib")

namespace
{
constexpr wchar_t AutoHookEnvironmentVariable[] = L"AOM_OVERLAY_AUTOHOOK";
constexpr wchar_t HookEventEnvironmentVariable[] = L"AOM_OVERLAY_HOOK_EVENT";
constexpr wchar_t HookHitEventName[] = L"Local\\AomOverlayPresentHookHit";
constexpr std::size_t PresentVTableIndex = 8;
constexpr std::size_t ResizeBuffersVTableIndex = 13;
constexpr std::size_t FactoryCreateSwapChainVTableIndex = 10;
constexpr std::size_t Factory2CreateSwapChainForHwndVTableIndex = 15;
constexpr DWORD ImportPatchRetryIntervalMs = 25;

using D3D11CreateDeviceAndSwapChainFn = HRESULT(WINAPI*)(
    IDXGIAdapter*,
    D3D_DRIVER_TYPE,
    HMODULE,
    UINT,
    const D3D_FEATURE_LEVEL*,
    UINT,
    UINT,
    const DXGI_SWAP_CHAIN_DESC*,
    IDXGISwapChain**,
    ID3D11Device**,
    D3D_FEATURE_LEVEL*,
    ID3D11DeviceContext**);

using D3D11CreateDeviceFn = HRESULT(WINAPI*)(
    IDXGIAdapter*,
    D3D_DRIVER_TYPE,
    HMODULE,
    UINT,
    const D3D_FEATURE_LEVEL*,
    UINT,
    UINT,
    ID3D11Device**,
    D3D_FEATURE_LEVEL*,
    ID3D11DeviceContext**);

using PresentFn = HRESULT(STDMETHODCALLTYPE*)(IDXGISwapChain*, UINT, UINT);
using ResizeBuffersFn = HRESULT(STDMETHODCALLTYPE*)(IDXGISwapChain*, UINT, UINT, UINT, DXGI_FORMAT, UINT);
using CreateSwapChainFn = HRESULT(STDMETHODCALLTYPE*)(IDXGIFactory*, IUnknown*, DXGI_SWAP_CHAIN_DESC*, IDXGISwapChain**);
using CreateSwapChainForHwndFn = HRESULT(STDMETHODCALLTYPE*)(
    IDXGIFactory2*,
    IUnknown*,
    HWND,
    const DXGI_SWAP_CHAIN_DESC1*,
    const DXGI_SWAP_CHAIN_FULLSCREEN_DESC*,
    IDXGIOutput*,
    IDXGISwapChain1**);

std::atomic<bool> g_isInitialized = false;
std::atomic<bool> g_presentEventRaised = false;
std::atomic<bool> g_shutdownRequested = false;
D3D11CreateDeviceAndSwapChainFn g_originalCreateDeviceAndSwapChain = nullptr;
D3D11CreateDeviceFn g_originalCreateDevice = nullptr;
PresentFn g_originalPresent = nullptr;
ResizeBuffersFn g_originalResizeBuffers = nullptr;
CreateSwapChainFn g_originalFactoryCreateSwapChain = nullptr;
CreateSwapChainForHwndFn g_originalFactoryCreateSwapChainForHwnd = nullptr;
HANDLE g_presentHitEvent = nullptr;

std::wstring GetEnvironmentVariableValue(const wchar_t* variableName)
{
    wchar_t buffer[512] {};
    const DWORD length = GetEnvironmentVariableW(variableName, buffer, static_cast<DWORD>(std::size(buffer)));
    if (length == 0 || length >= std::size(buffer))
    {
        return {};
    }

    return std::wstring(buffer, buffer + length);
}

std::wstring ToLowerInvariant(std::wstring value)
{
    std::transform(value.begin(), value.end(), value.begin(), [](wchar_t character)
    {
        return static_cast<wchar_t>(towlower(character));
    });

    return value;
}

bool EqualsIgnoreCase(const std::wstring_view left, const std::wstring_view right)
{
    if (left.size() != right.size())
    {
        return false;
    }

    for (std::size_t index = 0; index < left.size(); ++index)
    {
        if (towlower(left[index]) != towlower(right[index]))
        {
            return false;
        }
    }

    return true;
}

void SignalPresentHookHit()
{
    if (!g_presentEventRaised.exchange(true) && g_presentHitEvent != nullptr)
    {
        SetEvent(g_presentHitEvent);
    }
}

bool PatchPointer(void** targetPointer, void* replacement, void** originalPointer)
{
    if (targetPointer == nullptr || replacement == nullptr)
    {
        return false;
    }

    DWORD oldProtect = 0;
    if (!VirtualProtect(targetPointer, sizeof(void*), PAGE_EXECUTE_READWRITE, &oldProtect))
    {
        return false;
    }

    if (originalPointer != nullptr)
    {
        *originalPointer = *targetPointer;
    }

    *targetPointer = replacement;
    FlushInstructionCache(GetCurrentProcess(), targetPointer, sizeof(void*));

    DWORD ignored = 0;
    VirtualProtect(targetPointer, sizeof(void*), oldProtect, &ignored);
    return true;
}

HRESULT STDMETHODCALLTYPE HookedPresent(IDXGISwapChain* swapChain, UINT syncInterval, UINT flags)
{
    aom::overlay::OverlayRuntime::Instance().RenderFrame(swapChain);
    SignalPresentHookHit();
    return g_originalPresent != nullptr
        ? g_originalPresent(swapChain, syncInterval, flags)
        : E_FAIL;
}

HRESULT STDMETHODCALLTYPE HookedResizeBuffers(
    IDXGISwapChain* swapChain,
    UINT bufferCount,
    UINT width,
    UINT height,
    DXGI_FORMAT newFormat,
    UINT swapChainFlags)
{
    aom::overlay::OverlayRuntime::Instance().Reset();
    return g_originalResizeBuffers != nullptr
        ? g_originalResizeBuffers(swapChain, bufferCount, width, height, newFormat, swapChainFlags)
        : E_FAIL;
}

void InstallSwapChainHooks(IDXGISwapChain* swapChain)
{
    if (swapChain == nullptr)
    {
        return;
    }

    auto*** swapChainAsVTablePointer = reinterpret_cast<void***>(swapChain);
    if (swapChainAsVTablePointer == nullptr || *swapChainAsVTablePointer == nullptr)
    {
        return;
    }

    void** vTable = *swapChainAsVTablePointer;

    if (g_originalPresent == nullptr && vTable[PresentVTableIndex] != reinterpret_cast<void*>(&HookedPresent))
    {
        PatchPointer(&vTable[PresentVTableIndex], reinterpret_cast<void*>(&HookedPresent), reinterpret_cast<void**>(&g_originalPresent));
    }

    if (g_originalResizeBuffers == nullptr && vTable[ResizeBuffersVTableIndex] != reinterpret_cast<void*>(&HookedResizeBuffers))
    {
        PatchPointer(&vTable[ResizeBuffersVTableIndex], reinterpret_cast<void*>(&HookedResizeBuffers), reinterpret_cast<void**>(&g_originalResizeBuffers));
    }
}

HRESULT STDMETHODCALLTYPE HookedCreateSwapChain(
    IDXGIFactory* factory,
    IUnknown* device,
    DXGI_SWAP_CHAIN_DESC* description,
    IDXGISwapChain** swapChain)
{
    if (g_originalFactoryCreateSwapChain == nullptr)
    {
        return E_FAIL;
    }

    const HRESULT result = g_originalFactoryCreateSwapChain(factory, device, description, swapChain);
    if (SUCCEEDED(result) && swapChain != nullptr && *swapChain != nullptr)
    {
        InstallSwapChainHooks(*swapChain);
    }

    return result;
}

HRESULT STDMETHODCALLTYPE HookedCreateSwapChainForHwnd(
    IDXGIFactory2* factory,
    IUnknown* device,
    HWND window,
    const DXGI_SWAP_CHAIN_DESC1* description,
    const DXGI_SWAP_CHAIN_FULLSCREEN_DESC* fullscreenDescription,
    IDXGIOutput* restrictToOutput,
    IDXGISwapChain1** swapChain)
{
    if (g_originalFactoryCreateSwapChainForHwnd == nullptr)
    {
        return E_FAIL;
    }

    const HRESULT result = g_originalFactoryCreateSwapChainForHwnd(
        factory,
        device,
        window,
        description,
        fullscreenDescription,
        restrictToOutput,
        swapChain);

    if (SUCCEEDED(result) && swapChain != nullptr && *swapChain != nullptr)
    {
        InstallSwapChainHooks(*swapChain);
    }

    return result;
}

void InstallFactoryHooks(IDXGIFactory* factory)
{
    if (factory == nullptr)
    {
        return;
    }

    auto*** factoryAsVTablePointer = reinterpret_cast<void***>(factory);
    if (factoryAsVTablePointer == nullptr || *factoryAsVTablePointer == nullptr)
    {
        return;
    }

    void** vTable = *factoryAsVTablePointer;
    if (g_originalFactoryCreateSwapChain == nullptr && vTable[FactoryCreateSwapChainVTableIndex] != reinterpret_cast<void*>(&HookedCreateSwapChain))
    {
        PatchPointer(&vTable[FactoryCreateSwapChainVTableIndex], reinterpret_cast<void*>(&HookedCreateSwapChain), reinterpret_cast<void**>(&g_originalFactoryCreateSwapChain));
    }

    IDXGIFactory2* factory2 = nullptr;
    if (SUCCEEDED(factory->QueryInterface(__uuidof(IDXGIFactory2), reinterpret_cast<void**>(&factory2))) && factory2 != nullptr)
    {
        auto*** factory2AsVTablePointer = reinterpret_cast<void***>(factory2);
        if (factory2AsVTablePointer != nullptr && *factory2AsVTablePointer != nullptr)
        {
            void** factory2VTable = *factory2AsVTablePointer;
            if (g_originalFactoryCreateSwapChainForHwnd == nullptr
                && factory2VTable[Factory2CreateSwapChainForHwndVTableIndex] != reinterpret_cast<void*>(&HookedCreateSwapChainForHwnd))
            {
                PatchPointer(
                    &factory2VTable[Factory2CreateSwapChainForHwndVTableIndex],
                    reinterpret_cast<void*>(&HookedCreateSwapChainForHwnd),
                    reinterpret_cast<void**>(&g_originalFactoryCreateSwapChainForHwnd));
            }
        }

        factory2->Release();
    }
}

void InstallFactoryHooksFromDevice(ID3D11Device* device)
{
    if (device == nullptr)
    {
        return;
    }

    IDXGIDevice* dxgiDevice = nullptr;
    if (FAILED(device->QueryInterface(__uuidof(IDXGIDevice), reinterpret_cast<void**>(&dxgiDevice))) || dxgiDevice == nullptr)
    {
        return;
    }

    IDXGIAdapter* adapter = nullptr;
    if (SUCCEEDED(dxgiDevice->GetAdapter(&adapter)) && adapter != nullptr)
    {
        IDXGIFactory* factory = nullptr;
        if (SUCCEEDED(adapter->GetParent(__uuidof(IDXGIFactory), reinterpret_cast<void**>(&factory))) && factory != nullptr)
        {
            InstallFactoryHooks(factory);
            factory->Release();
        }

        adapter->Release();
    }

    dxgiDevice->Release();
}

HRESULT WINAPI HookedD3D11CreateDevice(
    IDXGIAdapter* adapter,
    D3D_DRIVER_TYPE driverType,
    HMODULE software,
    UINT flags,
    const D3D_FEATURE_LEVEL* featureLevels,
    UINT featureLevelsCount,
    UINT sdkVersion,
    ID3D11Device** device,
    D3D_FEATURE_LEVEL* featureLevel,
    ID3D11DeviceContext** immediateContext)
{
    if (g_originalCreateDevice == nullptr)
    {
        return E_FAIL;
    }

    const HRESULT result = g_originalCreateDevice(
        adapter,
        driverType,
        software,
        flags,
        featureLevels,
        featureLevelsCount,
        sdkVersion,
        device,
        featureLevel,
        immediateContext);

    if (SUCCEEDED(result) && device != nullptr && *device != nullptr)
    {
        InstallFactoryHooksFromDevice(*device);
    }

    return result;
}

HRESULT WINAPI HookedD3D11CreateDeviceAndSwapChain(
    IDXGIAdapter* adapter,
    D3D_DRIVER_TYPE driverType,
    HMODULE software,
    UINT flags,
    const D3D_FEATURE_LEVEL* featureLevels,
    UINT featureLevelsCount,
    UINT sdkVersion,
    const DXGI_SWAP_CHAIN_DESC* swapChainDescription,
    IDXGISwapChain** swapChain,
    ID3D11Device** device,
    D3D_FEATURE_LEVEL* featureLevel,
    ID3D11DeviceContext** immediateContext)
{
    if (g_originalCreateDeviceAndSwapChain == nullptr)
    {
        return E_FAIL;
    }

    const HRESULT result = g_originalCreateDeviceAndSwapChain(
        adapter,
        driverType,
        software,
        flags,
        featureLevels,
        featureLevelsCount,
        sdkVersion,
        swapChainDescription,
        swapChain,
        device,
        featureLevel,
        immediateContext);

    if (SUCCEEDED(result) && swapChain != nullptr && *swapChain != nullptr)
    {
        InstallSwapChainHooks(*swapChain);
    }

    if (SUCCEEDED(result) && device != nullptr && *device != nullptr)
    {
        InstallFactoryHooksFromDevice(*device);
    }

    return result;
}

bool PatchImportEntryForModule(HMODULE moduleHandle)
{
    if (moduleHandle == nullptr)
    {
        return false;
    }

    const auto* dosHeader = reinterpret_cast<const IMAGE_DOS_HEADER*>(moduleHandle);
    if (dosHeader->e_magic != IMAGE_DOS_SIGNATURE)
    {
        return false;
    }

    const auto* ntHeaders = reinterpret_cast<const IMAGE_NT_HEADERS*>(reinterpret_cast<const std::byte*>(moduleHandle) + dosHeader->e_lfanew);
    if (ntHeaders->Signature != IMAGE_NT_SIGNATURE)
    {
        return false;
    }

    const auto& importDirectory = ntHeaders->OptionalHeader.DataDirectory[IMAGE_DIRECTORY_ENTRY_IMPORT];
    if (importDirectory.VirtualAddress == 0)
    {
        return false;
    }

    auto* importDescriptor = reinterpret_cast<IMAGE_IMPORT_DESCRIPTOR*>(reinterpret_cast<std::byte*>(moduleHandle) + importDirectory.VirtualAddress);
    bool patchedAny = false;

    while (importDescriptor->Name != 0)
    {
        const auto* importedModuleName = reinterpret_cast<const char*>(reinterpret_cast<const std::byte*>(moduleHandle) + importDescriptor->Name);
        std::wstring wideModuleName;
        for (const char* cursor = importedModuleName; *cursor != '\0'; ++cursor)
        {
            wideModuleName.push_back(static_cast<unsigned char>(*cursor));
        }

        if (!EqualsIgnoreCase(ToLowerInvariant(wideModuleName), L"d3d11.dll"))
        {
            ++importDescriptor;
            continue;
        }

        auto* originalThunk = reinterpret_cast<IMAGE_THUNK_DATA*>(reinterpret_cast<std::byte*>(moduleHandle) + importDescriptor->OriginalFirstThunk);
        auto* firstThunk = reinterpret_cast<IMAGE_THUNK_DATA*>(reinterpret_cast<std::byte*>(moduleHandle) + importDescriptor->FirstThunk);
        if (importDescriptor->OriginalFirstThunk == 0)
        {
            originalThunk = firstThunk;
        }

        while (originalThunk->u1.AddressOfData != 0)
        {
            if ((originalThunk->u1.Ordinal & IMAGE_ORDINAL_FLAG) == 0)
            {
                const auto* importByName = reinterpret_cast<const IMAGE_IMPORT_BY_NAME*>(reinterpret_cast<const std::byte*>(moduleHandle) + originalThunk->u1.AddressOfData);
                if (std::strcmp(reinterpret_cast<const char*>(importByName->Name), "D3D11CreateDeviceAndSwapChain") == 0)
                {
                    PatchPointer(reinterpret_cast<void**>(&firstThunk->u1.Function), reinterpret_cast<void*>(&HookedD3D11CreateDeviceAndSwapChain), nullptr);
                    patchedAny = true;
                }
                else if (std::strcmp(reinterpret_cast<const char*>(importByName->Name), "D3D11CreateDevice") == 0)
                {
                    PatchPointer(reinterpret_cast<void**>(&firstThunk->u1.Function), reinterpret_cast<void*>(&HookedD3D11CreateDevice), nullptr);
                    patchedAny = true;
                }
            }

            ++originalThunk;
            ++firstThunk;
        }

        ++importDescriptor;
    }

    return patchedAny;
}

void PatchImportsForLoadedModules()
{
    std::array<HMODULE, 512> modules {};
    DWORD bytesNeeded = 0;
    if (!EnumProcessModules(GetCurrentProcess(), modules.data(), static_cast<DWORD>(modules.size() * sizeof(HMODULE)), &bytesNeeded))
    {
        return;
    }

    const auto moduleCount = std::min<std::size_t>(modules.size(), bytesNeeded / sizeof(HMODULE));
    for (std::size_t index = 0; index < moduleCount; ++index)
    {
        PatchImportEntryForModule(modules[index]);
    }
}
}

namespace aom::overlay
{
bool OverlayAutoHook::ShouldAutoInstall()
{
    wchar_t buffer[8] {};
    return GetEnvironmentVariableW(AutoHookEnvironmentVariable, buffer, static_cast<DWORD>(std::size(buffer))) > 0;
}

void OverlayAutoHook::Initialize()
{
    if (g_isInitialized.exchange(true))
    {
        return;
    }

    g_shutdownRequested = false;

    const auto eventName = GetEnvironmentVariableValue(HookEventEnvironmentVariable);
    g_presentHitEvent = CreateEventW(nullptr, TRUE, FALSE, eventName.empty() ? HookHitEventName : eventName.c_str());

    HMODULE d3d11Module = GetModuleHandleW(L"d3d11.dll");
    if (d3d11Module == nullptr)
    {
        d3d11Module = LoadLibraryW(L"d3d11.dll");
    }

    if (d3d11Module == nullptr)
    {
        return;
    }

    g_originalCreateDeviceAndSwapChain = reinterpret_cast<D3D11CreateDeviceAndSwapChainFn>(
        GetProcAddress(d3d11Module, "D3D11CreateDeviceAndSwapChain"));
    g_originalCreateDevice = reinterpret_cast<D3D11CreateDeviceFn>(
        GetProcAddress(d3d11Module, "D3D11CreateDevice"));

    if (g_originalCreateDeviceAndSwapChain == nullptr && g_originalCreateDevice == nullptr)
    {
        return;
    }

    PatchImportsForLoadedModules();

    while (!g_shutdownRequested.load() && !g_presentEventRaised.load())
    {
        PatchImportsForLoadedModules();
        Sleep(ImportPatchRetryIntervalMs);
    }
}

void OverlayAutoHook::Shutdown()
{
    g_shutdownRequested = true;

    if (g_presentHitEvent != nullptr)
    {
        CloseHandle(g_presentHitEvent);
        g_presentHitEvent = nullptr;
    }

    g_presentEventRaised = false;
    g_isInitialized = false;
}
}