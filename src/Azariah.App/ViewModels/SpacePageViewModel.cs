using System.Collections.ObjectModel;
using System.Globalization;
using Azariah.App.Services;
using Azariah.Core.Drive;
using Azariah.Core.Files;
using Azariah.Core.Space;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Azariah.App.ViewModels;

public sealed record SpaceFolderItem(string Path, string Name, string SizeText, string CountText, double Percent);

public sealed record SpaceFileItem(string Path, string Name, string SizeText, string LocationText);

public sealed record DuplicateItem(DuplicateGroup Group, string Name, string CopiesText, string WastedText, string LocationsText);

/// <summary>Where the drive's space goes: folders, biggest files, duplicates, and what's sitting in Trash.</summary>
public sealed partial class SpacePageViewModel : ViewModelBase
{
    private readonly WorkspaceSession _s;
    private readonly WorkspaceViewModel _workspace;
    private CancellationTokenSource? _cts;

    public SpacePageViewModel(WorkspaceSession session, WorkspaceViewModel workspace)
    {
        _s = session;
        _workspace = workspace;
    }

    public ObservableCollection<SpaceFolderItem> Folders { get; } = [];
    public ObservableCollection<SpaceFileItem> BiggestFiles { get; } = [];
    public ObservableCollection<DuplicateItem> Duplicates { get; } = [];

    public bool HasDuplicates => Duplicates.Count > 0;

    public bool HasStatus => !string.IsNullOrEmpty(Status);

    [ObservableProperty]
    public partial bool IsScanning { get; set; }

    [ObservableProperty]
    public partial string UsageText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial double UsedPercent { get; set; }

    [ObservableProperty]
    public partial string ScanText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string DuplicatesTitle { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string TrashText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string? Status { get; set; }

    /// <summary>Finishes when the current scan is done (used by tests).</summary>
    public Task Pending { get; private set; } = Task.CompletedTask;

    partial void OnStatusChanged(string? value) => OnPropertyChanged(nameof(HasStatus));

    [RelayCommand]
    public void Rescan() => Pending = ScanAsync();

    private async Task ScanAsync()
    {
        _cts?.Cancel();
        var cts = _cts = new CancellationTokenSource();
        var summary = DriveSummary.For(_s.Layout.Root, _s.Volumes);
        UsageText = summary.TotalBytes > 0
            ? $"{Format.Bytes(summary.UsedBytes)} used of {Format.Bytes(summary.TotalBytes)}  ·  {Format.Bytes(summary.FreeBytes)} free"
            : string.Empty;
        UsedPercent = summary.UsedFraction * 100;
        IsScanning = true;
        ScanText = "Scanning…";
        try
        {
            var report = await SpaceScanner.ScanAsync(_s.Layout, topFiles: 15, ct: cts.Token);
            var trash = await Task.Run(() => FolderBytes(_s.Layout.TrashFolder), cts.Token);
            Apply(report, trash);
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            if (ReferenceEquals(cts, _cts))
            {
                IsScanning = false;
            }
        }
    }

    private void Apply(SpaceReport report, long trashBytes)
    {
        var layout = _s.Layout;
        var max = report.Folders.Count > 0 ? Math.Max(1, report.Folders[0].Bytes) : 1;
        Folders.Clear();
        foreach (var folder in report.Folders)
        {
            Folders.Add(new SpaceFolderItem(folder.Path, folder.Name, Format.Bytes(folder.Bytes),
                folder.FileCount == 1 ? "1 file" : string.Create(CultureInfo.CurrentCulture, $"{folder.FileCount:N0} files"),
                100.0 * folder.Bytes / max));
        }

        BiggestFiles.Clear();
        foreach (var file in report.BiggestFiles)
        {
            BiggestFiles.Add(new SpaceFileItem(file.FullPath, file.Name, Format.Bytes(file.Size ?? 0), Where(layout, file.FullPath)));
        }

        Duplicates.Clear();
        foreach (var group in report.Duplicates)
        {
            Duplicates.Add(new DuplicateItem(
                group,
                Path.GetFileName(group.Paths[0]),
                string.Create(CultureInfo.CurrentCulture, $"{group.Paths.Count} copies"),
                $"{Format.Bytes(group.WastedBytes)} extra",
                string.Join("  ·  ", group.Paths.Select(p => Where(layout, p)))));
        }

        DuplicatesTitle = $"Duplicates  ·  {Format.Bytes(report.WastedBytes)} extra";
        TrashText = trashBytes > 0 ? Format.Bytes(trashBytes) : "Empty";
        ScanText = string.Create(CultureInfo.CurrentCulture, $"{report.TotalFiles:N0} files  ·  {Format.Bytes(report.TotalBytes)}");
        OnPropertyChanged(nameof(HasDuplicates));
    }

    [RelayCommand]
    private void OpenFolder(SpaceFolderItem folder) =>
        _workspace.OpenInFiles(Directory.Exists(folder.Path) ? folder.Path : _s.Layout.Root);

    [RelayCommand]
    private void ShowFile(SpaceFileItem file) =>
        _workspace.OpenInFiles(Path.GetDirectoryName(file.Path) ?? _s.Layout.Root);

    [RelayCommand]
    private async Task TrashFile(SpaceFileItem file)
    {
        var ok = await _s.Dialogs.ConfirmAsync(
            "Move to Trash?",
            $"Move \"{file.Name}\" ({file.SizeText}) to Trash? The space comes back when you empty Trash.",
            "Move to Trash",
            danger: true);
        if (ok)
        {
            await TrashAsync([file.Path]);
        }
    }

    [RelayCommand]
    private async Task KeepOne(DuplicateItem duplicate)
    {
        var extra = duplicate.Group.Paths.Skip(1).ToList();
        var ok = await _s.Dialogs.ConfirmAsync(
            "Keep one copy?",
            $"Keep {Where(_s.Layout, duplicate.Group.Paths[0])}\\{duplicate.Name} and move the other {extra.Count} to Trash.",
            "Move copies to Trash",
            danger: true);
        if (ok)
        {
            await TrashAsync(extra);
        }
    }

    [RelayCommand]
    private void OpenTrash() => _workspace.OpenTrash();

    private async Task TrashAsync(IReadOnlyList<string> paths)
    {
        var report = await _s.Files.MoveToTrashAsync(paths);
        Status = report.HasErrors ? report.Errors[0].Message : null;
        Rescan();
    }

    private static string Where(DriveLayout layout, string path)
    {
        var parent = Path.GetDirectoryName(path);
        if (parent is null || !layout.Contains(parent))
        {
            return parent ?? string.Empty;
        }

        var relative = layout.ToRelative(parent);
        return relative == "." ? "Drive" : relative;
    }

    private static long FolderBytes(string folder)
    {
        if (!Directory.Exists(folder))
        {
            return 0;
        }

        try
        {
            var options = new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true, AttributesToSkip = FileAttributes.ReparsePoint };
            return new DirectoryInfo(folder).EnumerateFiles("*", options).Sum(f => f.Length);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return 0;
        }
    }
}
