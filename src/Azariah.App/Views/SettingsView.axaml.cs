using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using Azariah.App.ViewModels;

namespace Azariah.App.Views;

public partial class SettingsView : UserControl
{
    public SettingsView()
    {
        InitializeComponent();
        UpdateDrop.AddHandler(DragDrop.DragOverEvent, OnDragOver);
        UpdateDrop.AddHandler(DragDrop.DragLeaveEvent, (_, _) => UpdateDrop.Classes.Remove("over"));
        UpdateDrop.AddHandler(DragDrop.DropEvent, OnDrop);
    }

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        var ok = e.DataTransfer.Contains(DataFormat.File);
        e.DragEffects = ok ? DragDropEffects.Copy : DragDropEffects.None;
        UpdateDrop.Classes.Set("over", ok);
    }

    private async void OnDrop(object? sender, DragEventArgs e)
    {
        UpdateDrop.Classes.Remove("over");
        var path = e.DataTransfer.TryGetFiles()?.Select(f => f.TryGetLocalPath()).OfType<string>().FirstOrDefault();
        if (path is not null && DataContext is SettingsViewModel vm)
        {
            await vm.InstallUpdateFromAsync(path);
        }
    }
}
