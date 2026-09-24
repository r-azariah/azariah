using Azariah.Core.Serialization;

namespace Azariah.Core.Launcher;

public sealed record PairedDrive(Guid DriveId, string DisplayName, DateTimeOffset AddedUtc);

/// <summary>Per-PC auto-launch settings, stored on the PC (never on the USB).</summary>
public sealed record LauncherConfig
{
    public int Version { get; init; } = 1;
    public List<PairedDrive> Drives { get; init; } = [];

    /// <summary>True once AZARIAH set this user's removable-drive AutoPlay to "take no action".</summary>
    public bool AutoPlayChanged { get; init; }

    /// <summary>The removable-drive AutoPlay choices from before that change (null = unset), to put back.</summary>
    public string? AutoPlayBefore { get; init; }

    public string? AutoPlayDefaultBefore { get; init; }
}

/// <summary>Where the auto-launcher lives on this PC. Per-user locations, no admin rights needed.</summary>
public sealed class LauncherPaths(string localAppData)
{
    public const string ExeName = "Azariah.exe";

    public static LauncherPaths ForCurrentUser() =>
        new(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));

    /// <summary>%LOCALAPPDATA%\Programs\Azariah: the verified local copy that the launcher runs.</summary>
    public string InstallFolder => Path.Combine(localAppData, "Programs", "Azariah");

    public string InstalledExe => Path.Combine(InstallFolder, ExeName);

    /// <summary>%LOCALAPPDATA%\Azariah: per-PC data (launcher config, logs; Phase 3 device keys).</summary>
    public string DataFolder => Path.Combine(localAppData, "Azariah");

    public string ConfigFile => Path.Combine(DataFolder, "launcher.json");

    public string LogsFolder => Path.Combine(DataFolder, "logs");
}

public sealed class LauncherConfigStore(LauncherPaths paths)
{
    public LauncherConfig Load() =>
        JsonFile.TryRead(paths.ConfigFile, AzariahJsonContext.Default.LauncherConfig) ?? new LauncherConfig();

    public void Save(LauncherConfig config) =>
        JsonFile.WriteAtomic(paths.ConfigFile, config, AzariahJsonContext.Default.LauncherConfig);

    public DateTime LastWriteUtc => File.Exists(paths.ConfigFile) ? File.GetLastWriteTimeUtc(paths.ConfigFile) : DateTime.MinValue;
}
