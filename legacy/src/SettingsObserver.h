#pragma once
#include "pch.h"
#include "SettingsConstants.h"
class SettingsObserver {
public:
    SettingsObserver(std::unordered_set<SettingId> observed) : m_observedSettings(std::move(observed)) {}
    virtual ~SettingsObserver() = default;
    virtual void SettingsUpdate(SettingId) {}
    virtual bool WantsToBeNotified(SettingId type) const noexcept { return m_observedSettings.find(type) != m_observedSettings.end(); }
protected:
    std::unordered_set<SettingId> m_observedSettings;
};
