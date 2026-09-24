using System.Runtime.CompilerServices;
using Azariah.Core.Drive;

namespace Azariah.Core.Files;

public sealed record ContentMatch(string Path, int Line, string Text);

/// <summary>
/// Searches inside text files (Luau scripts by default) for a phrase, line by line, ignoring case.
/// Big files are skipped and app-managed areas are never searched.
/// </summary>
public sealed class ContentSearchService(DriveLayout layout)
{
    public const long MaxFileBytes = 2 * 1024 * 1024;

    public static readonly IReadOnlySet<FileKind> Scripts = new HashSet<FileKind> { FileKind.LuauScript };

    public async IAsyncEnumerable<ContentMatch> SearchAsync(
        string folder,
        string text,
        IReadOnlySet<FileKind>? kinds = null,
        int maxResults = 200,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(text);
        var phrase = text.Trim();
        if (phrase.Length == 0)
        {
            yield break;
        }

        var found = 0;
        var query = new SearchQuery(string.Empty, Kinds: kinds ?? Scripts, MaxResults: int.MaxValue);
        await foreach (var file in new FileSearchService(layout).SearchAsync(folder, query, ct))
        {
            if (file.IsDirectory || file.Size is null or > MaxFileBytes)
            {
                continue;
            }

            string[] lines;
            try
            {
                lines = await File.ReadAllLinesAsync(file.FullPath, ct);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                continue;
            }

            for (var i = 0; i < lines.Length; i++)
            {
                if (lines[i].Contains(phrase, StringComparison.OrdinalIgnoreCase))
                {
                    yield return new ContentMatch(file.FullPath, i + 1, lines[i].Trim());
                    if (++found >= maxResults)
                    {
                        yield break;
                    }
                }
            }
        }
    }
}
