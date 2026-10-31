#pragma once
// 目标 Windows 10 及以上：启用 GetDpiForWindow / AdjustWindowRectExForDpi 等 API
#ifndef WINVER
#define WINVER 0x0A00
#endif
#ifndef _WIN32_WINNT
#define _WIN32_WINNT 0x0A00
#endif
#define WIN32_LEAN_AND_MEAN
#define NOMINMAX
#ifndef _CRT_SECURE_NO_WARNINGS
#define _CRT_SECURE_NO_WARNINGS
#endif
#include <windows.h>
#include <shellapi.h>
#include <commctrl.h>
#include <shlwapi.h>
#include <string>
#include <string_view>
#include <vector>
#include <memory>
#include <algorithm>
#include <functional>
#include <atomic>
#include <thread>
#include <mutex>
#include <chrono>
#include <fstream>
#include <sstream>
#include <iostream>
#include <filesystem>
#include <unordered_set>
#include <optional>
#include <cmath>
#include <ctime>
#ifndef _countof
#define _countof(arr) (sizeof(arr) / sizeof((arr)[0]))
#endif
#pragma comment(lib, "comctl32.lib")
#pragma comment(lib, "shlwapi.lib")
