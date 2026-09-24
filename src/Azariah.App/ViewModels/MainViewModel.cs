using Avalonia.Controls;
using Avalonia.Threading;
using Azariah.App.Services;
using Azariah.Core.Drive;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Azariah.App.ViewModels;

/// <summary>Top level: either the welcome/setup screen or an open workspace for a drive.</summary>
public sealed partial class MainViewModel : ViewModelBase
{
    private readonly IVolumeProvider _volumes;
    private readonly DriveRootLocator _locator;
    private DriveMonitor? _monitor;

    public MainViewModel(LocateResult located, IVolumeProvider volumes, DriveRootLocator locator)
    {
        ArgumentNullException.ThrowIfNull(located);
        _volumes = volumes;
        _locator = locator;
        DialogService = new DialogService(Dialogs);

        if (located is { Outcome: LocateOutcome.Found, Root: { } root, Marker: { } marker })
        {
            OpenWorkspace(root, marker);
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
    public partial string WindowTitle { get; set; } = "AZARIAH";

    public WorkspaceViewModel? Workspace => Current as WorkspaceViewModel;

    public void AttachWindow(TopLevel window) => Ui.TopLevel = window;

    internal void OpenWorkspace(string root, DriveMarker marker, string? pageKey = null)
    {
        var layout = new DriveLayout(root);
        DriveInitializer.EnsureFolders(layout);
        var session = new WorkspaceSession(layout, marker, _volumes, DialogService, Ui, Shell);
        App.ApplyTheme(session.Settings.Theme);
        var workspace = new WorkspaceViewModel(session);
        if (pageKey is not null)
        {
            workspace.Navigate(pageKey);
        }

        Current = workspace;
        WindowTitle = $"AZARIAH · {marker.DisplayName}";
        session.Log.Info("Workspace opened.");
        StartMonitor(root, marker.DriveId);
    }

    private void StartMonitor(string root, Guid driveId)
    {
        _ = _monitor?.DisposeAsync().AsTask();
        _monitor = new DriveMonitor(_locator, root, driveId);
        _monitor.Disconnected += (_, _) => Dispatcher.UIThread.Post(OnDisconnected);
        _monitor.Reconnected += (_, newRoot) => Dispatcher.UIThread.Post(() => OnReconnected(newRoot));
        _monitor.Start();
    }

    private void OnDisconnected()
    {
        // Phase 2: lock the Vault here (LockReason.DriveRemoved) before anything else.
        if (Workspace is { } ws)
        {
            ws.IsDisconnected = true;
        }
    }

    private void OnReconnected(string newRoot)
    {
        if (Workspace is not { } ws)
        {
            return;
        }

        if (Core.Files.PathGuard.AreSame(ws.Session.Layout.Root, newRoot))
        {
            ws.IsDisconnected = false;
            ws.RefreshCurrentPage();
            return;
        }

        // Same drive, new letter: rebuild everything against the new root.
        var marker = DriveMarkerStore.TryRead(newRoot);
        if (marker is not null)
        {
            OpenWorkspace(newRoot, marker, ws.SelectedNav?.Key);
        }
    }

    public void Shutdown()
    {
        // Phase 2: lock the Vault (LockReason.AppExit) and wipe key material here.
        Workspace?.Session.Log.Info("Azariah closed.");
        _ = _monitor?.DisposeAsync().AsTask();
    }
}
