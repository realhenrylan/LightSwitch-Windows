namespace LightSwitch.App;

/// <summary>Parses the small command-line surface kept from the C++ build.</summary>
public sealed record CommandLine(bool Toggle, bool Light, bool Dark, bool Startup, bool Hidden)
{
    public bool IsOneShot => Toggle || Light || Dark;

    public static CommandLine Parse(string[] args)
    {
        var toggle = false;
        var light = false;
        var dark = false;
        var startup = false;
        var hidden = false;

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
            }
        }

        return new CommandLine(toggle, light, dark, startup, hidden);
    }
}
