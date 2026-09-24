using System.Collections.ObjectModel;
using Azariah.App.Services;
using Azariah.Core.Files;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Material.Icons;

namespace Azariah.App.ViewModels;

public sealed partial class NavItem(string key, string title, MaterialIconKind icon, Func<ViewModelBase> factory, string? badge = null)
    : ObservableObject
{
    private ViewModelBase? _page;

    public string Key { get; } = key;
    public string Title { get; } = title;
    public MaterialIconKind Icon { get; } = icon;
    public string? Badge { get; } = badge;
    public bool HasBadge => Badge is not null;

    public ViewModelBase Page => _page ??= factory();
}

/// <summary>The main shell for an open drive: sidebar + current page.</summary>
public sealed partial class WorkspaceViewModel : ViewModelBase
{
    public WorkspaceViewModel(WorkspaceSession session)
    {
        Session = session;
        var layout = session.Layout;

        Files = new FilesPageViewModel(session, "Files", layout.Root, "Drive");
        Roblox = new RobloxPageViewModel(session, this);
        CommandBar = new CommandBarViewModel(this);

        // Interim IA: only places that exist and do something. Vault/Passwords return when built;
        // AI becomes the command surface; Transfer is a location (see docs/DESIGN.md).
        NavItems =
        [
            new("home", "Home", MaterialIconKind.HomeVariantOutline, () => new HomeViewModel(session, this)),
            new("files", "Files", MaterialIconKind.FolderOutline, () => Files),
            new("roblox", "Roblox", MaterialIconKind.CubeOutline, () => Roblox),
            new("setupkit", "Setup", MaterialIconKind.ToolboxOutline, () => FilesPageViewModel.SetupKit(session)),
            new("settings", "Settings", MaterialIconKind.CogOutline, () => new SettingsViewModel(session, this)),
        ];

        SelectedNav = NavItems[0];
    }

    public WorkspaceSession Session { get; }

    public FilesPageViewModel Files { get; }

    public RobloxPageViewModel Roblox { get; }

    public CommandBarViewModel CommandBar { get; }

    public ObservableCollection<NavItem> NavItems { get; }

    [ObservableProperty]
    public partial NavItem? SelectedNav { get; set; }

    [ObservableProperty]
    public partial ViewModelBase? CurrentPage { get; set; }

    public string DriveName => Session.Marker.DisplayName;

    /// <summary>The wordmark already says AZARIAH; only show the drive name when it's something else.</summary>
    public bool ShowDriveName => !string.Equals(DriveName.Trim('[', ']', ' '), "AZARIAH", StringComparison.OrdinalIgnoreCase);

    public string RootText => Session.Layout.Root;

    partial void OnSelectedNavChanged(NavItem? value)
    {
        if (value is not null)
        {
            CurrentPage = value.Page;
            RefreshCurrentPage();
        }
    }

    public void Navigate(string key)
    {
        var item = NavItems.FirstOrDefault(n => n.Key == key);
        if (item is not null)
        {
            SelectedNav = item;
        }
    }

    /// <summary>Opens a folder in the Files page.</summary>
    public void OpenInFiles(string folder)
    {
        Navigate("files");
        Files.Browser.NavigateTo(folder);
    }

    /// <summary>Shows one game on the Roblox page.</summary>
    public void OpenProject(string folder)
    {
        Navigate("roblox");
        Roblox.Select(folder);
    }

    /// <summary>Opens a folder in Files, or a file with its default app. Programs ask first.</summary>
    public async Task OpenPathAsync(string path)
    {
        if (Directory.Exists(path))
        {
            OpenInFiles(path);
            return;
        }

        var name = Path.GetFileName(path);
        if (FileBrowserViewModel.RunnableExtensions.Contains(Path.GetExtension(path)))
        {
            var ok = await Session.Dialogs.ConfirmAsync(
                "Run this program?",
                $"\"{name}\" will run on {Session.Shell.MachineDisplayName} with your permissions.",
                "Run",
                danger: true);
            if (!ok)
            {
                return;
            }
        }

        try
        {
            Session.Shell.Open(path);
            Session.Recent.Add(path);
        }
        catch (Exception ex)
        {
            await Session.Dialogs.AlertAsync("Couldn't open it", $"\"{name}\": {ex.Message}");
        }
    }

    [RelayCommand]
    private void OpenCommandBar() => CommandBar.Open();

    [RelayCommand]
    public void OpenTrash()
    {
        SelectedNav = null;
        CurrentPage = new TrashViewModel(Session, this);
    }

    public void RefreshCurrentPage()
    {
        switch (CurrentPage)
        {
            case HomeViewModel home:
                home.Refresh();
                break;
            case FilesPageViewModel files:
                files.Browser.Refresh();
                break;
            case RobloxPageViewModel roblox:
                roblox.Refresh();
                break;
            case SettingsViewModel settings:
                _ = settings.RefreshAsync();
                break;
        }
    }

    public void NotifyDriveRenamed()
    {
        OnPropertyChanged(nameof(DriveName));
        OnPropertyChanged(nameof(ShowDriveName));
    }

    /// <summary>True if the path is somewhere the Files browser can show.</summary>
    public bool CanShow(string path) => Session.Layout.Contains(path) && !Session.Layout.IsProtected(path);

    public static bool IsUnder(string parent, string path) => PathGuard.IsInsideOrEqual(parent, path);
}
