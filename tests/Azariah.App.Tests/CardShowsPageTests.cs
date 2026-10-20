using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Azariah.App.ViewModels;
using Azariah.App.Views;
using Azariah.Core.CardShows;
using Azariah.Core.Drive;

namespace Azariah.App.Tests;

public class CardShowsPageTests
{
    private const string Shows = """
        [
          {"event":"October Turtle Boyz Show","start":"2026-10-19","end":"2026-10-19","city":"Malden","state":"MA","when":"Upcoming","status":"Pending Review","table_type":"High-top bar table","price":35,"price_basis":"Exact","payment":"Full","paid":"Not billed yet","pay_note":"Charged after the host accepts you"},
          {"event":"the Big Free Admission Sports Card & More Show","start":"2026-10-24","city":"Marlboro","state":"MA","when":"Upcoming","status":"Waitlisted","price":55,"price_basis":"Exact","payment":"Full","paid":"Not billed yet"},
          {"event":"The Collector Expo - Boston/Medford","start":"2026-10-25","city":"Medford","state":"MA","when":"Upcoming","status":"Not applied","price":125,"price_basis":"Lowest listed","paid":"Not applied"},
          {"event":"CARDIVALLE - at DORCHESTER BREWING COMPANY","start":"2026-11-08","city":"Boston","state":"MA","when":"Upcoming","status":"Unpaid","price":75,"price_basis":"Exact","payment":"Full","due_now":80.57,"paid":"Unpaid"},
          {"event":"New England's Biggest 2 Day Free Admission Card Show","start":"2026-11-14","end":"2026-11-15","city":"North Attleborough","state":"MA","when":"Upcoming","status":"Unpaid","price":115,"price_basis":"Exact","payment":"Deposit","due_now":21.94,"due_at_show":95,"paid":"Unpaid"},
          {"event":"South Shore Card Show","start":"2026-10-18","city":"Weymouth","state":"MA","when":"Past","status":"Had table","price":100,"paid":"Paid","table":"98"},
          {"event":"Union Station Card Show","start":"2026-09-27","city":"Worcester","state":"MA","when":"Past","status":"No table","paid":"Withdrawn"}
        ]
        """;

    [AvaloniaFact]
    public void Card_shows_appear_on_Home_and_on_their_own_page()
    {
        var (window, workspace, folder) = Open(Shows);
        File.WriteAllText(Path.Combine(folder, CardShowBook.DashboardFile), "<html></html>");
        File.WriteAllText(Path.Combine(folder, CardShowBook.OnlineShortcut), "[InternetShortcut]\r\nURL=https://claude.ai/artifact/example\r\n");
        var shots = Environment.GetEnvironmentVariable("AZARIAH_SCREENSHOTS");

        workspace.SelectedNav = workspace.NavItems[0];
        Settle();
        var home = Assert.IsType<HomeViewModel>(workspace.CurrentPage);
        Assert.Equal(4, home.CardShows.Count);
        Assert.Equal("October Turtle Boyz Show", home.CardShows[0].Name);
        Assert.Contains("$102.51 due now for 2 shows", home.CardShowsSummary, StringComparison.Ordinal);
        Save(window, shots, "home-cardshows.png");

        Assert.Equal("cardshows", workspace.NavItems[1].Key);
        workspace.OpenCardShows();
        Settle();
        var page = Assert.IsType<CardShowsPageViewModel>(workspace.CurrentPage);
        Assert.Equal("cardshows", workspace.SelectedNav?.Key);
        Assert.Equal(5, page.Upcoming.Count);
        Assert.Equal(2, page.Past.Count);
        Assert.Equal("$102.51", page.DueNowText);
        Assert.Equal("$95", page.DueAtShowsText);
        Assert.Equal("$90", page.NotBilledText);
        Assert.Equal("$100", page.SpentText);
        Assert.True(page.IsShowsTab);
        Assert.True(page.HasDashboard);
        Assert.True(page.HasOnline);
        Assert.Equal("Sat, Nov 14 – Nov 15", page.Upcoming[4].DateText);
        Assert.Equal("from $125", page.Upcoming[2].CostText);
        Save(window, shots, "cardshows.png");

        window.Close();
    }

    private const string PastShows = """
        [
          {"event":"Kingston MA (Gone Broke)","start":"2026-09-20","when":"Past","status":"Had table","price":74,"paid":"Paid","table":"4"},
          {"event":"Cards After Dark card show","start":"2026-09-27","when":"Past","status":"Had table","price":93,"paid":"Paid elsewhere","table":"25"},
          {"event":"South Shore Card Show","start":"2026-10-18","when":"Past","status":"Had table","price":100,"paid":"Paid","table":"98"},
          {"event":"October Turtle Boyz Show","start":"2026-10-19","when":"Past","status":"Had table","price":20,"paid":"Paid elsewhere"}
        ]
        """;

    private const string Results = """
        [
          {"date":"2026-09-20","event":"Kingston MA (Gone Broke)","trans":11,"start":3890,"end":4500.83,"realized":47.63,"start_cash":600,"end_cash":277},
          {"date":"2026-09-27","event":"Cards After Dark card show","trans":9,"start":4828.83,"end":4448.21,"realized":231.34,"start_cash":605,"end_cash":1040},
          {"date":"2026-10-18","event":"South Shore Card Show","trans":25,"start":4244.42,"end":4470.62,"realized":92.72,"start_cash":300,"end_cash":461.48},
          {"date":"2026-10-19","event":"October Turtle Boyz Show","trans":8,"start":4269.14,"end":4244.42,"realized":113.78,"start_cash":260,"end_cash":423}
        ]
        """;

    private const string Allocation = """
        [
          {"event":"Kingston MA (Gone Broke)","date":"2026-09-20","growth_estimate":74},
          {"event":"Cards After Dark card show","date":"2026-09-27","growth_estimate":400},
          {"event":"South Shore Card Show","date":"2026-10-18","growth_estimate":362.43},
          {"event":"October Turtle Boyz Show","date":"2026-10-19","growth_estimate":-24.72}
        ]
        """;

    private const string Walk = """
        [
          {"step":"1","show":"October Turtle Boyz Show","date":"2026-10-19","label":"Start: collection + cash","amount":4269.14,"kind":"total","detail":"Oct 18 end collection $4,009.14 + $260.00 cash"},
          {"step":"2","show":"October Turtle Boyz Show","date":"2026-10-19","label":"Sold 5 items","amount":-4.75,"kind":"change","detail":"$422.75 of market value left, $418.00 came in as sales."},
          {"step":"3","show":"October Turtle Boyz Show","date":"2026-10-19","label":"Trades and buys","amount":-103.86,"kind":"change","detail":"8 cards out ($1,336.88) + $255.00 cash for 14 cards in ($1,488.02)."},
          {"step":"4","show":"October Turtle Boyz Show","date":"2026-10-19","label":"End: collection + cash","amount":4244.42,"kind":"total","detail":"Oct 19 collection $3,821.42 + $423.00 cash."}
        ]
        """;

    private const string Moves = """
        [
          {"id":"s1","move":"Sold","card":"30th Celebration Elite Trainer Box","detail":"Sealed","market":124.38,"amount":125,"realized":35.7,"show":"October Turtle Boyz Show"},
          {"id":"s2","move":"Sold","card":"Bulbasaur","detail":"Near Mint","market":3.42,"amount":3,"realized":-0.16,"show":"October Turtle Boyz Show"},
          {"id":"o1","move":"Traded out","card":"Ash-Greninja ex","detail":"PSA 8","market":400,"amount":null,"realized":null,"show":"October Turtle Boyz Show"},
          {"id":"i1","move":"Came in","card":"Shining Arceus","detail":"PSA 10","market":531.15,"amount":427.5,"realized":null,"show":"October Turtle Boyz Show"}
        ]
        """;

    [AvaloniaFact]
    public void Results_and_Collection_tabs_show_the_dashboard_inside_the_app()
    {
        var (window, workspace, folder) = Open();
        var data = Path.Combine(folder, "data");
        File.WriteAllText(Path.Combine(data, CardShowBook.DataFile), PastShows);
        File.WriteAllText(Path.Combine(data, "results.json"), Results);
        File.WriteAllText(Path.Combine(data, "results-display-allocation.json"), Allocation);
        File.WriteAllText(Path.Combine(data, "portfolio-change.json"), Walk);
        File.WriteAllText(Path.Combine(data, "portfolio-moves.json"), Moves);
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        var rows = Enumerable.Range(1, 40).Select(i => string.Create(inv,
            $"{{\"name\":\"Card number {i} with a fairly long name\",\"set\":\"Evolving Skies\",\"condition\":\"Near Mint\",\"kind\":\"{(i == 1 ? "graded" : "raw")}\",\"quantity\":{(i % 3 == 0 ? 2 : 1)},\"value\":{400 - (i * 9)}.5,\"cost_each\":{300 - (i * 7)}.25}}"));
        File.WriteAllText(Path.Combine(data, "collection.json"), "[" + string.Join(",", rows) + "]");
        File.WriteAllText(Path.Combine(folder, CardShowBook.DashboardFile), "<html></html>");
        var shots = Environment.GetEnvironmentVariable("AZARIAH_SCREENSHOTS");

        workspace.OpenCardShows();
        Settle();
        var page = Assert.IsType<CardShowsPageViewModel>(workspace.CurrentPage);

        page.OpenResultsTabCommand.Execute(null);
        Settle();
        Assert.True(page.IsResultsTab);
        Assert.False(page.IsShowsTab);
        Assert.Equal(new[] { "Portfolio change", "\u2212 Show costs", "= After cost", "Realized (info)", "Potential" }, page.ResultFigures.Select(f => f.Label));
        Assert.Equal("$4,350.17", page.ResultFigures[4].Text);
        Assert.Equal(4, page.ResultRows.Count);
        var turtle = page.ResultRows[0];
        Assert.Equal("October Turtle Boyz Show", turtle.Show);
        Assert.Equal(("\u221224.72", true), (turtle.PortfolioChange.Text, turtle.PortfolioChange.IsNegative));
        Assert.Equal("20.00", turtle.Cost);
        Assert.Equal("\u221244.72", turtle.AfterCost.Text);
        Assert.Equal("113.78", turtle.Realized);
        Assert.Equal("4,350.17", turtle.Potential);
        Assert.Equal("\u22123.09", turtle.PerTrans.Text);
        Assert.StartsWith("Portfolio change \u00b7 October Turtle Boyz Show, Oct 19, 2026", page.WalkTitle, StringComparison.Ordinal);
        Assert.Equal(4, page.WalkRows.Count);
        Assert.Equal(4, page.MoveRows.Count);
        Assert.Equal("\u22120.16", page.MoveRows[1].Realized.Text);
        Save(window, shots, "cardshows-results.png");

        page.OpenCollectionTabCommand.Execute(null);
        Settle();
        Assert.True(page.IsCollectionTab);
        Assert.Equal(40, page.CollectionRows.Count);
        Assert.Equal("Market value", page.CollectionFigures[0].Label);
        Assert.Equal("Unrealized", page.CollectionFigures[2].Label);
        Assert.True(page.CollectionFigures[2].IsPositive);
        Assert.Equal(new[] { "Raw", "Graded", "Sealed" }, page.KindFigures.Select(f => f.Label));
        Save(window, shots, "cardshows-collection.png");

        window.Close();
    }

    private static (MainWindow Window, WorkspaceViewModel Workspace, string Folder) Open(string? shows = null)
    {
        var root = Path.Combine(Path.GetTempPath(), "azariah-ui", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var volumes = new SystemVolumeProvider();
        var marker = new DriveInitializer(volumes).Initialize(root, "AZARIAH");
        SeedData.Write(root);
        var folder = CardShowBook.FolderFor(new DriveLayout(root));
        Directory.CreateDirectory(Path.Combine(folder, "data"));
        if (shows is not null)
        {
            File.WriteAllText(Path.Combine(folder, "data", CardShowBook.DataFile), shows);
        }

        var main = new MainViewModel(new LocateResult(LocateOutcome.Found, root, marker, "test", []), volumes, new DriveRootLocator(volumes));
        var window = new MainWindow { DataContext = main, Width = 1280, Height = 800 };
        main.AttachWindow(window);
        window.Show();
        return (window, Assert.IsType<WorkspaceViewModel>(main.Current), folder);
    }

    private static void Settle()
    {
        for (var i = 0; i < 4; i++)
        {
            Dispatcher.UIThread.RunJobs();
            Thread.Sleep(60);
        }
    }

    private static void Save(MainWindow window, string? shots, string name)
    {
        var frame = window.CaptureRenderedFrame();
        Assert.NotNull(frame);
        if (!string.IsNullOrEmpty(shots))
        {
            Directory.CreateDirectory(shots);
            using var stream = File.Create(Path.Combine(shots, name));
            frame!.Save(stream, new Avalonia.Media.Imaging.PngBitmapEncoderOptions());
        }
    }
}
