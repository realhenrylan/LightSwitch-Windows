#include "pch.h"
#include "ConfigManager.h"
#include <cctype>
#include <algorithm>
static std::wstring Trim(const std::wstring& s) {
    size_t start = s.find_first_not_of(L" \t\r\n");
    if (start == std::wstring::npos) return L"";
    size_t end = s.find_last_not_of(L" \t\r\n");
    return s.substr(start, end - start + 1);
}
static std::wstring ReadFile(const std::wstring& path) {
    std::ifstream fs(path, std::ios::binary);
    if (!fs) return L"";
    std::string str((std::istreambuf_iterator<char>(fs)), std::istreambuf_iterator<char>());
    if (str.empty()) return L"";
    int wlen = MultiByteToWideChar(CP_UTF8, 0, str.c_str(), (int)str.size(), nullptr, 0);
    if (wlen <= 0) return L"";
    std::wstring result(wlen, 0);
    MultiByteToWideChar(CP_UTF8, 0, str.c_str(), (int)str.size(), &result[0], wlen);
    return result;
}
static void WriteFile(const std::wstring& path, const std::wstring& content) {
    std::ofstream fs(path, std::ios::binary);
    if (!fs) return;
    int len = WideCharToMultiByte(CP_UTF8, 0, content.c_str(), (int)content.size(), nullptr, 0, nullptr, nullptr);
    if (len <= 0) return;
    std::vector<char> utf8(len);
    WideCharToMultiByte(CP_UTF8, 0, content.c_str(), (int)content.size(), utf8.data(), len, nullptr, nullptr);
    fs.write(utf8.data(), len);
}
std::wstring ConfigManager::ExtractString(const std::wstring& json, const std::wstring& key, const std::wstring& def) {
    std::wstring search = L"\"" + key + L"\"";
    size_t pos = json.find(search);
    if (pos == std::wstring::npos) return def;
    pos = json.find(L':', pos + search.length());
    if (pos == std::wstring::npos) return def;
    pos++;
    while (pos < json.size() && (json[pos]==' '||json[pos]=='\t'||json[pos]=='\n'||json[pos]=='\r')) pos++;
    if (pos >= json.size() || json[pos] != L'"') {
        size_t end = json.find_first_of(L",}\n", pos);
        if (end == std::wstring::npos) end = json.size();
        return Trim(json.substr(pos, end - pos));
    }
    pos++;
    std::wstring v;
    while (pos < json.size() && json[pos] != L'"') { v.push_back(json[pos]); pos++; }
    return v;
}
int ConfigManager::ExtractInt(const std::wstring& json, const std::wstring& key, int def) {
    try { return std::stoi(ExtractString(json, key, std::to_wstring(def))); } catch(...) { return def; }
}
bool ConfigManager::ExtractBool(const std::wstring& json, const std::wstring& key, bool def) {
    std::wstring v = ExtractString(json, key, def ? L"true" : L"false");
    return (v == L"true" || v == L"True" || v == L"1");
}
ConfigManager& ConfigManager::Instance() { static ConfigManager inst; return inst; }
std::wstring ConfigManager::GetConfigPath() {
    wchar_t path[MAX_PATH]; GetModuleFileNameW(nullptr, path, MAX_PATH);
    std::wstring p(path); size_t ls = p.find_last_of(L"\\/");
    if (ls != std::wstring::npos) p = p.substr(0, ls + 1);
    p += L"config.json"; return p;
}
void ConfigManager::Load() {
    std::wstring content = ReadFile(GetConfigPath());
    if (content.empty()) { Save(); return; }
    m_config.scheduleMode = FromString(ExtractString(content, L"scheduleMode", L"FixedHours"));
    m_config.changeSystem = ExtractBool(content, L"changeSystem", true);
    m_config.changeApps = ExtractBool(content, L"changeApps", true);
    m_config.lightTime = ExtractInt(content, L"lightTime", 480);
    m_config.darkTime = ExtractInt(content, L"darkTime", 1200);
    m_config.latitude = ExtractString(content, L"latitude", L"39.9");
    m_config.longitude = ExtractString(content, L"longitude", L"116.4");
    m_config.sunrise_offset = ExtractInt(content, L"sunrise_offset", 0);
    m_config.sunset_offset = ExtractInt(content, L"sunset_offset", 0);
}
void ConfigManager::Save() const {
    std::wstring json = LR"({
  "scheduleMode": ")" + ToString(m_config.scheduleMode) + LR"(",
  "changeSystem": )" + std::wstring(m_config.changeSystem ? L"true" : L"false") + LR"(,
  "changeApps": )" + std::wstring(m_config.changeApps ? L"true" : L"false") + LR"(,
  "lightTime": )" + std::to_wstring(m_config.lightTime) + LR"(,
  "darkTime": )" + std::to_wstring(m_config.darkTime) + LR"(,
  "latitude": ")" + m_config.latitude + LR"(",
  "longitude": ")" + m_config.longitude + LR"(",
  "sunrise_offset": )" + std::to_wstring(m_config.sunrise_offset) + LR"(,
  "sunset_offset": )" + std::to_wstring(m_config.sunset_offset) + LR"(
}
)";
    WriteFile(GetConfigPath(), json);
}
