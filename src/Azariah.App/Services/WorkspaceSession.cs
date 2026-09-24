using Azariah.App.Platform;
using Azariah.Core.Diagnostics;
using Azariah.Core.Drive;
using Azariah.Core.Files;
using Azariah.Core.Launcher;
using Azariah.Core.Notes;
using Azariah.Core.Settings;

namespace Azariah.App.Services;

/// <summary>
/// Everything that belongs to one mounted drive. If the drive comes back under a different
/// letter the whole session is rebuilt, so no service can hold a stale path.
/// Future modules (Vault, Passwords, AI) hang off this session the same way.
/// </summary>
public sealed class WorkspaceSession
{
    public WorkspaceSession(
        DriveLayout layout,
        DriveMarker marker,
        IVolumeProvider volumes,
        IDialogService dialogs,
        PlatformUi ui,
        IShellService shell)
    {
        Layout = layout;
        Marker = marker;
        Volumes = volumes;
        Dialogs = dialogs;
        Ui = ui;
        Shell = shell;
        Log = new FileAppLog(layout.LogsFolder);
        Trash = new TrashService(layout, Log);
        Files = new FileOperationService(layout, Trash, Log);
        Search = new FileSearchService(layout);
        Notes = new NotesService(layout, Files);
        Recent = new RecentFilesStore(layout);
        SettingsStore = new SettingsStore(layout);
        Settings = SettingsStore.Load();
        AutoLaunch = new AutoLaunchManager(LauncherPaths.ForCurrentUser(), Log);
        Updates = new UpdateManager(AutoLaunch.Paths, Log);
    }

    public DriveLayout Layout { get; }
    public DriveMarker Marker { get; private set; }
    public IVolumeProvider Volumes { get; }
    public IDialogService Dialogs { get; }
    public PlatformUi Ui { get; }
    public IShellService Shell { get; }
    public IAppLog Log { get; }
    public TrashService Trash { get; }
    public FileOperationService Files { get; }
    public FileSearchService Search { get; }
    public NotesService Notes { get; }
    public RecentFilesStore Recent { get; }
    public SettingsStore SettingsStore { get; }
    public AppSettings Settings { get; private set; }
    public AutoLaunchManager AutoLaunch { get; }
    public UpdateManager Updates { get; }
    public FileClipboard Clipboard { get; } = new();

    public event EventHandler? SettingsChanged;

    public void UpdateSettings(Func<AppSettings, AppSettings> change)
    {
        ArgumentNullException.ThrowIfNull(change);
        Settings = change(Settings);
        SettingsStore.Save(Settings);
        SettingsChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Rename(string displayName)
    {
        Marker = Marker with { DisplayName = displayName };
        DriveMarkerStore.Write(Layout.Root, Marker);
    }
}
