using Avalonia.Threading;
using Azariah.Core.Drive;
using Azariah.Core.Files;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Azariah.App.ViewModels;

/// <summary>Shown when no initialized Azariah drive was found, or a fresh drive needs setting up.</summary>
public sealed partial class WelcomeViewModel : ViewModelBase
{
    private readonly MainViewModel _main;
    private readonly IVolumeProvider _volumes;
    private readonly DriveRootLocator _locator;
    private readonly DispatcherTimer _scan;

    public WelcomeViewModel(MainViewModel main, LocateResult located, IVolumeProvider volumes, DriveRootLocator locator)
    {
        ArgumentNullException.ThrowIfNull(located);
        _main = main;
        _volumes = volumes;
        _locator = locator;
        Apply(located);

        // Keep looking in the background so plugging the drive in "just works".
        _scan = new DispatcherTimer(TimeSpan.FromSeconds(2), DispatcherPriority.Background, (_, _) => ScanTick());
        _scan.Start();
    }

    [ObservableProperty]
    public partial string? CandidateRoot { get; set; }

    [ObservableProperty]
    public partial bool NeedsSetup { get; set; }

    [ObservableProperty]
    public partial string DriveName { get; set; } = "AZARIAH";

    [ObservableProperty]
    public partial string? Error { get; set; }

    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    public List<DriveCandidate> Candidates { get; } = [];

    public bool HasCandidates => Candidates.Count > 1;

    public string Headline => NeedsSetup ? "Set up this drive" : HasCandidates ? "Pick your drive" : "Plug in your drive";

    public string Explanation => NeedsSetup
        ? "AZARIAH will create its folders here: Files, Roblox, Transfer, Public and Setup Kit, plus a small hidden .azariah folder for its own data. Nothing that's already on the drive is touched."
        : HasCandidates
            ? "More than one AZARIAH drive is connected. Choose the one to open."
            : "No AZARIAH drive is connected. Plug it in and this screen will pick it up automatically, or choose a drive or folder yourself.";

    [RelayCommand]
    private void SetUp()
    {
        if (CandidateRoot is null)
        {
            return;
        }

        var name = DriveName.Trim();
        if (name.Length == 0)
        {
            Error = "Give your drive a name.";
            return;
        }

        try
        {
            IsBusy = true;
            var marker = new DriveInitializer(_volumes).Initialize(CandidateRoot, name);
            _scan.Stop();
            _main.OpenWorkspace(CandidateRoot, marker);
        }
        catch (Exception ex)
        {
            Error = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ChooseFolder()
    {
        var folder = await _main.Ui.PickFolderAsync("Choose your AZARIAH drive (or a folder to use as one)");
        if (folder is null)
        {
            return;
        }

        var root = PathGuard.Normalize(folder);
        if (new DriveInitializer(_volumes).IsSystemDrive(root))
        {
            Error = "That's your Windows system drive. Choose your USB drive or a folder instead.";
            return;
        }

        if (DriveMarkerStore.TryRead(root) is { } marker)
        {
            _scan.Stop();
            _main.OpenWorkspace(root, marker);
            return;
        }

        Error = null;
        CandidateRoot = root;
        NeedsSetup = true;
        RaiseText();
    }

    [RelayCommand]
    private void Open(DriveCandidate candidate)
    {
        _scan.Stop();
        _main.OpenWorkspace(candidate.Root, candidate.Marker);
    }

    private void ScanTick()
    {
        if (NeedsSetup)
        {
            return;
        }

        var found = _locator.ScanVolumes();
        if (found.Count == 1)
        {
            _scan.Stop();
            _main.OpenWorkspace(found[0].Root, found[0].Marker);
        }
        else if (found.Count != Candidates.Count)
        {
            Candidates.Clear();
            Candidates.AddRange(found);
            RaiseText();
        }
    }

    private void Apply(LocateResult located)
    {
        CandidateRoot = located.Root;
        NeedsSetup = located.Outcome == LocateOutcome.NeedsInitialization;
        Candidates.AddRange(located.Candidates);
        RaiseText();
    }

    private void RaiseText()
    {
        OnPropertyChanged(nameof(Headline));
        OnPropertyChanged(nameof(Explanation));
        OnPropertyChanged(nameof(HasCandidates));
        OnPropertyChanged(nameof(Candidates));
    }
}
