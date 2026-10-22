using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using Avalonia.Threading;
using Azariah.App.Platform;
using Azariah.App.Services;
using Azariah.Core.Devices;
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

    // TEST placeholder password (owner-chosen). It is compared in memory only and never stored on the
    // drive. Replace with an Argon2id verifier when file encryption lands; do not ship this as the real gate.
    private const string UnlockPassword = "88801550";

    /// <summary>Test mode (the owner previewing on their own device) ends on its own after this long.</summary>
    public static readonly TimeSpan TestDuration = TimeSpan.FromMinutes(15);

    private readonly WorkspaceSession _s;
    private readonly Action? _exitPreview;
    private readonly Action? _onUnlock;
    private readonly GuestLockStore _lockStore;
    private readonly StringBuilder _typed = new();
    private readonly DispatcherTimer _countdown;
    private readonly DispatcherTimer? _testTimer;
    private GuestLockState _lockState;
    private DateTimeOffset _testEnds;
    private bool _lastKeyTab;

    public GuestViewModel(WorkspaceSession session, Action? exitPreview = null, Action? onUnlock = null)
    {
        _s = session;
        _exitPreview = exitPreview;
        _onUnlock = onUnlock;
        _lockStore = new GuestLockStore(session.Layout);
        _countdown = new DispatcherTimer(TimeSpan.FromSeconds(1), DispatcherPriority.Background, (_, _) => TickCountdown());

        // Clear an expired lock on open; show the locked screen if still inside the window.
        var now = DateTimeOffset.Now;
        _lockState = GuestLock.Refresh(_lockStore.Load(), now);
        if (!IsPreview)
        {
            _lockStore.Save(_lockState);
        }

        if (GuestLock.IsLocked(_lockState, now))
        {
            EnterLockedUi();
        }

        // In test mode (owner preview), strikes and the locked screen run but nothing is ejected or saved
        // (IsPreview guards those), and the whole session ends on its own after TestDuration.
        if (IsPreview)
        {
            _testEnds = now + TestDuration;
            _testTimer = new DispatcherTimer(TimeSpan.FromSeconds(1), DispatcherPriority.Background, (_, _) => TickTest());
            _testTimer.Start();
            TickTest();
        }

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

    /// <summary>True while the owner is part-way through typing the unlock code (so Enter/Tab reset it).</summary>
    public bool IsEntering => _typed.Length > 0;

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

    /// <summary>After too many unlock attempts: the drive ejected and the guest page is locked.</summary>
    [ObservableProperty]
    public partial bool IsLocked { get; set; }

    [ObservableProperty]
    public partial string LockText { get; set; } = string.Empty;

    /// <summary>Time left in test mode, e.g. "14:32 left". Empty outside test mode.</summary>
    [ObservableProperty]
    public partial string TestLeftText { get; set; } = string.Empty;

    /// <summary>Finishes when the current storage scan is done (used by tests).</summary>
    public Task Pending { get; private set; } = Task.CompletedTask;

    /// <summary>A digit typed anywhere on the guest page builds the unlock code. Non-digits are ignored.</summary>
    public void TypeDigit(char digit)
    {
        if (IsLocked || !char.IsAsciiDigit(digit))
        {
            return;
        }

        _lastKeyTab = false;
        _typed.Append(digit);
        if (_typed.Length < UnlockPassword.Length)
        {
            return;
        }

        var attempt = _typed.ToString();
        _typed.Clear();
        if (string.Equals(attempt, UnlockPassword, StringComparison.Ordinal))
        {
            DoUnlock();
        }
        else
        {
            RegisterStrike();
        }
    }

    /// <summary>Enter resets the current entry so the owner can retype (counts as a strike).</summary>
    public void OnEnter()
    {
        _lastKeyTab = false;
        RegisterStrike();
    }

    /// <summary>Tab twice in a row resets the current entry (counts as a strike).</summary>
    public void OnTab()
    {
        if (_lastKeyTab)
        {
            _lastKeyTab = false;
            RegisterStrike();
        }
        else
        {
            _lastKeyTab = true;
        }
    }

    private void DoUnlock()
    {
        _typed.Clear();
        StopTimers();
        if (!IsPreview)
        {
            _lockStore.Save(GuestLock.Unlock());
        }

        _onUnlock?.Invoke();
    }

    private void RegisterStrike()
    {
        if (IsLocked)
        {
            return;
        }

        _typed.Clear();
        var now = DateTimeOffset.Now;
        _lockState = GuestLock.Strike(_lockState, now);
        if (!IsPreview)
        {
            _lockStore.Save(_lockState);
        }

        if (GuestLock.IsLocked(_lockState, now))
        {
            if (!IsPreview)
            {
                // Lock time is already written; ejecting closes this session (we run from the PC copy).
                UsbEject.TryEject(_s.Layout.Root);
            }

            EnterLockedUi();
        }
    }

    private void EnterLockedUi()
    {
        IsLocked = true;
        Panel = GuestPanel.Home;
        Opened = null;
        TickCountdown();
        _countdown.Start();
    }

    private void TickCountdown()
    {
        var remaining = GuestLock.Remaining(_lockState, DateTimeOffset.Now);
        if (remaining <= TimeSpan.Zero)
        {
            _countdown.Stop();
            _lockState = GuestLock.Unlock();
            if (!IsPreview)
            {
                _lockStore.Save(_lockState);
            }

            IsLocked = false;
            return;
        }

        LockText = string.Create(CultureInfo.CurrentCulture, $"Try again in {remaining.Minutes}:{remaining.Seconds:D2}.");
    }

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
    private void ExitPreview() => LeaveTestMode();

    private void LeaveTestMode()
    {
        StopTimers();
        _exitPreview?.Invoke();
    }

    private void StopTimers()
    {
        _countdown.Stop();
        _testTimer?.Stop();
    }

    private void TickTest()
    {
        var remaining = _testEnds - DateTimeOffset.Now;
        if (remaining <= TimeSpan.Zero)
        {
            LeaveTestMode();
            return;
        }

        TestLeftText = string.Create(CultureInfo.CurrentCulture, $"{(int)remaining.TotalMinutes}:{remaining.Seconds:D2} left");
    }

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
