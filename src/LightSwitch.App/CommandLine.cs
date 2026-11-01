namespace LightSwitch.App;

/// <summary>Parses the small command-line surface kept from the C++ build.</summary>
public sealed record CommandLine(
    bool Toggle,
    bool Light,
    bool Dark,
    bool Startup,
    bool Hidden,
    bool EnableStartup,
    bool DisableStartup)
{
    public bool IsOneShot => Toggle || Light || Dark || EnableStartup || DisableStartup;

    /// <summary>
    /// True/false when the caller wants to change the sign-in setting, null for
    /// the normal theme commands. The installer uses the two startup switches
    /// so the registry entry and config.json stay in sync.
    /// </summary>
    public bool? StartupPreference
    {
        get
        {
            if (EnableStartup)
            {
                return true;
            }

            if (DisableStartup)
            {
                return false;
            }

            return null;
        }
    }

    public static CommandLine Parse(string[] args)
    {
        var toggle = false;
        var light = false;
        var dark = false;
        var startup = false;
        var hidden = false;
        var enableStartup = false;
        var disableStartup = false;

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i].Trim();
            switch (arg.ToLowerInvariant())
            {
                case "/toggle":
                case "-toggle":
                case "--toggle":
                    toggle = true;
                    break;
                case "--light":
                case "/light":
                    light = true;
                    break;
                case "--dark":
                case "/dark":
                    dark = true;
                    break;
                case "--apply":
                    if (i + 1 < args.Length)
                    {
                        var value = args[++i].Trim().ToLowerInvariant();
                        light |= value == "light";
                        dark |= value == "dark";
                    }

                    break;
                case "--startup":
                    startup = true;
                    hidden = true;
                    break;
                case "--hidden":
                case "--tray":
                    hidden = true;
                    break;
                case "--enable-startup":
                case "/enable-startup":
                    enableStartup = true;
                    break;
                case "--disable-startup":
                case "/disable-startup":
                    disableStartup = true;
                    break;
            }
        }

        return new CommandLine(toggle, light, dark, startup, hidden, enableStartup, disableStartup);
    }
}
