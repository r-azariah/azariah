using System.Diagnostics;

namespace Azariah.App.Services;

/// <summary>Hands files to the operating system (open with the default app, show in Explorer).</summary>
public interface IShellService
{
    void Open(string path);

    void Reveal(string path);

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
