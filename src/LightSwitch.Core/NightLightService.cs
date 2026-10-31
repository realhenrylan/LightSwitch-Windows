using Microsoft.Win32;

namespace LightSwitch.Core;

/// <summary>
/// Reads the Night Light state from the same undocumented-but-stable CloudStore
/// blob the original app used. Windows does not expose a public API for this.
/// </summary>
public sealed class NightLightService : INightLightService
{
    public const string RegistryPath =
        @"Software\Microsoft\Windows\CurrentVersion\CloudStore\Store\DefaultAccount\Current\" +
        @"default$windows.data.bluelightreduction.bluelightreductionstate\" +
        @"windows.data.bluelightreduction.bluelightreductionstate";

    public bool IsActive => TryRead(out var active) && active;

    public bool TryRead(out bool active)
    {
        active = false;
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegistryPath);
            if (key?.GetValue("Data") is not byte[] data || data.Length < 25)
            {
                return false;
            }

            // Same signature as the C++ implementation: the two bytes at 23/24 flip to 10 00 when enabled.
            active = data[23] == 0x10 && data[24] == 0x00;
            return true;
        }
        catch (Exception ex)
        {
            Log.Warn($"Unable to read Night Light state: {ex.Message}");
            return false;
        }
    }
}
