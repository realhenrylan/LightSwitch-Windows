using LightSwitch.Core;

namespace LightSwitch.Tests;

public class ScheduleMathTests
{
    [Theory]
    [InlineData(480, 480, 1200, true)]   // exactly at light start counts as light
    [InlineData(1199, 480, 1200, true)]
    [InlineData(1200, 480, 1200, false)] // exactly at dark start counts as dark
    [InlineData(60, 480, 1200, false)]
    public void ShouldBeLight_WithinSameDay(int now, int light, int dark, bool expected)
    {
        Assert.Equal(expected, ScheduleMath.ShouldBeLight(now, light, dark));
    }

    [Theory]
    [InlineData(20 * 60, true)]
    [InlineData(23 * 60 + 59, true)]
    [InlineData(0, true)]
    [InlineData(6 * 60 + 59, true)]
    [InlineData(7 * 60, false)]
    [InlineData(12 * 60, false)]
    [InlineData(18 * 60, false)]
    public void ShouldBeLight_CrossesMidnight(int now, bool expected)
    {
        // light 20:00 -> dark 07:00
        Assert.Equal(expected, ScheduleMath.ShouldBeLight(now, 1200, 420));
    }

    [Fact]
    public void NextBoundary_PicksTheEarlierOfLightAndDark()
    {
        var now = new DateTime(2026, 11, 1, 10, 0, 0);
        var next = ScheduleMath.NextBoundary(now, 480, 1200);
        Assert.Equal(new DateTime(2026, 11, 1, 20, 0, 0), next);
    }

    [Fact]
    public void NextBoundary_RollsOverToTomorrow()
    {
        var now = new DateTime(2026, 11, 1, 21, 0, 0);
        var next = ScheduleMath.NextBoundary(now, 480, 1200);
        Assert.Equal(new DateTime(2026, 11, 2, 8, 0, 0), next);
    }

    [Fact]
    public void NextBoundary_AtExactBoundaryMovesToTheNextDay()
    {
        var now = new DateTime(2026, 11, 1, 8, 0, 0);
        var next = ScheduleMath.NextBoundary(now, 480, 1200);
        Assert.Equal(new DateTime(2026, 11, 1, 20, 0, 0), next);

        var atDark = new DateTime(2026, 11, 1, 20, 0, 0);
        var afterDark = ScheduleMath.NextBoundary(atDark, 480, 1200);
        Assert.Equal(new DateTime(2026, 11, 2, 8, 0, 0), afterDark);
    }

    [Fact]
    public void NormalizeMinutes_HandlesNegativeAndOverflow()
    {
        Assert.Equal(1439, ScheduleMath.NormalizeMinutes(-1));
        Assert.Equal(0, ScheduleMath.NormalizeMinutes(1440));
        Assert.Equal(30, ScheduleMath.NormalizeMinutes(1470));
    }
}
