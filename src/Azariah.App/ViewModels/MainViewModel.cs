using Avalonia.Controls;
using Avalonia.Threading;
using Azariah.App.Platform;
using Azariah.App.Services;
using Azariah.Core.Devices;
using Azariah.Core.Drive;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Azariah.App.ViewModels;

/// <summary>Top level: either the welcome/setup screen or an open workspace for a drive.</summary>
public sealed partial class MainViewModel : ViewModelBase
{
    private readonly IVolumeProvider _volumes;
    private readonly DriveRootLocator _locator;
    private DriveMonitor? _monitor;
    private WorkspaceViewModel? _previewFrom;
    private bool _shutDown;

    public MainViewModel(LocateResult located, IVolumeProvider volumes, DriveRootLocator locator)
    {
        ArgumentNullException.ThrowIfNull(located);
        _volumes = volumes;
        _locator = locator;
        DialogService = new DialogService(Dialogs);

        if (located is { Outcome: LocateOutcome.Found, Root: { } root, Marker: { } marker })
        {
            OpenDrive(root, marker);
        }
        else
        {
            Current = new WelcomeViewModel(this, located, volumes, locator);
        }
    }

    public DialogHost Dialogs { get; } = new();

    public IDialogService DialogService { get; }

    public PlatformUi Ui { get; } = new();

    public IShellService Shell { get; } = new ShellService();

    [ObservableProperty]
    public partial ViewModelBase? Current { get; set; }

    [ObservableProperty]
    public partial string WindowTitle { get; set; } = Brand.Name;

    public WorkspaceViewModel? Workspace => Current as WorkspaceViewModel;

    /// <summary>The drive was unplugged. The window plays the disconnect screen and the app exits.</summary>
    public event EventHandler? DriveLost;

    public void AttachWindow(TopLevel window) => Ui.TopLevel = window;

    /// <summary>
    /// A drive was found. On one of the owner's own computers this opens the full workspace; on any
    /// other PC it opens the guest page. The device list lives on the drive (<c>.azariah/devices.json</c>);
    /// see <see cref="OwnerDevicesStore"/> for why this is a convenience signal, not a security boundary.
    /// </summary>
    public void OpenDrive(string root, DriveMarker marker, string? pageKey = null)
    {
        var layout = new DriveLayout(root);
        Directory.CreateDirectory(layout.SystemFolder);
        if (new OwnerDevicesStore(layout).IsOwnerDevice(MachineIdentity.Current))
        {
            OpenWorkspace(root, marker, pageKey);
        }
        else
        {
            OpenGuest(root, marker);
        }
    }

    internal void OpenWorkspace(string root, DriveMarker marker, string? pageKey = null)
    {
        var layout = new DriveLayout(root);

        // Only the app's own folder. Standard folders are made once at setup (or from Settings),
        // so anything the owner deletes stays deleted.
        Directory.CreateDirectory(layout.SystemFolder);
        var session = new WorkspaceSession(layout, marker, _volumes, DialogService, Ui, Shell);
        App.ApplyTheme(session.Settings.Theme);
        var workspace = new WorkspaceViewModel(session);
        workspace.PreviewGuestRequested += () =>
        {
            _previewFrom = workspace;
            Current = new GuestViewModel(session, ExitGuestPreview);
        };
        if (pageKey is not null)
        {
            workspace.Navigate(pageKey);
        }

        Current = workspace;
        WindowTitle = Brand.Name;
        session.Log.Info("Workspace opened.");
        StartMonitor(root, marker.DriveId);
    }

    private void OpenGuest(string root, DriveMarker marker)
    {
        var layout = new DriveLayout(root);
        var session = new WorkspaceSession(layout, marker, _volumes, DialogService, Ui, Shell);
        App.ApplyTheme(session.Settings.Theme);
        Current = new GuestViewModel(session);
        WindowTitle = Brand.Name;
        session.Log.Info("Guest page opened (not an owner device).");
        StartMonitor(root, marker.DriveId);
    }

    private void ExitGuestPreview()
    {
        if (_previewFrom is { } workspace)
        {
            Current = workspace;
            _previewFrom = null;
        }
    }

    private void StartMonitor(string root, Guid driveId)
    {
        _ = _monitor?.DisposeAsync().AsTask();
        _monitor = new DriveMonitor(_locator, root, driveId);
        _monitor.Disconnected += (_, _) => Dispatcher.UIThread.Post(OnDisconnected);
        _monitor.Start();
    }

    private void OnDisconnected()
    {
        // Phase 2: lock the Vault here (LockReason.DriveRemoved) before anything else.
        Workspace?.Session.Log.Info("Drive removed; closing.");
        _ = _monitor?.DisposeAsync().AsTask();
        _monitor = null;
        DriveLost?.Invoke(this, EventArgs.Empty);
    }

    public void Shutdown()
    {
        if (_shutDown)
        {
            return;
        }

        _shutDown = true;

        // Phase 2: lock the Vault (LockReason.AppExit) and wipe key material here.
        Workspace?.Session.Log.Info("Azariah closed.");
        _ = _monitor?.DisposeAsync().AsTask();
    }
}
