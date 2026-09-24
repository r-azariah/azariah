using System.Diagnostics;

namespace Azariah.App.Services;

/// <summary>Hands files to the operating system (open with the default app, show in Explorer).</summary>
public interface IShellService
{
    void Open(string path);

    void Reveal(string path);

    /// <summary>Opens a web link (http/https only) in the default browser.</summary>
    void OpenUri(Uri uri);

    /// <summary>
    /// Starts Roblox Studio on this PC, straight into the published place when both ids are known.
    /// Returns false when Studio isn't installed.
    /// </summary>
    bool OpenRobloxStudio(string? placeId, string? universeId);

    /// <summary>Display only. Never used to decide whether a computer is trusted.</summary>
    string MachineDisplayName { get; }
}

public sealed class ShellService : IShellService
{
    public string MachineDisplayName => Environment.MachineName;

    public void Open(string path)
    {
        var info = new ProcessStartInfo(path)
        {
            UseShellExecute = true,
            WorkingDirectory = Directory.Exists(path) ? path : Path.GetDirectoryName(path) ?? string.Empty,
        };
        using var _ = Process.Start(info);
    }

    public void OpenUri(Uri uri)
    {
        ArgumentNullException.ThrowIfNull(uri);
        if (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp)
        {
            throw new ArgumentException("Only web links can be opened.", nameof(uri));
        }

        using var _ = Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
    }

    public bool OpenRobloxStudio(string? placeId, string? universeId)
    {
        var studio = FindRobloxStudio();
        if (studio is null)
        {
            return false;
        }

        var info = new ProcessStartInfo(studio) { UseShellExecute = false, WorkingDirectory = Path.GetDirectoryName(studio)! };
        if (IsDigits(placeId) && IsDigits(universeId))
        {
            // Documented Studio command line: opens the latest published version of the place.
            info.ArgumentList.Add("--task");
            info.ArgumentList.Add("EditPlace");
            info.ArgumentList.Add("--placeId");
            info.ArgumentList.Add(placeId!);
            info.ArgumentList.Add("--universeId");
            info.ArgumentList.Add(universeId!);
        }

        using var _ = Process.Start(info);
        return true;
    }

    /// <summary>Studio installs per user in %LOCALAPPDATA%\Roblox\Versions\&lt;version&gt;; the newest install wins.</summary>
    private static string? FindRobloxStudio()
    {
        var versions = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Roblox", "Versions");
        try
        {
            if (!Directory.Exists(versions))
            {
                return null;
            }

            return Directory.EnumerateDirectories(versions)
                .Select(dir => Path.Combine(dir, "RobloxStudioBeta.exe"))
                .Where(File.Exists)
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .FirstOrDefault();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static bool IsDigits(string? text) => !string.IsNullOrEmpty(text) && text.All(char.IsAsciiDigit);

    public void Reveal(string path)
    {
        if (OperatingSystem.IsWindows())
        {
            var args = File.Exists(path) || Directory.Exists(path) ? $"/select,\"{path}\"" : $"\"{Path.GetDirectoryName(path)}\"";
            using var _ = Process.Start(new ProcessStartInfo("explorer.exe", args) { UseShellExecute = false });
            return;
        }

        var folder = Directory.Exists(path) ? path : Path.GetDirectoryName(path);
        if (folder is not null)
        {
            Open(folder);
        }
    }
}
