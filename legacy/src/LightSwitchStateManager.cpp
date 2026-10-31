#include "pch.h"
#include "LightSwitchStateManager.h"
#include "ThemeHelper.h"
#include "ThemeScheduler.h"
#include "LightSwitchUtils.h"
#include "ConfigManager.h"
static void ApplyTheme(bool light) {
    const auto& c = ConfigManager::Instance().Get();
    if (c.changeSystem) SetSystemTheme(light);
    if (c.changeApps) SetAppsTheme(light);
    OutputDebugStringW(light ? L"[LSM] Applied LIGHT\n" : L"[LSM] Applied DARK\n");
}
LightSwitchStateManager::LightSwitchStateManager() { OutputDebugStringW(L"[LSM] Init\n"); }
void LightSwitchStateManager::OnSettingsChanged() {
    std::lock_guard<std::mutex> l(_stateMutex);
    if (_state.isManualOverride) _state.isManualOverride = false;
    // 强制日出日落时间重新计算：设置变更（如经纬度修改）后，
    // 缓存的 sun times 已过时，必须使 lastEvaluatedDay 失效以触发 UpdateSunTimes()
    _state.lastEvaluatedDay = -1;
    EvaluateAndApplyIfNeeded();
}
void LightSwitchStateManager::OnTick() {
    std::lock_guard<std::mutex> l(_stateMutex);
    if (_state.lastAppliedMode != ScheduleMode::FollowNightLight) EvaluateAndApplyIfNeeded();
}
void LightSwitchStateManager::OnManualOverride() {
    std::lock_guard<std::mutex> l(_stateMutex);
    _state.isManualOverride = !_state.isManualOverride;
    _state.isSystemLightActive = GetCurrentSystemTheme();
    _state.isAppsLightActive = GetCurrentAppsTheme();
    EvaluateAndApplyIfNeeded();
}
void LightSwitchStateManager::OnNightLightChange() {
    std::lock_guard<std::mutex> l(_stateMutex);
    bool n = IsNightLightEnabled();
    if (_state.lastAppliedMode == ScheduleMode::FollowNightLight && _state.isManualOverride) _state.isManualOverride = false;
    if (n != _state.isNightLightActive) _state.isNightLightActive = n;
    EvaluateAndApplyIfNeeded();
}
void LightSwitchStateManager::SyncInitialThemeState() {
    std::lock_guard<std::mutex> l(_stateMutex);
    _state.isSystemLightActive = GetCurrentSystemTheme();
    _state.isAppsLightActive = GetCurrentAppsTheme();
    _state.isNightLightActive = IsNightLightEnabled();
    EvaluateAndApplyIfNeeded();
}
bool LightSwitchStateManager::CoordinatesAreValid(const std::wstring& lat, const std::wstring& lon) {
    try { double la = std::stod(lat), lo = std::stod(lon); return !(la==0&&lo==0) && la>=-90&&la<=90 && lo>=-180&&lo<=180; }
    catch(...) { return false; }
}
std::pair<int,int> LightSwitchStateManager::UpdateSunTimes() {
    const auto& c = ConfigManager::Instance().Get();
    double lat = std::stod(c.latitude), lon = std::stod(c.longitude);
    SYSTEMTIME st; GetLocalTime(&st);
    auto sun = CalculateSunriseSunset(lat, lon, st.wYear, st.wMonth, st.wDay);
    int lt = sun.sunriseHour*60 + sun.sunriseMinute, dt = sun.sunsetHour*60 + sun.sunsetMinute;
    ConfigManager::Instance().Get().lightTime = lt;
    ConfigManager::Instance().Get().darkTime = dt;
    ConfigManager::Instance().Save();
    return {lt, dt};
}
void LightSwitchStateManager::EvaluateAndApplyIfNeeded() {
    const auto& cfg = ConfigManager::Instance().Get();
    int now = GetNowMinutes();
    if (cfg.scheduleMode == ScheduleMode::Off) { _state.lastTickMinutes = now; _state.lastAppliedMode = ScheduleMode::Off; return; }
    bool coordsValid = CoordinatesAreValid(cfg.latitude, cfg.longitude);
    if (cfg.scheduleMode == ScheduleMode::SunsetToSunrise && coordsValid) {
        SYSTEMTIME st; GetLocalTime(&st);
        if (_state.lastEvaluatedDay != st.wDay || _state.lastAppliedMode != ScheduleMode::SunsetToSunrise) {
            auto [lt, dt] = UpdateSunTimes(); _state.lastEvaluatedDay = st.wDay;
            _state.effectiveLightMinutes = lt + cfg.sunrise_offset; _state.effectiveDarkMinutes = dt + cfg.sunset_offset;
        } else {
            _state.effectiveLightMinutes = cfg.lightTime + cfg.sunrise_offset; _state.effectiveDarkMinutes = cfg.darkTime + cfg.sunset_offset;
        }
    } else if (cfg.scheduleMode == ScheduleMode::FixedHours) {
        _state.effectiveLightMinutes = cfg.lightTime; _state.effectiveDarkMinutes = cfg.darkTime;
    }
    if (_state.isManualOverride) {
        bool crossed = false;
        if (_state.lastTickMinutes != -1) {
            int prev = _state.lastTickMinutes;
            if (now < prev) {
                crossed = (prev <= _state.effectiveLightMinutes || now >= _state.effectiveLightMinutes) ||
                          (prev <= _state.effectiveDarkMinutes || now >= _state.effectiveDarkMinutes);
            } else {
                crossed = (prev < _state.effectiveLightMinutes && now >= _state.effectiveLightMinutes) ||
                          (prev < _state.effectiveDarkMinutes && now >= _state.effectiveDarkMinutes);
            }
        }
        if (crossed) _state.isManualOverride = false;
        else { _state.lastTickMinutes = now; return; }
    }
    _state.lastAppliedMode = cfg.scheduleMode;
    bool shouldBeLight = (cfg.scheduleMode == ScheduleMode::FollowNightLight) ? !_state.isNightLightActive
                        : ShouldBeLight(now, _state.effectiveLightMinutes, _state.effectiveDarkMinutes);
    bool appsChange = cfg.changeApps && _state.isAppsLightActive != shouldBeLight;
    bool sysChange = cfg.changeSystem && _state.isSystemLightActive != shouldBeLight;
    if (!_state.isManualOverride && (appsChange || sysChange)) {
        ApplyTheme(shouldBeLight);
        _state.isSystemLightActive = GetCurrentSystemTheme();
        _state.isAppsLightActive = GetCurrentAppsTheme();
    }
    _state.lastTickMinutes = now;
}
