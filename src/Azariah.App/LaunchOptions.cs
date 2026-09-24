namespace Azariah.App;

/// <summary>Command-line options. Unknown arguments are ignored.</summary>
public sealed record LaunchOptions(string? Root, bool Watch, bool LaunchedByWatcher)
{
    public static LaunchOptions Parse(IReadOnlyList<string> args)
    {
        ArgumentNullException.ThrowIfNull(args);
        string? root = null;
        var watch = false;
        var byWatcher = false;
        for (var i = 0; i < args.Count; i++)
        {
            switch (args[i])
            {
                case "--root" when i + 1 < args.Count:
                    root = args[++i];
                    break;
                case "--watch":
                    watch = true;
                    break;
                case "--launched-by-watcher":
                    byWatcher = true;
                    break;
            }
        }

        return new LaunchOptions(root, watch, byWatcher);
    }
}
