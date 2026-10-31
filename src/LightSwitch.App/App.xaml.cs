using Microsoft.UI.Xaml;

namespace LightSwitch.App;

public partial class App : Application
{
    public static new App Current => (App)Application.Current;

    public AppHost Host { get; private set; } = null!;

    public App()
    {
        InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        Host = new AppHost(Program.StartupArgs);
        Host.Start();
    }
}
