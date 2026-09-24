using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Azariah.App.ViewModels;

namespace Azariah.App.Views;

public partial class WorkspaceView : UserControl
{
    private CommandBarViewModel? _bar;

    public WorkspaceView()
    {
        InitializeComponent();
        CommandInput.AddHandler(KeyDownEvent, OnCommandKeyDown, RoutingStrategies.Tunnel);
        CommandResults.Tapped += OnResultTapped;
        CommandShade.PointerPressed += (_, _) => _bar?.Close();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (_bar is not null)
        {
            _bar.PropertyChanged -= OnBarChanged;
        }

        _bar = (DataContext as WorkspaceViewModel)?.CommandBar;
        if (_bar is not null)
        {
            _bar.PropertyChanged += OnBarChanged;
        }
    }

    private void OnBarChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(CommandBarViewModel.IsOpen) && _bar is { IsOpen: true })
        {
            Dispatcher.UIThread.Post(() => CommandInput.Focus(), DispatcherPriority.Loaded);
        }
        else if (e.PropertyName == nameof(CommandBarViewModel.Selected) && _bar?.Selected is { } selected)
        {
            CommandResults.ScrollIntoView(selected);
        }
    }

    private void OnCommandKeyDown(object? sender, KeyEventArgs e)
    {
        if (_bar is null)
        {
            return;
        }

        switch (e.Key)
        {
            case Key.Down:
                _bar.Move(1);
                break;
            case Key.Up:
                _bar.Move(-1);
                break;
            case Key.Enter:
                _ = _bar.RunAsync();
                break;
            case Key.Escape:
                _bar.Close();
                break;
            default:
                return;
        }

        e.Handled = true;
    }

    private void OnResultTapped(object? sender, TappedEventArgs e)
    {
        if (_bar is not null && e.Source is Control { DataContext: CommandItem item })
        {
            _ = _bar.RunAsync(item);
        }
    }
}
