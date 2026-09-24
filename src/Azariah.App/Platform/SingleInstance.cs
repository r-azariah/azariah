namespace Azariah.App.Platform;

/// <summary>
/// One window per drive. A second launch asks the running window to come to the front and waits
/// for it to confirm. If the running instance doesn't answer (e.g. it hung when its drive was
/// yanked), the new launch goes ahead instead of silently doing nothing. Windows only.
/// </summary>
internal sealed class SingleInstance : IDisposable
{
    private static readonly TimeSpan AckTimeout = TimeSpan.FromSeconds(2);

    private readonly Mutex _mutex;
    private readonly EventWaitHandle _activate;
    private readonly EventWaitHandle _ack;
    private readonly CancellationTokenSource _cts = new();

    private SingleInstance(Mutex mutex, EventWaitHandle activate, EventWaitHandle ack)
    {
        _mutex = mutex;
        _activate = activate;
        _ack = ack;
    }

    /// <summary>
    /// Returns an owner handle, or null. <paramref name="alreadyRunning"/> is true only when a
    /// responsive instance took over; then this process should exit.
    /// </summary>
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
        var ack = new EventWaitHandle(false, EventResetMode.AutoReset, name + ".Ack");
        if (createdNew)
        {
            return new SingleInstance(mutex, activate, ack);
        }

        activate.Set();
        alreadyRunning = ack.WaitOne(AckTimeout);
        activate.Dispose();
        ack.Dispose();
        mutex.Dispose();
        return null;
    }

    /// <summary>
    /// Runs <paramref name="onActivate"/> whenever another launch asks, then confirms. The callback
    /// should block until the window is actually in front (e.g. Dispatcher.Invoke), so a hung UI
    /// never confirms.
    /// </summary>
    public void Listen(Action onActivate)
    {
        var thread = new Thread(() =>
        {
            var handles = new WaitHandle[] { _activate, _cts.Token.WaitHandle };
            while (WaitHandle.WaitAny(handles) == 0)
            {
                try
                {
                    onActivate();
                    _ack.Set();
                }
                catch (Exception)
                {
                    // Never confirm if bringing the window forward failed.
                }
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
        _ack.Dispose();
        _mutex.Dispose();
        _cts.Dispose();
    }
}
