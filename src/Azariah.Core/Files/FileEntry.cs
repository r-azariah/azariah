namespace Azariah.Core.Files;

public sealed record FileEntry(
    string FullPath,
    string Name,
    bool IsDirectory,
    long? Size,
    DateTime ModifiedUtc,
    DateTime CreatedUtc,
    FileAttributes Attributes,
    FileKind Kind)
{
    public string Extension => IsDirectory ? string.Empty : Path.GetExtension(Name);

    public bool IsHidden => (Attributes & (FileAttributes.Hidden | FileAttributes.System)) != 0 || Name.StartsWith('.');

    public static FileEntry FromInfo(FileSystemInfo info)
    {
        ArgumentNullException.ThrowIfNull(info);
        var isDir = info is DirectoryInfo;
        return new FileEntry(
            info.FullName,
            info.Name,
            isDir,
            info is FileInfo f ? f.Length : null,
            info.LastWriteTimeUtc,
            info.CreationTimeUtc,
            info.Attributes,
            isDir ? FileKind.Folder : FileKinds.FromExtension(info.Extension));
    }

    public static FileEntry? TryFromPath(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                return FromInfo(new DirectoryInfo(path));
            }

            if (File.Exists(path))
            {
                return FromInfo(new FileInfo(path));
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }

        return null;
    }
}
