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

        Files = new FilesPageViewModel(session, "Files", "Everything on your drive.", MaterialIconKind.FolderMultiple, layout.Root, "Drive");

        NavItems =
        [
            new("home", "Home", MaterialIconKind.HomeVariantOutline, () => new HomeViewModel(session, this)),
            new("files", "Files", MaterialIconKind.FolderOutline, () => Files),
            new("vault", "Vault", MaterialIconKind.ShieldLockOutline, ComingSoonViewModel.Vault, "Soon"),
            new("passwords", "Passwords", MaterialIconKind.KeyVariant, ComingSoonViewModel.Passwords, "Soon"),
            new("roblox", "Roblox", MaterialIconKind.CubeOutline, () => FilesPageViewModel.Roblox(session)),
            new("setupkit", "Setup Kit", MaterialIconKind.ToolboxOutline, () => FilesPageViewModel.SetupKit(session)),
            new("transfer", "Transfer", MaterialIconKind.SwapHorizontal, () => FilesPageViewModel.Transfer(session)),
            new("ai", "AI", MaterialIconKind.RobotOutline, () => new AiViewModel(), "Soon"),
            new("settings", "Settings", MaterialIconKind.CogOutline, () => new SettingsViewModel(session, this)),
        ];

        SelectedNav = NavItems[0];
    }

    public WorkspaceSession Session { get; }

    public FilesPageViewModel Files { get; }

    public ObservableCollection<NavItem> NavItems { get; }

    [ObservableProperty]
    public partial NavItem? SelectedNav { get; set; }

    [ObservableProperty]
    public partial ViewModelBase? CurrentPage { get; set; }

    [ObservableProperty]
    public partial bool IsDisconnected { get; set; }

    public string DriveName => Session.Marker.DisplayName;

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
            case SettingsViewModel settings:
                _ = settings.RefreshAsync();
                break;
        }
    }

    public void NotifyDriveRenamed() => OnPropertyChanged(nameof(DriveName));

    [RelayCommand]
    private void RetryConnection() => RefreshCurrentPage();

    /// <summary>True if the path is somewhere the Files browser can show.</summary>
    public bool CanShow(string path) => Session.Layout.Contains(path) && !Session.Layout.IsProtected(path);

    public static bool IsUnder(string parent, string path) => PathGuard.IsInsideOrEqual(parent, path);
}
