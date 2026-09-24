using System.Collections.ObjectModel;
using Azariah.App.Platform;
using Azariah.App.Services;
using Azariah.Core.Drive;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Material.Icons;

namespace Azariah.App.ViewModels;

public sealed record ShortcutTile(string Key, string Title, string Subtitle, MaterialIconKind Icon);

public sealed partial class HomeViewModel : ViewModelBase
{
    private readonly WorkspaceSession _s;
    private readonly WorkspaceViewModel _workspace;

    public HomeViewModel(WorkspaceSession session, WorkspaceViewModel workspace)
    {
        _s = session;
        _workspace = workspace;
        Shortcuts =
        [
            new("files", "Files", "Everything on the drive", MaterialIconKind.FolderOutline),
            new("roblox", "Roblox", "Places, scripts, assets", MaterialIconKind.CubeOutline),
            new("setupkit", "Setup Kit", "Installers and AI skills", MaterialIconKind.ToolboxOutline),
            new("transfer", "Transfer", "Move files between PCs", MaterialIconKind.SwapHorizontal),
        ];
        Refresh();
    }

    public IReadOnlyList<ShortcutTile> Shortcuts { get; }
    public ObservableCollection<FileItemViewModel> Recent { get; } = [];

    public string Greeting => Format.Greeting();
    public string DriveName => _s.Marker.DisplayName;
    public string MachineName => _s.Shell.MachineDisplayName;
    public string RootText => _s.Layout.Root;

    [ObservableProperty]
    public partial string UsedText { get; set; } = "-";

    [ObservableProperty]
    public partial string CapacityText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial double UsedPercent { get; set; }

    [ObservableProperty]
    public partial string FileSystemText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string AutoLaunchText { get; set; } = "Checking...";

    [ObservableProperty]
    public partial bool AutoLaunchOn { get; set; }

    public bool HasRecent => Recent.Count > 0;

    public void Refresh()
    {
        var summary = DriveSummary.For(_s.Layout.Root, _s.Volumes);
        UsedText = Format.Bytes(summary.UsedBytes);
        CapacityText = summary.TotalBytes > 0
            ? $"of {Format.Bytes(summary.TotalBytes)}  ·  {Format.Bytes(summary.FreeBytes)} free"
            : "Storage info unavailable";
        UsedPercent = summary.UsedFraction * 100;
        FileSystemText = string.Join("  ·  ", new[] { summary.FileSystem, summary.VolumeLabel }.Where(s => !string.IsNullOrWhiteSpace(s)));

        Recent.Clear();
        foreach (var entry in _s.Recent.GetRecent())
        {
            Recent.Add(new FileItemViewModel(entry, _s.Layout, showLocation: true));
        }

        OnPropertyChanged(nameof(HasRecent));
        OnPropertyChanged(nameof(Greeting));
        OnPropertyChanged(nameof(DriveName));
        _ = RefreshAutoLaunchAsync();
    }

    private async Task RefreshAutoLaunchAsync()
    {
        var status = await _s.AutoLaunch.GetStatusAsync(_s.Marker.DriveId);
        AutoLaunchOn = status.State == AutoLaunchState.On;
        AutoLaunchText = status.State switch
        {
            AutoLaunchState.On => "Opens automatically on this PC",
            AutoLaunchState.Off => "Off on this PC",
            _ => "Windows only",
        };
    }

    [RelayCommand]
    private void Go(string key) => _workspace.Navigate(key);

    [RelayCommand]
    private void OpenRecent(FileItemViewModel item)
    {
        try
        {
            _s.Shell.Open(item.FullPath);
            _s.Recent.Add(item.FullPath);
        }
        catch (Exception)
        {
            _workspace.OpenInFiles(Path.GetDirectoryName(item.FullPath)!);
        }
    }

    [RelayCommand]
    private void ShowRecentInFiles(FileItemViewModel item) => _workspace.OpenInFiles(Path.GetDirectoryName(item.FullPath)!);
}
