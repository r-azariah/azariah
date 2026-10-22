using Azariah.Core.Drive;
using Azariah.Core.Serialization;

namespace Azariah.Core.Devices;

/// <summary>
/// Attempt/lockout state for the guest unlock, stored on the drive in <c>.azariah/lock.json</c>.
/// Wrong entries and resets both count as strikes; after <see cref="MaxStrikes"/> the drive locks for
/// <see cref="LockDuration"/> (wall-clock). The lock time is written before the flash ejects, so a re-plug
/// within the window stays locked. This gates the guest unlock gesture; real protection is file encryption.
/// </summary>
public sealed record GuestLockState
{
    public int Strikes { get; init; }

    public DateTimeOffset? LockedUntilUtc { get; init; }
}

public static class GuestLock
{
    public const int MaxStrikes = 5;

    public static readonly TimeSpan LockDuration = TimeSpan.FromMinutes(10);

    public static bool IsLocked(GuestLockState state, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(state);
        return state.LockedUntilUtc is { } until && now < until;
    }

    public static TimeSpan Remaining(GuestLockState state, DateTimeOffset now) =>
        IsLocked(state, now) ? state.LockedUntilUtc!.Value - now : TimeSpan.Zero;

    /// <summary>On plug-in: once the lock window has passed, clear it so attempts are allowed again.</summary>
    public static GuestLockState Refresh(GuestLockState state, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(state);
        return state.LockedUntilUtc is { } until && now >= until ? new GuestLockState() : state;
    }

    /// <summary>A wrong entry or a reset. Crossing <see cref="MaxStrikes"/> starts the lock.</summary>
    public static GuestLockState Strike(GuestLockState state, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (IsLocked(state, now))
        {
            return state;
        }

        var strikes = state.Strikes + 1;
        return strikes >= MaxStrikes
            ? new GuestLockState { Strikes = strikes, LockedUntilUtc = now + LockDuration }
            : state with { Strikes = strikes };
    }

    /// <summary>Correct password: clear strikes and any lock.</summary>
    public static GuestLockState Unlock() => new();
}

/// <summary>Reads and writes the guest lock state at <c>.azariah/lock.json</c>.</summary>
public sealed class GuestLockStore(DriveLayout layout)
{
    public GuestLockState Load() =>
        JsonFile.TryRead(layout.LockFile, AzariahJsonContext.Default.GuestLockState) ?? new GuestLockState();

    public void Save(GuestLockState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        try
        {
            JsonFile.WriteAtomic(layout.LockFile, state, AzariahJsonContext.Default.GuestLockState);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }
}
