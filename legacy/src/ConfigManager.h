#pragma once
#include "pch.h"
enum class ScheduleMode { Off, FixedHours, SunsetToSunrise, FollowNightLight };
inline std::wstring ToString(ScheduleMode m) {
    switch(m) { case ScheduleMode::FixedHours: return L"FixedHours"; case ScheduleMode::SunsetToSunrise: return L"SunsetToSunrise"; case ScheduleMode::FollowNightLight: return L"FollowNightLight"; default: return L"Off"; }
}
inline ScheduleMode FromString(const std::wstring& s) {
    if (s == L"SunsetToSunrise") return ScheduleMode::SunsetToSunrise; if (s == L"FixedHours") return ScheduleMode::FixedHours; if (s == L"FollowNightLight") return ScheduleMode::FollowNightLight; return ScheduleMode::Off;
}
struct LightSwitchConfig {
    ScheduleMode scheduleMode = ScheduleMode::FixedHours;
    bool changeSystem = true, changeApps = true;
    int lightTime = 480, darkTime = 1200;
    std::wstring latitude = L"39.9", longitude = L"116.4";
    int sunrise_offset = 0, sunset_offset = 0;
};
class ConfigManager {
public:
    static ConfigManager& Instance();
    void Load(); void Save() const;
    const LightSwitchConfig& Get() const { return m_config; }
    LightSwitchConfig& Get() { return m_config; }
    static std::wstring GetConfigPath();
private:
    ConfigManager() = default;
    LightSwitchConfig m_config;
    static std::wstring ExtractString(const std::wstring& json, const std::wstring& key, const std::wstring& defaultVal);
    static int ExtractInt(const std::wstring& json, const std::wstring& key, int defaultVal);
    static bool ExtractBool(const std::wstring& json, const std::wstring& key, bool defaultVal);
};
