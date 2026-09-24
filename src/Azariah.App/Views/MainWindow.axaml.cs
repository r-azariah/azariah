using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Azariah.App.Platform;
using Azariah.App.Services;
using Azariah.App.ViewModels;

namespace Azariah.App.Views;

public partial class MainWindow : Window
{
    private DialogHost? _dialogs;
    private MainViewModel? _main;

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
        ForegroundWindow.Force(this);
        Topmost = true;
        Topmost = false;
    }

    public void PrepareOpenAnimation() => Boot.PrepareOpen();

    public Task PlayOpenAnimationAsync() => Boot.PlayOpenAsync();

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (_dialogs is not null)
        {
            _dialogs.PropertyChanged -= OnDialogChanged;
        }

        if (_main is not null)
        {
            _main.DriveLost -= OnDriveLost;
        }

        _main = DataContext as MainViewModel;
        _dialogs = _main?.Dialogs;
        if (_dialogs is not null)
        {
            _dialogs.PropertyChanged += OnDialogChanged;
        }

        if (_main is not null)
        {
            _main.DriveLost += OnDriveLost;
        }
    }

    /// <summary>Drive unplugged: short disconnect screen, then close.</summary>
    private async void OnDriveLost(object? sender, EventArgs e)
    {
        try
        {
            if (WindowState == WindowState.Minimized)
            {
                WindowState = WindowState.Normal;
            }

            await Boot.PlayCloseAsync("Disconnected", SystemAnimations.Enabled);
        }
        finally
        {
            App.Exit();
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
        if (Boot.IsPlayingOpen)
        {
            Boot.Skip();
            e.Handled = true;
            return;
        }

        // Ctrl+Space (or Ctrl+K) opens the command bar from anywhere in the workspace.
        if (e.KeyModifiers == KeyModifiers.Control && e.Key is (Key.Space or Key.K)
            && DataContext is MainViewModel { Dialogs.Active: null, Workspace: { } workspace })
        {
            workspace.CommandBar.Toggle();
            e.Handled = true;
            return;
        }

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
