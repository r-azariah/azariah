namespace Azariah.App.Platform;

/// <summary>Named kernel objects shared between the app and the background watcher (Windows).</summary>
internal static class WatcherSignals
{
    /// <summary>Held by the running watcher so only one runs per user session.</summary>
    public const string MutexName = @"Local\Azariah.Watcher";

    /// <summary>Set to ask the running watcher to exit (e.g. before updating or turning it off).</summary>
    public const string StopEventName = @"Local\Azariah.Watcher.Stop";

    /// <summary>Asks a running watcher to stop and waits until it has released its mutex.</summary>
    public static bool StopRunningWatcher(TimeSpan timeout)
    {
        if (!OperatingSystem.IsWindows())
        {
            return true;
        }

        if (EventWaitHandle.TryOpenExisting(StopEventName, out var stop))
        {
            using (stop)
            {
                stop.Set();
            }
        }

        using var mutex = new Mutex(false, MutexName);
        try
        {
            if (mutex.WaitOne(timeout))
            {
                mutex.ReleaseMutex();
                return true;
            }

            return false;
        }
        catch (AbandonedMutexException)
        {
            mutex.ReleaseMutex();
            return true;
        }
    }
}
