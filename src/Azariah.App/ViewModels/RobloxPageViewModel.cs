using System.Collections.ObjectModel;
using Avalonia.Platform.Storage;
using Azariah.App.Services;
using Azariah.App.ViewModels.Dialogs;
using Azariah.Core.Drive;
using Azariah.Core.Files;
using Azariah.Core.Roblox;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Azariah.App.ViewModels;

public sealed class SaveItemViewModel(SaveEntry entry)
{
    public SaveEntry Entry { get; } = entry;

    /// <summary>The date already has its own column, so "2026-09-20 Obby Rush.rbxl" shows as "Obby Rush.rbxl".</summary>
    public string Name => Entry.Date is not null && Entry.Name.Length > 11 && Entry.Name[10] == ' ' ? Entry.Name[11..] : Entry.Name;
    public string DateText => Entry.Date is { } d ? Format.Day(d) : Format.Ago(Entry.ModifiedUtc);
    public string SizeText => Entry.IsFolder ? "Folder" : Entry.Size is { } size ? Format.Bytes(size) : string.Empty;
}

/// <summary>One game, shaped for the Roblox page and Home.</summary>
public sealed class RobloxProjectViewModel(RobloxProject project)
{
    public RobloxProject Project { get; } = project;
    public string Folder => Project.Folder;
    public string Title => Project.Title;
    public IReadOnlyList<string> Summary => Project.Summary;
    public bool HasSummary => Summary.Count > 0;
    public IReadOnlyList<string> NextStep => Project.NextStep;
    public bool HasNextStep => NextStep.Count > 0;

    public bool HasLatest => Project.Latest is not null;
    public string SaveText => HasLatest ? "Save new version" : "Add first save";
    public string LatestText => Project.Latest is { } l ? $"{Day(l)}  ·  {Size(l)}" : "No save yet";

    public bool HasPlace => Project.PlaceUri is not null;
    public string PlaceText => Project.PlaceId ?? "Not published";

    public string PassText => Project.LastPass?.Heading ?? "No passes yet";
    public string? PassLine => Project.LastPass?.FirstLine;
    public bool HasPassLine => !string.IsNullOrEmpty(PassLine);

    public IReadOnlyList<SaveItemViewModel> OldVersions { get; } = project.OldVersions.Select(v => new SaveItemViewModel(v)).ToList();
    public bool HasOldVersions => OldVersions.Count > 0;
    public string OldVersionsTitle => $"Old versions  {OldVersions.Count}";

    /// <summary>The short line under the name in lists.</summary>
    public string ListDetail => Project.Latest is { } l ? $"Saved {Day(l)}" : Project.LastPass is { } p ? p.Heading : "Empty";

    private static string Day(SaveEntry save) =>
        save.Date is { } d ? Format.Day(d) : Format.Ago(save.ModifiedUtc);

    private static string Size(SaveEntry save) => save.Size is { } size ? Format.Bytes(size) : string.Empty;
}

/// <summary>
/// Roblox: every game in <c>Roblox\Games</c> as a project (notes, last pass, latest save, old versions)
/// plus the general <c>Roblox\</c> folder with kind filters.
/// </summary>
public sealed partial class RobloxPageViewModel : ViewModelBase
{
    private static readonly FilePickerFileType PlaceFiles = new("Roblox place") { Patterns = ["*.rbxl", "*.rbxlx"] };

    private readonly WorkspaceSession _s;
    private readonly WorkspaceViewModel _workspace;

    public RobloxPageViewModel(WorkspaceSession session, WorkspaceViewModel workspace)
    {
        _s = session;
        _workspace = workspace;
        General = new FileBrowserViewModel(session, session.Layout.Roblox, "Roblox");
        Filters =
        [
            new FilterChip("All", null) { IsActive = true },
            new FilterChip("Places", new HashSet<FileKind> { FileKind.RobloxPlace }),
            new FilterChip("Scripts", new HashSet<FileKind> { FileKind.LuauScript }),
            new FilterChip("Models", new HashSet<FileKind> { FileKind.RobloxModel }),
            new FilterChip("Images", new HashSet<FileKind> { FileKind.Image }),
            new FilterChip("Docs", new HashSet<FileKind> { FileKind.Text, FileKind.Document, FileKind.Pdf }),
            new FilterChip("Backups", new HashSet<FileKind> { FileKind.Archive }),
        ];
        Refresh();
    }

    public ObservableCollection<RobloxProjectViewModel> Projects { get; } = [];

    public FileBrowserViewModel General { get; }

    public IReadOnlyList<FilterChip> Filters { get; }

    public bool HasProjects => Projects.Count > 0;

    public bool ShowProjects => !ShowGeneral;

    public bool HasStatus => !string.IsNullOrEmpty(Status);

    [ObservableProperty]
    public partial RobloxProjectViewModel? Selected { get; set; }

    [ObservableProperty]
    public partial bool ShowGeneral { get; set; }

    [ObservableProperty]
    public partial string? Status { get; set; }

    [ObservableProperty]
    public partial bool StatusIsError { get; set; }

    public void Refresh()
    {
        var keep = Selected?.Folder;
        Projects.Clear();
        foreach (var project in RobloxProjects.Load(_s.Layout))
        {
            Projects.Add(new RobloxProjectViewModel(project));
        }

        Selected = Projects.FirstOrDefault(p => keep is not null && PathGuard.AreSame(p.Folder, keep)) ?? Projects.FirstOrDefault();
        OnPropertyChanged(nameof(HasProjects));
        if (ShowGeneral)
        {
            General.Refresh();
        }
    }

    /// <summary>Shows one game (from Home or the command bar).</summary>
    public void Select(string folder)
    {
        ShowGeneral = false;
        Refresh();
        Selected = Projects.FirstOrDefault(p => PathGuard.AreSame(p.Folder, folder)) ?? Selected;
    }

    partial void OnShowGeneralChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowProjects));
        if (value)
        {
            General.Refresh();
        }
    }

    partial void OnSelectedChanged(RobloxProjectViewModel? value) => Status = null;

    partial void OnStatusChanged(string? value) => OnPropertyChanged(nameof(HasStatus));

    [RelayCommand]
    private void ShowGames() => ShowGeneral = false;

    [RelayCommand]
    private void ShowGeneralFolder() => ShowGeneral = true;

    [RelayCommand]
    private void ApplyFilter(FilterChip chip)
    {
        foreach (var f in Filters)
        {
            f.IsActive = ReferenceEquals(f, chip);
        }

        General.ApplyKindFilter(chip.Kinds is null ? null : chip.Label, chip.Kinds);
    }

    [RelayCommand]
    private async Task OpenInStudio()
    {
        if (Selected is { } project)
        {
            await _workspace.OpenGameAsync(project.Project);
        }
    }

    [RelayCommand]
    private async Task SaveNewVersion()
    {
        if (Selected is not { } project)
        {
            return;
        }

        var picked = await ChooseSaveAsync(project);
        if (picked is null)
        {
            return;
        }

        try
        {
            var latest = await RobloxProjects.SaveNewVersionAsync(_s.Files, project.Project, picked, DateOnly.FromDateTime(DateTime.Now));
            _s.Recent.Add(latest);
            Refresh();
            SetStatus($"Saved as {Path.GetFileName(latest)}", error: false);
        }
        catch (Exception ex)
        {
            Refresh();
            SetStatus(OperationReport.Describe(ex), error: true);
        }
    }

    [RelayCommand]
    private void OpenFolder()
    {
        if (Selected is { } project)
        {
            _workspace.OpenInFiles(project.Folder);
        }
    }

    [RelayCommand]
    private async Task OpenNotes()
    {
        if (Selected is not { } project)
        {
            return;
        }

        if (File.Exists(project.Project.NotesPath))
        {
            await _workspace.OpenPathAsync(project.Project.NotesPath);
        }
        else
        {
            SetStatus("No notes yet. Claude writes them on the first pass.", error: false);
        }
    }

    [RelayCommand]
    private void OpenOnRoblox()
    {
        if (Selected?.Project.PlaceUri is not { } uri)
        {
            return;
        }

        try
        {
            _s.Shell.OpenUri(uri);
        }
        catch (Exception ex)
        {
            SetStatus($"Couldn't open the Roblox page: {ex.Message}", error: true);
        }
    }

    [RelayCommand]
    private async Task OpenSave(SaveItemViewModel save)
    {
        if (save.Entry.IsFolder)
        {
            _workspace.OpenInFiles(save.Entry.Path);
        }
        else
        {
            await _workspace.OpenPathAsync(save.Entry.Path);
        }
    }

    [RelayCommand]
    private async Task NewGame()
    {
        var games = _s.Layout.Games;
        var name = await _s.Dialogs.PromptAsync(
            "New game",
            null,
            confirmText: "Create",
            validate: n => FileNameRules.Validate(n)
                ?? (Directory.Exists(Path.Combine(games, n.Trim())) ? "That game already exists." : null));
        if (name is null)
        {
            return;
        }

        try
        {
            if (!Directory.Exists(games))
            {
                _s.Files.CreateFolder(_s.Layout.Roblox, DriveLayout.GamesFolderName);
            }

            var folder = _s.Files.CreateFolder(games, name.Trim()).FullPath;
            _s.Files.CreateFolder(folder, RobloxProjects.OldVersionsFolderName);
            Select(folder);
        }
        catch (Exception ex)
        {
            SetStatus(OperationReport.Describe(ex), error: true);
        }
    }

    /// <summary>Offers the newest place saved on this PC; otherwise (or if asked) a file picker.</summary>
    private async Task<string?> ChooseSaveAsync(RobloxProjectViewModel project)
    {
        var latest = project.Project.Latest?.Path;
        var newest = await Task.Run(() => PlaceFinder.FindNewest(
            PlaceFinder.DefaultRoots(project.Folder),
            path => latest is not null && PathGuard.AreSame(path, latest)));
        if (newest is not null)
        {
            var choice = await _s.Dialogs.ChooseAsync(
                $"New save for {project.Title}",
                $"{newest.Name}  ·  {Where(newest.FullPath, project.Folder)}  ·  saved {Format.Ago(newest.ModifiedUtc)}",
                new ChoiceOption("pick", "Pick another file"),
                new ChoiceOption("use", "Use this file", IsPrimary: true));
            if (choice is null)
            {
                return null;
            }

            if (choice == "use")
            {
                return newest.FullPath;
            }
        }

        var files = await _s.Ui.PickFilesAsync($"New save for {project.Title}", PlaceFiles);
        return files.Count > 0 ? files[0] : null;
    }

    private static string Where(string file, string projectFolder)
    {
        var folder = Path.GetDirectoryName(file) ?? file;
        if (PathGuard.AreSame(folder, projectFolder))
        {
            return "in this game's folder";
        }

        var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return profile.Length > 0 && PathGuard.IsInsideOrEqual(profile, folder) ? Path.GetRelativePath(profile, folder) : folder;
    }

    private void SetStatus(string message, bool error)
    {
        Status = message;
        StatusIsError = error;
    }
}
