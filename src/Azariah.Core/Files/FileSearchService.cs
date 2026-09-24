using System.IO.Enumeration;
using System.Runtime.CompilerServices;
using Azariah.Core.Drive;

namespace Azariah.Core.Files;

public sealed record SearchQuery(string Text, bool IncludeHidden = false, IReadOnlySet<FileKind>? Kinds = null, int MaxResults = 2000);

/// <summary>
/// Recursive name search. Plain text matches anywhere in the name; <c>*</c> and <c>?</c> act as
/// wildcards (e.g. <c>*.rbxl</c>). App-managed areas are never searched.
/// </summary>
public sealed class FileSearchService(DriveLayout layout)
{
    public async IAsyncEnumerable<FileEntry> SearchAsync(
        string folder,
        SearchQuery query,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        var text = query.Text.Trim();
        var hasKinds = query.Kinds is { Count: > 0 };
        if (text.Length == 0 && !hasKinds)
        {
            yield break;
        }

        var isPattern = text.Contains('*', StringComparison.Ordinal) || text.Contains('?', StringComparison.Ordinal);
        var pending = new Stack<string>();
        pending.Push(PathGuard.Normalize(folder));
        var found = 0;
        var options = new EnumerationOptions { IgnoreInaccessible = true, AttributesToSkip = 0 };

        while (pending.Count > 0)
        {
            ct.ThrowIfCancellationRequested();
            var current = pending.Pop();
            IEnumerable<FileSystemInfo> children;
            try
            {
                children = new DirectoryInfo(current).EnumerateFileSystemInfos("*", options).ToList();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                continue;
            }

            foreach (var info in children)
            {
                if (layout.IsProtected(info.FullName))
                {
                    continue;
                }

                var entry = FileEntry.FromInfo(info);
                if (!query.IncludeHidden && entry.IsHidden)
                {
                    continue;
                }

                if (entry.IsDirectory)
                {
                    pending.Push(entry.FullPath);
                }

                if (Matches(entry, text, isPattern, query.Kinds))
                {
                    yield return entry;
                    if (++found >= query.MaxResults)
                    {
                        yield break;
                    }
                }
            }

            // Keep the UI responsive on huge trees.
            await Task.Yield();
        }
    }

    private static bool Matches(FileEntry entry, string text, bool isPattern, IReadOnlySet<FileKind>? kinds)
    {
        if (kinds is { Count: > 0 } && !kinds.Contains(entry.Kind))
        {
            return false;
        }

        if (text.Length == 0)
        {
            return true;
        }

        return isPattern
            ? FileSystemName.MatchesSimpleExpression(text, entry.Name, ignoreCase: true)
            : entry.Name.Contains(text, StringComparison.OrdinalIgnoreCase);
    }
}
