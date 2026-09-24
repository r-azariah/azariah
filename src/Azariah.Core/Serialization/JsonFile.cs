using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace Azariah.Core.Serialization;

/// <summary>
/// JSON persistence helpers. Writes go to a temporary sibling first and are then moved over
/// the target, so a yanked USB or crash mid-write leaves either the old or the new file,
/// never a half-written one (as far as the file system allows).
/// </summary>
public static class JsonFile
{
    public static T? Read<T>(string path, JsonTypeInfo<T> typeInfo)
    {
        if (!File.Exists(path))
        {
            return default;
        }

        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        return JsonSerializer.Deserialize(stream, typeInfo);
    }

    /// <summary>Reads a file, returning default instead of throwing on missing or malformed JSON.</summary>
    public static T? TryRead<T>(string path, JsonTypeInfo<T> typeInfo)
    {
        try
        {
            return Read(path, typeInfo);
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return default;
        }
    }

    public static void WriteAtomic<T>(string path, T value, JsonTypeInfo<T> typeInfo)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(value, typeInfo);
        AtomicFile.WriteAllBytes(path, bytes);
    }
}

public static class AtomicFile
{
    public static void WriteAllBytes(string path, ReadOnlySpan<byte> bytes)
    {
        var folder = Path.GetDirectoryName(Path.GetFullPath(path))!;
        Directory.CreateDirectory(folder);
        var temp = Path.Combine(folder, $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");
        try
        {
            using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
            {
                stream.Write(bytes);
                stream.Flush(flushToDisk: true);
            }

            File.Move(temp, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temp))
            {
                File.Delete(temp);
            }
        }
    }
}
