using Azariah.Core.CardShows;

namespace Azariah.Core.Tests;

public sealed class CardShowsTests : IDisposable
{
    private readonly TempDrive _drive = new();

    public void Dispose() => _drive.Dispose();

    private const string Shows = """
        [
          {"event":"Mill City","start":"2026-11-01","end":"2026-11-01","city":"Lowell","state":"MA","when":"Upcoming","status":"Pending Review","table_type":"8ft","price":0,"price_basis":"Exact","payment":"Free","due_now":null,"due_at_show":null,"paid":"No charge","table":"","pay_note":""},
          {"event":"North Attleborough 2 Day","start":"2026-11-14","end":"2026-11-15","city":"North Attleborough","state":"MA","when":"Upcoming","status":"Unpaid","table_type":"6ft","price":115,"price_basis":"Exact","payment":"Deposit","due_now":21.94,"due_at_show":95,"paid":"Unpaid","table":"","pay_note":"Deposit"},
          {"event":"Dorchester","start":"2026-11-08","end":"2026-11-08","city":"Boston","state":"MA","when":"Upcoming","status":"Unpaid","table_type":"6ft","price":75,"price_basis":"Exact","payment":"Full","due_now":80.57,"due_at_show":null,"paid":"Unpaid","table":"","pay_note":""},
          {"event":"Turtle Boyz","start":"2026-10-19","end":"2026-10-19","city":"Malden","state":"MA","when":"Upcoming","status":"Pending Review","price":35,"price_basis":"Exact","payment":"Full","paid":"Not billed yet"},
          {"event":"Collector Expo","start":"2026-10-25","city":"Medford","state":"MA","when":"Upcoming","status":"Not applied","price":125,"price_basis":"Lowest listed","paid":"Not applied"},
          {"event":"South Shore","start":"2026-10-18","end":"2026-10-18","city":"Weymouth","state":"MA","when":"Past","status":"Had table","price":100,"paid":"Paid","table":"98"},
          {"event":"Cards After Dark","start":"2026-09-27","city":"Taunton","state":"MA","when":"Past","status":"Had table","price":93,"paid":"Paid elsewhere","table":"25"},
          {"event":"Union Station","start":"2026-09-27","city":"Worcester","state":"MA","when":"Past","status":"No table","price":null,"paid":"Withdrawn"},
          {"event":"","start":"2026-09-01"},
          {"event":"No date"}
        ]
        """;

    private CardShowBook Seed()
    {
        _drive.File(@"Projects/Card Shows/data/shows.json", Shows);
        return CardShowBook.Load(_drive.Layout);
    }

    [Fact]
    public void Shows_are_read_and_rows_without_a_name_or_date_are_skipped()
    {
        var book = Seed();

        Assert.Equal(8, book.Shows.Count);
        Assert.Null(book.Problem);
        Assert.Equal(new[] { "Turtle Boyz", "Collector Expo", "Mill City", "Dorchester", "North Attleborough 2 Day" }, book.Upcoming.Select(s => s.Name));
        Assert.Equal(new[] { "South Shore", "Cards After Dark", "Union Station" }, book.Past.Select(s => s.Name));

        var twoDay = book.Shows.Single(s => s.Name == "North Attleborough 2 Day");
        Assert.Equal(new DateOnly(2026, 11, 15), twoDay.End);
        Assert.Equal("North Attleborough, MA", twoDay.Town);
        Assert.True(twoDay.NeedsPayment);
        Assert.True(book.Shows.Single(s => s.Name == "Collector Expo").PriceIsLowest);
    }

    [Fact]
    public void Money_totals_follow_payment_state()
    {
        var book = Seed();

        Assert.Equal(102.51m, book.DueNow);
        Assert.Equal(95m, book.DueAtShows);
        Assert.Equal(35m, book.NotBilled);
        Assert.Equal(193m, book.SpentOnPastTables);
    }

    [Fact]
    public void Links_come_from_the_folder()
    {
        _drive.File(@"Projects/Card Shows/" + CardShowBook.DashboardFile, "<html></html>");
        _drive.File(@"Projects/Card Shows/" + CardShowBook.OnlineShortcut, "[InternetShortcut]\r\nURL=https://claude.ai/artifact/abc\r\n");
        var book = Seed();

        Assert.NotNull(book.DashboardPath);
        Assert.Equal(new Uri("https://claude.ai/artifact/abc"), book.OnlineUri);
    }

    [Fact]
    public void A_non_https_shortcut_is_ignored()
    {
        _drive.File(@"Projects/Card Shows/" + CardShowBook.OnlineShortcut, "[InternetShortcut]\r\nURL=file:///C:/Windows/System32/calc.exe\r\n");
        Assert.Null(Seed().OnlineUri);
    }

    [Fact]
    public void A_missing_or_broken_file_means_no_shows()
    {
        var missing = CardShowBook.Load(_drive.Layout);
        Assert.False(missing.HasShows);
        Assert.Null(missing.Problem);

        _drive.File(@"Projects/Card Shows/data/shows.json", "{ not json");
        var broken = CardShowBook.Load(_drive.Layout);
        Assert.False(broken.HasShows);
        Assert.NotNull(broken.Problem);
    }
}
