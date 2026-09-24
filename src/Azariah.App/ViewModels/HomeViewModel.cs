using System.Collections.ObjectModel;
using Azariah.App.Platform;
using Azariah.App.Services;
using Azariah.Core.Drive;
using Azariah.Core.Roblox;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Azariah.App.ViewModels;

/// <summary>A place on the drive. Either a page (NavKey) or a folder opened in Files.</summary>
public sealed record HomeLocation(string Title, string? NavKey, string? Folder);

/// <summary>Home is current state: what's on the drive, what was touched, what this PC is doing.</summary>
public sealed partial class HomeViewModel : ViewModelBase
{
    private readonly WorkspaceSession _s;
    private readonly WorkspaceViewModel _workspace;

    public HomeViewModel(WorkspaceSession session, WorkspaceViewModel workspace)
    {
        _s = session;
        _workspace = workspace;
        var layout = session.Layout;
        Locations =
        [
            new("Files", "files", null),
            new("Roblox", "roblox", null),
            new("Setup Kit", "setupkit", null),
            new("Transfer", null, layout.Transfer),
            new("Public", null, layout.Public),
        ];
        Refresh();
    }

    public IReadOnlyList<HomeLocation> Locations { get; }
    public ObservableCollection<FileItemViewModel> Recent { get; } = [];

    /// <summary>Games, most recently changed first: where you left off.</summary>
    public ObservableCollection<RobloxProjectViewModel> Games { get; } = [];

    public bool HasGames => Games.Count > 0;

    public string DriveName => _s.Marker.DisplayName;
    public string MachineName => _s.Shell.MachineDisplayName;
    public string RootText => _s.Layout.Root;

    [ObservableProperty]
    public partial string UsedText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string FreeText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial double UsedPercent { get; set; }

    [ObservableProperty]
    public partial string AutoLaunchText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string TransferText { get; set; } = string.Empty;

    public bool HasRecent => Recent.Count > 0;

    public void Refresh()
    {
        var summary = DriveSummary.For(_s.Layout.Root, _s.Volumes);
        UsedText = summary.TotalBytes > 0 ? $"{Format.Bytes(summary.UsedBytes)} of {Format.Bytes(summary.TotalBytes)}" : "Unknown";
        FreeText = summary.TotalBytes > 0 ? $"{Format.Bytes(summary.FreeBytes)} free" : string.Empty;
        UsedPercent = summary.UsedFraction * 100;

        var waiting = CountEntries(_s.Layout.Transfer);
        TransferText = waiting switch
        {
            0 => "Empty",
            1 => "1 item waiting",
            _ => $"{waiting} items waiting",
        };

        Recent.Clear();
        foreach (var entry in _s.Recent.GetRecent(10))
        {
            Recent.Add(new FileItemViewModel(entry, _s.Layout, showLocation: true));
        }

        Games.Clear();
        foreach (var game in RobloxProjects.Load(_s.Layout).Take(5))
        {
            Games.Add(new RobloxProjectViewModel(game));
        }

        OnPropertyChanged(nameof(HasGames));
        OnPropertyChanged(nameof(HasRecent));
        OnPropertyChanged(nameof(DriveName));
        _ = RefreshAutoLaunchAsync();
    }

    private static int CountEntries(string folder)
    {
        try
        {
            return Directory.Exists(folder) ? Directory.EnumerateFileSystemEntries(folder).Count() : 0;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return 0;
        }
    }

    private async Task RefreshAutoLaunchAsync()
    {
        var status = await _s.AutoLaunch.GetStatusAsync(_s.Marker.DriveId);
        AutoLaunchText = status.State switch
        {
            AutoLaunchState.On => "On",
            AutoLaunchState.Off => "Off",
            _ => "Windows only",
        };
    }

    [RelayCommand]
    private void OpenLocation(HomeLocation location)
    {
        if (location.NavKey is not null)
        {
            _workspace.Navigate(location.NavKey);
        }
        else if (location.Folder is not null)
        {
            _workspace.OpenInFiles(location.Folder);
        }
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

    [RelayCommand]
    private void OpenGame(RobloxProjectViewModel game) => _workspace.OpenProject(game.Folder);

    [RelayCommand]
    private Task OpenGameInStudio(RobloxProjectViewModel game) => _workspace.OpenGameAsync(game.Project);}
