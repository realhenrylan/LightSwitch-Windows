#include "pch.h"
#include "ThemeScheduler.h"
#include <utility>
SunTimes CalculateSunriseSunset(double lat, double lon, int year, int month, int day)
{
    double zenith = 90.833;
    int N1 = (int)floor(275.0 * month / 9.0);
    int N2 = (int)floor((month + 9) / 12.0);
    int N3 = (int)floor((1.0 + floor((year - 4.0 * floor(year / 4.0) + 2.0) / 3.0)));
    int N = N1 - (N2 * N3) + day - 30;
    auto calc = [&](bool sunrise) -> double {
        double lngHour = lon / 15.0;
        double t = sunrise ? N + ((6 - lngHour) / 24) : N + ((18 - lngHour) / 24);
        double M = (0.9856 * t) - 3.289;
        double L = M + (1.916 * sin(deg2rad(M))) + (0.020 * sin(2 * deg2rad(M))) + 282.634;
        while (L < 0) L += 360; while (L > 360) L -= 360;
        double RA = rad2deg(atan(0.91764 * tan(deg2rad(L))));
        while (RA < 0) RA += 360; while (RA > 360) RA -= 360;
        double Lq = floor(L / 90) * 90, RAq = floor(RA / 90) * 90;
        RA = RA + (Lq - RAq); RA /= 15;
        double sinDec = 0.39782 * sin(deg2rad(L));
        double cosDec = cos(asin(sinDec));
        double cosH = (cos(deg2rad(zenith)) - (sinDec * sin(deg2rad(lat)))) / (cosDec * cos(deg2rad(lat)));
        if (cosH > 1 || cosH < -1) return -1;
        double H = sunrise ? 360 - rad2deg(acos(cosH)) : rad2deg(acos(cosH)); H /= 15;
        double T = H + RA - (0.06571 * t) - 6.622;
        double UT = T - lngHour;
        while (UT < 0) UT += 24; while (UT >= 24) UT -= 24;
        return UT;
    };
    double rise = calc(true), set = calc(false);
    auto toLocal = [](double UT) -> std::pair<int,int> {
        TIME_ZONE_INFORMATION tz; DWORD s = GetTimeZoneInformation(&tz);
        double bias = tz.Bias;
        if (s == TIME_ZONE_ID_DAYLIGHT) bias += tz.DaylightBias;
        else if (s == TIME_ZONE_ID_STANDARD) bias += tz.StandardBias;
        double lt = UT - (bias / 60.0);
        while (lt < 0) lt += 24; while (lt >= 24) lt -= 24;
        return { (int)lt, (int)((lt - (int)lt) * 60) };
    };
    auto [rh, rm] = toLocal(rise); auto [sh, sm] = toLocal(set);
    return { rh, rm, sh, sm };
}
