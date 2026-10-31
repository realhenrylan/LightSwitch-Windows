namespace LightSwitch.Core;

/// <summary>
/// Sunrise/sunset using the same NOAA-style solar position algorithm as the
/// original C++ build (zenith 90.833, day-of-year N1/N2/N3 form).
/// </summary>
public static class SunCalculator
{
    private const double Zenith = 90.833;
    private const double DegreesToRadians = Math.PI / 180.0;
    private const double RadiansToDegrees = 180.0 / Math.PI;

    /// <summary>
    /// Calculates local sunrise/sunset for <paramref name="localDate"/>, or null
    /// when the sun does not cross the horizon (polar day/night).
    /// </summary>
    public static SunTimes? Calculate(DateTime localDate, double latitude, double longitude, TimeZoneInfo? timeZone = null)
    {
        if (double.IsNaN(latitude) || double.IsNaN(longitude) ||
            latitude is < -90 or > 90 || longitude is < -180 or > 180)
        {
            return null;
        }

        var date = localDate.Date;
        var n1 = Math.Floor(275.0 * date.Month / 9.0);
        var n2 = Math.Floor((date.Month + 9) / 12.0);
        var n3 = Math.Floor(1.0 + Math.Floor((date.Year - 4.0 * Math.Floor(date.Year / 4.0) + 2.0) / 3.0));
        var n = n1 - n2 * n3 + date.Day - 30.0;

        var sunriseUtc = EventUtcHour(n, latitude, longitude, sunrise: true);
        var sunsetUtc = EventUtcHour(n, latitude, longitude, sunrise: false);
        if (sunriseUtc is null || sunsetUtc is null)
        {
            return null;
        }

        timeZone ??= TimeZoneInfo.Local;
        // The event is computed for "today"; the offset in effect around local noon is the right one.
        var offsetHours = timeZone.GetUtcOffset(date.AddHours(12)).TotalHours;
        var sunrise = ToLocalMinutes(sunriseUtc.Value + offsetHours);
        var sunset = ToLocalMinutes(sunsetUtc.Value + offsetHours);
        return new SunTimes(sunrise, sunset);
    }

    private static double? EventUtcHour(double n, double latitude, double longitude, bool sunrise)
    {
        var lngHour = longitude / 15.0;
        var t = sunrise ? n + (6.0 - lngHour) / 24.0 : n + (18.0 - lngHour) / 24.0;

        var m = 0.9856 * t - 3.289;
        var l = m + 1.916 * Math.Sin(m * DegreesToRadians) +
                0.020 * Math.Sin(2 * m * DegreesToRadians) + 282.634;
        l = NormalizeDegrees(l);

        var ra = RadiansToDegrees * Math.Atan(0.91764 * Math.Tan(l * DegreesToRadians));
        ra = NormalizeDegrees(ra);
        var lQuadrant = Math.Floor(l / 90.0) * 90.0;
        var raQuadrant = Math.Floor(ra / 90.0) * 90.0;
        ra = (ra + (lQuadrant - raQuadrant)) / 15.0;

        var sinDec = 0.39782 * Math.Sin(l * DegreesToRadians);
        var cosDec = Math.Cos(Math.Asin(sinDec));
        var cosLat = Math.Cos(latitude * DegreesToRadians);
        if (Math.Abs(cosLat) < 1e-12)
        {
            return null;
        }

        var cosH = (Math.Cos(Zenith * DegreesToRadians) - sinDec * Math.Sin(latitude * DegreesToRadians)) /
                   (cosDec * cosLat);
        if (cosH > 1 || cosH < -1)
        {
            // The sun never rises or never sets on this date.
            return null;
        }

        var h = sunrise ? 360.0 - RadiansToDegrees * Math.Acos(cosH) : RadiansToDegrees * Math.Acos(cosH);
        h /= 15.0;

        var localT = h + ra - 0.06571 * t - 6.622;
        var ut = localT - lngHour;
        return NormalizeHours(ut);
    }

    private static double NormalizeDegrees(double value)
    {
        value %= 360.0;
        return value < 0 ? value + 360.0 : value;
    }

    private static double NormalizeHours(double value)
    {
        value %= 24.0;
        return value < 0 ? value + 24.0 : value;
    }

    private static int ToLocalMinutes(double hours)
    {
        var normalized = NormalizeHours(hours);
        var total = (int)Math.Floor(normalized * 60.0 + 1e-9);
        return Math.Clamp(total, 0, 1439);
    }
}
