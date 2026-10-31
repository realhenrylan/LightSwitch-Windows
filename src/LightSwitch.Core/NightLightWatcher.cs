using System.Runtime.InteropServices;
using Microsoft.Win32;
using Microsoft.Win32.SafeHandles;

namespace LightSwitch.Core;

/// <summary>
/// Watches the Night Light registry blob with RegNotifyChangeKeyValue on a
/// dedicated background thread. The one-minute timer is still the safety net;
/// this watcher only makes the reaction immediate.
/// </summary>
public sealed class NightLightWatcher : IDisposable
{
    private const int RegNotifyChangeLastSet = 0x00000004;

    private readonly object _gate = new();
    private Thread? _thread;
    private ManualResetEvent? _stopEvent;
    private bool _disposed;

    public event EventHandler? Changed;

    public void Start()
    {
        lock (_gate)
        {
            if (_disposed || _thread is not null)
            {
                return;
            }

            _stopEvent = new ManualResetEvent(false);
            _thread = new Thread(WatchLoop)
            {
                IsBackground = true,
                Name = "LightSwitch.NightLightWatcher",
            };
            _thread.Start(_stopEvent);
        }
    }

    private void WatchLoop(object? state)
    {
        var stop = (ManualResetEvent)state!;
        using var changed = new ManualResetEvent(false);

        while (!_disposed)
        {
            RegistryKey? key = null;
            try
            {
                key = Registry.CurrentUser.OpenSubKey(NightLightService.RegistryPath);
                if (key is null)
                {
                    if (WaitHandle.WaitAny(new WaitHandle[] { stop }, 5000) == 0)
                    {
                        return;
                    }

                    continue;
                }

                using var hKey = key;
                key = null;

                var notify = RegNotifyChangeKeyValue(hKey.Handle, false, RegNotifyChangeLastSet,
                    changed.SafeWaitHandle, true);
                if (notify != 0)
                {
                    Log.Warn($"RegNotifyChangeKeyValue failed with {notify}; Night Light changes will be picked up by the timer.");
                    return;
                }

                if (WaitHandle.WaitAny(new WaitHandle[] { changed, stop }) != 0)
                {
                    return;
                }

                changed.Reset();
                try
                {
                    Changed?.Invoke(this, EventArgs.Empty);
                }
                catch (Exception ex)
                {
                    Log.Error("Night Light watcher handler failed", ex);
                }
            }
            catch (Exception ex)
            {
                Log.Warn($"Night Light watcher stopped: {ex.Message}");
                return;
            }
            finally
            {
                key?.Dispose();
            }
        }
    }

    public void Dispose()
    {
        Thread? thread;
        ManualResetEvent? stop;
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            thread = _thread;
            stop = _stopEvent;
            _thread = null;
            _stopEvent = null;
        }

        try
        {
            stop?.Set();
        }
        catch
        {
            // Ignore.
        }

        if (thread is not null && thread.IsAlive)
        {
            thread.Join(TimeSpan.FromSeconds(2));
        }

        stop?.Dispose();
        Changed = null;
    }

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern int RegNotifyChangeKeyValue(
        SafeRegistryHandle hKey,
        [MarshalAs(UnmanagedType.Bool)] bool bWatchSubtree,
        int dwNotifyFilter,
        SafeWaitHandle hEvent,
        [MarshalAs(UnmanagedType.Bool)] bool fAsynchronous);
}
