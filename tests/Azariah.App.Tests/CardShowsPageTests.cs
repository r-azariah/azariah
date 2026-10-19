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
        var root = Path.Combine(Path.GetTempPath(), "azariah-ui", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var volumes = new SystemVolumeProvider();
        var marker = new DriveInitializer(volumes).Initialize(root, "AZARIAH");
        SeedData.Write(root);
        var folder = CardShowBook.FolderFor(new DriveLayout(root));
        Directory.CreateDirectory(Path.Combine(folder, "data"));
        File.WriteAllText(Path.Combine(folder, "data", CardShowBook.DataFile), Shows);
        File.WriteAllText(Path.Combine(folder, CardShowBook.DashboardFile), "<html></html>");
        File.WriteAllText(Path.Combine(folder, CardShowBook.OnlineShortcut), "[InternetShortcut]\r\nURL=https://claude.ai/artifact/example\r\n");

        var main = new MainViewModel(new LocateResult(LocateOutcome.Found, root, marker, "test", []), volumes, new DriveRootLocator(volumes));
        var window = new MainWindow { DataContext = main, Width = 1280, Height = 800 };
        main.AttachWindow(window);
        window.Show();
        var shots = Environment.GetEnvironmentVariable("AZARIAH_SCREENSHOTS");
        var workspace = Assert.IsType<WorkspaceViewModel>(main.Current);

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
        Assert.True(page.HasDashboard);
        Assert.True(page.HasOnline);
        Assert.Equal("Sat, Nov 14 – Nov 15", page.Upcoming[4].DateText);
        Assert.Equal("from $125", page.Upcoming[2].CostText);
        Save(window, shots, "cardshows.png");

        window.Close();
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
