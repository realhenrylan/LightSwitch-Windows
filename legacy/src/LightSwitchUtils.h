#pragma once
#include "pch.h"
constexpr bool ShouldBeLight(int now, int light, int dark)
{
    int nLight = (light % 1440 + 1440) % 1440, nDark = (dark % 1440 + 1440) % 1440, nNow = (now % 1440 + 1440) % 1440;
    if (nLight < nDark) return nNow >= nLight && nNow < nDark;
    return nNow >= nLight || nNow < nDark;
}
inline int GetNowMinutes() { SYSTEMTIME st; GetLocalTime(&st); return st.wHour * 60 + st.wMinute; }
