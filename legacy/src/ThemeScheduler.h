#pragma once
#include "pch.h"
struct SunTimes { int sunriseHour, sunriseMinute, sunsetHour, sunsetMinute; };
constexpr double PI = 3.14159265358979323846;
constexpr double deg2rad(double deg) { return deg * PI / 180.0; }
constexpr double rad2deg(double rad) { return rad * 180.0 / PI; }
SunTimes CalculateSunriseSunset(double lat, double lon, int year, int month, int day);
