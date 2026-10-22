using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Azariah.App.ViewModels;

namespace Azariah.App.Views;

public partial class GuestView : UserControl
{
    public GuestView()
    {
        InitializeComponent();
        AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
    }

    /// <summary>
    /// Ambient unlock entry: digits typed anywhere on the guest page build the code; Enter or Tab-twice
    /// resets it. Typing inside a text box (e.g. the Setup Kit search) is left alone, and Enter/Tab only
    /// reset while the owner is mid-entry, so buttons keep working.
    /// </summary>
    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not GuestViewModel vm)
        {
            return;
        }

        if (TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() is TextBox)
        {
            return;
        }

        switch (e.Key)
        {
            case >= Key.D0 and <= Key.D9:
                vm.TypeDigit((char)('0' + (e.Key - Key.D0)));
                e.Handled = true;
                break;
            case >= Key.NumPad0 and <= Key.NumPad9:
                vm.TypeDigit((char)('0' + (e.Key - Key.NumPad0)));
                e.Handled = true;
                break;
            case Key.Enter when vm.IsEntering:
                vm.OnEnter();
                e.Handled = true;
                break;
            case Key.Tab when vm.IsEntering:
                vm.OnTab();
                e.Handled = true;
                break;
        }
    }
}
