using Azariah.App.Services;
using Azariah.Core.Files;
using Azariah.Core.SetupKit;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Azariah.App.ViewModels;

public sealed partial class FilterChip(string label, IReadOnlySet<FileKind>? kinds) : ObservableObject
{
    public string Label { get; } = label;
    public IReadOnlySet<FileKind>? Kinds { get; } = kinds;

    [ObservableProperty]
    public partial bool IsActive { get; set; }
}

public sealed record SectionLink(string Label, string Path);

/// <summary>One root the page can switch between (e.g. Roblox: Games / General), each with its own browser.</summary>
public sealed partial class ScopeItem(string label, FileBrowserViewModel browser) : ObservableObject
{
    public string Label { get; } = label;
    public FileBrowserViewModel Browser { get; } = browser;

    [ObservableProperty]
    public partial bool IsActive { get; set; }
}

/// <summary>A titled page around a file browser. Used for Files, Roblox and Setup.</summary>
public sealed partial class FilesPageViewModel : ViewModelBase
{
    public FilesPageViewModel(WorkspaceSession session, string title, string root, string rootLabel)
        : this(title, [new ScopeItem(rootLabel, new FileBrowserViewModel(session, root, rootLabel))])
    {
    }

    private FilesPageViewModel(string title, IReadOnlyList<ScopeItem> scopes)
    {
        Title = title;
        Scopes = scopes;
        scopes[0].IsActive = true;
        Browser = scopes[0].Browser;
    }

    public string Title { get; }

    [ObservableProperty]
    public partial FileBrowserViewModel Browser { get; set; }

    public IReadOnlyList<ScopeItem> Scopes { get; }
    public bool HasScopes => Scopes.Count > 1;

    public IReadOnlyList<FilterChip> Filters { get; init; } = [];
    public bool HasFilters => Filters.Count > 0;

    public IReadOnlyList<SectionLink> Sections { get; init; } = [];
    public bool HasSections => Sections.Count > 0;

    [RelayCommand]
    private void SelectScope(ScopeItem scope)
    {
        foreach (var s in Scopes)
        {
            s.IsActive = ReferenceEquals(s, scope);
        }

        foreach (var f in Filters)
        {
            f.IsActive = f.Kinds is null;
        }

        scope.Browser.ClearSearch();
        Browser = scope.Browser;
        Browser.Refresh();
    }

    [RelayCommand]
    private void ApplyFilter(FilterChip chip)
    {
        foreach (var f in Filters)
        {
            f.IsActive = ReferenceEquals(f, chip);
        }

        Browser.ApplyKindFilter(chip.Kinds is null ? null : chip.Label, chip.Kinds);
    }

    [RelayCommand]
    private void OpenSection(SectionLink link) => Browser.NavigateTo(link.Path);

    /// <summary>Roblox\Games\ holds one folder per game; the rest of Roblox\ is general stuff not tied to a game.</summary>
    public static FilesPageViewModel Roblox(WorkspaceSession s) =>
        new("Roblox",
        [
            new ScopeItem("Games", new FileBrowserViewModel(s, s.Layout.Games, "Games")),
            new ScopeItem("General", new FileBrowserViewModel(s, s.Layout.Roblox, "Roblox")),
        ])
        {
            Filters =
            [
                new FilterChip("All", null) { IsActive = true },
                new FilterChip("Places", new HashSet<FileKind> { FileKind.RobloxPlace }),
                new FilterChip("Scripts", new HashSet<FileKind> { FileKind.LuauScript }),
                new FilterChip("Models", new HashSet<FileKind> { FileKind.RobloxModel }),
                new FilterChip("Images", new HashSet<FileKind> { FileKind.Image }),
                new FilterChip("Docs", new HashSet<FileKind> { FileKind.Text, FileKind.Document, FileKind.Pdf }),
                new FilterChip("Backups", new HashSet<FileKind> { FileKind.Archive }),
            ],
        };

    public static FilesPageViewModel SetupKit(WorkspaceSession s)
    {
        var kit = s.Layout.SetupKit;
        return new(s, "Setup", kit, "Setup Kit")
        {
            Sections =
            [
                new SectionLink("Installers", Path.Combine(kit, SetupKitLayout.InstallersFolder)),
                new SectionLink("Skills", Path.Combine(kit, SetupKitLayout.SkillsFolder)),
                new SectionLink("Configs", Path.Combine(kit, SetupKitLayout.ConfigsFolder)),
                new SectionLink("Docs", Path.Combine(kit, SetupKitLayout.DocsFolder)),
            ],
        };
    }
}
