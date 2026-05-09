#include "OverlaySharedStateReader.h"

#include <Shlwapi.h>

#include <algorithm>
#include <cstring>
#include <string>
#include <string_view>
#include <vector>

#pragma comment(lib, "Shlwapi.lib")

namespace
{
constexpr wchar_t MappingName[] = L"Local\\AomDesktop.OverlayState";
constexpr wchar_t UpdatedEventName[] = L"Local\\AomDesktop.OverlayUpdated";

std::wstring Utf8ToWide(const std::string_view value)
{
    if (value.empty())
    {
        return {};
    }

    const int requiredLength = MultiByteToWideChar(CP_UTF8, 0, value.data(), static_cast<int>(value.size()), nullptr, 0);
    if (requiredLength <= 0)
    {
        return {};
    }

    std::wstring result(static_cast<std::size_t>(requiredLength), L'\0');
    MultiByteToWideChar(CP_UTF8, 0, value.data(), static_cast<int>(value.size()), result.data(), requiredLength);
    return result;
}

bool TryExtractBool(const std::string_view json, const std::string_view key, bool& value)
{
    const std::string pattern = "\"" + std::string(key) + "\":";
    const auto start = json.find(pattern);
    if (start == std::string_view::npos)
    {
        return false;
    }

    const auto valueStart = start + pattern.size();
    if (json.compare(valueStart, 4, "true") == 0)
    {
        value = true;
        return true;
    }

    if (json.compare(valueStart, 5, "false") == 0)
    {
        value = false;
        return true;
    }

    return false;
}

bool TryExtractJsonString(const std::string_view json, const std::string_view key, std::string& value)
{
    const std::string pattern = "\"" + std::string(key) + "\":\"";
    const auto start = json.find(pattern);
    if (start == std::string_view::npos)
    {
        return false;
    }

    auto cursor = start + pattern.size();
    std::string result;

    while (cursor < json.size())
    {
        const char current = json[cursor++];
        if (current == '\\')
        {
            if (cursor >= json.size())
            {
                return false;
            }

            const char escaped = json[cursor++];
            switch (escaped)
            {
            case '\\':
            case '"':
            case '/':
                result.push_back(escaped);
                break;
            case 'b':
                result.push_back('\b');
                break;
            case 'f':
                result.push_back('\f');
                break;
            case 'n':
                result.push_back('\n');
                break;
            case 'r':
                result.push_back('\r');
                break;
            case 't':
                result.push_back('\t');
                break;
            case 'u':
                if (cursor + 4 > json.size())
                {
                    return false;
                }

                cursor += 4;
                result.push_back('?');
                break;
            default:
                return false;
            }

            continue;
        }

        if (current == '"')
        {
            value = std::move(result);
            return true;
        }

        result.push_back(current);
    }

    return false;
}
}

namespace aom::overlay
{
OverlaySharedStateReader::OverlaySharedStateReader()
    : mappingHandle_(nullptr),
      updatedEventHandle_(nullptr),
      mappedView_(nullptr)
{
    localBuffer_.fill(std::byte { 0 });
}

OverlaySharedStateReader::~OverlaySharedStateReader()
{
    Reset();
}

bool OverlaySharedStateReader::TryReadLatest(OverlaySnapshot& snapshot)
{
    if (!EnsureHandles())
    {
        return false;
    }

    std::memcpy(localBuffer_.data(), mappedView_, localBuffer_.size());

    std::int32_t version = 0;
    std::int32_t payloadLength = 0;
    std::uint64_t startSequence = 0;
    std::uint64_t endSequence = 0;
    std::memcpy(&version, localBuffer_.data(), sizeof(version));
    std::memcpy(&payloadLength, localBuffer_.data() + 4, sizeof(payloadLength));
    std::memcpy(&startSequence, localBuffer_.data() + 8, sizeof(startSequence));
    std::memcpy(&endSequence, localBuffer_.data() + 16, sizeof(endSequence));

    if (version != PacketVersion
        || payloadLength <= 0
        || static_cast<std::size_t>(payloadLength) > localBuffer_.size() - HeaderSizeBytes
        || startSequence == 0
        || startSequence != endSequence)
    {
        return false;
    }

    OverlaySnapshot nextSnapshot;
    nextSnapshot.sequence = startSequence;
    nextSnapshot.isFresh = true;

    if (!TryParseSnapshot(reinterpret_cast<const char*>(localBuffer_.data() + HeaderSizeBytes), static_cast<std::size_t>(payloadLength), nextSnapshot))
    {
        return false;
    }

    snapshot = std::move(nextSnapshot);
    return true;
}

void OverlaySharedStateReader::Reset()
{
    if (mappedView_ != nullptr)
    {
        UnmapViewOfFile(mappedView_);
        mappedView_ = nullptr;
    }

    if (updatedEventHandle_ != nullptr)
    {
        CloseHandle(updatedEventHandle_);
        updatedEventHandle_ = nullptr;
    }

    if (mappingHandle_ != nullptr)
    {
        CloseHandle(mappingHandle_);
        mappingHandle_ = nullptr;
    }
}

bool OverlaySharedStateReader::EnsureHandles()
{
    if (mappingHandle_ != nullptr && mappedView_ != nullptr)
    {
        return true;
    }

    mappingHandle_ = OpenFileMappingW(FILE_MAP_READ, FALSE, MappingName);
    if (mappingHandle_ == nullptr)
    {
        return false;
    }

    mappedView_ = MapViewOfFile(mappingHandle_, FILE_MAP_READ, 0, 0, SharedMemoryCapacityBytes);
    if (mappedView_ == nullptr)
    {
        Reset();
        return false;
    }

    updatedEventHandle_ = OpenEventW(SYNCHRONIZE, FALSE, UpdatedEventName);
    return true;
}

bool OverlaySharedStateReader::TryParseSnapshot(const char* json, const std::size_t jsonLength, OverlaySnapshot& snapshot) const
{
    const std::string_view document(json, jsonLength);
    std::string value;

    TryExtractBool(document, "isVisible", snapshot.isVisible);

    if (TryExtractJsonString(document, "currentPresetDisplayName", value))
    {
        snapshot.currentPresetDisplayName = Utf8ToWide(value);
    }

    if (TryExtractJsonString(document, "liveTrackIrStatus", value))
    {
        snapshot.liveTrackIrStatus = Utf8ToWide(value);
    }

    if (TryExtractJsonString(document, "udpStreamingStatus", value))
    {
        snapshot.udpStreamingStatus = Utf8ToWide(value);
    }

    if (TryExtractJsonString(document, "outputPoseSummary", value))
    {
        snapshot.outputPoseSummary = Utf8ToWide(value);
    }

    if (TryExtractJsonString(document, "runtimeStateSummary", value))
    {
        snapshot.runtimeStateSummary = Utf8ToWide(value);
    }

    if (TryExtractJsonString(document, "trackIrRateSummary", value))
    {
        snapshot.trackIrRateSummary = Utf8ToWide(value);
    }

    if (TryExtractJsonString(document, "udpRateSummary", value))
    {
        snapshot.udpRateSummary = Utf8ToWide(value);
    }

    if (TryExtractJsonString(document, "updatedAtUtc", value))
    {
        snapshot.updatedAtUtc = Utf8ToWide(value);
    }

    return true;
}
}