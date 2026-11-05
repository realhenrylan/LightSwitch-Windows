using LightSwitch.Core;

namespace LightSwitch.Tests;

public class SettingsServiceTests
{
    [Fact]
    public void Load_ParsesLegacyConfigWithSnakeCaseOffsets()
    {
        var path = Path.Combine(Path.GetTempPath(), $"LightSwitch-test-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, """
        {
          "scheduleMode": "FixedHours",
          "changeSystem": true,
          "changeApps": false,
          "lightTime": 420,
          "darkTime": 1140,
          "latitude": "39.9",
          "longitude": "116.4",
          "sunrise_offset": -5,
          "sunset_offset": 10,
          "unknownFutureField": 42
        }
        """);

        try
        {
            var service = new SettingsService(path);
            var settings = service.Load();

            Assert.Equal(ScheduleMode.FixedHours, settings.ScheduleMode);
            Assert.True(settings.ChangeSystem);
            Assert.False(settings.ChangeApps);
            Assert.Equal(420, settings.LightTime);
            Assert.Equal(1140, settings.DarkTime);
            Assert.Equal(-5, settings.SunriseOffset);
            Assert.Equal(10, settings.SunsetOffset);
            Assert.False(settings.ChangeWallpaper);
            Assert.Equal(WallpaperPosition.Fill, settings.WallpaperPosition);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void SaveAndLoad_RoundTripsNewWallpaperFields()
    {
        var path = Path.Combine(Path.GetTempPath(), $"LightSwitch-test-{Guid.NewGuid():N}.json");
        try
        {
            var service = new SettingsService(path);
            var settings = new AppSettings
            {
                ScheduleMode = ScheduleMode.SunsetToSunrise,
                ChangeWallpaper = true,
                LightWallpaper = @"C:\Pictures\light image.png",
                DarkWallpaper = @"C:\Pictures\dark image.png",
                WallpaperPosition = WallpaperPosition.Span,
                Latitude = "31.23",
                Longitude = "121.47",
            };

            Assert.Equal(SaveResult.Saved, service.Save(settings));

            var reloaded = new SettingsService(path).Load();
            Assert.True(reloaded.ChangeWallpaper);
            Assert.Equal(@"C:\Pictures\light image.png", reloaded.LightWallpaper);
            Assert.Equal(@"C:\Pictures\dark image.png", reloaded.DarkWallpaper);
            Assert.Equal(WallpaperPosition.Span, reloaded.WallpaperPosition);
            Assert.Equal("31.23", reloaded.Latitude);
            Assert.Equal("121.47", reloaded.Longitude);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Save_WhenContentIsUnchanged_ReturnsUnchanged()
    {
        var path = Path.Combine(Path.GetTempPath(), $"LightSwitch-test-{Guid.NewGuid():N}.json");
        try
        {
            var service = new SettingsService(path);
            var settings = new AppSettings
            {
                ScheduleMode = ScheduleMode.FixedHours,
                LightTime = 420,
            };

            Assert.Equal(SaveResult.Saved, service.Save(settings));
            Assert.Equal(SaveResult.Unchanged, service.Save(settings.Clone()));
            Assert.Null(service.LastError);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Save_WhenTheTargetCannotBeReplaced_ReturnsFailed()
    {
        // Point the config path at a directory: the temp file can be written but
        // can never replace the target, which is exactly the "write failed" path.
        var path = Path.Combine(Path.GetTempPath(), $"LightSwitch-dir-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        try
        {
            var service = new SettingsService(path);
            var result = service.Save(new AppSettings
            {
                ScheduleMode = ScheduleMode.FixedHours,
            });

            Assert.Equal(SaveResult.Failed, result);
            Assert.NotNull(service.LastError);
        }
        finally
        {
            File.Delete(path + ".tmp");
            Directory.Delete(path, recursive: false);
        }
    }

    [Fact]
    public void Normalize_ClampsInvalidValues()
    {
        var settings = new AppSettings
        {
            LightTime = -10,
            DarkTime = 5000,
            SunriseOffset = -2000,
            SunsetOffset = 2000,
            Latitude = " 39.9 ",
            ScheduleMode = (ScheduleMode)99,
            WallpaperPosition = (WallpaperPosition)99,
        };

        settings.Normalize();

        Assert.Equal(0, settings.LightTime);
        Assert.Equal(1439, settings.DarkTime);
        Assert.Equal(-720, settings.SunriseOffset);
        Assert.Equal(720, settings.SunsetOffset);
        Assert.Equal("39.9", settings.Latitude);
        Assert.Equal(ScheduleMode.Off, settings.ScheduleMode);
        Assert.Equal(WallpaperPosition.Fill, settings.WallpaperPosition);
    }
}
