using System.Globalization;
using System.Text;
using Azariah.Core.Drive;
using Azariah.Core.Files;
using Azariah.Core.Serialization;

namespace Azariah.Core.Notes;

public sealed record NoteInfo(string Path, string Title, string Folder, DateTime ModifiedUtc, string Preview);

/// <summary>
/// Notes are plain Markdown files in <c>&lt;drive&gt;\Notes</c>, laid out the way Obsidian does it, so the
/// folder opens as an Obsidian vault and the AI can later read and edit the same files through these calls.
/// Daily notes live in <c>Notes\Daily\yyyy-MM-dd.md</c> (Obsidian's default). Content is written
/// atomically; renames and deletes go through <see cref="FileOperationService"/> (deletes go to Trash).
/// </summary>
public sealed class NotesService(DriveLayout layout, FileOperationService files)
{
    public const string FolderName = "Notes";
    public const string DailyFolderName = "Daily";
    public const string Extension = ".md";

    private const int PreviewLength = 140;
    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false);

    public string Folder => Path.Combine(layout.Root, FolderName);

    public string DailyFolder => Path.Combine(Folder, DailyFolderName);

    public string DailyPath(DateOnly day) =>
        Path.Combine(DailyFolder, day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + Extension);

    /// <summary>Every note, most recently edited first. Dot folders (like Obsidian's <c>.obsidian</c>) are skipped.</summary>
    public IReadOnlyList<NoteInfo> List()
    {
        if (!Directory.Exists(Folder))
        {
            return [];
        }

        var options = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = true,
            AttributesToSkip = FileAttributes.Hidden | FileAttributes.System | FileAttributes.ReparsePoint,
        };

        var notes = new List<NoteInfo>();
        foreach (var path in Directory.EnumerateFiles(Folder, "*" + Extension, options))
        {
            var relative = Path.GetRelativePath(Folder, path);
            if (relative.Split(Path.DirectorySeparatorChar).Any(part => part.StartsWith('.')))
            {
                continue;
            }

            if (Describe(path) is { } note)
            {
                notes.Add(note);
            }
        }

        return notes.OrderByDescending(n => n.ModifiedUtc).ToList();
    }

    /// <summary>Notes whose title or text contains <paramref name="query"/> (ignoring case).</summary>
    public IReadOnlyList<NoteInfo> Search(string query)
    {
        ArgumentNullException.ThrowIfNull(query);
        var text = query.Trim();
        if (text.Length == 0)
        {
            return List();
        }

        return List()
            .Where(n => n.Title.Contains(text, StringComparison.OrdinalIgnoreCase)
                || ReadSafe(n.Path).Contains(text, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    public NoteInfo? Describe(string path)
    {
        try
        {
            var info = new FileInfo(path);
            if (!info.Exists)
            {
                return null;
            }

            var folder = Path.GetRelativePath(Folder, info.DirectoryName!);
            return new NoteInfo(info.FullName, Path.GetFileNameWithoutExtension(info.Name), folder == "." ? string.Empty : folder,
                info.LastWriteTimeUtc, Preview(info.FullName));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    public string Read(string path)
    {
        EnsureNote(path);
        return File.ReadAllText(path, Utf8);
    }

    public void Write(string path, string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        EnsureNote(path);
        AtomicFile.WriteAllBytes(path, Utf8.GetBytes(text));
    }

    /// <summary>Creates an empty note (the title is the file name, as in Obsidian) and returns its path.</summary>
    public string Create(string title = "Untitled")
    {
        ArgumentNullException.ThrowIfNull(title);
        EnsureFolder(layout.Root, FolderName);
        var name = FileNameRules.Validate(title) is null ? title.Trim() : "Untitled";
        var path = UniqueNames.Next(Path.Combine(Folder, name + Extension));
        AtomicFile.WriteAllBytes(path, []);
        return path;
    }

    /// <summary>Today's daily note, created with a heading if it doesn't exist yet.</summary>
    public string OpenDaily(DateOnly day)
    {
        EnsureFolder(layout.Root, FolderName);
        EnsureFolder(Folder, DailyFolderName);
        var path = DailyPath(day);
        if (!File.Exists(path))
        {
            Write(path, string.Create(CultureInfo.InvariantCulture, $"# {day:yyyy-MM-dd}\n\n"));
        }

        return path;
    }

    /// <summary>Quick capture: appends "- HH:mm text" to the daily note for <paramref name="now"/>.</summary>
    public string AppendToDaily(string text, DateTime now)
    {
        ArgumentNullException.ThrowIfNull(text);
        var path = OpenDaily(DateOnly.FromDateTime(now));
        var existing = File.ReadAllText(path, Utf8);
        var separator = existing.Length == 0 || existing.EndsWith('\n') ? string.Empty : "\n";
        Write(path, existing + separator + string.Create(CultureInfo.InvariantCulture, $"- {now:HH:mm} {text.Trim()}\n"));
        return path;
    }

    /// <summary>Renames a note (its title) and returns the new path.</summary>
    public string Rename(string path, string title)
    {
        ArgumentNullException.ThrowIfNull(title);
        EnsureNote(path);
        return files.Rename(path, title.Trim() + Extension);
    }

    public Task<OperationReport> MoveToTrashAsync(string path)
    {
        EnsureNote(path);
        return files.MoveToTrashAsync([path]);
    }

    private void EnsureNote(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        if (!PathGuard.IsInsideOrEqual(Folder, path)
            || !string.Equals(Path.GetExtension(path), Extension, StringComparison.OrdinalIgnoreCase))
        {
            throw new FileOperationException("That isn't a note.");
        }
    }

    private void EnsureFolder(string parent, string name)
    {
        if (!Directory.Exists(Path.Combine(parent, name)))
        {
            files.CreateFolder(parent, name);
        }
    }

    /// <summary>The first line of real text: not blank, not a heading.</summary>
    private static string Preview(string path)
    {
        try
        {
            using var reader = new StreamReader(path, Utf8);
            for (var i = 0; i < 40 && reader.ReadLine() is { } line; i++)
            {
                var text = line.Trim().TrimStart('-', '*', '>').Trim();
                if (text.Length > 0 && !line.TrimStart().StartsWith('#'))
                {
                    return text.Length > PreviewLength ? text[..PreviewLength] + "…" : text;
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }

        return string.Empty;
    }

    private static string ReadSafe(string path)
    {
        try
        {
            return File.ReadAllText(path, Utf8);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return string.Empty;
        }
    }
}
