using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Azariah.App.ViewModels;

namespace Azariah.App.Views;

public partial class NotesPageView : UserControl
{
    private NotesPageViewModel? _vm;

    public NotesPageView()
    {
        InitializeComponent();
        TitleBox.AddHandler(KeyDownEvent, OnTitleKeyDown, RoutingStrategies.Tunnel);
        TitleBox.LostFocus += (_, _) => _vm?.CommitTitleCommand.Execute(null);
        Editor.LostFocus += (_, _) => _vm?.Flush();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (_vm is not null)
        {
            _vm.TitleFocusRequested -= OnTitleFocus;
            _vm.EditorFocusRequested -= OnEditorFocus;
        }

        _vm = DataContext as NotesPageViewModel;
        if (_vm is not null)
        {
            _vm.TitleFocusRequested += OnTitleFocus;
            _vm.EditorFocusRequested += OnEditorFocus;
        }
    }

    private void OnTitleKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key is Key.Enter or Key.Down)
        {
            _vm?.CommitTitleCommand.Execute(null);
            Editor.Focus();
            e.Handled = true;
        }
    }

    private void OnTitleFocus(object? sender, EventArgs e) =>
        Dispatcher.UIThread.Post(() =>
        {
            TitleBox.Focus();
            TitleBox.SelectAll();
        }, DispatcherPriority.Loaded);

    private void OnEditorFocus(object? sender, EventArgs e) =>
        Dispatcher.UIThread.Post(() =>
        {
            Editor.Focus();
            Editor.CaretIndex = Editor.Text?.Length ?? 0;
        }, DispatcherPriority.Loaded);
}
