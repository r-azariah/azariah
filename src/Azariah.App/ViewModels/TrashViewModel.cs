using System.Collections.ObjectModel;
using Azariah.App.Services;
using Azariah.Core.Files;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Material.Icons;

namespace Azariah.App.ViewModels;

public sealed class TrashItemViewModel(TrashEntry entry)
{
    public TrashEntry Entry { get; } = entry;
    public string Name => Entry.Name;
    public string OriginalLocation => Path.GetDirectoryName(Entry.Info.OriginalRelativePath) is { Length: > 0 } dir ? dir : "Drive";
    public string DeletedText => Format.Ago(Entry.Info.DeletedUtc.UtcDateTime);
    public string SizeText => Format.Bytes(Entry.Info.SizeBytes);
    public MaterialIconKind Icon => FileItemViewModel.IconFor(Entry.Info.IsDirectory ? FileKind.Folder : FileKinds.FromPath(Entry.Name));
}

public sealed partial class TrashViewModel : ViewModelBase
{
    private readonly WorkspaceSession _s;
    private readonly WorkspaceViewModel _workspace;

    public TrashViewModel(WorkspaceSession session, WorkspaceViewModel workspace)
    {
        _s = session;
        _workspace = workspace;
        Refresh();
    }

    public ObservableCollection<TrashItemViewModel> Items { get; } = [];

    [ObservableProperty]
    public partial TrashItemViewModel? Selected { get; set; }

    [ObservableProperty]
    public partial string Summary { get; set; } = string.Empty;

    public bool IsEmpty => Items.Count == 0;

    [RelayCommand]
    private void Refresh()
    {
        Items.Clear();
        foreach (var entry in _s.Trash.List())
        {
            Items.Add(new TrashItemViewModel(entry));
        }

        var total = Items.Sum(i => i.Entry.Info.SizeBytes);
        Summary = Items.Count == 0 ? "Trash is empty." : $"{Items.Count} item(s)  ·  {Format.Bytes(total)}";
        OnPropertyChanged(nameof(IsEmpty));
    }

    [RelayCommand]
    private async Task Restore(TrashItemViewModel? item)
    {
        item ??= Selected;
        if (item is null)
        {
            return;
        }

        try
        {
            var restored = _s.Trash.Restore(item.Entry);
            Refresh();
            var ok = await _s.Dialogs.ConfirmAsync("Restored", $"\"{Path.GetFileName(restored)}\" is back.", "Show it", details: null);
            if (ok)
            {
                _workspace.OpenInFiles(Path.GetDirectoryName(restored)!);
            }
        }
        catch (Exception ex)
        {
            await _s.Dialogs.AlertAsync("Couldn't restore", OperationReport.Describe(ex));
        }
    }

    [RelayCommand]
    private async Task DeleteForever(TrashItemViewModel? item)
    {
        item ??= Selected;
        if (item is null)
        {
            return;
        }

        var ok = await _s.Dialogs.ConfirmAsync("Delete forever?", $"\"{item.Name}\" will be permanently deleted. This can't be undone.", "Delete forever", danger: true);
        if (!ok)
        {
            return;
        }

        try
        {
            _s.Trash.DeletePermanently(item.Entry);
        }
        catch (Exception ex)
        {
            await _s.Dialogs.AlertAsync("Couldn't delete", OperationReport.Describe(ex));
        }

        Refresh();
    }

    [RelayCommand]
    private async Task EmptyTrash()
    {
        if (Items.Count == 0)
        {
            return;
        }

        var ok = await _s.Dialogs.ConfirmAsync("Empty Trash?", $"Permanently delete all {Items.Count} item(s)? This can't be undone.", "Empty Trash", danger: true);
        if (ok)
        {
            _s.Trash.Empty();
            Refresh();
        }
    }

    [RelayCommand]
    private void Back() => _workspace.Navigate("files");
}
