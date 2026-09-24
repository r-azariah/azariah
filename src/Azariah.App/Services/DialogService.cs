using Azariah.App.ViewModels.Dialogs;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Azariah.App.Services;

/// <summary>Holds the dialog currently shown as an overlay in the main window.</summary>
public sealed partial class DialogHost : ObservableObject
{
    [ObservableProperty]
    public partial DialogViewModelBase? Active { get; set; }
}

public interface IDialogService
{
    Task<TResult> ShowAsync<TResult>(DialogViewModel<TResult> dialog);

    Task<bool> ConfirmAsync(string title, string message, string confirmText = "OK", bool danger = false, IReadOnlyList<DetailRow>? details = null);

    Task AlertAsync(string title, string message, IReadOnlyList<DetailRow>? details = null);

    Task<string?> PromptAsync(string title, string? message, string initialText = "", string confirmText = "OK", Func<string, string?>? validate = null, int selectionLength = -1);

    Task<string?> ChooseAsync(string title, string message, params ChoiceOption[] options);
}

/// <summary>In-window dialogs, one at a time, styled like the rest of the app.</summary>
public sealed class DialogService(DialogHost host) : IDialogService
{
    private readonly SemaphoreSlim _gate = new(1, 1);

    public async Task<TResult> ShowAsync<TResult>(DialogViewModel<TResult> dialog)
    {
        ArgumentNullException.ThrowIfNull(dialog);
        await _gate.WaitAsync();
        try
        {
            host.Active = dialog;
            return await dialog.Result;
        }
        finally
        {
            host.Active = null;
            _gate.Release();
        }
    }

    public Task<bool> ConfirmAsync(string title, string message, string confirmText = "OK", bool danger = false, IReadOnlyList<DetailRow>? details = null) =>
        ShowAsync(new ConfirmDialogViewModel
        {
            Title = title,
            Message = message,
            ConfirmText = confirmText,
            IsDanger = danger,
            Details = details ?? [],
        });

    public Task AlertAsync(string title, string message, IReadOnlyList<DetailRow>? details = null) =>
        ShowAsync(new ConfirmDialogViewModel
        {
            Title = title,
            Message = message,
            ShowCancel = false,
            Details = details ?? [],
        });

    public Task<string?> PromptAsync(string title, string? message, string initialText = "", string confirmText = "OK", Func<string, string?>? validate = null, int selectionLength = -1) =>
        ShowAsync(new PromptDialogViewModel
        {
            Title = title,
            Message = message,
            Text = initialText,
            ConfirmText = confirmText,
            Validate = validate,
            SelectionLength = selectionLength,
        });

    public Task<string?> ChooseAsync(string title, string message, params ChoiceOption[] options) =>
        ShowAsync(new ChoiceDialogViewModel { Title = title, Message = message, Options = options });
}
