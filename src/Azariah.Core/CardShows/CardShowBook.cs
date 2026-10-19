using System.Globalization;
using System.Text.Json;
using Azariah.Core.Drive;

namespace Azariah.Core.CardShows;

/// <summary>One show from <c>Projects\Card Shows\data\shows.json</c>: a table applied for, booked or vended.</summary>
public sealed record CardShow(
    string Name,
    DateOnly Start,
    DateOnly End,
    string Town,
    bool IsUpcoming,
    string Status,
    string TableType,
    decimal? Price,
    bool PriceIsLowest,
    string Payment,
    decimal? DueNow,
    decimal? DueAtShow,
    string Paid,
    string TableNumber,
    string Note)
{
    public bool NeedsPayment => Paid == "Unpaid";

    public bool NotBilledYet => IsUpcoming && Paid == "Not billed yet";

    public bool NotApplied => Paid == "Not applied";

    public bool HadTable => !IsUpcoming && Paid.StartsWith("Paid", StringComparison.Ordinal);
}

/// <summary>
/// The card show tracker kept on the drive. Claude writes the data file from OnTreasure and the owner's notes;
/// this only reads it. A missing or broken file means "no card shows", never an error on Home.
/// </summary>
public sealed class CardShowBook
{
    public const string FolderName = "Card Shows";
    public const string DataFile = "shows.json";
    public const string DashboardFile = "AZARIAH Supply Dashboard.html";
    public const string OnlineShortcut = "Online dashboard.url";

    private CardShowBook(string folder, IReadOnlyList<CardShow> shows, string? problem)
    {
        Folder = folder;
        Shows = shows;
        Problem = problem;
    }

    public string Folder { get; }

    public IReadOnlyList<CardShow> Shows { get; }

    /// <summary>Why the data file couldn't be read, if it exists but is broken.</summary>
    public string? Problem { get; }

    public bool HasShows => Shows.Count > 0;

    /// <summary>Upcoming, soonest first.</summary>
    public IEnumerable<CardShow> Upcoming => Shows.Where(s => s.IsUpcoming).OrderBy(s => s.Start).ThenBy(s => s.Name, StringComparer.Ordinal);

    /// <summary>Past, most recent first.</summary>
    public IEnumerable<CardShow> Past => Shows.Where(s => !s.IsUpcoming).OrderByDescending(s => s.Start).ThenBy(s => s.Name, StringComparer.Ordinal);

    public decimal DueNow => Shows.Where(s => s.NeedsPayment).Sum(s => s.DueNow ?? 0);

    public decimal DueAtShows => Shows.Where(s => s.IsUpcoming).Sum(s => s.DueAtShow ?? 0);

    public decimal NotBilled => Shows.Where(s => s.NotBilledYet).Sum(s => s.Price ?? 0);

    public decimal SpentOnPastTables => Shows.Where(s => s.HadTable).Sum(s => s.Price ?? 0);

    public string? DashboardPath => ExistingFile(DashboardFile);

    /// <summary>The claude.ai dashboard, read from the folder's Internet shortcut.</summary>
    public Uri? OnlineUri
    {
        get
        {
            var path = ExistingFile(OnlineShortcut);
            if (path is null)
            {
                return null;
            }

            try
            {
                foreach (var line in File.ReadLines(path))
                {
                    if (line.StartsWith("URL=", StringComparison.OrdinalIgnoreCase)
                        && Uri.TryCreate(line[4..].Trim(), UriKind.Absolute, out var uri)
                        && uri.Scheme == Uri.UriSchemeHttps)
                    {
                        return uri;
                    }
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
            }

            return null;
        }
    }

    public static string FolderFor(DriveLayout layout) => Path.Combine(layout.Root, "Projects", FolderName);

    public static CardShowBook Load(DriveLayout layout) => Load(FolderFor(layout));

    public static CardShowBook Load(string folder)
    {
        var path = Path.Combine(folder, "data", DataFile);
        if (!File.Exists(path))
        {
            return new CardShowBook(folder, [], null);
        }

        try
        {
            using var stream = File.OpenRead(path);
            using var doc = JsonDocument.Parse(stream, new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip });
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
            {
                return new CardShowBook(folder, [], "shows.json isn't a list of shows.");
            }

            var shows = new List<CardShow>();
            foreach (var row in doc.RootElement.EnumerateArray())
            {
                if (Parse(row) is { } show)
                {
                    shows.Add(show);
                }
            }

            return new CardShowBook(folder, shows, null);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return new CardShowBook(folder, [], "Couldn't read shows.json: " + ex.Message);
        }
    }

    private static CardShow? Parse(JsonElement row)
    {
        if (row.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var name = Text(row, "event");
        if (string.IsNullOrWhiteSpace(name) || !TryDate(Text(row, "start"), out var start))
        {
            return null;
        }

        var end = TryDate(Text(row, "end"), out var e) && e >= start ? e : start;
        var city = Text(row, "city");
        var state = Text(row, "state");
        var town = string.IsNullOrEmpty(state) ? city : string.IsNullOrEmpty(city) ? state : city + ", " + state;

        return new CardShow(
            name.Trim(),
            start,
            end,
            town,
            string.Equals(Text(row, "when"), "Upcoming", StringComparison.OrdinalIgnoreCase),
            Text(row, "status"),
            Text(row, "table_type"),
            Number(row, "price"),
            string.Equals(Text(row, "price_basis"), "Lowest listed", StringComparison.OrdinalIgnoreCase),
            Text(row, "payment"),
            Number(row, "due_now"),
            Number(row, "due_at_show"),
            Text(row, "paid"),
            Text(row, "table"),
            Text(row, "pay_note"));
    }

    private static string Text(JsonElement row, string name) =>
        row.TryGetProperty(name, out var v) ? v.ValueKind switch
        {
            JsonValueKind.String => v.GetString() ?? string.Empty,
            JsonValueKind.Number => v.GetRawText(),
            _ => string.Empty,
        } : string.Empty;

    private static decimal? Number(JsonElement row, string name)
    {
        if (!row.TryGetProperty(name, out var v))
        {
            return null;
        }

        return v.ValueKind switch
        {
            JsonValueKind.Number when v.TryGetDecimal(out var d) => d,
            JsonValueKind.String when decimal.TryParse(v.GetString(), NumberStyles.Number, CultureInfo.InvariantCulture, out var d) => d,
            _ => null,
        };
    }

    private static bool TryDate(string text, out DateOnly date) =>
        DateOnly.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date);

    private string? ExistingFile(string name)
    {
        var path = Path.Combine(Folder, name);
        return File.Exists(path) ? path : null;
    }
}
