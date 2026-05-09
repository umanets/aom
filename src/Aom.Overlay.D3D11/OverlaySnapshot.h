#pragma once

#include <cstdint>
#include <string>

namespace aom::overlay
{
struct OverlaySnapshot
{
    bool isVisible = false;
    bool isFresh = false;
    std::uint64_t sequence = 0;
    std::wstring currentPresetDisplayName;
    std::wstring liveTrackIrStatus;
    std::wstring udpStreamingStatus;
    std::wstring outputPoseSummary;
    std::wstring runtimeStateSummary;
    std::wstring trackIrRateSummary;
    std::wstring udpRateSummary;
    std::wstring updatedAtUtc;
};
}