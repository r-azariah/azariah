namespace Azariah.Core.Files;

/// <summary>
/// Path comparisons used to keep every write inside the drive root. Windows file systems
/// (NTFS/exFAT/FAT32) are case-insensitive, so comparisons are too when running on Windows.
/// </summary>
public static class PathGuard
{
    public static StringComparison Comparison { get; } =
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    public static StringComparer Comparer { get; } =
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    /// <summary>Full path without a trailing separator (except for volume roots like <c>E:\</c>).</summary>
    public static string Normalize(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var full = Path.GetFullPath(path);
        var root = Path.GetPathRoot(full);
        if (!string.IsNullOrEmpty(root) && full.Length > root.Length)
        {
            full = Path.TrimEndingDirectorySeparator(full);
        }

        return full;
    }

    public static bool AreSame(string a, string b) => string.Equals(Normalize(a), Normalize(b), Comparison);

    /// <summary>True when <paramref name="path"/> is <paramref name="parent"/> or somewhere beneath it.</summary>
    public static bool IsInsideOrEqual(string parent, string path)
    {
        var p = Normalize(parent);
        var c = Normalize(path);
        if (string.Equals(p, c, Comparison))
        {
            return true;
        }

        var prefix = Path.EndsInDirectorySeparator(p) ? p : p + Path.DirectorySeparatorChar;
        return c.StartsWith(prefix, Comparison);
    }

    /// <summary>True when <paramref name="path"/> is strictly beneath <paramref name="parent"/>.</summary>
    public static bool IsStrictlyInside(string parent, string path) =>
        IsInsideOrEqual(parent, path) && !AreSame(parent, path);

    public static bool IsOnSameVolume(string a, string b) =>
        string.Equals(Path.GetPathRoot(Normalize(a)), Path.GetPathRoot(Normalize(b)), Comparison);
}
