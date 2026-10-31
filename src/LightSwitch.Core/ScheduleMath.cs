namespace LightSwitch.Core;

public static class ScheduleMath
{
    /// <summary>
    /// Whether <paramref name="nowMinutes"/> falls in the light window.
    /// Handles windows that cross midnight (light 20:00 -> dark 07:00).
    /// </summary>
    public static bool ShouldBeLight(int nowMinutes, int lightMinutes, int darkMinutes)
    {
        var light = NormalizeMinutes(lightMinutes);
        var dark = NormalizeMinutes(darkMinutes);
        var now = NormalizeMinutes(nowMinutes);

        return light < dark
            ? now >= light && now < dark
            : now >= light || now < dark;
    }

    public static int NormalizeMinutes(int minutes)
    {
        var normalized = minutes % 1440;
        return normalized < 0 ? normalized + 1440 : normalized;
    }

    public static int ToMinutes(int hour, int minute) => NormalizeMinutes(hour * 60 + minute);

    public static string FormatMinutes(int minutes) =>
        TimeSpan.FromMinutes(NormalizeMinutes(minutes)).ToString(@"hh\:mm");

    /// <summary>The next light or dark boundary strictly after <paramref name="now"/>.</summary>
    public static DateTime NextBoundary(DateTime now, int lightMinutes, int darkMinutes)
    {
        var nextLight = NextOccurrence(now, lightMinutes);
        var nextDark = NextOccurrence(now, darkMinutes);
        return nextLight <= nextDark ? nextLight : nextDark;
    }

    public static DateTime? NextBoundary(DateTime now, SunTimes? sunTimes, ScheduleMode mode, int fallbackLight, int fallbackDark)
    {
        var (light, dark) = mode == ScheduleMode.SunsetToSunrise && sunTimes is { } times
            ? (times.SunriseMinutes, times.SunsetMinutes)
            : (fallbackLight, fallbackDark);
        return NextBoundary(now, light, dark);
    }

    private static DateTime NextOccurrence(DateTime now, int minutes)
    {
        var candidate = now.Date.AddMinutes(NormalizeMinutes(minutes));
        return candidate <= now ? candidate.AddDays(1) : candidate;
    }
}
