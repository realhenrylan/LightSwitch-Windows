namespace LightSwitch.Core.Updates;

/// <summary>
/// Detects whether the app was installed by Inno Setup. The uninstaller is the
/// authoritative marker; merely living under %LOCALAPPDATA%\Programs is not
/// enough because users may extract the portable ZIP there.
/// </summary>
public static class InstallationModeDetector
{
    public static InstallationMode Detect(string? executablePath, Func<string, bool>? fileExists = null)
    {
        fileExists ??= File.Exists;

        var directory = string.IsNullOrWhiteSpace(executablePath)
            ? null
            : Path.GetDirectoryName(executablePath);

        if (string.IsNullOrEmpty(directory))
        {
            return InstallationMode.Portable;
        }

        return fileExists(Path.Combine(directory, "unins000.exe"))
            ? InstallationMode.Installed
            : InstallationMode.Portable;
    }
}
