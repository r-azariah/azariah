using Azariah.Core.CardShows;

namespace Azariah.Core.Tests;

public sealed class CardShowResultsTests : IDisposable
{
    private readonly TempDrive _drive = new();

    public void Dispose() => _drive.Dispose();

    private const string Shows = """
        [
          {"event":"Kingston MA (Gone Broke)","start":"2026-09-20","when":"Past","status":"Had table","price":74,"paid":"Paid"},
          {"event":"October Turtle Boyz Show","start":"2026-10-19","when":"Past","status":"Had table","price":20,"paid":"Paid elsewhere"},
          {"event":"Union Station Card Show","start":"2026-09-27","when":"Past","status":"No table","price":null,"paid":"Withdrawn"}
        ]
        """;

    private const string Results = """
        [
          {"date":"2026-09-20","event":"Kingston MA (Gone Broke)","trans":11,"start":3890,"end":4500.83,"growth":610.83,"realized":47.63,"start_cash":600,"end_cash":277},
          {"date":"2026-10-19","event":"October Turtle Boyz Show","trans":8,"start":4269.14,"end":4244.42,"growth":-24.72,"realized":113.78,"start_cash":260,"end_cash":423}
        ]
        """;

    private const string Allocation = """
        [
          {"event":"Kingston MA (Gone Broke)","date":"2026-09-20","growth_estimate":74},
          {"event":"October Turtle Boyz Show","date":"2026-10-19","growth_estimate":-24.72}
        ]
        """;

    private const string Walk = """
        [
          {"step":"2","show":"October Turtle Boyz Show","date":"2026-10-19","label":"Sold 5 items","amount":-4.75,"kind":"change","detail":"$422.75 left, $418.00 came in."},
          {"step":"1","show":"October Turtle Boyz Show","date":"2026-10-19","label":"Start: collection + cash","amount":4269.14,"kind":"total","detail":"Oct 18 end"},
          {"step":"3","show":"October Turtle Boyz Show","date":"2026-10-19","label":"End: collection + cash","amount":4244.42,"kind":"total","detail":"Oct 19 end"},
          {"step":"1","show":"Kingston MA (Gone Broke)","date":"2026-09-20","label":"Start: older show","amount":1,"kind":"total","detail":""}
        ]
        """;

    private const string Moves = """
        [
          {"id":"s1","move":"Sold","card":"Houndour (JP)","detail":"Lightly Played","market":46.19,"amount":45,"realized":11.84,"show":"October Turtle Boyz Show"},
          {"id":"o1","move":"Traded out","card":"Venusaur","detail":"PSA 8","market":320,"amount":null,"realized":null,"show":"October Turtle Boyz Show"},
          {"id":"i1","move":"Came in","card":"Shining Arceus","detail":"PSA 10","market":531.15,"amount":427.5,"realized":null,"show":"October Turtle Boyz Show"},
          {"id":"x1","move":"Sold","card":"From an older show","detail":"","market":1,"amount":1,"realized":0,"show":"Kingston MA (Gone Broke)"}
        ]
        """;

    private const string Everything = """
        [
          {"name":"Cheap Stamp","set":"Base","condition":"Near Mint","kind":"raw","quantity":2,"value":10.28,"cost_each":8.13,"cost_total":16.26},
          {"name":"Shining Arceus","set":"Shining Legends","condition":"PSA 10","kind":"graded","quantity":1,"value":531.15,"cost_each":427.5,"cost_total":427.5},
          {"name":"Elite Trainer Box","set":"30th","condition":"Sealed","kind":"sealed","quantity":3,"value":124.38,"cost_each":89.99,"cost_total":269.97},
          {"name":"Odd One","set":"X","condition":"Near Mint","kind":"slab","quantity":1,"value":1.5,"cost_total":1},
          {"name":"","set":"nameless row is skipped","quantity":1,"value":999}
        ]
        """;

    private CardShowBook Seed()
    {
        _drive.File(@"Projects/Card Shows/data/shows.json", Shows);
        _drive.File(@"Projects/Card Shows/data/results.json", Results);
        _drive.File(@"Projects/Card Shows/data/results-display-allocation.json", Allocation);
        _drive.File(@"Projects/Card Shows/data/portfolio-change.json", Walk);
        _drive.File(@"Projects/Card Shows/data/portfolio-moves.json", Moves);
        _drive.File(@"Projects/Card Shows/data/collection.json", Everything);
        return CardShowBook.Load(_drive.Layout);
    }

    [Fact]
    public void A_show_row_uses_the_owner_balanced_portfolio_change_and_adds_up_left_to_right()
    {
        var results = CardShowResults.Load(Seed());

        Assert.Equal(2, results.Rows.Count);
        var turtle = results.Rows[0];
        Assert.Equal("October Turtle Boyz Show", turtle.Show);
        Assert.Equal(new DateOnly(2026, 10, 19), turtle.Date);
        Assert.Equal(8, turtle.Trans);
        Assert.Equal(-24.72m, turtle.PortfolioChange);
        Assert.Equal(20m, turtle.ShowCost);
        Assert.Equal(-44.72m, turtle.AfterCost);
        Assert.Equal(113.78m, turtle.Realized);
        Assert.Equal(4350.17m, turtle.Potential);
        Assert.Equal(-3.09m, turtle.PerTrans);

        var kingston = results.Rows[1];
        Assert.Equal(74m, kingston.PortfolioChange);
        Assert.Equal(74m, kingston.ShowCost);
        Assert.Equal(0m, kingston.AfterCost);
        Assert.Equal(4570.08m, kingston.Potential);
        Assert.Equal(6.73m, kingston.PerTrans);

        foreach (var row in results.Rows)
        {
            Assert.Equal(row.PortfolioChange - (row.ShowCost ?? 0), row.AfterCost);
        }
    }

    [Fact]
    public void Realized_is_information_only_and_never_added_to_the_portfolio_change()
    {
        var row = CardShowResults.Load(Seed()).Rows[0];

        Assert.Equal(113.78m, row.Realized);
        Assert.NotEqual(row.PortfolioChange + row.Realized - 20m, row.AfterCost);
        Assert.Equal(row.PortfolioChange - 20m, row.AfterCost);
    }

    [Fact]
    public void The_totals_row_adds_up_and_potential_is_the_latest_shows()
    {
        var totals = CardShowResults.Load(Seed()).Totals;

        Assert.Equal(49.28m, totals.PortfolioChange);
        Assert.Equal(94m, totals.ShowCosts);
        Assert.Equal(-44.72m, totals.AfterCost);
        Assert.Equal(161.41m, totals.Realized);
        Assert.Equal(4350.17m, totals.Potential);
        Assert.Equal(totals.PortfolioChange - totals.ShowCosts, totals.AfterCost);
    }

    [Fact]
    public void A_results_label_replaces_the_event_name_but_the_cost_still_matches_by_event()
    {
        _drive.File(@"Projects/Card Shows/data/shows.json", Shows);
        _drive.File(@"Projects/Card Shows/data/results-display-allocation.json", Allocation);
        _drive.File(@"Projects/Card Shows/data/results.json", """[{"date":"2026-09-20","event":"Kingston MA (Gone Broke)","label":"Kingston + extras","trans":11,"start":3890,"end":4500.83,"realized":47.63,"start_cash":600,"end_cash":277}]""");
        var row = CardShowResults.Load(CardShowBook.Load(_drive.Layout)).Rows.Single();

        Assert.Equal("Kingston + extras", row.Show);
        Assert.Equal(74m, row.ShowCost);
        Assert.Equal(74m, row.PortfolioChange);
    }

    [Fact]
    public void A_show_without_an_estimate_falls_back_to_measured_and_without_a_tracker_entry_has_no_cost()
    {
        _drive.File(@"Projects/Card Shows/data/results-display-allocation.json", """[{"event":"Other","date":"2026-10-20","growth_estimate":999}]""");
        _drive.File(@"Projects/Card Shows/data/shows.json", "[]");
        _drive.File(@"Projects/Card Shows/data/results.json", """[{"date":"2026-10-20","event":"Mystery","trans":0,"start":100,"end":105,"realized":2,"start_cash":10,"end_cash":40}]""");
        var row = CardShowResults.Load(CardShowBook.Load(_drive.Layout)).Rows.Single();

        Assert.Equal(5m, row.PortfolioChange);
        Assert.Null(row.ShowCost);
        Assert.Null(row.PerTrans);
        Assert.Equal(5m, row.AfterCost);
        Assert.Equal(115m, row.Potential);
    }

    [Fact]
    public void The_walk_and_the_moves_are_for_the_newest_show()
    {
        var results = CardShowResults.Load(Seed());

        Assert.Equal("October Turtle Boyz Show", results.LatestShow);
        Assert.Equal(new DateOnly(2026, 10, 19), results.LatestDate);
        Assert.Equal(new[] { "Start: collection + cash", "Sold 5 items", "End: collection + cash" }, results.Steps.Select(s => s.Label));
        Assert.Equal(new[] { true, false, true }, results.Steps.Select(s => s.IsTotal));
        Assert.Equal(-4.75m, results.Steps[1].Amount);

        Assert.Equal(new[] { "Houndour (JP)", "Venusaur", "Shining Arceus" }, results.Moves.Select(m => m.Card));
        var sold = results.Moves[0];
        Assert.Equal(("Sold", "Lightly Played", 46.19m, 45m, 11.84m), (sold.Move, sold.Condition, sold.Market, sold.Amount, sold.Realized));
        Assert.Null(results.Moves[1].Amount);
        Assert.Null(results.Moves[2].Realized);
    }

    [Fact]
    public void The_collection_adds_up_value_and_paid_per_copy()
    {
        var all = CardCollection.Load(Seed());

        Assert.Equal(4, all.Entries);
        Assert.Equal(7, all.Copies);
        Assert.Equal(20.56m + 531.15m + 373.14m + 1.5m, all.Value);
        Assert.Equal(16.26m + 427.5m + 269.97m + 1m, all.Paid);
        Assert.Equal(all.Value - all.Paid, all.Unrealized);
        Assert.Equal(20.56m, all.RawValue);
        Assert.Equal(531.15m, all.GradedValue);
        Assert.Equal(373.14m, all.SealedValue);
        Assert.Equal(1.5m, all.OtherValue);
        Assert.Equal(all.Value, all.RawValue + all.GradedValue + all.SealedValue + all.OtherValue);
        Assert.Equal(new[] { "Shining Arceus", "Elite Trainer Box", "Cheap Stamp", "Odd One" }, all.Items.Select(i => i.Name));
        Assert.Equal(1m, all.Items[3].CostEach);
    }

    [Fact]
    public void Missing_or_broken_files_mean_empty_results()
    {
        var empty = CardShowBook.Load(_drive.Layout);
        var none = CardShowResults.Load(empty);
        Assert.False(none.HasResults);
        Assert.Null(none.Totals.Potential);
        Assert.Empty(none.Steps);
        Assert.Empty(none.Moves);
        Assert.Null(none.Problem);
        Assert.False(CardCollection.Load(empty).HasItems);

        _drive.File(@"Projects/Card Shows/data/results.json", "{ nope");
        _drive.File(@"Projects/Card Shows/data/collection.json", "{\"not\":\"a list\"}");
        var broken = CardShowBook.Load(_drive.Layout);
        Assert.NotNull(CardShowResults.Load(broken).Problem);
        Assert.NotNull(CardCollection.Load(broken).Problem);
    }
}
