using LightSwitch.Core;

namespace LightSwitch.Tests;

public class SunCalculatorTests
{
    private static readonly TimeZoneInfo Beijing =
        TimeZoneInfo.CreateCustomTimeZone("Test/UTC+8", TimeSpan.FromHours(8), "UTC+8", "UTC+8");

    [Fact]
    public void Beijing_November_ProducesPlausibleSunriseAndSunset()
    {
        var result = SunCalculator.Calculate(new DateTime(2026, 11, 1), 39.9, 116.4, Beijing);

        Assert.NotNull(result);
        // Sunrise around 06:40, sunset around 17:10 in Beijing on Nov 1.
        Assert.InRange(result!.Value.SunriseMinutes, 5 * 60, 8 * 60);
        Assert.InRange(result.Value.SunsetMinutes, 16 * 60, 18 * 60 + 30);
        Assert.True(result.Value.SunriseMinutes < result.Value.SunsetMinutes);
    }

    [Fact]
    public void Equator_Equinox_IsCloseToSixAndEighteen()
    {
        var result = SunCalculator.Calculate(new DateTime(2026, 3, 20), 0.0, 0.0, TimeZoneInfo.Utc);

        Assert.NotNull(result);
        Assert.InRange(result!.Value.SunriseMinutes, 5 * 60 + 45, 6 * 60 + 15);
        Assert.InRange(result.Value.SunsetMinutes, 17 * 60 + 45, 18 * 60 + 15);
    }

    [Fact]
    public void PolarDay_ReturnsNull()
    {
        // Midsummer in northern Norway: the sun never sets.
        var result = SunCalculator.Calculate(new DateTime(2026, 6, 21), 78.0, 15.0, TimeZoneInfo.Utc);
        Assert.Null(result);
    }

    [Theory]
    [InlineData(91.0, 0.0)]
    [InlineData(0.0, 181.0)]
    [InlineData(double.NaN, 0.0)]
    public void InvalidCoordinates_ReturnNull(double latitude, double longitude)
    {
        Assert.Null(SunCalculator.Calculate(new DateTime(2026, 11, 1), latitude, longitude, Beijing));
    }
}
