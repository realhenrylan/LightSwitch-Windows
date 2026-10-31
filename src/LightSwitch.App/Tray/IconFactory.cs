using System.IO;

namespace LightSwitch.App;

internal static class IconFactory
{
    /// <summary>Loads the app icon from Assets\app.ico, with a system fallback.</summary>
    public static IntPtr LoadAppIcon()
    {
        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, "Assets", "app.ico");
            if (File.Exists(path))
            {
                var icon = NativeMethods.LoadImageW(
                    IntPtr.Zero,
                    path,
                    NativeMethods.IMAGE_ICON,
                    0,
                    0,
                    NativeMethods.LR_LOADFROMFILE | NativeMethods.LR_DEFAULTSIZE);

                if (icon != IntPtr.Zero)
                {
                    return icon;
                }
            }
        }
        catch
        {
            // Fall through to the generic icon.
        }

        return NativeMethods.LoadIconW(IntPtr.Zero, (IntPtr)NativeMethods.IDI_APPLICATION);
    }
}
