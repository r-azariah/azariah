using System.Globalization;

namespace Azariah.App.Services;

public static class Format
{
    private static readonly string[] Units = ["B", "KB", "MB", "GB", "TB"];

    public static string Bytes(long bytes)
    {
        double value = bytes;
        var unit = 0;
        while (value >= 1024 && unit < Units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        return unit == 0
            ? string.Create(CultureInfo.CurrentCulture, $"{bytes} B")
            : string.Create(CultureInfo.CurrentCulture, $"{value:0.#} {Units[unit]}");
    }

    public static string Date(DateTime utc) =>
        utc.ToLocalTime().ToString("MMM d, yyyy  h:mm tt", CultureInfo.CurrentCulture);

    public static string Ago(DateTime utc)
    {
        var span = DateTime.UtcNow - utc;
        return span.TotalMinutes switch
        {
            < 1 => "just now",
            < 60 => $"{(int)span.TotalMinutes} min ago",
            < 60 * 24 => $"{(int)span.TotalHours} hr ago",
            < 60 * 24 * 7 => $"{(int)span.TotalDays} d ago",
            _ => utc.ToLocalTime().ToString("MMM d", CultureInfo.CurrentCulture),
        };
    }

    public static string Greeting()
    {
        var hour = DateTime.Now.Hour;
        return hour switch
        {
            < 5 => "Up late.",
            < 12 => "Good morning.",
            < 17 => "Good afternoon.",
            _ => "Good evening.",
        };
    }
}
