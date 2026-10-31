#pragma once
#include "pch.h"
#include "ConfigManager.h"
#include "SettingsConstants.h"
#include "SettingsObserver.h"
struct LightSwitchState {
    ScheduleMode lastAppliedMode = ScheduleMode::Off;
    bool isManualOverride = false, isSystemLightActive = false, isAppsLightActive = false, isNightLightActive = false;
    int lastEvaluatedDay = -1, lastTickMinutes = -1;
    int effectiveLightMinutes = 0, effectiveDarkMinutes = 0;
};
class LightSwitchStateManager {
public:
    LightSwitchStateManager();
    void OnSettingsChanged();
    void OnTick();
    void OnManualOverride();
    void OnNightLightChange();
    void SyncInitialThemeState();
    const LightSwitchState& GetState() const { return _state; }
private:
    LightSwitchState _state;
    std::mutex _stateMutex;
    void EvaluateAndApplyIfNeeded();
    bool CoordinatesAreValid(const std::wstring& lat, const std::wstring& lon);
    std::pair<int, int> UpdateSunTimes();
};
