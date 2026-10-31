namespace LightSwitch.Core;

public interface IClock
{
    DateTime Now { get; }
}

public sealed class SystemClock : IClock
{
    public DateTime Now => DateTime.Now;
}

public interface IThemeService
{
    bool? GetSystemLight();

    bool? GetAppsLight();

    bool SetSystemLight(bool light);

    bool SetAppsLight(bool light);

    void BroadcastThemeChanged();
}

public interface INightLightService
{
    bool IsActive { get; }
}

public interface IWallpaperService
{
    WallpaperSnapshot GetSnapshot();

    WallpaperApplyResult Apply(string path, WallpaperPosition position);

    bool Matches(WallpaperSnapshot snapshot, string path, WallpaperPosition position);
}

public interface IStartupService
{
    bool IsEnabled { get; }

    string? RegisteredCommand { get; }

    bool TrySetEnabled(bool enabled, out string? error);
}
