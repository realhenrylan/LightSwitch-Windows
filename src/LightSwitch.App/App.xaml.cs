using LightSwitch.Core;
using Microsoft.UI.Xaml;

namespace LightSwitch.App;

public partial class App : Application
{
    public static new App Current => (App)Application.Current;

    public AppHost Host { get; private set; } = null!;

    public App()
    {
        InitializeComponent();

        // AppHost.Start() initializes the log later; do it here too so anything
        // that goes wrong before that point still reaches the log file.
        Log.Initialize(AppPaths.LogPath);

        // Without these three handlers an exception in a XAML event handler
        // terminates the process with no trace in the log. A tray utility is
        // more useful alive than silently gone, so keep running and log instead.
        UnhandledException += (_, e) =>
        {
            if (e.Exception is { } exception)
            {
                Log.Error("Unhandled UI exception", exception);
            }
            else
            {
                // Exceptions that crossed the native/XAML boundary can arrive
                // without a managed Exception object.
                Log.Error($"Unhandled UI exception: {e.Message}");
            }

            e.Handled = true;
        };

        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            var exception = e.ExceptionObject as Exception;
            Log.Error($"Unhandled exception (terminating={e.IsTerminating})", exception);
        };

        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            Log.Error("Unobserved task exception", e.Exception);
            e.SetObserved();
        };
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        Host = new AppHost(Program.StartupArgs);
        Host.Start();
    }
}
