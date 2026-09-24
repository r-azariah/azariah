using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Azariah.App.Services;
using Azariah.App.ViewModels;

namespace Azariah.App.Views;

public partial class MainWindow : Window
{
    private DialogHost? _dialogs;

    public MainWindow()
    {
        InitializeComponent();
        AddHandler(KeyDownEvent, OnPreviewKeyDown, RoutingStrategies.Tunnel);
    }

    public void BringToFront()
    {
        if (WindowState == WindowState.Minimized)
        {
            WindowState = WindowState.Normal;
        }

        Activate();
        Topmost = true;
        Topmost = false;
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (_dialogs is not null)
        {
            _dialogs.PropertyChanged -= OnDialogChanged;
        }

        _dialogs = (DataContext as MainViewModel)?.Dialogs;
        if (_dialogs is not null)
        {
            _dialogs.PropertyChanged += OnDialogChanged;
        }
    }

    /// <summary>Moves keyboard focus into a dialog as soon as it opens, so Enter/Delete never hit the page behind it.</summary>
    private void OnDialogChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(DialogHost.Active) || _dialogs?.Active is null)
        {
            return;
        }

        Dispatcher.UIThread.Post(
            () =>
            {
                var controls = DialogOverlay.GetVisualDescendants().OfType<Control>().ToList();
                if (controls.OfType<TextBox>().FirstOrDefault() is { } box)
                {
                    box.Focus();
                    return;
                }

                var target = controls.OfType<Button>().FirstOrDefault(b => b.IsDefault && b.IsEffectivelyVisible)
                    ?? controls.OfType<Button>().LastOrDefault(b => b.IsEffectivelyVisible);
                target?.Focus();
            },
            DispatcherPriority.Loaded);
    }

    private void OnPreviewKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not MainViewModel { Dialogs.Active: { } dialog })
        {
            return;
        }

        if (e.Key == Key.Escape)
        {
            dialog.Dismiss();
            e.Handled = true;
            return;
        }

        // While a dialog is open, keys aimed at the page behind it are swallowed.
        if (e.Source is not Visual source || !DialogOverlay.IsVisualAncestorOf(source))
        {
            e.Handled = true;
        }
    }
}
