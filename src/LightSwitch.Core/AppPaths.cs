namespace LightSwitch.Core;

/// <summary>
/// Well-known locations used by LightSwitch.
/// </summary>
public static class AppPaths
{
    public const string AppName = "LightSwitch";

    /// <summary>%LOCALAPPDATA%\LightSwitch</summary>
    public static string DataDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), AppName);

    /// <summary>%LOCALAPPDATA%\LightSwitch\config.json</summary>
    public static string ConfigPath => Path.Combine(DataDirectory, "config.json");

    /// <summary>%LOCALAPPDATA%\LightSwitch\LightSwitch.log</summary>
    public static string LogPath => Path.Combine(DataDirectory, "LightSwitch.log");

    /// <summary>%LOCALAPPDATA%\LightSwitch\update-state.json (last check time, postponed version).</summary>
    public static string UpdateStatePath => Path.Combine(DataDirectory, "update-state.json");

    /// <summary>%LOCALAPPDATA%\LightSwitch\updates (verified update packages).</summary>
    public static string UpdatesDirectory => Path.Combine(DataDirectory, "updates");

    /// <summary>
    /// The old standalone build stored config.json next to the executable.
    /// It is imported once when no per-user configuration exists yet.
    /// </summary>
    public static string LegacyConfigPath => Path.Combine(AppContext.BaseDirectory, "config.json");

    /// <summary>Full path of the running executable (not the dotnet host).</summary>
    public static string ExecutablePath
    {
        get
        {
            var path = Environment.ProcessPath;
            if (!string.IsNullOrWhiteSpace(path) &&
                !string.Equals(Path.GetFileNameWithoutExtension(path), "dotnet", StringComparison.OrdinalIgnoreCase))
            {
                return path;
            }

            return Path.Combine(AppContext.BaseDirectory, $"{AppName}.exe");
        }
    }

    public static void EnsureDataDirectory() => Directory.CreateDirectory(DataDirectory);
}
