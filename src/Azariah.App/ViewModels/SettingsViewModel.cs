using System.Reflection;
using Azariah.App.Platform;
using Azariah.App.Services;
using Azariah.App.ViewModels.Dialogs;
using Azariah.Core.Drive;
using Azariah.Core.Files;
using Azariah.Core.Settings;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Azariah.App.ViewModels;

public sealed partial class SettingsViewModel : ViewModelBase
{
    private readonly WorkspaceSession _s;
    private readonly WorkspaceViewModel _workspace;
    private bool _loading;

    public SettingsViewModel(WorkspaceSession session, WorkspaceViewModel workspace)
    {
        _s = session;
        _workspace = workspace;
        _loading = true;
        DriveName = session.Marker.DisplayName;
        ShowHiddenFiles = session.Settings.ShowHiddenFiles;
        ConfirmMoveToTrash = session.Settings.ConfirmMoveToTrash;
        SelectedTheme = session.Settings.Theme;
        _loading = false;
        _ = RefreshAsync();
    }

    public IReadOnlyList<ThemePreference> Themes { get; } = [ThemePreference.Dark, ThemePreference.Light, ThemePreference.System];

    [ObservableProperty]
    public partial string DriveName { get; set; }

    [ObservableProperty]
    public partial ThemePreference SelectedTheme { get; set; }

    [ObservableProperty]
    public partial bool ShowHiddenFiles { get; set; }

    [ObservableProperty]
    public partial bool ConfirmMoveToTrash { get; set; }

    [ObservableProperty]
    public partial string AutoLaunchTitle { get; set; } = "Checking...";

    [ObservableProperty]
    public partial string AutoLaunchDetail { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool AutoLaunchOn { get; set; }

    [ObservableProperty]
    public partial bool CanUpdateLocalCopy { get; set; }

    [ObservableProperty]
    public partial bool IsWorking { get; set; }

    public bool AutoLaunchSupported => AutoLaunchManager.IsSupported;

    public string RootText => _s.Layout.Root;
    public string DriveIdText => _s.Marker.DriveId.ToString("D")[..8];
    public string MachineName => _s.Shell.MachineDisplayName;

    public string VolumeText
    {
        get
        {
            var v = _s.Volumes.GetVolumeFor(_s.Layout.Root);
            return v is null ? "Unknown" : $"{v.FileSystem}  ·  {(v.DriveType == DriveType.Removable ? "Removable" : v.DriveType.ToString())}";
        }
    }

    public string VersionText =>
        typeof(SettingsViewModel).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0] ?? "dev";

    public async Task RefreshAsync()
    {
        var status = await _s.AutoLaunch.GetStatusAsync(_s.Marker.DriveId);
        AutoLaunchOn = status.State == AutoLaunchState.On;
        CanUpdateLocalCopy = AutoLaunchOn && !status.LocalCopyUpToDate && !status.RunningFromLocalCopy;
        (AutoLaunchTitle, AutoLaunchDetail) = status.State switch
        {
            AutoLaunchState.On when CanUpdateLocalCopy => ("On, older copy on this PC", "The drive has a newer build than this PC."),
            AutoLaunchState.On => ("On", $"Opens when {_s.Marker.DisplayName} is plugged in. Runs from {status.InstallFolder}."),
            AutoLaunchState.Off => ("Off", "Opens AZARIAH when this drive is plugged into this PC."),
            _ => ("Windows only", string.Empty),
        };
    }

    [RelayCommand]
    private async Task EnableAutoLaunch()
    {
        var ok = await _s.Dialogs.ConfirmAsync(
            "Turn on auto-launch?",
            $"AZARIAH will open whenever {_s.Marker.DisplayName} is plugged into {MachineName}.",
            "Turn on",
            details:
            [
                new DetailRow("Copies AZARIAH to", _s.AutoLaunch.Paths.InstallFolder),
                new DetailRow("Startup app", "\"Azariah\" (Task Manager > Startup apps)"),
                new DetailRow("Opens for", $"{_s.Marker.DisplayName} only (drive id {DriveIdText})"),
                new DetailRow("Admin rights", "Not needed"),
            ]);
        if (!ok)
        {
            return;
        }

        await RunAsync(async () =>
        {
            await _s.AutoLaunch.EnableAsync(_s.Marker);
            await _s.Dialogs.AlertAsync(
                "Auto-launch is on",
                $"Unplug {_s.Marker.DisplayName} and plug it back in to try it.");
        });
    }

    [RelayCommand]
    private async Task DisableAutoLaunch()
    {
        var ok = await _s.Dialogs.ConfirmAsync("Turn off auto-launch?", $"AZARIAH stops opening by itself on {MachineName}.", "Turn off");
        if (ok)
        {
            await RunAsync(() => _s.AutoLaunch.DisableAsync(_s.Marker.DriveId));
        }
    }

    [RelayCommand]
    private Task UpdateLocalCopy() => RunAsync(_s.AutoLaunch.UpdateLocalCopyAsync);

    [RelayCommand]
    private void SaveDriveName()
    {
        var name = DriveName.Trim();
        if (name.Length == 0 || name == _s.Marker.DisplayName)
        {
            DriveName = _s.Marker.DisplayName;
            return;
        }

        _s.Rename(name);
        _workspace.NotifyDriveRenamed();
    }

    [RelayCommand]
    private void OpenTrash() => _workspace.OpenTrash();

    [RelayCommand]
    private async Task RepairFolders()
    {
        DriveInitializer.EnsureFolders(_s.Layout);
        await _s.Dialogs.AlertAsync("Folders checked", "Missing standard folders were recreated.");
    }

    [RelayCommand]
    private void OpenLogs()
    {
        try
        {
            _s.Shell.Open(_s.Layout.LogsFolder);
        }
        catch (Exception)
        {
            // Nothing useful to do if Explorer can't open.
        }
    }

    partial void OnSelectedThemeChanged(ThemePreference value)
    {
        if (_loading)
        {
            return;
        }

        App.ApplyTheme(value);
        _s.UpdateSettings(s => s with { Theme = value });
    }

    partial void OnShowHiddenFilesChanged(bool value)
    {
        if (!_loading)
        {
            _s.UpdateSettings(s => s with { ShowHiddenFiles = value });
        }
    }

    partial void OnConfirmMoveToTrashChanged(bool value)
    {
        if (!_loading)
        {
            _s.UpdateSettings(s => s with { ConfirmMoveToTrash = value });
        }
    }

    private async Task RunAsync(Func<Task> action)
    {
        try
        {
            IsWorking = true;
            await action();
        }
        catch (Exception ex)
        {
            await _s.Dialogs.AlertAsync("That didn't work", ex.Message);
        }
        finally
        {
            IsWorking = false;
            await RefreshAsync();
        }
    }
}
