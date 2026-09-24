using System.Collections.ObjectModel;
using Azariah.App.Services;
using Azariah.Core.Files;
using Azariah.Core.Notes;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Azariah.App.ViewModels;

public sealed partial class NoteItemViewModel(NoteInfo info) : ObservableObject
{
    public NoteInfo Info { get; private set; } = info;
    public string Path => Info.Path;
    public string Title => Info.Title;
    public string Preview => Info.Preview;
    public bool HasPreview => Preview.Length > 0;
    public string Detail => Info.Folder.Length > 0 ? $"{Info.Folder}  ·  {Format.Ago(Info.ModifiedUtc)}" : Format.Ago(Info.ModifiedUtc);

    public void Update(NoteInfo info)
    {
        Info = info;
        OnPropertyChanged(string.Empty);
    }
}

/// <summary>
/// Notes: Markdown files in <c>Notes\</c> (Obsidian's layout). Typing saves on its own after a short
/// pause, and whenever you switch notes or pages. The file name is the title, as in Obsidian.
/// </summary>
public sealed partial class NotesPageViewModel : ViewModelBase
{
    private static readonly TimeSpan SaveDelay = TimeSpan.FromMilliseconds(400);

    private readonly WorkspaceSession _s;
    private readonly WorkspaceViewModel _workspace;
    private CancellationTokenSource? _saveCts;
    private string? _currentPath;
    private string _savedText = string.Empty;
    private bool _loading;

    public NotesPageViewModel(WorkspaceSession session, WorkspaceViewModel workspace)
    {
        _s = session;
        _workspace = workspace;
        Refresh();
    }

    public ObservableCollection<NoteItemViewModel> Notes { get; } = [];

    public bool HasNotes => Notes.Count > 0;

    public bool HasSelection => Selected is not null;

    public bool HasStatus => !string.IsNullOrEmpty(Status);

    [ObservableProperty]
    public partial NoteItemViewModel? Selected { get; set; }

    [ObservableProperty]
    public partial string Text { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Title { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Search { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string? Status { get; set; }

    /// <summary>A new note wants its title typed; a daily note wants the cursor at the end.</summary>
    public event EventHandler? TitleFocusRequested;

    public event EventHandler? EditorFocusRequested;

    public void Refresh()
    {
        Flush();
        var keep = _currentPath;
        IReadOnlyList<NoteInfo> list;
        try
        {
            list = Search.Trim().Length > 0 ? _s.Notes.Search(Search) : _s.Notes.List();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            list = [];
        }

        Notes.Clear();
        foreach (var note in list)
        {
            Notes.Add(new NoteItemViewModel(note));
        }

        Selected = Notes.FirstOrDefault(n => keep is not null && PathGuard.AreSame(n.Path, keep)) ?? Notes.FirstOrDefault();
        OnPropertyChanged(nameof(HasNotes));
    }

    public void Select(string path)
    {
        var item = Notes.FirstOrDefault(n => PathGuard.AreSame(n.Path, path));
        if (item is null)
        {
            _loading = true;
            Search = string.Empty;
            _loading = false;
            Refresh();
            item = Notes.FirstOrDefault(n => PathGuard.AreSame(n.Path, path));
        }

        Selected = item ?? Selected;
    }

    /// <summary>Writes unsaved typing now (switching notes or pages, closing the editor).</summary>
    public void Flush()
    {
        _saveCts?.Cancel();
        if (_currentPath is null || string.Equals(Text, _savedText, StringComparison.Ordinal))
        {
            return;
        }

        try
        {
            _s.Notes.Write(_currentPath, Text);
            _savedText = Text;
            var path = _currentPath;
            if (Notes.FirstOrDefault(n => PathGuard.AreSame(n.Path, path)) is { } item && _s.Notes.Describe(path) is { } info)
            {
                item.Update(info);
            }
        }
        catch (Exception ex)
        {
            Status = $"Couldn't save: {OperationReport.Describe(ex)}";
        }
    }

    partial void OnSelectedChanged(NoteItemViewModel? value)
    {
        Flush();
        _loading = true;
        try
        {
            _currentPath = value?.Path;
            Title = value?.Title ?? string.Empty;
            Text = value is null ? string.Empty : ReadSafe(value.Path);
            _savedText = Text;
            Status = null;
        }
        finally
        {
            _loading = false;
        }

        OnPropertyChanged(nameof(HasSelection));
    }

    partial void OnTextChanged(string value)
    {
        if (_loading || _currentPath is null)
        {
            return;
        }

        _saveCts?.Cancel();
        var cts = _saveCts = new CancellationTokenSource();
        _ = SaveLaterAsync(cts.Token);
    }

    partial void OnSearchChanged(string value)
    {
        if (!_loading)
        {
            Refresh();
        }
    }

    partial void OnStatusChanged(string? value) => OnPropertyChanged(nameof(HasStatus));

    [RelayCommand]
    private void CommitTitle()
    {
        if (_currentPath is null || Selected is not { } note)
        {
            return;
        }

        var title = Title.Trim();
        if (title.Length == 0 || string.Equals(title, note.Title, StringComparison.Ordinal))
        {
            Title = note.Title;
            return;
        }

        Flush();
        try
        {
            _currentPath = _s.Notes.Rename(_currentPath, title);
            if (_s.Notes.Describe(_currentPath) is { } info)
            {
                note.Update(info);
            }

            Title = note.Title;
            Status = null;
        }
        catch (Exception ex)
        {
            Title = note.Title;
            Status = OperationReport.Describe(ex);
        }
    }

    [RelayCommand]
    public void NewNote()
    {
        try
        {
            Flush();
            var path = _s.Notes.Create();
            Select(path);
            TitleFocusRequested?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            Status = OperationReport.Describe(ex);
        }
    }

    [RelayCommand]
    public void Today()
    {
        try
        {
            Flush();
            var path = _s.Notes.OpenDaily(DateOnly.FromDateTime(DateTime.Now));
            Select(path);
            EditorFocusRequested?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            Status = OperationReport.Describe(ex);
        }
    }

    [RelayCommand]
    private async Task DeleteNote()
    {
        if (Selected is not { } note)
        {
            return;
        }

        var ok = await _s.Dialogs.ConfirmAsync(
            "Move to Trash?",
            $"Move \"{note.Title}\" to Trash? You can restore it from Settings > Trash.",
            "Move to Trash",
            danger: true);
        if (!ok)
        {
            return;
        }

        Flush();
        var report = await _s.Notes.MoveToTrashAsync(note.Path);
        _currentPath = null;
        Refresh();
        if (report.HasErrors)
        {
            Status = report.Errors[0].Message;
        }
    }

    [RelayCommand]
    private void OpenFolder()
    {
        Flush();
        if (Directory.Exists(_s.Notes.Folder))
        {
            _workspace.OpenInFiles(_s.Notes.Folder);
        }
        else
        {
            Status = "No notes yet.";
        }
    }

    private async Task SaveLaterAsync(CancellationToken ct)
    {
        try
        {
            await Task.Delay(SaveDelay, ct);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        Flush();
    }

    private string ReadSafe(string path)
    {
        try
        {
            return _s.Notes.Read(path);
        }
        catch (Exception ex)
        {
            Status = $"Couldn't open: {OperationReport.Describe(ex)}";
            return string.Empty;
        }
    }
}
