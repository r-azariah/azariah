using System.Collections.ObjectModel;
using System.Globalization;
using Azariah.App.Services;
using Azariah.Core.Space;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Azariah.App.ViewModels;

public enum GuestPanel
{
    Home,
    Storage,
    Shared,
}

/// <summary>One of the big buttons on the guest page. <paramref name="Badge"/> "Soon" marks a tool
/// that isn't built yet.</summary>
public sealed record GuestTile(string Title, string Subtitle, string? Badge, IRelayCommand Command)
{
    public bool HasBadge => !string.IsNullOrEmpty(Badge);
}

/// <summary>A friendly storage category shown as a bar (Photos, Downloads, Videos...), with its biggest
/// files for the drill-in. <see cref="BarWidth"/> is pixels against the view's fixed track width.</summary>
public sealed record GuestCategory(string Name, string SizeText, string CountText, double BarWidth, IReadOnlyList<SpaceFileItem> Files);

/// <summary>
/// The outward page AZARIAH shows on a PC that isn't one of the owner's: a greeting and a few big,
/// easy tools a guest can run, plus the files the owner chose to share. It never surfaces the owner's
/// Notes, games, card shows or Files, and it has no Settings or updates.
/// </summary>
public sealed partial class GuestViewModel : ViewModelBase
{
    /// <summary>The widest bar in the storage chart, in pixels; the view's track matches it.</summary>
    public const double BarTrack = 460;

    private readonly WorkspaceSession _s;
    private readonly Action? _exitPreview;

    public GuestViewModel(WorkspaceSession session, Action? exitPreview = null)
    {
        _s = session;
        _exitPreview = exitPreview;
        Shared = FilesPageViewModel.SetupKit(session);
        Tiles =
        [
            new GuestTile("Windows makeover", "Windhawk with Azariah's mods", null, MakeoverCommand),
            new GuestTile("Storage", "See what's filling this PC", null, ShowStorageCommand),
            new GuestTile("Shared files", "Public, Transfer and the Setup Kit", null, ShowSharedCommand),
            new GuestTile("Optimize PC", "Clear junk and tune it up", "Soon", ComingSoonCommand),
            new GuestTile("Local AI", "Runs on this PC, stored on the flash", "Soon", ComingSoonCommand),
            new GuestTile("Virus check", "Scan this PC with Windows Defender", "Soon", ComingSoonCommand),
        ];
    }

    public IReadOnlyList<GuestTile> Tiles { get; }

    public FilesPageViewModel Shared { get; }

    /// <summary>Storage split into friendly categories, biggest first.</summary>
    public ObservableCollection<GuestCategory> Categories { get; } = [];

    public string DriveName => _s.Marker.DisplayName.Trim('[', ']', ' ');

    /// <summary>True when the owner is previewing the guest page from their own device.</summary>
    public bool IsPreview => _exitPreview is not null;

    public bool IsHome => Panel == GuestPanel.Home;

    public bool IsStorage => Panel == GuestPanel.Storage;

    public bool IsShared => Panel == GuestPanel.Shared;

    /// <summary>In the storage panel: the bar overview (no category opened) vs one category's files.</summary>
    public bool IsOverview => Opened is null;

    public bool HasStatus => !string.IsNullOrEmpty(Status);

    [ObservableProperty]
    public partial GuestPanel Panel { get; set; } = GuestPanel.Home;

    [ObservableProperty]
    public partial GuestCategory? Opened { get; set; }

    [ObservableProperty]
    public partial bool IsScanning { get; set; }

    [ObservableProperty]
    public partial string ScanText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string? Status { get; set; }

    /// <summary>Finishes when the current storage scan is done (used by tests).</summary>
    public Task Pending { get; private set; } = Task.CompletedTask;

    partial void OnPanelChanged(GuestPanel value)
    {
        Status = null;
        OnPropertyChanged(nameof(IsHome));
        OnPropertyChanged(nameof(IsStorage));
        OnPropertyChanged(nameof(IsShared));
    }

    partial void OnOpenedChanged(GuestCategory? value) => OnPropertyChanged(nameof(IsOverview));

    partial void OnStatusChanged(string? value) => OnPropertyChanged(nameof(HasStatus));

    [RelayCommand]
    private void Back() => Panel = GuestPanel.Home;

    [RelayCommand]
    private void ExitPreview() => _exitPreview?.Invoke();

    [RelayCommand]
    private async Task Makeover()
    {
        var installer = Path.Combine(_s.Layout.SetupKit, "Installers", "Utilities", "Windhawk", "Install Windhawk.cmd");
        if (!File.Exists(installer))
        {
            Status = "The Windhawk kit isn't on this drive yet.";
            return;
        }

        var ok = await _s.Dialogs.ConfirmAsync(
            "Windows makeover",
            $"This installs Windhawk and Azariah's mods on {_s.Shell.MachineDisplayName}. Windows will ask for administrator.",
            "Install",
            danger: true);
        if (!ok)
        {
            return;
        }

        try
        {
            _s.Shell.Open(installer);
            Status = "Installing. Follow the window that opens.";
        }
        catch (Exception ex)
        {
            Status = $"Couldn't start it: {ex.Message}";
        }
    }

    [RelayCommand]
    private void ShowStorage()
    {
        Opened = null;
        Panel = GuestPanel.Storage;
        Pending = ScanAsync();
    }

    [RelayCommand]
    private void OpenCategory(GuestCategory category) => Opened = category;

    [RelayCommand]
    private void BackToOverview() => Opened = null;

    [RelayCommand]
    private void ShowShared() => Panel = GuestPanel.Shared;

    [RelayCommand]
    private void ComingSoon() => Status = "Coming soon.";

    [RelayCommand]
    private void OpenFile(SpaceFileItem file) => _s.Shell.Reveal(file.Path);

    private async Task ScanAsync()
    {
        Categories.Clear();
        IsScanning = true;
        ScanText = "Looking through this PC…";
        try
        {
            var report = await PcContent.ScanAsync(BigFilesScanner.UserRoots());
            var largest = Math.Max(1, report.LargestCategoryBytes);
            foreach (var category in report.Categories)
            {
                var files = category.TopFiles
                    .Select(f => new SpaceFileItem(f.FullPath, f.Name, Format.Bytes(f.Size ?? 0), Folder(f.FullPath)))
                    .ToList();
                Categories.Add(new GuestCategory(
                    category.Name,
                    Format.Bytes(category.Bytes),
                    category.FileCount == 1 ? "1 file" : string.Create(CultureInfo.CurrentCulture, $"{category.FileCount:N0} files"),
                    Math.Max(3, BarTrack * category.Bytes / largest),
                    files));
            }

            ScanText = report.TotalFiles == 0
                ? "Nothing found in the usual folders."
                : string.Create(CultureInfo.CurrentCulture, $"{Format.Bytes(report.TotalBytes)} across {report.TotalFiles:N0} files in your folders");
        }
        catch (Exception ex)
        {
            ScanText = $"Couldn't scan: {ex.Message}";
        }
        finally
        {
            IsScanning = false;
        }
    }

    private static string Folder(string path)
    {
        var folder = Path.GetDirectoryName(path);
        if (folder is null)
        {
            return string.Empty;
        }

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return home.Length > 0 && folder.StartsWith(home, StringComparison.OrdinalIgnoreCase)
            ? "~" + folder[home.Length..]
            : folder;
    }
}
