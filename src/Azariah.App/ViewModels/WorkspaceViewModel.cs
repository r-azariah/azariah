using System.Collections.ObjectModel;
using Azariah.App.Services;
using Azariah.App.ViewModels.Dialogs;
using Azariah.Core.Files;
using Azariah.Core.Roblox;
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
        Notes = new NotesPageViewModel(session, this);
        CommandBar = new CommandBarViewModel(this);

        // Interim IA: only places that exist and do something. Vault/Passwords return when built;
        // AI becomes the command surface; Transfer is a location (see docs/DESIGN.md).
        NavItems =
        [
            new("home", "Home", MaterialIconKind.HomeVariantOutline, () => new HomeViewModel(session, this)),
            new("files", "Files", MaterialIconKind.FolderOutline, () => Files),
            new("notes", "Notes", MaterialIconKind.NoteTextOutline, () => Notes),
            new("roblox", "Roblox", MaterialIconKind.CubeOutline, () => Roblox),
            new("setupkit", "Setup", MaterialIconKind.ToolboxOutline, () => FilesPageViewModel.SetupKit(session)),
            new("settings", "Settings", MaterialIconKind.CogOutline, () => new SettingsViewModel(session, this)),
        ];

        SelectedNav = NavItems[0];
    }

    public WorkspaceSession Session { get; }

    public FilesPageViewModel Files { get; }

    public RobloxPageViewModel Roblox { get; }

    public NotesPageViewModel Notes { get; }

    public SpacePageViewModel? Space { get; private set; }

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
        Notes.Flush();
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

    /// <summary>Shows one note on the Notes page.</summary>
    public void OpenNote(string path)
    {
        Navigate("notes");
        Notes.Select(path);
    }

    /// <summary>Where the drive's space goes. Not a sidebar place: reached from Home and Ctrl+Space.</summary>
    public void OpenSpace()
    {
        Notes.Flush();
        SelectedNav = null;
        Space ??= new SpacePageViewModel(Session, this);
        CurrentPage = Space;
        Space.Rescan();
    }

    /// <summary>Quick capture from Ctrl+Space: a timestamped line in today's daily note.</summary>
    public string CaptureNote(string text)
    {
        Notes.Flush();
        var path = Session.Notes.AppendToDaily(text, DateTime.Now);
        Notes.Refresh();
        return path;
    }

    /// <summary>Shows one game on the Roblox page.</summary>
    public void OpenProject(string folder)
    {
        Navigate("roblox");
        Roblox.Select(folder);
    }

    /// <summary>
    /// Opens a game in Studio. The published place is the newest version; the LATEST save on the drive
    /// is a backup that can be older, so this asks which one.
    /// </summary>
    public async Task OpenGameAsync(RobloxProject game)
    {
        ArgumentNullException.ThrowIfNull(game);
        var published = game.PlaceId is not null && game.UniverseId is not null;
        var options = new List<ChoiceOption>();
        string message;
        if (game.Latest is { } backup)
        {
            var saved = backup.Date is { } d ? Format.Day(d) : Format.Ago(backup.ModifiedUtc);
            options.Add(new ChoiceOption("backup", $"Open backup ({saved})"));
            message = published
                ? $"Studio opens the published place, the newest version. The backup is the save from {saved}."
                : $"The backup is the save from {saved}.";
        }
        else
        {
            message = "There's no backup on the drive yet.";
        }

        options.Add(new ChoiceOption("studio", "Open Studio", IsPrimary: true));
        var choice = await Session.Dialogs.ChooseAsync($"Open {game.Title}", message, [.. options]);
        if (choice == "backup" && game.Latest is { } latest)
        {
            await OpenPathAsync(latest.Path);
        }
        else if (choice == "studio")
        {
            try
            {
                if (!Session.Shell.OpenRobloxStudio(game.PlaceId, game.UniverseId))
                {
                    await Session.Dialogs.AlertAsync("Roblox Studio isn't installed", "Install it from create.roblox.com, or open the backup.");
                }
            }
            catch (Exception ex)
            {
                await Session.Dialogs.AlertAsync("Couldn't open Studio", ex.Message);
            }
        }
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
            case NotesPageViewModel notes:
                notes.Refresh();
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
