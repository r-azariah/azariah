namespace Azariah.Core.Files;

public static class UniqueNames
{
    /// <summary>
    /// Returns <paramref name="desiredPath"/> if nothing exists there, otherwise
    /// "name (2).ext", "name (3).ext", ... in the same folder.
    /// </summary>
    public static string Next(string desiredPath)
    {
        if (!Exists(desiredPath))
        {
            return desiredPath;
        }

        var folder = Path.GetDirectoryName(desiredPath) ?? string.Empty;
        var isDirectory = Directory.Exists(desiredPath);
        var fileName = Path.GetFileName(desiredPath);
        var stem = isDirectory ? fileName : Path.GetFileNameWithoutExtension(fileName);
        var ext = isDirectory ? string.Empty : Path.GetExtension(fileName);

        for (var i = 2; i < 10_000; i++)
        {
            var candidate = Path.Combine(folder, $"{stem} ({i}){ext}");
            if (!Exists(candidate))
            {
                return candidate;
            }
        }

        return Path.Combine(folder, $"{stem} ({Guid.NewGuid():N}){ext}");
    }

    public static bool Exists(string path) => File.Exists(path) || Directory.Exists(path);
}
