using Azariah.Core.Files;

namespace Azariah.Core.Drive;

/// <summary>
/// Well-known locations on an Azariah drive. Everything is derived from the root that was
/// detected at runtime, so the drive letter is never hardcoded.
/// </summary>
public sealed class DriveLayout
{
    public const string SystemFolderName = ".azariah";
    public const string MarkerFileName = "drive.json";
    public const string VaultFolderName = "Vault";
    public const string FilesFolderName = "Files";
    public const string RobloxFolderName = "Roblox";
    public const string TransferFolderName = "Transfer";
    public const string PublicFolderName = "Public";
    public const string SetupKitFolderName = "SetupKit";

    /// <summary>Roblox workspace sub-folders created on initialization.</summary>
    public static readonly IReadOnlyList<string> RobloxSubfolders =
        ["Places", "Scripts", "Models", "Assets", "Images", "Docs", "Backups", "Exports"];

    public DriveLayout(string root)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        Root = PathGuard.Normalize(root);
    }

    public string Root { get; }

    public string SystemFolder => Path.Combine(Root, SystemFolderName);
    public string MarkerFile => Path.Combine(SystemFolder, MarkerFileName);
    public string SettingsFile => Path.Combine(SystemFolder, "settings.json");
    public string RecentFile => Path.Combine(SystemFolder, "recent.json");
    public string LogsFolder => Path.Combine(SystemFolder, "logs");
    public string TrashFolder => Path.Combine(SystemFolder, "Trash");

    public string Files => Path.Combine(Root, FilesFolderName);
    public string Roblox => Path.Combine(Root, RobloxFolderName);
    public string Transfer => Path.Combine(Root, TransferFolderName);
    public string Public => Path.Combine(Root, PublicFolderName);
    public string SetupKit => Path.Combine(Root, SetupKitFolderName);

    /// <summary>
    /// Reserved for the encrypted Vault (Phase 2). Contents are ciphertext only; the folder
    /// name is not a security control.
    /// </summary>
    public string Vault => Path.Combine(Root, VaultFolderName);

    /// <summary>User-facing folders created when a drive is initialized.</summary>
    public IEnumerable<string> StandardFolders()
    {
        yield return Files;
        yield return Roblox;
        foreach (var sub in RobloxSubfolders)
        {
            yield return Path.Combine(Roblox, sub);
        }

        yield return Transfer;
        yield return Public;
        yield return SetupKit;
    }

    public bool Contains(string path) => PathGuard.IsInsideOrEqual(Root, path);

    /// <summary>
    /// True for app-managed areas (system data and Vault ciphertext). The Files browser refuses
    /// to modify these so a misclick cannot destroy the Vault. This is accident prevention,
    /// not a security boundary: security of the Vault comes from encryption.
    /// </summary>
    public bool IsProtected(string path) =>
        PathGuard.IsInsideOrEqual(SystemFolder, path) || PathGuard.IsInsideOrEqual(Vault, path);

    /// <summary>Converts an absolute path on the drive to a root-relative path for storage.</summary>
    public string ToRelative(string fullPath)
    {
        var normalized = PathGuard.Normalize(fullPath);
        if (!Contains(normalized))
        {
            throw new ArgumentException("Path is not on this drive.", nameof(fullPath));
        }

        return Path.GetRelativePath(Root, normalized);
    }

    /// <summary>Resolves a stored root-relative path, refusing anything that escapes the root.</summary>
    public string ToFull(string relativePath)
    {
        ArgumentNullException.ThrowIfNull(relativePath);
        if (Path.IsPathRooted(relativePath))
        {
            throw new ArgumentException("Expected a drive-relative path.", nameof(relativePath));
        }

        var full = PathGuard.Normalize(Path.Combine(Root, relativePath));
        if (!Contains(full))
        {
            throw new ArgumentException("Path escapes the drive root.", nameof(relativePath));
        }

        return full;
    }
}
