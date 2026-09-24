using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Azariah.App.ViewModels.Dialogs;

public sealed record DetailRow(string Label, string Value, bool IsWarning = false);

public abstract class DialogViewModelBase : ViewModelBase
{
    public required string Title { get; init; }

    public string? Message { get; init; }

    /// <summary>Called for Escape / clicking outside.</summary>
    public abstract void Dismiss();
}

public abstract partial class DialogViewModel<TResult> : DialogViewModelBase
{
    private readonly TaskCompletionSource<TResult> _result = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Task<TResult> Result => _result.Task;

    protected void Close(TResult value) => _result.TrySetResult(value);
}

public sealed partial class ConfirmDialogViewModel : DialogViewModel<bool>
{
    public string ConfirmText { get; init; } = "OK";
    public string CancelText { get; init; } = "Cancel";
    public bool ShowCancel { get; init; } = true;
    public bool IsDanger { get; init; }
    public IReadOnlyList<DetailRow> Details { get; init; } = [];
    public bool HasDetails => Details.Count > 0;

    [RelayCommand]
    private void Confirm() => Close(true);

    [RelayCommand]
    private void Cancel() => Close(false);

    public override void Dismiss() => Close(false);
}

public sealed partial class PromptDialogViewModel : DialogViewModel<string?>
{
    [ObservableProperty]
    public partial string Text { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string? Error { get; set; }

    public string ConfirmText { get; init; } = "OK";
    public string? Watermark { get; init; }
    public Func<string, string?>? Validate { get; init; }

    /// <summary>Characters to pre-select (e.g. the name without its extension).</summary>
    public int SelectionLength { get; init; } = -1;

    partial void OnTextChanged(string value) => Error = null;

    [RelayCommand]
    private void Confirm()
    {
        var error = Validate?.Invoke(Text);
        if (error is not null)
        {
            Error = error;
            return;
        }

        Close(Text);
    }

    [RelayCommand]
    private void Cancel() => Close(null);

    public override void Dismiss() => Close(null);
}

public sealed record ChoiceOption(string Id, string Label, bool IsPrimary = false);

public sealed partial class ChoiceDialogViewModel : DialogViewModel<string?>
{
    public IReadOnlyList<ChoiceOption> Options { get; init; } = [];

    [RelayCommand]
    private void Choose(ChoiceOption option) => Close(option.Id);

    [RelayCommand]
    private void Cancel() => Close(null);

    public override void Dismiss() => Close(null);
}
