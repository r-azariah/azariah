namespace Azariah.App.Platform;

/// <summary>
/// One window per drive. A second launch (e.g. double-clicking the exe while the watcher already
/// opened it) just brings the existing window to the front. Windows only; elsewhere a no-op.
/// </summary>
internal sealed class SingleInstance : IDisposable
{
    private readonly Mutex _mutex;
    private readonly EventWaitHandle _activate;
    private readonly CancellationTokenSource _cts = new();

    private SingleInstance(Mutex mutex, EventWaitHandle activate)
    {
        _mutex = mutex;
        _activate = activate;
    }

    /// <summary>Returns null if another instance already owns this drive (and signals it).</summary>
    public static SingleInstance? TryAcquire(Guid driveId, out bool alreadyRunning)
    {
        alreadyRunning = false;
        if (!OperatingSystem.IsWindows())
        {
            return null;
        }

        var name = $@"Local\Azariah.App.{driveId:N}";
        var mutex = new Mutex(initiallyOwned: true, name, out var createdNew);
        var activate = new EventWaitHandle(false, EventResetMode.AutoReset, name + ".Activate");
        if (!createdNew)
        {
            alreadyRunning = true;
            activate.Set();
            activate.Dispose();
            mutex.Dispose();
            return null;
        }

        return new SingleInstance(mutex, activate);
    }

    /// <summary>Calls <paramref name="onActivate"/> (on a background thread) whenever another launch asks.</summary>
    public void Listen(Action onActivate)
    {
        var thread = new Thread(() =>
        {
            var handles = new WaitHandle[] { _activate, _cts.Token.WaitHandle };
            while (WaitHandle.WaitAny(handles) == 0)
            {
                onActivate();
            }
        })
        {
            IsBackground = true,
            Name = "Azariah single-instance listener",
        };
        thread.Start();
    }

    public void Dispose()
    {
        _cts.Cancel();
        _activate.Dispose();
        _mutex.Dispose();
        _cts.Dispose();
    }
}
