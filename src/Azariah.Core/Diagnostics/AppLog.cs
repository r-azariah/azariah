using System.Globalization;

namespace Azariah.Core.Diagnostics;

/// <summary>
/// Minimal application log.
/// RULE: never log passwords, keys, tokens, decrypted Vault content or Vault file names.
/// Log what happened (operation, outcome, error type), not secret data.
/// </summary>
public interface IAppLog
{
    void Info(string message);

    void Warn(string message);

    void Error(string message, Exception? exception = null);
}

public sealed class NullAppLog : IAppLog
{
    public static NullAppLog Instance { get; } = new();

    public void Info(string message)
    {
    }

    public void Warn(string message)
    {
    }

    public void Error(string message, Exception? exception = null)
    {
    }
}

/// <summary>Daily log files under <c>.azariah/logs</c>, pruned after <see cref="RetentionDays"/> days.</summary>
public sealed class FileAppLog : IAppLog
{
    public const int RetentionDays = 14;
    private readonly string _folder;
    private readonly Lock _gate = new();

    public FileAppLog(string folder)
    {
        _folder = folder;
        try
        {
            Directory.CreateDirectory(folder);
            Prune();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Logging must never take the app down.
        }
    }

    public void Info(string message) => Write("INFO", message, null);

    public void Warn(string message) => Write("WARN", message, null);

    public void Error(string message, Exception? exception = null) => Write("ERROR", message, exception);

    private void Write(string level, string message, Exception? exception)
    {
        var now = DateTimeOffset.Now;
        var line = string.Create(CultureInfo.InvariantCulture, $"{now:yyyy-MM-dd HH:mm:ss.fff zzz} [{level}] {message}");
        if (exception is not null)
        {
            // Type + message only. Stack traces are useful but can be long; keep the first frame.
            var frame = exception.StackTrace?.Split('\n').FirstOrDefault()?.Trim();
            line += $" | {exception.GetType().Name}: {exception.Message}{(frame is null ? string.Empty : " @ " + frame)}";
        }

        lock (_gate)
        {
            try
            {
                var file = Path.Combine(_folder, string.Create(CultureInfo.InvariantCulture, $"azariah-{now:yyyyMMdd}.log"));
                File.AppendAllText(file, line + Environment.NewLine);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
            }
        }
    }

    private void Prune()
    {
        var cutoff = DateTime.UtcNow.AddDays(-RetentionDays);
        foreach (var file in Directory.EnumerateFiles(_folder, "azariah-*.log"))
        {
            if (File.GetLastWriteTimeUtc(file) < cutoff)
            {
                File.Delete(file);
            }
        }
    }
}
