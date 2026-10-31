using System.Threading;
using LightSwitch.Core;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;

namespace LightSwitch.App;

public static class Program
{
    internal static string[] StartupArgs { get; private set; } = Array.Empty<string>();

    [STAThread]
    public static void Main(string[] args)
    {
        StartupArgs = args;
        var command = CommandLine.Parse(args);

        try
        {
            if (command.IsOneShot)
            {
                if (!CliRunner.TryForward(command))
                {
                    CliRunner.Execute(command);
                }

                return;
            }

            using var mutex = new Mutex(true, @"Local\LightSwitch.SingleInstance", out var createdNew);
            if (!createdNew)
            {
                // A logon/background launch must never pop the window of an
                // instance the user already has running.
                if (!command.Hidden)
                {
                    CliRunner.SignalShow();
                }

                return;
            }

            WinRT.ComWrappersSupport.InitializeComWrappers();
            Application.Start(_ =>
            {
                var context = new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread());
                SynchronizationContext.SetSynchronizationContext(context);
                new App();
            });

            GC.KeepAlive(mutex);
        }
        catch (Exception ex)
        {
            try
            {
                Log.Initialize(AppPaths.LogPath);
                Log.Error("Fatal startup error", ex);
            }
            catch
            {
                // Nothing else we can do.
            }

            NativeMethods.MessageBoxW(IntPtr.Zero, $"LightSwitch 启动失败：\n{ex.Message}", "LightSwitch", 0x10);
        }
    }
}
