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
            AutoLaunchState.On when CanUpdateLocalCopy => ("On, but this PC has an older copy",
                "The drive has a newer AZARIAH than the copy on this PC. Update it so auto-launch runs the latest version."),
            AutoLaunchState.On => ("On for this PC",
                $"When {_s.Marker.DisplayName} is plugged in, AZARIAH opens by itself within a couple of seconds. It runs from a verified copy in {status.InstallFolder}, never straight from the USB."),
            AutoLaunchState.Off => ("Off for this PC",
                "Turn it on and AZARIAH opens by itself whenever this drive is plugged into this PC. Only do this on your own computers."),
            _ => ("Windows only", "Auto-launch uses a Windows startup entry, so it's only available on Windows."),
        };
    }

    [RelayCommand]
    private async Task EnableAutoLaunch()
    {
        var ok = await _s.Dialogs.ConfirmAsync(
            "Turn on auto-launch for this PC?",
            "AZARIAH will open by itself whenever this drive is plugged into this computer. Only do this on your own PCs.",
            "Turn on",
            details:
            [
                new DetailRow("Copies AZARIAH to", _s.AutoLaunch.Paths.InstallFolder),
                new DetailRow("Adds startup app", "\"Azariah\" (turn it off any time in Task Manager > Startup apps)"),
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
                $"Next time you plug in {_s.Marker.DisplayName}, AZARIAH opens by itself. You'll find \"Azariah\" under Task Manager > Startup apps.");
        });
    }

    [RelayCommand]
    private async Task DisableAutoLaunch()
    {
        var ok = await _s.Dialogs.ConfirmAsync("Turn off auto-launch?", "AZARIAH will stop opening by itself on this PC. You can still open it from the drive.", "Turn off");
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
        await _s.Dialogs.AlertAsync("Folders checked", "Any missing standard folders were recreated. Nothing was deleted or overwritten.");
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
