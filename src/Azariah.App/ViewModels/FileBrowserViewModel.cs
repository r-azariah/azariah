using System.Collections.ObjectModel;
using System.Security.Cryptography;
using Avalonia.Controls.Selection;
using Azariah.App.Services;
using Azariah.App.ViewModels.Dialogs;
using Azariah.Core.Files;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Azariah.App.ViewModels;

public enum SortColumn
{
    Name,
    Modified,
    Type,
    Size,
}

/// <summary>A reusable file browser bound to a root folder (the whole drive, Roblox, Transfer, ...).</summary>
public sealed partial class FileBrowserViewModel : ViewModelBase
{
    internal static readonly HashSet<string> RunnableExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".exe", ".msi", ".msix", ".msixbundle", ".appx", ".appxbundle", ".bat", ".cmd", ".ps1", ".vbs",
        ".vbe", ".js", ".jse", ".wsf", ".wsh", ".scr", ".com", ".lnk", ".hta", ".cpl", ".jar", ".reg",
    };

    private readonly WorkspaceSession _s;
    private readonly Stack<string> _back = new();
    private readonly Stack<string> _forward = new();
    private List<FileItemViewModel> _items = [];
    private CancellationTokenSource? _searchCts;
    private CancellationTokenSource? _opCts;
    private int _loadVersion;

    public FileBrowserViewModel(WorkspaceSession session, string rootPath, string rootLabel)
    {
        _s = session;
        RootPath = PathGuard.Normalize(rootPath);
        RootLabel = rootLabel;
        CurrentFolder = RootPath;
        Selection = new SelectionModel<FileItemViewModel> { SingleSelect = false };
        Selection.SelectionChanged += (_, _) => OnSelectionChanged();
        session.Clipboard.Changed += (_, _) => RefreshCommandStates();
        session.SettingsChanged += (_, _) => Refresh();
        Refresh();
    }

    public string RootPath { get; }
    public string RootLabel { get; }
    public ObservableCollection<FileItemViewModel> Items { get; } = [];
    public ObservableCollection<BreadcrumbItem> Breadcrumbs { get; } = [];
    public SelectionModel<FileItemViewModel> Selection { get; }

    [ObservableProperty]
    public partial string CurrentFolder { get; set; }

    [ObservableProperty]
    public partial string SearchText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsSearchResults { get; set; }

    [ObservableProperty]
    public partial bool IsSearching { get; set; }

    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    public partial double ProgressPercent { get; set; }

    [ObservableProperty]
    public partial string? ProgressText { get; set; }

    [ObservableProperty]
    public partial string? StatusMessage { get; set; }

    [ObservableProperty]
    public partial bool StatusIsError { get; set; }

    [ObservableProperty]
    public partial SortColumn Sort { get; set; } = SortColumn.Name;

    [ObservableProperty]
    public partial bool SortAscending { get; set; } = true;

    [ObservableProperty]
    public partial FileItemViewModel? Details { get; set; }

    [ObservableProperty]
    public partial bool ShowDetails { get; set; } = true;

    [ObservableProperty]
    public partial string? FilterLabel { get; set; }

    private IReadOnlySet<FileKind>? _kindFilter;

    public bool HasSelection => Selection.SelectedItems.Count > 0;
    public bool HasSingleSelection => Selection.SelectedItems.Count == 1;
    public bool CanGoBack => _back.Count > 0;
    public bool CanGoForward => _forward.Count > 0;
    public bool CanGoUp => !PathGuard.AreSame(CurrentFolder, RootPath) && !IsSearchResults;
    public bool IsEmpty => Items.Count == 0 && !IsSearching;
    public bool HasStatus => !string.IsNullOrEmpty(StatusMessage);

    public string EmptyText => IsSearchResults
        ? (IsSearching ? "Searching..." : "Nothing matched.")
        : "Empty folder. Drop files here.";

    public string ItemCountText
    {
        get
        {
            var count = Items.Count;
            var selected = Selection.SelectedItems.Count;
            var text = count == 1 ? "1 item" : $"{count} items";
            return selected > 0 ? $"{text}  ·  {selected} selected" : text;
        }
    }

    public IReadOnlyList<string> SelectedPaths => Selection.SelectedItems.OfType<FileItemViewModel>().Select(i => i.FullPath).ToList();

    public void NavigateTo(string folder) => NavigateCore(folder, addHistory: true);

    [RelayCommand]
    private void GoToBreadcrumb(BreadcrumbItem crumb) => NavigateTo(crumb.Path);

    [RelayCommand(CanExecute = nameof(CanGoBack))]
    private void Back()
    {
        _forward.Push(CurrentFolder);
        NavigateCore(_back.Pop(), addHistory: false);
    }

    [RelayCommand(CanExecute = nameof(CanGoForward))]
    private void Forward()
    {
        _back.Push(CurrentFolder);
        NavigateCore(_forward.Pop(), addHistory: false);
    }

    [RelayCommand(CanExecute = nameof(CanGoUp))]
    private void Up()
    {
        var parent = Path.GetDirectoryName(CurrentFolder);
        if (parent is not null && PathGuard.IsInsideOrEqual(RootPath, parent))
        {
            NavigateTo(parent);
        }
    }

    [RelayCommand]
    public void Refresh()
    {
        if (IsSearchResults && _kindFilter is null && SearchText.Length > 0)
        {
            _ = SearchAsync();
            return;
        }

        if (_kindFilter is not null)
        {
            _ = RunSearchAsync(string.Empty, _kindFilter);
            return;
        }

        _ = LoadAsync(CurrentFolder, null);
    }

    [RelayCommand]
    private async Task OpenItem(FileItemViewModel? item)
    {
        item ??= Selection.SelectedItem;
        if (item is null)
        {
            return;
        }

        if (item.IsProtected)
        {
            SetStatus("That area is managed by AZARIAH.", error: false);
            return;
        }

        if (item.IsFolder)
        {
            ExitSearchMode();
            NavigateTo(item.FullPath);
            return;
        }

        if (RunnableExtensions.Contains(Path.GetExtension(item.Name)))
        {
            var ok = await _s.Dialogs.ConfirmAsync(
                "Run this program?",
                $"\"{item.Name}\" will run on {_s.Shell.MachineDisplayName} with your permissions.",
                "Run",
                danger: true,
                details:
                [
                    new DetailRow("File", item.Name),
                    new DetailRow("Location", item.LocationText),
                    new DetailRow("Size", item.SizeText),
                ]);
            if (!ok)
            {
                return;
            }
        }

        try
        {
            _s.Shell.Open(item.FullPath);
            _s.Recent.Add(item.FullPath);
        }
        catch (Exception ex)
        {
            SetStatus($"Couldn't open \"{item.Name}\": {ex.Message}", error: true);
        }
    }

    [RelayCommand]
    private async Task NewFolder()
    {
        if (IsSearchResults)
        {
            ExitSearchMode();
        }

        var suggested = Path.GetFileName(UniqueNames.Next(Path.Combine(CurrentFolder, "New folder")));
        var name = await _s.Dialogs.PromptAsync("New folder", null, suggested, "Create", FileNameRules.Validate);
        if (name is null)
        {
            return;
        }

        try
        {
            var entry = _s.Files.CreateFolder(CurrentFolder, name);
            await LoadAsync(CurrentFolder, [entry.FullPath]);
        }
        catch (Exception ex)
        {
            SetStatus(OperationReport.Describe(ex), error: true);
        }
    }

    [RelayCommand(CanExecute = nameof(HasSingleSelection))]
    private async Task Rename()
    {
        if (Selection.SelectedItem is not { } item || item.IsProtected)
        {
            return;
        }

        var stem = item.IsFolder ? item.Name.Length : Path.GetFileNameWithoutExtension(item.Name).Length;
        var name = await _s.Dialogs.PromptAsync("Rename", null, item.Name, "Rename", FileNameRules.Validate, stem);
        if (name is null || name == item.Name)
        {
            return;
        }

        try
        {
            var newPath = _s.Files.Rename(item.FullPath, name);
            await LoadAsync(IsSearchResults ? CurrentFolder : Path.GetDirectoryName(newPath)!, [newPath]);
        }
        catch (Exception ex)
        {
            SetStatus(OperationReport.Describe(ex), error: true);
        }
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task Copy()
    {
        var paths = SelectedPaths;
        _s.Clipboard.Set(paths, cut: false);
        await _s.Ui.SetClipboardFilesAsync(paths);
        SetStatus(paths.Count == 1 ? "Copied 1 item." : $"Copied {paths.Count} items.");
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void Cut()
    {
        var paths = SelectedPaths.Where(p => !_s.Layout.IsProtected(p)).ToList();
        _s.Clipboard.Set(paths, cut: true);
        SetStatus(paths.Count == 1 ? "Cut 1 item. Paste it somewhere to move it." : $"Cut {paths.Count} items. Paste to move them.");
    }

    [RelayCommand]
    private async Task Paste()
    {
        var target = IsSearchResults ? RootPath : CurrentFolder;
        if (_s.Clipboard.HasItems)
        {
            var cut = _s.Clipboard.IsCut;
            var paths = _s.Clipboard.Paths;
            await TransferAsync(paths, target, move: cut);
            if (cut)
            {
                _s.Clipboard.Clear();
            }

            return;
        }

        var external = await _s.Ui.GetClipboardFilesAsync();
        if (external.Count > 0)
        {
            await TransferAsync(external, target, move: false);
        }
        else
        {
            SetStatus("Nothing to paste.");
        }
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task Delete()
    {
        var paths = SelectedPaths.Where(p => !_s.Layout.IsProtected(p)).ToList();
        if (paths.Count == 0)
        {
            return;
        }

        if (_s.Settings.ConfirmMoveToTrash)
        {
            var what = paths.Count == 1 ? $"\"{Path.GetFileName(paths[0])}\"" : $"{paths.Count} items";
            var ok = await _s.Dialogs.ConfirmAsync(
                "Move to Trash?",
                $"Move {what} to Trash? You can restore it from Settings > Trash.",
                "Move to Trash",
                danger: true);
            if (!ok)
            {
                return;
            }
        }

        var report = await _s.Files.MoveToTrashAsync(paths);
        await ShowReportAsync(report, "Moved to Trash", "moved to Trash");
        Refresh();
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void Reveal()
    {
        if (Selection.SelectedItem is { } item)
        {
            try
            {
                _s.Shell.Reveal(item.FullPath);
            }
            catch (Exception ex)
            {
                SetStatus(ex.Message, error: true);
            }
        }
    }

    [RelayCommand]
    private void OpenCurrentInExplorer()
    {
        try
        {
            _s.Shell.Open(CurrentFolder);
        }
        catch (Exception ex)
        {
            SetStatus(ex.Message, error: true);
        }
    }

    [RelayCommand]
    public async Task SearchAsync()
    {
        var text = SearchText.Trim();
        if (text.Length == 0)
        {
            ClearSearch();
            return;
        }

        _kindFilter = null;
        FilterLabel = null;
        await RunSearchAsync(text, null);
    }

    [RelayCommand]
    public void ClearSearch()
    {
        _searchCts?.Cancel();
        SearchText = string.Empty;
        _kindFilter = null;
        FilterLabel = null;
        if (IsSearchResults)
        {
            IsSearchResults = false;
            _ = LoadAsync(CurrentFolder, null);
        }
    }

    /// <summary>Shows every file of the given kinds under this browser's root (e.g. all Roblox places).</summary>
    public void ApplyKindFilter(string? label, IReadOnlySet<FileKind>? kinds)
    {
        if (kinds is null || kinds.Count == 0)
        {
            ClearSearch();
            return;
        }

        SearchText = string.Empty;
        _kindFilter = kinds;
        FilterLabel = label;
        _ = RunSearchAsync(string.Empty, kinds);
    }

    [RelayCommand]
    private void SortBy(SortColumn column)
    {
        if (Sort == column)
        {
            SortAscending = !SortAscending;
        }
        else
        {
            Sort = column;
            SortAscending = true;
        }

        ApplySort(SelectedPaths);
    }

    [RelayCommand]
    private void CancelOperation() => _opCts?.Cancel();

    [RelayCommand]
    private void ToggleDetails() => ShowDetails = !ShowDetails;

    [RelayCommand]
    private void DismissStatus() => StatusMessage = null;

    [RelayCommand]
    private async Task ComputeHash()
    {
        if (Details is not { IsFolder: false } item || item.IsHashing)
        {
            return;
        }

        try
        {
            item.IsHashing = true;
            await using var stream = new FileStream(item.FullPath, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 20, FileOptions.Asynchronous | FileOptions.SequentialScan);
            var hash = await SHA256.HashDataAsync(stream);
            item.Sha256 = Convert.ToHexStringLower(hash);
        }
        catch (Exception ex)
        {
            SetStatus($"Couldn't hash the file: {OperationReport.Describe(ex)}", error: true);
        }
        finally
        {
            item.IsHashing = false;
        }
    }

    [RelayCommand]
    private async Task ImportFromPc()
    {
        var files = await _s.Ui.PickFilesAsync("Choose files to copy onto your drive");
        if (files.Count > 0)
        {
            await TransferAsync(files, IsSearchResults ? RootPath : CurrentFolder, move: false);
        }
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task ExportToPc()
    {
        var paths = SelectedPaths;
        var folder = await _s.Ui.PickFolderAsync("Choose where to copy the selected items on this PC");
        if (folder is null)
        {
            return;
        }

        var policy = await ResolveConflictsAsync(paths, folder, checkInsideRoot: false);
        if (policy is null)
        {
            return;
        }

        await RunOperationAsync("Copying to this PC", (p, ct) => _s.Files.ExportAsync(paths, folder, policy.Value, p, ct), "Copied to this PC", "copied");
    }

    /// <summary>Handles drops and pastes. Items already on the drive move by default; everything else copies.</summary>
    public async Task TransferAsync(IReadOnlyList<string> sources, string targetFolder, bool move)
    {
        if (sources.Count == 0)
        {
            return;
        }

        var policy = await ResolveConflictsAsync(sources, targetFolder, checkInsideRoot: true);
        if (policy is null)
        {
            return;
        }

        if (move)
        {
            await RunOperationAsync("Moving", (p, ct) => _s.Files.MoveAsync(sources, targetFolder, policy.Value, p, ct), "Moved", "moved");
        }
        else
        {
            await RunOperationAsync("Copying", (p, ct) => _s.Files.CopyAsync(sources, targetFolder, policy.Value, p, ct), "Copied", "copied");
        }
    }

    public bool IsOnDrive(string path) => _s.Layout.Contains(path);

    private async Task<ConflictPolicy?> ResolveConflictsAsync(IReadOnlyList<string> sources, string dest, bool checkInsideRoot)
    {
        IReadOnlyList<string> conflicts;
        try
        {
            conflicts = checkInsideRoot
                ? _s.Files.FindConflicts(sources, dest)
                : sources.Select(Path.GetFileName).OfType<string>().Where(n => UniqueNames.Exists(Path.Combine(dest, n))).ToList();
        }
        catch (Exception ex)
        {
            SetStatus(OperationReport.Describe(ex), error: true);
            return null;
        }

        if (conflicts.Count == 0)
        {
            return ConflictPolicy.KeepBoth;
        }

        var message = conflicts.Count == 1
            ? $"\"{conflicts[0]}\" already exists in this folder."
            : $"{conflicts.Count} items already exist in this folder.";
        var choice = await _s.Dialogs.ChooseAsync(
            "Already exists",
            message,
            new ChoiceOption("keep", "Keep both", IsPrimary: true),
            new ChoiceOption("replace", "Replace"),
            new ChoiceOption("skip", "Skip"));
        return choice switch
        {
            "keep" => ConflictPolicy.KeepBoth,
            "replace" => ConflictPolicy.Replace,
            "skip" => ConflictPolicy.Skip,
            _ => null,
        };
    }

    private async Task RunOperationAsync(
        string label,
        Func<IProgress<FileProgress>, CancellationToken, Task<OperationReport>> operation,
        string doneTitle,
        string doneVerb)
    {
        if (IsBusy)
        {
            SetStatus("Another operation is still running.", error: true);
            return;
        }

        _opCts = new CancellationTokenSource();
        IsBusy = true;
        ProgressPercent = 0;
        ProgressText = $"{label}...";
        var progress = new Progress<FileProgress>(p =>
        {
            ProgressPercent = p.Fraction * 100;
            ProgressText = p.BytesTotal > 0
                ? $"{label} {p.CurrentItem}  ·  {Format.Bytes(p.BytesDone)} of {Format.Bytes(p.BytesTotal)}"
                : $"{label} {p.CurrentItem}";
        });

        try
        {
            var report = await operation(progress, _opCts.Token);
            await ShowReportAsync(report, doneTitle, doneVerb);
            await LoadAsync(CurrentFolder, report.CreatedPaths);
        }
        catch (Exception ex)
        {
            SetStatus(OperationReport.Describe(ex), error: true);
        }
        finally
        {
            IsBusy = false;
            ProgressText = null;
            _opCts.Dispose();
            _opCts = null;
        }
    }

    private async Task ShowReportAsync(OperationReport report, string title, string verb)
    {
        if (report.Cancelled)
        {
            SetStatus($"Cancelled. {report.Succeeded} item(s) were {verb} before stopping.", error: true);
        }
        else if (report.HasErrors)
        {
            SetStatus($"{report.Succeeded} {verb}, {report.Errors.Count} failed.", error: true);
            var rows = report.Errors.Take(6)
                .Select(e => new DetailRow(Path.GetFileName(e.Path), e.Message, IsWarning: true))
                .ToList();
            await _s.Dialogs.AlertAsync($"{title} with problems", $"{report.Errors.Count} item(s) couldn't be {verb}.", rows);
        }
        else
        {
            var skipped = report.Skipped > 0 ? $", {report.Skipped} skipped" : string.Empty;
            SetStatus($"{report.Succeeded} item(s) {verb}{skipped}.");
        }
    }

    private void NavigateCore(string folder, bool addHistory)
    {
        var target = PathGuard.Normalize(folder);
        if (!PathGuard.IsInsideOrEqual(RootPath, target) || _s.Layout.IsProtected(target))
        {
            target = RootPath;
        }

        if (addHistory && !PathGuard.AreSame(target, CurrentFolder))
        {
            _back.Push(CurrentFolder);
            _forward.Clear();
        }

        _searchCts?.Cancel();
        IsSearchResults = false;
        _kindFilter = null;
        FilterLabel = null;
        SearchText = string.Empty;
        CurrentFolder = target;
        _ = LoadAsync(target, null);
    }

    private void ExitSearchMode()
    {
        _searchCts?.Cancel();
        IsSearchResults = false;
        _kindFilter = null;
        FilterLabel = null;
        SearchText = string.Empty;
    }

    private async Task LoadAsync(string folder, IReadOnlyList<string>? select)
    {
        var version = ++_loadVersion;
        var showHidden = _s.Settings.ShowHiddenFiles;
        IReadOnlyList<FileEntry> entries;
        try
        {
            var target = folder;
            while (!Directory.Exists(target) && !PathGuard.AreSame(target, RootPath))
            {
                target = Path.GetDirectoryName(target) ?? RootPath;
            }

            if (!Directory.Exists(target))
            {
                Directory.CreateDirectory(target);
            }

            if (!PathGuard.AreSame(target, CurrentFolder))
            {
                CurrentFolder = target;
            }

            entries = await Task.Run(() => _s.Files.List(target, showHidden));
        }
        catch (Exception ex)
        {
            if (version == _loadVersion)
            {
                SetStatus($"Couldn't read this folder: {OperationReport.Describe(ex)}", error: true);
                Items.Clear();
                RaiseListState();
            }

            return;
        }

        if (version != _loadVersion || IsSearchResults)
        {
            return;
        }

        _items = entries.Select(e => new FileItemViewModel(e, _s.Layout)).ToList();
        ApplySort(select);
        UpdateBreadcrumbs();
    }

    private async Task RunSearchAsync(string text, IReadOnlySet<FileKind>? kinds)
    {
        _searchCts?.Cancel();
        var cts = _searchCts = new CancellationTokenSource();
        var scope = kinds is not null ? RootPath : CurrentFolder;
        IsSearchResults = true;
        IsSearching = true;
        Items.Clear();
        _items = [];
        RaiseListState();

        var batch = new List<FileItemViewModel>();
        try
        {
            var query = new SearchQuery(text, _s.Settings.ShowHiddenFiles, kinds);
            await foreach (var entry in _s.Search.SearchAsync(scope, query, cts.Token))
            {
                batch.Add(new FileItemViewModel(entry, _s.Layout, showLocation: true));
                if (batch.Count >= 64)
                {
                    Flush();
                }
            }

            Flush();
            var what = FilterLabel ?? $"\"{text}\"";
            SetStatus(_items.Count == 0 ? $"No results for {what}." : $"{_items.Count} result(s) for {what}.");
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            if (_searchCts == cts)
            {
                IsSearching = false;
                RaiseListState();
            }
        }

        void Flush()
        {
            if (cts.IsCancellationRequested)
            {
                return;
            }

            foreach (var item in batch)
            {
                _items.Add(item);
                Items.Add(item);
            }

            batch.Clear();
            RaiseListState();
        }
    }

    private void ApplySort(IEnumerable<string>? select)
    {
        IEnumerable<FileItemViewModel> ordered = Sort switch
        {
            SortColumn.Modified => _items.OrderBy(i => i.Entry.ModifiedUtc),
            SortColumn.Type => _items.OrderBy(i => i.TypeText, StringComparer.CurrentCultureIgnoreCase).ThenBy(i => i.Name, StringComparer.CurrentCultureIgnoreCase),
            SortColumn.Size => _items.OrderBy(i => i.Entry.Size ?? -1),
            _ => _items.OrderBy(i => i.Name, StringComparer.CurrentCultureIgnoreCase),
        };
        if (!SortAscending)
        {
            ordered = ordered.Reverse();
        }

        var list = ordered.OrderBy(i => i.IsFolder ? 0 : 1).ToList();
        Selection.Clear();
        Items.Clear();
        foreach (var item in list)
        {
            Items.Add(item);
        }

        if (select is not null)
        {
            var wanted = new HashSet<string>(select, PathGuard.Comparer);
            for (var i = 0; i < Items.Count; i++)
            {
                if (wanted.Contains(Items[i].FullPath))
                {
                    Selection.Select(i);
                }
            }
        }

        RaiseListState();
    }

    private void UpdateBreadcrumbs()
    {
        Breadcrumbs.Clear();
        var parts = new List<BreadcrumbItem>();
        var current = CurrentFolder;
        while (PathGuard.IsStrictlyInside(RootPath, current))
        {
            parts.Add(new BreadcrumbItem(Path.GetFileName(current), current, false));
            current = Path.GetDirectoryName(current)!;
        }

        parts.Add(new BreadcrumbItem(RootLabel, RootPath, false));
        parts.Reverse();
        for (var i = 0; i < parts.Count; i++)
        {
            Breadcrumbs.Add(parts[i] with { IsLast = i == parts.Count - 1 });
        }

        RefreshCommandStates();
    }

    private void OnSelectionChanged()
    {
        Details = Selection.SelectedItems.Count == 1 ? Selection.SelectedItem : null;
        RefreshCommandStates();
        OnPropertyChanged(nameof(ItemCountText));
    }

    private void RaiseListState()
    {
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(EmptyText));
        OnPropertyChanged(nameof(ItemCountText));
        RefreshCommandStates();
    }

    private void RefreshCommandStates()
    {
        OnPropertyChanged(nameof(HasSelection));
        OnPropertyChanged(nameof(HasSingleSelection));
        OnPropertyChanged(nameof(CanGoUp));
        BackCommand.NotifyCanExecuteChanged();
        ForwardCommand.NotifyCanExecuteChanged();
        UpCommand.NotifyCanExecuteChanged();
        RenameCommand.NotifyCanExecuteChanged();
        CopyCommand.NotifyCanExecuteChanged();
        CutCommand.NotifyCanExecuteChanged();
        DeleteCommand.NotifyCanExecuteChanged();
        RevealCommand.NotifyCanExecuteChanged();
        ExportToPcCommand.NotifyCanExecuteChanged();
    }

    partial void OnStatusMessageChanged(string? value) => OnPropertyChanged(nameof(HasStatus));

    private void SetStatus(string message, bool error = false)
    {
        StatusIsError = error;
        StatusMessage = message;
    }
}
