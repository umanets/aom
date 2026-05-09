#pragma once

#include <Windows.h>

#include <array>
#include <cstddef>

#include "OverlaySnapshot.h"

namespace aom::overlay
{
class OverlaySharedStateReader
{
public:
    OverlaySharedStateReader();
    ~OverlaySharedStateReader();

    OverlaySharedStateReader(const OverlaySharedStateReader&) = delete;
    OverlaySharedStateReader& operator=(const OverlaySharedStateReader&) = delete;

    bool TryReadLatest(OverlaySnapshot& snapshot);
    void Reset();

private:
    static constexpr std::size_t SharedMemoryCapacityBytes = 64 * 1024;
    static constexpr std::size_t HeaderSizeBytes = 24;
    static constexpr std::int32_t PacketVersion = 1;

    bool EnsureHandles();
    bool TryParseSnapshot(const char* json, std::size_t jsonLength, OverlaySnapshot& snapshot) const;

    HANDLE mappingHandle_;
    HANDLE updatedEventHandle_;
    void* mappedView_;
    std::array<std::byte, SharedMemoryCapacityBytes> localBuffer_;
};
}