using Microsoft.Win32;

namespace LightSwitch.Core;

/// <summary>
/// Manages the HKCU Run entry. The application always enables/disables it from
/// the UI, so there is no need for a separate installer step.
/// </summary>
public sealed class StartupService : IStartupService
{
    public const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    public const string ValueName = "LightSwitch";
    public const string StartupArgument = "--startup";

    public bool IsEnabled => !string.IsNullOrEmpty(RegisteredCommand);

    public string? RegisteredCommand
    {
        get
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath);
                return key?.GetValue(ValueName) as string;
            }
            catch
            {
                return null;
            }
        }
    }

    public string ExpectedCommand => $"\"{AppPaths.ExecutablePath}\" {StartupArgument}";

    public bool TrySetEnabled(bool enabled, out string? error)
    {
        error = null;
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);
            if (key is null)
            {
                error = "无法打开当前用户的启动项注册表键。";
                return false;
            }

            if (enabled)
            {
                var exe = AppPaths.ExecutablePath;
                if (!File.Exists(exe))
                {
                    error = "找不到应用程序路径，无法设置开机启动。";
                    return false;
                }

                key.SetValue(ValueName, $"\"{exe}\" {StartupArgument}", RegistryValueKind.String);
                Log.Info($"Startup enabled: \"{exe}\" {StartupArgument}");
            }
            else
            {
                key.DeleteValue(ValueName, throwOnMissingValue: false);
                Log.Info("Startup disabled");
            }

            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            Log.Error("Failed to update the startup entry", ex);
            return false;
        }
    }
}
