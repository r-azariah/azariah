namespace Azariah.Core.Drive;

/// <summary>
/// Watches the active drive and reports when it disappears or comes back (possibly under a
/// different drive letter). Polling is used because it is simple, reliable across USB
/// controllers and costs almost nothing at this interval. Phase 2 hooks Vault locking onto
/// <see cref="Disconnected"/>.
/// </summary>
public sealed class DriveMonitor : IAsyncDisposable
{
    private readonly DriveRootLocator _locator;
    private readonly Guid _driveId;
    private readonly TimeSpan _interval;
    private readonly CancellationTokenSource _cts = new();
    private Task? _loop;

    public DriveMonitor(DriveRootLocator locator, string root, Guid driveId, TimeSpan? interval = null)
    {
        _locator = locator;
        Root = root;
        _driveId = driveId;
        _interval = interval ?? TimeSpan.FromSeconds(1);
    }

    public string Root { get; private set; }

    public bool IsConnected { get; private set; } = true;

    /// <summary>Raised on a background thread when the drive can no longer be reached.</summary>
    public event EventHandler? Disconnected;

    /// <summary>Raised on a background thread with the (possibly new) root once the drive is back.</summary>
    public event EventHandler<string>? Reconnected;

    public void Start() => _loop ??= Task.Run(() => RunAsync(_cts.Token));

    /// <summary>Checks once, synchronously. Exposed for tests and for "retry now" buttons.</summary>
    public void Poll()
    {
        if (IsConnected)
        {
            if (!IsDriveReachable(Root))
            {
                IsConnected = false;
                Disconnected?.Invoke(this, EventArgs.Empty);
            }

            return;
        }

        var found = IsDriveReachable(Root) ? Root : _locator.FindByDriveId(_driveId)?.Root;
        if (found is not null)
        {
            Root = found;
            IsConnected = true;
            Reconnected?.Invoke(this, found);
        }
    }

    private bool IsDriveReachable(string root)
    {
        try
        {
            return DriveMarkerStore.TryRead(root) is { } marker && marker.DriveId == _driveId;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private async Task RunAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(_interval);
        try
        {
            while (await timer.WaitForNextTickAsync(ct))
            {
                Poll();
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _cts.CancelAsync();
        if (_loop is not null)
        {
            await _loop;
        }

        _cts.Dispose();
    }
}
