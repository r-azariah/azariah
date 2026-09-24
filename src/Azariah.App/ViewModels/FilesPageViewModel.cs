using Azariah.App.Services;
using Azariah.Core.Files;
using Azariah.Core.SetupKit;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Material.Icons;

namespace Azariah.App.ViewModels;

public sealed partial class FilterChip(string label, IReadOnlySet<FileKind>? kinds) : ObservableObject
{
    public string Label { get; } = label;
    public IReadOnlySet<FileKind>? Kinds { get; } = kinds;

    [ObservableProperty]
    public partial bool IsActive { get; set; }
}

public sealed record SectionLink(string Label, string Path, MaterialIconKind Icon);

/// <summary>A titled page around a file browser. Used for Files, Roblox, Setup Kit and Transfer.</summary>
public sealed partial class FilesPageViewModel : ViewModelBase
{
    public FilesPageViewModel(WorkspaceSession session, string title, string subtitle, MaterialIconKind icon, string root, string rootLabel)
    {
        Title = title;
        Subtitle = subtitle;
        Icon = icon;
        Browser = new FileBrowserViewModel(session, root, rootLabel);
    }

    public string Title { get; }
    public string Subtitle { get; }
    public MaterialIconKind Icon { get; }
    public FileBrowserViewModel Browser { get; }

    public IReadOnlyList<FilterChip> Filters { get; init; } = [];
    public bool HasFilters => Filters.Count > 0;

    public IReadOnlyList<SectionLink> Sections { get; init; } = [];
    public bool HasSections => Sections.Count > 0;

    public bool ShowTransferActions { get; init; }

    public string? Banner { get; init; }
    public bool HasBanner => Banner is not null;

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

    public static FilesPageViewModel Roblox(WorkspaceSession s) =>
        new(s, "Roblox", "Places, scripts, assets and backups for your games.", MaterialIconKind.CubeOutline, s.Layout.Roblox, "Roblox")
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
        return new(s, "Setup Kit", "Installers, AI skills, configs and guides for setting up a PC.", MaterialIconKind.ToolboxOutline, kit, "Setup Kit")
        {
            Sections =
            [
                new SectionLink("Installers", Path.Combine(kit, SetupKitLayout.InstallersFolder), MaterialIconKind.PackageDown),
                new SectionLink("Skills", Path.Combine(kit, SetupKitLayout.SkillsFolder), MaterialIconKind.Brain),
                new SectionLink("Configs", Path.Combine(kit, SetupKitLayout.ConfigsFolder), MaterialIconKind.FileCog),
                new SectionLink("Docs", Path.Combine(kit, SetupKitLayout.DocsFolder), MaterialIconKind.FileDocumentOutline),
            ],
            Banner = "Public storage: no passwords, API keys or tokens here. Installers never run on their own; opening one asks first.",
        };
    }

    public static FilesPageViewModel Transfer(WorkspaceSession s) =>
        new(s, "Transfer", "A drop zone for moving files between computers.", MaterialIconKind.SwapHorizontal, s.Layout.Transfer, "Transfer")
        {
            ShowTransferActions = true,
        };
}
