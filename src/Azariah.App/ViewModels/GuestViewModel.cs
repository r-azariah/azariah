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
    BigFiles,
    Shared,
}

/// <summary>One of the big buttons on the guest page. <paramref name="Badge"/> "Soon" marks a tool
/// that isn't built yet.</summary>
public sealed record GuestTile(string Title, string Subtitle, string? Badge, IRelayCommand Command)
{
    public bool HasBadge => !string.IsNullOrEmpty(Badge);
}

/// <summary>
/// The outward page AZARIAH shows on a PC that isn't one of the owner's: a greeting and a few big,
/// easy tools a guest can run, plus the files the owner chose to share. It never surfaces the owner's
/// Notes, games, card shows or Files, and it has no Settings or updates.
/// </summary>
public sealed partial class GuestViewModel : ViewModelBase
{
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
            new GuestTile("Biggest files", "See what's filling this PC", null, ShowBigFilesCommand),
            new GuestTile("Shared files", "Public, Transfer and the Setup Kit", null, ShowSharedCommand),
            new GuestTile("Optimize PC", "Clear junk and tune it up", "Soon", ComingSoonCommand),
            new GuestTile("Local AI", "Runs on this PC, stored on the flash", "Soon", ComingSoonCommand),
            new GuestTile("Virus check", "Scan this PC with Windows Defender", "Soon", ComingSoonCommand),
        ];
    }

    public IReadOnlyList<GuestTile> Tiles { get; }

    public FilesPageViewModel Shared { get; }

    public ObservableCollection<SpaceFileItem> BigFiles { get; } = [];

    public string DriveName => _s.Marker.DisplayName.Trim('[', ']', ' ');

    /// <summary>True when the owner is previewing the guest page from their own device.</summary>
    public bool IsPreview => _exitPreview is not null;

    public bool IsHome => Panel == GuestPanel.Home;

    public bool IsBigFiles => Panel == GuestPanel.BigFiles;

    public bool IsShared => Panel == GuestPanel.Shared;

    public bool HasStatus => !string.IsNullOrEmpty(Status);

    [ObservableProperty]
    public partial GuestPanel Panel { get; set; } = GuestPanel.Home;

    [ObservableProperty]
    public partial bool IsScanning { get; set; }

    [ObservableProperty]
    public partial string ScanText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string? Status { get; set; }

    /// <summary>Finishes when the current big-files scan is done (used by tests).</summary>
    public Task Pending { get; private set; } = Task.CompletedTask;

    partial void OnPanelChanged(GuestPanel value)
    {
        Status = null;
        OnPropertyChanged(nameof(IsHome));
        OnPropertyChanged(nameof(IsBigFiles));
        OnPropertyChanged(nameof(IsShared));
    }

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
    private void ShowBigFiles()
    {
        Panel = GuestPanel.BigFiles;
        Pending = ScanAsync();
    }

    [RelayCommand]
    private void ShowShared() => Panel = GuestPanel.Shared;

    [RelayCommand]
    private void ComingSoon() => Status = "Coming soon.";

    [RelayCommand]
    private void OpenBigFile(SpaceFileItem file) => _s.Shell.Reveal(file.Path);

    private async Task ScanAsync()
    {
        BigFiles.Clear();
        IsScanning = true;
        ScanText = "Scanning this PC…";
        try
        {
            var files = await BigFilesScanner.ScanAsync(BigFilesScanner.UserRoots(), top: 30);
            foreach (var file in files)
            {
                BigFiles.Add(new SpaceFileItem(file.FullPath, file.Name, Format.Bytes(file.Size ?? 0), Folder(file.FullPath)));
            }

            ScanText = BigFiles.Count == 0
                ? "Nothing found in the usual folders."
                : string.Create(CultureInfo.CurrentCulture, $"Top {BigFiles.Count} by size in your folders");
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
