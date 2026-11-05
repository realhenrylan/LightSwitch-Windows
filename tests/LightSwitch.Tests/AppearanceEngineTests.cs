using LightSwitch.Core;

namespace LightSwitch.Tests;

public class AppearanceEngineTests
{
    private static readonly TimeZoneInfo Beijing =
        TimeZoneInfo.CreateCustomTimeZone("Test/UTC+8", TimeSpan.FromHours(8), "UTC+8", "UTC+8");

    [Fact]
    public void FixedHours_AppliesThemeOnlyAtBoundaries()
    {
        var clock = new FakeClock(new DateTime(2026, 11, 1, 7, 0, 0));
        var theme = new FakeTheme();
        var (engine, wallpaper, _, _) = Create(clock, theme, new AppSettings
        {
            ScheduleMode = ScheduleMode.FixedHours,
            LightTime = 8 * 60,
            DarkTime = 20 * 60,
        });

        engine.Evaluate(EvaluationTrigger.Startup);
        Assert.False(engine.State.IsLightTarget);
        Assert.False(theme.SystemLight);
        Assert.False(theme.AppsLight);

        clock.Now = new DateTime(2026, 11, 1, 9, 0, 0);
        engine.Evaluate(EvaluationTrigger.Timer);
        Assert.True(engine.State.IsLightTarget);
        Assert.True(theme.SystemLight);
        Assert.True(theme.AppsLight);

        clock.Now = new DateTime(2026, 11, 1, 19, 59, 0);
        engine.Evaluate(EvaluationTrigger.Timer);
        Assert.True(theme.SystemLight);

        clock.Now = new DateTime(2026, 11, 1, 20, 0, 0);
        engine.Evaluate(EvaluationTrigger.Timer);
        Assert.False(engine.State.IsLightTarget);
        Assert.False(theme.SystemLight);
        Assert.False(theme.AppsLight);
        Assert.Equal(0, wallpaper.Applies);
    }

    [Fact]
    public void ManualApply_PausesAutomationUntilTheNextBoundary()
    {
        var clock = new FakeClock(new DateTime(2026, 11, 1, 21, 0, 0));
        var theme = new FakeTheme();
        var (engine, _, _, _) = Create(clock, theme, new AppSettings
        {
            ScheduleMode = ScheduleMode.FixedHours,
            LightTime = 8 * 60,
            DarkTime = 20 * 60,
        });

        engine.Evaluate(EvaluationTrigger.Startup);
        Assert.False(theme.SystemLight);

        engine.ManualApply(true);
        Assert.True(theme.SystemLight);
        Assert.True(engine.State.IsManualOverride);
        Assert.True(engine.State.ManualLight);
        Assert.Equal(new DateTime(2026, 11, 2, 8, 0, 0), engine.State.ManualUntil);

        clock.Now = new DateTime(2026, 11, 2, 7, 59, 0);
        engine.Evaluate(EvaluationTrigger.Timer);
        Assert.True(theme.SystemLight);
        Assert.True(engine.State.IsManualOverride);

        clock.Now = new DateTime(2026, 11, 2, 8, 0, 0);
        engine.Evaluate(EvaluationTrigger.Timer);
        Assert.True(theme.SystemLight);
        Assert.False(engine.State.IsManualOverride);
        Assert.Null(engine.State.ManualLight);
        Assert.Null(engine.State.ManualUntil);

        clock.Now = new DateTime(2026, 11, 2, 20, 0, 0);
        engine.Evaluate(EvaluationTrigger.Timer);
        Assert.False(theme.SystemLight);
    }

    [Fact]
    public void ManualApply_SurvivesReentrantEvaluateFromBroadcast()
    {
        var clock = new FakeClock(new DateTime(2026, 11, 1, 21, 0, 0));   // The schedule asks for dark.
        var theme = new FakeTheme();
        var (engine, _, _, _) = Create(clock, theme, new AppSettings
        {
            ScheduleMode = ScheduleMode.FixedHours,
            LightTime = 8 * 60,
            DarkTime = 20 * 60,
        });

        engine.Evaluate(EvaluationTrigger.Startup);
        Assert.False(theme.SystemLight);

        // The real BroadcastThemeChanged is synchronous: our own tray window
        // procedure receives WM_SETTINGCHANGE and asks the engine to evaluate
        // again while ManualApply is still running.
        theme.OnBroadcast = () => engine.Evaluate(EvaluationTrigger.SystemEvent);
        engine.ManualApply(true);

        Assert.True(theme.SystemLight);
        Assert.True(theme.AppsLight);
        Assert.True(engine.State.IsManualOverride);
        Assert.True(engine.State.ManualLight);

        // The override keeps holding after the nested request has been replayed.
        clock.Now = new DateTime(2026, 11, 1, 23, 0, 0);
        engine.Evaluate(EvaluationTrigger.Timer);
        Assert.True(theme.SystemLight);
        Assert.True(engine.State.IsManualOverride);
    }

    [Fact]
    public void ManualApply_KeepsTheChosenAppearanceWhenThemeTargetsAreDisabled()
    {
        var clock = new FakeClock(new DateTime(2026, 11, 1, 21, 0, 0));
        var theme = new FakeTheme { SystemLight = false, AppsLight = false };
        var (engine, _, _, _) = Create(clock, theme, new AppSettings
        {
            ScheduleMode = ScheduleMode.FixedHours,
            LightTime = 8 * 60,
            DarkTime = 20 * 60,
            ChangeSystem = false,
            ChangeApps = false,
            ChangeWallpaper = false,
        });

        engine.Evaluate(EvaluationTrigger.Startup);
        engine.ManualApply(true);
        Assert.True(engine.State.ManualLight);

        clock.Now = clock.Now.AddSeconds(30);
        engine.Evaluate(EvaluationTrigger.Timer);

        // The manual choice is remembered even though the system theme did not
        // change, so the UI can keep highlighting the button the user pressed.
        Assert.True(engine.State.IsManualOverride);
        Assert.True(engine.State.ManualLight);
        Assert.False(engine.State.IsLightTarget);
    }

    [Fact]
    public void FollowNightLight_TracksTheRegistryState()
    {
        var clock = new FakeClock(new DateTime(2026, 11, 1, 12, 0, 0));
        var theme = new FakeTheme();
        var nightLight = new FakeNightLight { IsActive = true };
        var engine = new AppearanceEngine(new AppSettings
        {
            ScheduleMode = ScheduleMode.FollowNightLight,
        }, theme, nightLight, new FakeWallpaper(), clock);

        engine.Evaluate(EvaluationTrigger.Startup);
        Assert.False(engine.State.IsLightTarget);
        Assert.True(engine.State.NightLightActive);
        Assert.False(theme.SystemLight);

        nightLight.IsActive = false;
        engine.Evaluate(EvaluationTrigger.NightLightChanged);
        Assert.True(engine.State.IsLightTarget);
        Assert.True(theme.SystemLight);
    }

    [Fact]
    public void ChangeWallpaper_AppliesTheMatchingImage()
    {
        var lightImage = Path.Combine(Path.GetTempPath(), $"LightSwitch-light-{Guid.NewGuid():N}.png");
        var darkImage = Path.Combine(Path.GetTempPath(), $"LightSwitch-dark-{Guid.NewGuid():N}.png");
        File.WriteAllBytes(lightImage, new byte[] { 0x89, 0x50, 0x4E, 0x47 });
        File.WriteAllBytes(darkImage, new byte[] { 0x89, 0x50, 0x4E, 0x47 });

        try
        {
            var clock = new FakeClock(new DateTime(2026, 11, 1, 9, 0, 0));
            var theme = new FakeTheme();
            var wallpaper = new FakeWallpaper();
            var settings = new AppSettings
            {
                ScheduleMode = ScheduleMode.FixedHours,
                LightTime = 8 * 60,
                DarkTime = 20 * 60,
                ChangeWallpaper = true,
                LightWallpaper = lightImage,
                DarkWallpaper = darkImage,
                WallpaperPosition = WallpaperPosition.Fit,
            };
            var engine = new AppearanceEngine(settings, theme, new FakeNightLight(), wallpaper, clock);

            engine.Evaluate(EvaluationTrigger.Startup);
            Assert.Equal(lightImage, wallpaper.Path);
            Assert.Equal(WallpaperPosition.Fit, wallpaper.Position);
            Assert.Equal(1, wallpaper.Applies);

            clock.Now = new DateTime(2026, 11, 1, 21, 0, 0);
            engine.Evaluate(EvaluationTrigger.Timer);
            Assert.Equal(darkImage, wallpaper.Path);
            Assert.Equal(2, wallpaper.Applies);
        }
        finally
        {
            File.Delete(lightImage);
            File.Delete(darkImage);
        }
    }

    [Fact]
    public void OffMode_NeverTouchesTheThemeButSyncsWallpaper()
    {
        var darkImage = Path.Combine(Path.GetTempPath(), $"LightSwitch-dark-{Guid.NewGuid():N}.png");
        File.WriteAllBytes(darkImage, new byte[] { 0x89, 0x50, 0x4E, 0x47 });
        try
        {
            var clock = new FakeClock(new DateTime(2026, 11, 1, 9, 0, 0));
            var theme = new FakeTheme { SystemLight = false, AppsLight = false };
            var wallpaper = new FakeWallpaper();
            var engine = new AppearanceEngine(new AppSettings
            {
                ScheduleMode = ScheduleMode.Off,
                ChangeWallpaper = true,
                LightWallpaper = darkImage,
                DarkWallpaper = darkImage,
            }, theme, new FakeNightLight(), wallpaper, clock);

            engine.Evaluate(EvaluationTrigger.Startup);
            Assert.Equal(0, theme.SetSystemCalls);
            Assert.Equal(0, theme.SetAppsCalls);
            Assert.Equal(darkImage, wallpaper.Path);
        }
        finally
        {
            File.Delete(darkImage);
        }
    }

    [Fact]
    public void SunsetToSunrise_ComputesSunTimesAndShiftsByOffsets()
    {
        var clock = new FakeClock(new DateTime(2026, 11, 1, 12, 0, 0));
        var theme = new FakeTheme();
        var (engine, _, _, _) = Create(clock, theme, new AppSettings
        {
            ScheduleMode = ScheduleMode.SunsetToSunrise,
            Latitude = "39.9",
            Longitude = "116.4",
            SunriseOffset = 30,
            SunsetOffset = -30,
        }, Beijing);

        engine.Evaluate(EvaluationTrigger.Startup);
        Assert.NotNull(engine.State.SunTimes);
        var times = engine.State.SunTimes!.Value;
        Assert.InRange(times.SunriseMinutes, 6 * 60, 8 * 60);
        Assert.InRange(times.SunsetMinutes, 16 * 60, 18 * 60);
    }

    private static (AppearanceEngine Engine, FakeWallpaper Wallpaper, FakeTheme Theme, FakeNightLight NightLight)
        Create(FakeClock clock, FakeTheme theme, AppSettings settings, TimeZoneInfo? timeZone = null)
    {
        var wallpaper = new FakeWallpaper();
        var nightLight = new FakeNightLight();
        var engine = new AppearanceEngine(settings, theme, nightLight, wallpaper, clock, timeZone);
        return (engine, wallpaper, theme, nightLight);
    }

    private sealed class FakeClock : IClock
    {
        public FakeClock(DateTime now) => Now = now;

        public DateTime Now { get; set; }
    }

    private sealed class FakeTheme : IThemeService
    {
        public bool? SystemLight { get; set; }
        public bool? AppsLight { get; set; }
        public int SetSystemCalls { get; private set; }
        public int SetAppsCalls { get; private set; }
        public int Broadcasts { get; private set; }

        /// <summary>Simulates the synchronous WM_SETTINGCHANGE loop back into the tray.</summary>
        public Action? OnBroadcast { get; set; }

        public bool? GetSystemLight() => SystemLight;

        public bool? GetAppsLight() => AppsLight;

        public bool SetSystemLight(bool light)
        {
            SystemLight = light;
            SetSystemCalls++;
            return true;
        }

        public bool SetAppsLight(bool light)
        {
            AppsLight = light;
            SetAppsCalls++;
            return true;
        }

        public void BroadcastThemeChanged()
        {
            Broadcasts++;
            OnBroadcast?.Invoke();
        }
    }

    private sealed class FakeNightLight : INightLightService
    {
        public bool IsActive { get; set; }
    }

    private sealed class FakeWallpaper : IWallpaperService
    {
        public string? Path { get; private set; }
        public WallpaperPosition Position { get; private set; }
        public int Applies { get; private set; }

        public WallpaperSnapshot GetSnapshot() =>
            new(Array.Empty<string>(), Array.Empty<string>(), null, null);

        public WallpaperApplyResult Apply(string path, WallpaperPosition position)
        {
            Path = path;
            Position = position;
            Applies++;
            return new WallpaperApplyResult(true, 1, false, null);
        }

        public bool Matches(WallpaperSnapshot snapshot, string path, WallpaperPosition position) => false;
    }
}
