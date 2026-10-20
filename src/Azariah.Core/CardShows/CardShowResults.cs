using System.Text.Json;

namespace Azariah.Core.CardShows;

/// <summary>
/// One finished show, left to right: growth (end - start) is the whole story; cash change and value change split it;
/// growth minus the table is what the show left you with. Realized is information only (it is already inside growth).
/// </summary>
public sealed record ShowResult(
    DateOnly Date,
    string Show,
    int Trans,
    decimal Start,
    decimal End,
    decimal Growth,
    decimal CashChange,
    decimal ValueChange,
    decimal? ShowCost,
    decimal AfterCost,
    decimal Realized,
    decimal Potential,
    decimal? PerTrans);

/// <summary>The figures above the table. Potential is the latest show's.</summary>
public sealed record ResultTotals(
    decimal Growth,
    decimal ShowCosts,
    decimal AfterCost,
    decimal Realized,
    decimal? Potential);

/// <summary>One line of the latest show's collection-plus-cash walk (start, changes, end).</summary>
public sealed record WaterfallStep(string Label, decimal Amount, bool IsTotal, string Detail);

/// <summary>A card that left (sold, traded out) or came in during the latest show.</summary>
public sealed record CardMove(string Move, string Card, string Condition, decimal? Market, decimal? Amount, decimal? Realized);

/// <summary>
/// Results per show, read from the JSON files Claude keeps in <c>Projects\Card Shows\data</c>:
/// results.json, portfolio-change.json, portfolio-moves.json.
/// Read-only. Money is rounded to cents at every step so the columns add up across.
/// </summary>
public sealed class CardShowResults
{
    public const decimal CashFactor = 1.25m;

    private CardShowResults(
        IReadOnlyList<ShowResult> rows,
        ResultTotals totals,
        string latestShow,
        DateOnly? latestDate,
        IReadOnlyList<WaterfallStep> steps,
        IReadOnlyList<CardMove> moves,
        string? problem)
    {
        Rows = rows;
        Totals = totals;
        LatestShow = latestShow;
        LatestDate = latestDate;
        Steps = steps;
        Moves = moves;
        Problem = problem;
    }

    /// <summary>Most recent show first.</summary>
    public IReadOnlyList<ShowResult> Rows { get; }

    public ResultTotals Totals { get; }

    /// <summary>The show the walk and the moves belong to (the newest in portfolio-change.json).</summary>
    public string LatestShow { get; }

    public DateOnly? LatestDate { get; }

    public IReadOnlyList<WaterfallStep> Steps { get; }

    public IReadOnlyList<CardMove> Moves { get; }

    public string? Problem { get; }

    public bool HasResults => Rows.Count > 0;

    public static decimal Cents(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);

    public static CardShowResults Load(CardShowBook book)
    {
        var folder = book.Folder;
        var problems = new List<string>();

        var results = CardShowBook.ReadRows(folder, "results.json", out var p1);
        var change = CardShowBook.ReadRows(folder, "portfolio-change.json", out var p2);
        var moves = CardShowBook.ReadRows(folder, "portfolio-moves.json", out var p3);
        problems.AddRange(new[] { p1, p2, p3 }.OfType<string>());

        var rows = new List<ShowResult>();
        foreach (var r in results ?? [])
        {
            var name = CardShowBook.Text(r, "event").Trim();
            if (name.Length == 0 || Date(r, "date") is not { } date)
            {
                continue;
            }

            var trans = (int)(CardShowBook.Number(r, "trans") ?? 0);
            var start = Cents(CardShowBook.Number(r, "start") ?? 0);
            var end = Cents(CardShowBook.Number(r, "end") ?? 0);
            var endCash = Cents(CardShowBook.Number(r, "end_cash") ?? 0);
            var cashChange = Cents(endCash - (CardShowBook.Number(r, "start_cash") ?? 0));
            var realized = Cents(CardShowBook.Number(r, "realized") ?? 0);

            var show = book.Shows.FirstOrDefault(s => Same(s.Name, name) && s.Start == date) ?? book.Shows.FirstOrDefault(s => Same(s.Name, name));
            decimal? cost = show?.Price is { } price ? Cents(price) : null;

            var growth = Cents(end - start);
            var valueChange = Cents(growth - cashChange);
            var afterCost = Cents(growth - (cost ?? 0));
            var potential = Cents(end + endCash * (CashFactor - 1m));
            decimal? perTrans = trans > 0 ? Cents(growth / trans) : null;

            rows.Add(new ShowResult(date, name, trans, start, end, growth, cashChange, valueChange, cost, afterCost, realized, potential, perTrans));
        }

        var ordered = rows.OrderByDescending(r => r.Date).ThenBy(r => r.Show, StringComparer.Ordinal).ToList();
        var totals = new ResultTotals(
            Cents(ordered.Sum(r => r.Growth)),
            Cents(ordered.Sum(r => r.ShowCost ?? 0)),
            Cents(ordered.Sum(r => r.AfterCost)),
            Cents(ordered.Sum(r => r.Realized)),
            ordered.Count > 0 ? ordered[0].Potential : null);

        var (latestShow, latestDate, steps) = ReadWalk(change ?? []);
        var latestMoves = ReadMoves(moves ?? [], latestShow);

        return new CardShowResults(ordered, totals, latestShow, latestDate, steps, latestMoves, problems.Count == 0 ? null : string.Join(" ", problems));
    }

    private static (string Show, DateOnly? Date, List<WaterfallStep> Steps) ReadWalk(List<JsonElement> rows)
    {
        var parsed = rows
            .Select((r, i) => (Row: r, Index: i, Show: CardShowBook.Text(r, "show"), Date: Date(r, "date"), Step: CardShowBook.Number(r, "step") ?? i))
            .ToList();
        if (parsed.Count == 0)
        {
            return (string.Empty, null, []);
        }

        var newest = parsed.Max(p => p.Date ?? DateOnly.MinValue);
        var pick = parsed.Where(p => (p.Date ?? DateOnly.MinValue) == newest).ToList();
        var show = pick[^1].Show;
        var steps = pick
            .Where(p => Same(p.Show, show))
            .OrderBy(p => p.Step)
            .ThenBy(p => p.Index)
            .Select(p => new WaterfallStep(
                CardShowBook.Text(p.Row, "label"),
                Cents(CardShowBook.Number(p.Row, "amount") ?? 0),
                string.Equals(CardShowBook.Text(p.Row, "kind"), "total", StringComparison.OrdinalIgnoreCase),
                CardShowBook.Text(p.Row, "detail")))
            .ToList();
        return (show, newest == DateOnly.MinValue ? null : newest, steps);
    }

    private static List<CardMove> ReadMoves(List<JsonElement> rows, string show)
    {
        var mine = rows.Where(r => show.Length > 0 && Same(CardShowBook.Text(r, "show"), show)).ToList();
        if (mine.Count == 0)
        {
            mine = rows;
        }

        return mine
            .Select(r => new CardMove(
                CardShowBook.Text(r, "move"),
                CardShowBook.Text(r, "card"),
                CardShowBook.Text(r, "detail"),
                CardShowBook.Number(r, "market"),
                CardShowBook.Number(r, "amount"),
                CardShowBook.Number(r, "realized")))
            .ToList();
    }

    private static bool Same(string a, string b) => string.Equals(a.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase);

    private static DateOnly? Date(JsonElement row, string name) =>
        CardShowBook.TryDate(CardShowBook.Text(row, name), out var d) ? d : null;
}
