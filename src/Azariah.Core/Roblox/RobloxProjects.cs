using System.Globalization;
using System.Text.RegularExpressions;
using Azariah.Core.Drive;
using Azariah.Core.Files;

namespace Azariah.Core.Roblox;

/// <summary>A place save or checkpoint: <c>&lt;Name&gt; (LATEST 2026-09-22).rbxl</c>, or a dated entry in Old Versions.</summary>
public sealed record SaveEntry(string Path, string Name, DateOnly? Date, long? Size, bool IsFolder, DateTime ModifiedUtc);

/// <summary>The newest entry in a project's PASSES.md.</summary>
public sealed record PassEntry(string Heading, DateOnly? Date, string? FirstLine);

/// <summary>
/// One game in <c>Roblox\Games\&lt;GAME&gt;\</c>, read straight from its files (CLAUDE.md, PASSES.md,
/// the LATEST save, Old Versions). Nothing is stored anywhere else, so passes written by Claude on any
/// PC show up as soon as the drive is plugged in.
/// </summary>
public sealed record RobloxProject(
    string Folder,
    string Title,
    IReadOnlyList<string> Summary,
    IReadOnlyList<string> NextStep,
    string? PlaceId,
    string? UniverseId,
    SaveEntry? Latest,
    IReadOnlyList<SaveEntry> OldVersions,
    PassEntry? LastPass,
    DateTime LastChangedUtc)
{
    public string NotesPath => Path.Combine(Folder, RobloxProjects.NotesFileName);

    public string OldVersionsFolder => Path.Combine(Folder, RobloxProjects.OldVersionsFolderName);

    public Uri? PlaceUri => PlaceId is null ? null : new Uri($"https://www.roblox.com/games/{PlaceId}");
}

public static partial class RobloxProjects
{
    public const string NotesFileName = "CLAUDE.md";
    public const string PassesFileName = "PASSES.md";
    public const string OldVersionsFolderName = "Old Versions";

    private const int MaxSummaryLines = 4;
    private const int MaxNextStepLines = 3;

    /// <summary>Every game folder under <c>Roblox\Games</c>: games with saves or notes first, then by last change.</summary>
    public static IReadOnlyList<RobloxProject> Load(DriveLayout layout)
    {
        ArgumentNullException.ThrowIfNull(layout);
        if (!Directory.Exists(layout.Games))
        {
            return [];
        }

        var projects = new List<RobloxProject>();
        foreach (var folder in SafeDirectories(layout.Games))
        {
            if (FileEntry.TryFromPath(folder) is { IsHidden: false })
            {
                projects.Add(LoadProject(folder));
            }
        }

        return projects
            .OrderByDescending(p => p.Latest is not null || p.LastPass is not null || p.Summary.Count > 0 || p.OldVersions.Count > 0)
            .ThenByDescending(p => p.LastChangedUtc)
            .ToList();
    }

    public static RobloxProject LoadProject(string folder)
    {
        ArgumentNullException.ThrowIfNull(folder);
        var name = Path.GetFileName(PathGuard.Normalize(folder));
        var notes = ReadLines(Path.Combine(folder, NotesFileName));
        var passes = ReadLines(Path.Combine(folder, PassesFileName));

        var title = TitleFrom(notes) ?? name;
        var latest = FindLatest(folder);
        var oldVersions = ListOldVersions(Path.Combine(folder, OldVersionsFolderName));

        var changed = new List<DateTime> { SafeWriteTime(folder) };
        changed.Add(SafeWriteTime(Path.Combine(folder, NotesFileName)));
        changed.Add(SafeWriteTime(Path.Combine(folder, PassesFileName)));
        if (latest is not null)
        {
            changed.Add(latest.ModifiedUtc);
        }

        return new RobloxProject(
            folder,
            title,
            IntroLines(notes),
            SectionLines(notes, "next", MaxNextStepLines),
            IdFrom(notes, PlaceIdPattern()),
            IdFrom(notes, UniverseIdPattern()),
            latest,
            oldVersions,
            LastPassFrom(passes),
            changed.Max());
    }

    /// <summary>
    /// Makes <paramref name="sourcePlace"/> the new LATEST save of the project, the same steps a pass
    /// does by hand: the file is copied in (the original stays where it is), the previous LATEST moves
    /// to Old Versions as <c>yyyy-MM-dd &lt;Name&gt;.rbxl</c>, and the new file becomes
    /// <c>&lt;Name&gt; (LATEST yyyy-MM-dd).rbxl</c>. Returns the new LATEST path.
    /// </summary>
    public static async Task<string> SaveNewVersionAsync(
        FileOperationService files,
        RobloxProject project,
        string sourcePlace,
        DateOnly today,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(sourcePlace);

        var source = PathGuard.Normalize(sourcePlace);
        if (FileKinds.FromPath(source) != FileKind.RobloxPlace)
        {
            throw new FileOperationException("Pick a Roblox place file (.rbxl or .rbxlx).");
        }

        var extension = string.Equals(Path.GetExtension(source), ".rbxlx", StringComparison.OrdinalIgnoreCase) ? ".rbxlx" : ".rbxl";

        if (!File.Exists(source))
        {
            throw new FileOperationException("That file no longer exists.");
        }

        if (project.Latest is { } current && PathGuard.AreSame(current.Path, source))
        {
            throw new FileOperationException("That's already the latest save.");
        }

        var baseName = project.Latest is { } l && LatestName().Match(l.Name) is { Success: true } m
            ? m.Groups["name"].Value
            : SafeBaseName(project);

        // 1. Bring the new save into the project folder first, so a failure changes nothing else.
        string incoming;
        if (PathGuard.AreSame(Path.GetDirectoryName(source)!, project.Folder))
        {
            incoming = source;
        }
        else
        {
            var copy = await files.CopyAsync([source], project.Folder, ConflictPolicy.KeepBoth, null, ct);
            if (copy.HasErrors || copy.CreatedPaths.Count == 0)
            {
                throw new FileOperationException(copy.Errors.Count > 0 ? copy.Errors[0].Message : "The file couldn't be copied.");
            }

            incoming = copy.CreatedPaths[0];
        }

        // 2. The previous LATEST goes to Old Versions under its date.
        if (project.Latest is { } previous && File.Exists(previous.Path))
        {
            var date = previous.Date ?? DateOnly.FromDateTime(previous.ModifiedUtc.ToLocalTime());
            var archivedName = string.Create(CultureInfo.InvariantCulture, $"{date:yyyy-MM-dd} {baseName}{Path.GetExtension(previous.Path)}");
            var renamed = files.Rename(previous.Path, archivedName);
            if (!Directory.Exists(project.OldVersionsFolder))
            {
                files.CreateFolder(project.Folder, OldVersionsFolderName);
            }

            var move = await files.MoveAsync([renamed], project.OldVersionsFolder, ConflictPolicy.KeepBoth, null, ct);
            if (move.HasErrors)
            {
                throw new FileOperationException(move.Errors[0].Message);
            }
        }

        // 3. The incoming file becomes the new LATEST.
        var latestName = string.Create(CultureInfo.InvariantCulture, $"{baseName} (LATEST {today:yyyy-MM-dd}){extension}");
        return files.Rename(incoming, latestName);
    }

    [GeneratedRegex(@"^(?<name>.+?) \(LATEST (?<date>\d{4}-\d{2}-\d{2})\)\.rbxlx?$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex LatestName();

    [GeneratedRegex(@"^(?<date>\d{4}-\d{2}-\d{2})\b", RegexOptions.CultureInvariant)]
    private static partial Regex DatePrefix();

    [GeneratedRegex(@"\d{4}-\d{2}-\d{2}", RegexOptions.CultureInvariant)]
    private static partial Regex AnyDate();

    [GeneratedRegex(@"\bplace(?:\s+id)?\s*[:#]?\s*(?<id>\d{6,})", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex PlaceIdPattern();

    [GeneratedRegex(@"\buniverse(?:\s+id)?\s*[:#]?\s*(?<id>\d{6,})", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex UniverseIdPattern();

    [GeneratedRegex(@"\s*\([^)]*\)\s*$", RegexOptions.CultureInvariant)]
    private static partial Regex TrailingParenthetical();

    private static SaveEntry? FindLatest(string folder)
    {
        SaveEntry? best = null;
        foreach (var file in SafeFiles(folder))
        {
            var name = Path.GetFileName(file);
            var match = LatestName().Match(name);
            if (!match.Success)
            {
                continue;
            }

            var entry = ToEntry(file, ParseDate(match.Groups["date"].Value));
            if (entry is not null && (best is null || Compare(entry, best) > 0))
            {
                best = entry;
            }
        }

        return best;
    }

    private static List<SaveEntry> ListOldVersions(string folder)
    {
        if (!Directory.Exists(folder))
        {
            return [];
        }

        var entries = new List<SaveEntry>();
        foreach (var path in SafeDirectories(folder).Concat(SafeFiles(folder)))
        {
            var match = DatePrefix().Match(Path.GetFileName(path));
            if (ToEntry(path, match.Success ? ParseDate(match.Groups["date"].Value) : null) is { } entry)
            {
                entries.Add(entry);
            }
        }

        entries.Sort((a, b) => Compare(b, a));
        return entries;
    }

    /// <summary>Newer date first; undated entries fall back to their modified time.</summary>
    private static int Compare(SaveEntry a, SaveEntry b)
    {
        var byDate = Nullable.Compare(a.Date, b.Date);
        if (byDate != 0)
        {
            return byDate;
        }

        var byTime = a.ModifiedUtc.CompareTo(b.ModifiedUtc);
        return byTime != 0 ? byTime : string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
    }

    private static SaveEntry? ToEntry(string path, DateOnly? date)
    {
        var info = FileEntry.TryFromPath(path);
        if (info is null || info.IsHidden)
        {
            return null;
        }

        return new SaveEntry(info.FullPath, info.Name, date, info.Size, info.IsDirectory, info.ModifiedUtc);
    }

    private static DateOnly? ParseDate(string text) =>
        DateOnly.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) ? date : null;

    private static string? TitleFrom(List<string> notes)
    {
        foreach (var line in notes)
        {
            if (line.StartsWith("# ", StringComparison.Ordinal))
            {
                var title = TrailingParenthetical().Replace(line[2..].Trim(), string.Empty).Trim();
                return title.Length > 0 ? title : null;
            }
        }

        return null;
    }

    /// <summary>The lines between the title and the first section: what the game is and where it stands.</summary>
    private static List<string> IntroLines(List<string> notes)
    {
        var lines = new List<string>();
        var afterTitle = !notes.Any(l => l.StartsWith("# ", StringComparison.Ordinal));
        foreach (var line in notes)
        {
            if (line.StartsWith("# ", StringComparison.Ordinal))
            {
                afterTitle = true;
                continue;
            }

            if (line.StartsWith("## ", StringComparison.Ordinal))
            {
                break;
            }

            if (afterTitle && Clean(line) is { Length: > 0 } text)
            {
                lines.Add(text);
                if (lines.Count == MaxSummaryLines)
                {
                    break;
                }
            }
        }

        return lines;
    }

    /// <summary>The first lines of a <c>## </c> section whose heading starts with <paramref name="headingStart"/>.</summary>
    private static List<string> SectionLines(List<string> notes, string headingStart, int max)
    {
        var lines = new List<string>();
        var inside = false;
        foreach (var line in notes)
        {
            if (line.StartsWith("## ", StringComparison.Ordinal))
            {
                if (inside)
                {
                    break;
                }

                inside = line[3..].TrimStart().StartsWith(headingStart, StringComparison.OrdinalIgnoreCase);
                continue;
            }

            if (inside && Clean(line) is { Length: > 0 } text)
            {
                lines.Add(text);
                if (lines.Count == max)
                {
                    break;
                }
            }
        }

        return lines;
    }

    private static string? IdFrom(List<string> notes, Regex pattern)
    {
        foreach (var line in notes)
        {
            var match = pattern.Match(line);
            if (match.Success)
            {
                return match.Groups["id"].Value;
            }
        }

        return null;
    }

    private static PassEntry? LastPassFrom(List<string> passes)
    {
        for (var i = 0; i < passes.Count; i++)
        {
            if (!passes[i].StartsWith("## ", StringComparison.Ordinal))
            {
                continue;
            }

            var heading = passes[i][3..].Trim();
            var date = AnyDate().Match(heading) is { Success: true } d ? ParseDate(d.Value) : null;
            string? first = null;
            for (var j = i + 1; j < passes.Count && !passes[j].StartsWith('#'); j++)
            {
                if (Clean(passes[j]) is { Length: > 0 } text)
                {
                    first = text;
                    break;
                }
            }

            return new PassEntry(heading, date, first);
        }

        return null;
    }

    /// <summary>Markdown line to plain text: list markers, emphasis and code ticks removed.</summary>
    private static string Clean(string line)
    {
        var text = line.Trim();
        if (text.StartsWith("- ", StringComparison.Ordinal) || text.StartsWith("* ", StringComparison.Ordinal))
        {
            text = text[2..];
        }

        return text.Replace("**", string.Empty, StringComparison.Ordinal)
            .Replace("`", string.Empty, StringComparison.Ordinal)
            .Trim();
    }

    private static string SafeBaseName(RobloxProject project)
    {
        var title = project.Title.Trim();
        return title.Length > 0 && FileNameRules.Validate(title) is null ? title : Path.GetFileName(project.Folder);
    }

    private static List<string> ReadLines(string path)
    {
        try
        {
            return File.Exists(path) ? [.. File.ReadAllLines(path)] : [];
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    private static DateTime SafeWriteTime(string path)
    {
        try
        {
            return File.Exists(path) || Directory.Exists(path) ? File.GetLastWriteTimeUtc(path) : DateTime.MinValue;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return DateTime.MinValue;
        }
    }

    private static List<string> SafeDirectories(string folder)
    {
        try
        {
            return [.. Directory.EnumerateDirectories(folder).Order(StringComparer.OrdinalIgnoreCase)];
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    private static List<string> SafeFiles(string folder)
    {
        try
        {
            return [.. Directory.EnumerateFiles(folder)];
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }
}
