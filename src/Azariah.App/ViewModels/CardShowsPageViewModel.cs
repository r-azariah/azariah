using System.Collections.ObjectModel;
using System.Globalization;
using Azariah.App.Services;
using Azariah.Core.CardShows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Azariah.App.ViewModels;

/// <summary>One show as a list row. Color only for state: paid/had a table, needs money, waitlisted.</summary>
public sealed record CardShowRow(
    string DateText,
    string Name,
    string Town,
    string Status,
    string CostText,
    string PayText,
    string PaidText,
    string DueText,
    string TableText,
    string Note,
    bool IsOk,
    bool IsWarn,
    bool IsBad)
{
    public static CardShowRow From(CardShow s)
    {
        var date = s.Start.ToString("ddd, MMM d", CultureInfo.InvariantCulture)
            + (s.End > s.Start ? " – " + s.End.ToString("MMM d", CultureInfo.InvariantCulture) : string.Empty);
        var cost = s.Price switch
        {
            null => "—",
            0 => "Free",
            var p => (s.PriceIsLowest ? "from " : string.Empty) + CardShowsPageViewModel.Money(p.Value),
        };
        return new CardShowRow(
            date,
            s.Name,
            s.Town,
            s.IsUpcoming ? s.Status : (s.HadTable ? "Had table" : s.Paid),
            cost,
            string.IsNullOrEmpty(s.Payment) ? "—" : s.Payment,
            s.Paid,
            s.DueNow is { } d ? CardShowsPageViewModel.Money(d) : "—",
            string.IsNullOrEmpty(s.TableNumber) ? string.Empty : "Table " + s.TableNumber,
            s.Note,
            IsOk: s.HadTable || s.Paid.StartsWith("Paid", StringComparison.Ordinal),
            IsWarn: s.NeedsPayment || s.NotApplied,
            IsBad: s.Status == "Waitlisted");
    }
}

/// <summary>Home &gt; Card shows: tables applied for, what's owed, and past shows. Reads Projects\Card Shows.</summary>
public sealed partial class CardShowsPageViewModel : ViewModelBase
{
    private readonly WorkspaceSession _s;
    private readonly WorkspaceViewModel _workspace;
    private CardShowBook _book;

    public CardShowsPageViewModel(WorkspaceSession session, WorkspaceViewModel workspace)
    {
        _s = session;
        _workspace = workspace;
        _book = CardShowBook.Load(session.Layout);
        Refresh();
    }

    public ObservableCollection<CardShowRow> Upcoming { get; } = [];

    public ObservableCollection<CardShowRow> Past { get; } = [];

    [ObservableProperty]
    public partial string DueNowText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string DueAtShowsText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string NotBilledText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string SpentText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Summary { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string? Status { get; set; }

    [ObservableProperty]
    public partial bool HasShows { get; set; }

    [ObservableProperty]
    public partial bool HasDashboard { get; set; }

    [ObservableProperty]
    public partial bool HasOnline { get; set; }

    public bool HasStatus => !string.IsNullOrEmpty(Status);

    partial void OnStatusChanged(string? value) => OnPropertyChanged(nameof(HasStatus));

    internal static string Money(decimal value) =>
        "$" + value.ToString(decimal.Truncate(value) == value ? "#,0" : "#,0.00", CultureInfo.InvariantCulture);

    [RelayCommand]
    public void Refresh()
    {
        _book = CardShowBook.Load(_s.Layout);
        Upcoming.Clear();
        foreach (var show in _book.Upcoming)
        {
            Upcoming.Add(CardShowRow.From(show));
        }

        Past.Clear();
        foreach (var show in _book.Past)
        {
            Past.Add(CardShowRow.From(show));
        }

        HasShows = _book.HasShows;
        HasDashboard = _book.DashboardPath is not null;
        HasOnline = _book.OnlineUri is not null;
        DueNowText = Money(_book.DueNow);
        DueAtShowsText = Money(_book.DueAtShows);
        NotBilledText = Money(_book.NotBilled);
        SpentText = Money(_book.SpentOnPastTables);
        Summary = SummaryFor(_book);
        Status = _book.Problem ?? (_book.HasShows ? null : "No shows yet. Claude keeps Projects\\Card Shows\\data\\shows.json up to date.");
    }

    internal static string SummaryFor(CardShowBook book)
    {
        var unpaid = book.Shows.Count(s => s.NeedsPayment);
        var next = book.Upcoming.FirstOrDefault();
        var parts = new List<string>();
        if (next is not null)
        {
            parts.Add("Next: " + next.Name + ", " + next.Start.ToString("ddd MMM d", CultureInfo.InvariantCulture));
        }

        if (unpaid > 0)
        {
            parts.Add(Money(book.DueNow) + " due now for " + unpaid + (unpaid == 1 ? " show" : " shows"));
        }

        return string.Join("  ·  ", parts);
    }

    [RelayCommand]
    private void OpenDashboard()
    {
        if (_book.DashboardPath is { } path)
        {
            try
            {
                _s.Shell.Open(path);
            }
            catch (Exception ex)
            {
                Status = "Couldn't open the dashboard: " + ex.Message;
            }
        }
    }

    [RelayCommand]
    private void OpenOnline()
    {
        if (_book.OnlineUri is { } uri)
        {
            try
            {
                _s.Shell.OpenUri(uri);
            }
            catch (Exception ex)
            {
                Status = "Couldn't open the browser: " + ex.Message;
            }
        }
    }

    [RelayCommand]
    private void OpenFolder()
    {
        Directory.CreateDirectory(_book.Folder);
        _workspace.OpenInFiles(_book.Folder);
    }
}
