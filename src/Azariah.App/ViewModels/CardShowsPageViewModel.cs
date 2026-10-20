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
            null => string.Empty,
            0 => "Free",
            var p => (s.PriceIsLowest ? "from " : string.Empty) + CardShowsPageViewModel.Money(p.Value),
        };
        return new CardShowRow(
            date,
            s.Name,
            s.Town,
            s.IsUpcoming ? s.Status : (s.HadTable ? "Had table" : s.Paid),
            cost,
            s.Payment,
            s.Paid,
            s.DueNow is { } d ? CardShowsPageViewModel.Money(d) : string.Empty,
            string.IsNullOrEmpty(s.TableNumber) ? string.Empty : "Table " + s.TableNumber,
            s.Note,
            IsOk: s.HadTable || s.Paid.StartsWith("Paid", StringComparison.Ordinal),
            IsWarn: s.NeedsPayment || s.NotApplied,
            IsBad: s.Status == "Waitlisted");
    }
}

/// <summary>A figure with its sign carried separately, so the view can color only gains and losses.</summary>
public sealed record SignedFigure(string Text, bool IsNegative, bool IsPositive)
{
    public static SignedFigure Blank { get; } = new(string.Empty, false, false);

    public static SignedFigure Of(decimal value, string money = "") => new(
        (value > 0 ? "+" : value < 0 ? "−" : string.Empty) + money + CardShowsPageViewModel.Plain(Math.Abs(value)),
        value < 0,
        value > 0);

    public static SignedFigure OfOrBlank(decimal? value, string money = "") => value is { } v ? Of(v, money) : Blank;
}

/// <summary>A labelled number: the totals above the results table, and the collection summary.</summary>
public sealed record Figure(string Label, string Text, bool IsNegative = false, bool IsPositive = false, string Sub = "")
{
    public bool HasSub => Sub.Length > 0;

    public static Figure WithSign(string label, decimal value) => From(label, SignedFigure.Of(value, "$"));

    public static Figure From(string label, SignedFigure f) => new(label, f.Text, f.IsNegative, f.IsPositive);
}

/// <summary>One finished show, columns in the order they add up. Realized is information only.</summary>
public sealed record ResultRowView(
    string DateText,
    string Show,
    string Trans,
    string Start,
    string End,
    SignedFigure Growth,
    SignedFigure CashChange,
    SignedFigure ValueChange,
    string Cost,
    SignedFigure AfterCost,
    string Realized,
    string Potential,
    SignedFigure PerTrans)
{
    public static ResultRowView From(ShowResult r) => new(
        r.Date.ToString("MMM d", CultureInfo.InvariantCulture),
        r.Show,
        r.Trans.ToString(CultureInfo.InvariantCulture),
        CardShowsPageViewModel.Plain(r.Start),
        CardShowsPageViewModel.Plain(r.End),
        SignedFigure.Of(r.Growth),
        SignedFigure.Of(r.CashChange),
        SignedFigure.Of(r.ValueChange),
        r.ShowCost is { } c ? CardShowsPageViewModel.Plain(c) : string.Empty,
        SignedFigure.Of(r.AfterCost),
        CardShowsPageViewModel.Plain(r.Realized),
        CardShowsPageViewModel.Plain(r.Potential),
        SignedFigure.OfOrBlank(r.PerTrans));
}

/// <summary>A line of the latest show's start-to-end walk. Totals are plain, changes are signed.</summary>
public sealed record WalkRowView(string Label, SignedFigure Amount, string Detail, bool IsTotal)
{
    public static WalkRowView From(WaterfallStep s) => new(
        s.Label,
        s.IsTotal ? new SignedFigure(CardShowsPageViewModel.Plain(s.Amount), false, false) : SignedFigure.Of(s.Amount),
        s.Detail,
        s.IsTotal);
}

/// <summary>A card that left or came in at the latest show.</summary>
public sealed record MoveRowView(string Move, string Card, string Condition, string Market, string Amount, SignedFigure Realized)
{
    public static MoveRowView From(CardMove m) => new(
        m.Move,
        m.Card,
        m.Condition,
        m.Market is { } k ? CardShowsPageViewModel.Plain(k) : string.Empty,
        m.Amount is { } a ? CardShowsPageViewModel.Plain(a) : string.Empty,
        SignedFigure.OfOrBlank(m.Realized));
}

/// <summary>One collection entry for the top-items list (value and cost are per copy).</summary>
public sealed record CollectionRowView(string Name, string Set, string Condition, string Quantity, string ValueEach, string CostEach)
{
    public static CollectionRowView From(CollectionItem i) => new(
        i.Name,
        i.Set,
        i.Condition,
        i.Quantity.ToString(CultureInfo.InvariantCulture),
        CardShowsPageViewModel.Plain(i.ValueEach),
        CardShowsPageViewModel.Plain(i.CostEach));
}

public enum CardShowsTab
{
    Shows,
    Results,
    Collection,
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

    public ObservableCollection<Figure> ResultFigures { get; } = [];

    public ObservableCollection<ResultRowView> ResultRows { get; } = [];

    public ObservableCollection<WalkRowView> WalkRows { get; } = [];

    public ObservableCollection<MoveRowView> MoveRows { get; } = [];

    public ObservableCollection<Figure> CollectionFigures { get; } = [];

    public ObservableCollection<Figure> KindFigures { get; } = [];

    public ObservableCollection<CollectionRowView> CollectionRows { get; } = [];

    [ObservableProperty]
    public partial CardShowsTab Tab { get; set; } = CardShowsTab.Shows;

    [ObservableProperty]
    public partial bool HasResults { get; set; }

    [ObservableProperty]
    public partial string? ResultsNote { get; set; }

    [ObservableProperty]
    public partial string WalkTitle { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool HasCollection { get; set; }

    [ObservableProperty]
    public partial string? CollectionNote { get; set; }

    [ObservableProperty]
    public partial string ItemsTitle { get; set; } = string.Empty;

    public bool IsShowsTab => Tab == CardShowsTab.Shows;

    public bool IsResultsTab => Tab == CardShowsTab.Results;

    public bool IsCollectionTab => Tab == CardShowsTab.Collection;

    public bool HasWalk => WalkRows.Count > 0;

    public bool HasMoves => MoveRows.Count > 0;

    public bool HasResultsNote => !string.IsNullOrEmpty(ResultsNote);

    public bool HasCollectionNote => !string.IsNullOrEmpty(CollectionNote);

    partial void OnTabChanged(CardShowsTab value) => NotifyTabs();

    // Pressing the tab that is already open must leave it looking pressed, so the buttons are re-synced every time.
    private void NotifyTabs()
    {
        OnPropertyChanged(nameof(IsShowsTab));
        OnPropertyChanged(nameof(IsResultsTab));
        OnPropertyChanged(nameof(IsCollectionTab));
    }

    private void SelectTab(CardShowsTab tab)
    {
        Tab = tab;
        NotifyTabs();
    }

    partial void OnResultsNoteChanged(string? value) => OnPropertyChanged(nameof(HasResultsNote));

    partial void OnCollectionNoteChanged(string? value) => OnPropertyChanged(nameof(HasCollectionNote));

    [RelayCommand]
    public void OpenShowsTab() => SelectTab(CardShowsTab.Shows);

    [RelayCommand]
    public void OpenResultsTab() => SelectTab(CardShowsTab.Results);

    [RelayCommand]
    public void OpenCollectionTab() => SelectTab(CardShowsTab.Collection);

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

    /// <summary>Two decimals and thousands separators: 4,350.17. The sign, if any, is the caller's.</summary>
    internal static string Plain(decimal value) => value.ToString("#,0.00", CultureInfo.InvariantCulture);

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
        RefreshResults();
        RefreshCollection();
    }

    private void RefreshResults()
    {
        var results = CardShowResults.Load(_book);
        HasResults = results.HasResults;
        ResultsNote = results.Problem ?? (results.HasResults ? null : "No results yet. Claude keeps Projects\\Card Shows\\data\\results.json up to date.");

        ResultFigures.Clear();
        ResultRows.Clear();
        WalkRows.Clear();
        MoveRows.Clear();
        if (results.HasResults)
        {
            var t = results.Totals;
            ResultFigures.Add(Figure.WithSign("= Growth", t.Growth));
            ResultFigures.Add(new Figure("− Show costs", "$" + Plain(t.ShowCosts)));
            ResultFigures.Add(Figure.WithSign("= After cost", t.AfterCost));
            ResultFigures.Add(new Figure("Realized (already in growth)", "$" + Plain(t.Realized)));
            ResultFigures.Add(new Figure("Potential", t.Potential is { } p ? "$" + Plain(p) : string.Empty));
            foreach (var row in results.Rows)
            {
                ResultRows.Add(ResultRowView.From(row));
            }
        }

        foreach (var step in results.Steps)
        {
            WalkRows.Add(WalkRowView.From(step));
        }

        foreach (var move in results.Moves)
        {
            MoveRows.Add(MoveRowView.From(move));
        }

        WalkTitle = results.LatestShow.Length == 0
            ? "Portfolio change"
            : "Portfolio change · " + results.LatestShow + (results.LatestDate is { } d ? ", " + d.ToString("MMM d, yyyy", CultureInfo.InvariantCulture) : string.Empty);
        OnPropertyChanged(nameof(HasWalk));
        OnPropertyChanged(nameof(HasMoves));
    }

    private void RefreshCollection()
    {
        var all = CardCollection.Load(_book);
        HasCollection = all.HasItems;
        CollectionNote = all.Problem ?? (all.HasItems ? null : "No collection yet. Claude keeps Projects\\Card Shows\\data\\collection.json up to date.");

        CollectionFigures.Clear();
        KindFigures.Clear();
        CollectionRows.Clear();
        if (!all.HasItems)
        {
            ItemsTitle = string.Empty;
            return;
        }

        CollectionFigures.Add(new Figure("Market value", "$" + Plain(all.Value)));
        CollectionFigures.Add(new Figure("Paid", "$" + Plain(all.Paid)));
        CollectionFigures.Add(Figure.WithSign("Unrealized", all.Unrealized));
        CollectionFigures.Add(new Figure("Entries", all.Entries.ToString("#,0", CultureInfo.InvariantCulture)));
        CollectionFigures.Add(new Figure("Copies", all.Copies.ToString("#,0", CultureInfo.InvariantCulture)));

        KindFigures.Add(KindFigure("Raw", all.RawValue, all.Value));
        KindFigures.Add(KindFigure("Graded", all.GradedValue, all.Value));
        KindFigures.Add(KindFigure("Sealed", all.SealedValue, all.Value));
        if (all.OtherValue != 0)
        {
            KindFigures.Add(KindFigure("Other", all.OtherValue, all.Value));
        }

        ItemsTitle = "Top items by value (" + all.Entries.ToString("#,0", CultureInfo.InvariantCulture) + ")";
        foreach (var item in all.Items)
        {
            CollectionRows.Add(CollectionRowView.From(item));
        }
    }

    private static Figure KindFigure(string label, decimal value, decimal whole) =>
        new(label, "$" + Plain(value), Sub: whole > 0 ? (value / whole).ToString("0%", CultureInfo.InvariantCulture) + " of value" : string.Empty);

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
