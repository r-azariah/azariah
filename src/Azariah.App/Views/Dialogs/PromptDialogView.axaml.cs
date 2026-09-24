using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Azariah.App.ViewModels.Dialogs;

namespace Azariah.App.Views.Dialogs;

public partial class PromptDialogView : UserControl
{
    public PromptDialogView() => InitializeComponent();

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Dispatcher.UIThread.Post(() =>
        {
            Input.Focus();
            var length = (DataContext as PromptDialogViewModel)?.SelectionLength ?? -1;
            Input.SelectionStart = 0;
            Input.SelectionEnd = length >= 0 ? length : Input.Text?.Length ?? 0;
        });
    }
}
