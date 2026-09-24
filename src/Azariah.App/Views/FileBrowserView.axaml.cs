using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;
using Azariah.App.ViewModels;

namespace Azariah.App.Views;

public partial class FileBrowserView : UserControl
{
    /// <summary>Paths being dragged from inside AZARIAH (in-process only).</summary>
    private static readonly DataFormat<string> InternalPaths = DataFormat.CreateInProcessFormat<string>("azariah.paths");

    private PointerPressedEventArgs? _pressed;
    private Point _pressPoint;
    private bool _dragging;

    public FileBrowserView()
    {
        InitializeComponent();
        List.DoubleTapped += OnDoubleTapped;
        List.KeyDown += OnListKeyDown;
        List.AddHandler(PointerPressedEvent, OnPointerPressed, RoutingStrategies.Tunnel | RoutingStrategies.Bubble, handledEventsToo: true);
        List.AddHandler(PointerMovedEvent, OnPointerMoved, RoutingStrategies.Tunnel | RoutingStrategies.Bubble, handledEventsToo: true);
        List.AddHandler(PointerReleasedEvent, (_, _) => _pressed = null, RoutingStrategies.Tunnel | RoutingStrategies.Bubble, handledEventsToo: true);
        List.AddHandler(DragDrop.DragOverEvent, OnDragOver);
        List.AddHandler(DragDrop.DropEvent, OnDrop);
    }

    private FileBrowserViewModel? Vm => DataContext as FileBrowserViewModel;

    /// <summary>Focus the list when the page opens so keyboard shortcuts work right away.</summary>
    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        Avalonia.Threading.DispatcherTimer.RunOnce(FocusList, TimeSpan.FromMilliseconds(150));
    }

    private void FocusList()
    {
        // ListBox passes focus to its items; with no items, focus the view so shortcuts still work.
        var index = Math.Max(List.SelectedIndex, 0);
        if (List.ContainerFromIndex(index) is { } container && container.Focus(NavigationMethod.Tab))
        {
            return;
        }

        Focus();
    }

    private void OnDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (ItemFromSource(e.Source) is { } item)
        {
            Vm?.OpenItemCommand.Execute(item);
        }
    }

    private void OnListKeyDown(object? sender, KeyEventArgs e)
    {
        if (Vm is not { } vm)
        {
            return;
        }

        if (e.Key == Key.Enter)
        {
            vm.OpenItemCommand.Execute(null);
            e.Handled = true;
        }
        else if (e.Key == Key.Back)
        {
            vm.UpCommand.Execute(null);
            e.Handled = true;
        }
        else if (e.Key == Key.F && e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            SearchBox.Focus();
            e.Handled = true;
        }
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(List).Properties.IsLeftButtonPressed && ItemFromSource(e.Source) is not null)
        {
            _pressed = e;
            _pressPoint = e.GetPosition(List);
        }
    }

    private async void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_pressed is null || _dragging || Vm is not { } vm || !e.GetCurrentPoint(List).Properties.IsLeftButtonPressed)
        {
            return;
        }

        var delta = e.GetPosition(List) - _pressPoint;
        if (Math.Abs(delta.X) < 8 && Math.Abs(delta.Y) < 8)
        {
            return;
        }

        var paths = vm.SelectedPaths;
        if (paths.Count == 0)
        {
            return;
        }

        _dragging = true;
        try
        {
            var data = new DataTransfer();
            data.Add(DataTransferItem.Create(InternalPaths, string.Join('\n', paths)));
            await DragDrop.DoDragDropAsync(_pressed, data, DragDropEffects.Move | DragDropEffects.Copy);
        }
        finally
        {
            _dragging = false;
            _pressed = null;
        }
    }

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        var copy = e.KeyModifiers.HasFlag(KeyModifiers.Control);
        if (e.DataTransfer.Contains(InternalPaths))
        {
            // Dropping onto a file (not a folder) inside the same view does nothing useful.
            var target = ItemFromSource(e.Source);
            e.DragEffects = target is { IsFolder: false } ? DragDropEffects.None : copy ? DragDropEffects.Copy : DragDropEffects.Move;
        }
        else if (e.DataTransfer.Contains(DataFormat.File))
        {
            e.DragEffects = DragDropEffects.Copy;
        }
        else
        {
            e.DragEffects = DragDropEffects.None;
        }
    }

    private async void OnDrop(object? sender, DragEventArgs e)
    {
        if (Vm is not { } vm)
        {
            return;
        }

        var target = ItemFromSource(e.Source) is { IsFolder: true, IsProtected: false } folder
            ? folder.FullPath
            : vm.IsSearchResults ? vm.RootPath : vm.CurrentFolder;

        if (e.DataTransfer.TryGetValue(InternalPaths) is { } joined)
        {
            var paths = joined.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Where(p => !string.Equals(p, target, StringComparison.OrdinalIgnoreCase))
                .ToList();
            var copy = e.KeyModifiers.HasFlag(KeyModifiers.Control);
            await vm.TransferAsync(paths, target, move: !copy);
            return;
        }

        if (e.DataTransfer.TryGetFiles() is { } files)
        {
            var paths = files.Select(f => f.TryGetLocalPath()).OfType<string>().ToList();
            await vm.TransferAsync(paths, target, move: false);
        }
    }

    private static FileItemViewModel? ItemFromSource(object? source) =>
        (source as Visual)?.FindAncestorOfType<ListBoxItem>(includeSelf: true)?.DataContext as FileItemViewModel;
}
