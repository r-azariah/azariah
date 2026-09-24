namespace Azariah.Core.Files;

/// <summary>
/// Validates names using Windows rules regardless of the OS the code runs on, because the
/// drive is formatted and used for Windows.
/// </summary>
public static class FileNameRules
{
    private static readonly char[] InvalidChars = ['<', '>', ':', '"', '/', '\\', '|', '?', '*'];

    private static readonly HashSet<string> ReservedNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
    };

    public const int MaxNameLength = 255;

    /// <summary>Returns null when the name is valid, otherwise a human readable reason.</summary>
    public static string? Validate(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return "Name cannot be empty.";
        }

        if (name.Length > MaxNameLength)
        {
            return $"Name is longer than {MaxNameLength} characters.";
        }

        if (name is "." or "..")
        {
            return "That name is reserved.";
        }

        foreach (var c in name)
        {
            if (c < 32 || Array.IndexOf(InvalidChars, c) >= 0)
            {
                return "Names can't contain < > : \" / \\ | ? * or control characters.";
            }
        }

        if (name.EndsWith(' ') || name.EndsWith('.'))
        {
            return "Names can't end with a space or a period.";
        }

        var stem = name.Split('.')[0].TrimEnd();
        if (ReservedNames.Contains(stem))
        {
            return $"\"{stem}\" is reserved by Windows.";
        }

        return null;
    }

    public static bool IsValid(string? name) => Validate(name) is null;

    /// <summary>Turns arbitrary text (e.g. an app name) into a safe folder name.</summary>
    public static string Sanitize(string text, string fallback = "Untitled")
    {
        ArgumentNullException.ThrowIfNull(text);
        var chars = text.Select(c => c < 32 || Array.IndexOf(InvalidChars, c) >= 0 ? '_' : c).ToArray();
        var cleaned = new string(chars).Trim().TrimEnd('.').Trim();
        if (cleaned.Length > 100)
        {
            cleaned = cleaned[..100].TrimEnd();
        }

        return IsValid(cleaned) ? cleaned : fallback;
    }
}
