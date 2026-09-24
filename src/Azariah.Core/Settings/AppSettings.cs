namespace Azariah.Core.Settings;

public enum ThemePreference
{
    Dark,
    Light,
    System,
}

/// <summary>Settings that travel with the drive (stored in <c>.azariah/settings.json</c>).</summary>
public sealed record AppSettings
{
    public int Version { get; init; } = 1;
    public ThemePreference Theme { get; init; } = ThemePreference.Dark;
    public bool ShowHiddenFiles { get; init; }
    public bool ConfirmMoveToTrash { get; init; } = true;

    /// <summary>Phase 2: lock the Vault after this many idle minutes (0 = never).</summary>
    public int AutoLockMinutes { get; init; } = 10;
}
