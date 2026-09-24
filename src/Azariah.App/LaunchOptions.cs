using System.Globalization;

namespace Azariah.App;

/// <summary>Command-line options. Unknown arguments are ignored.</summary>
public sealed record LaunchOptions
{
    /// <summary>Drive root to open (<c>--root</c>), e.g. passed by the auto-launcher.</summary>
    public string? Root { get; init; }

    /// <summary>Run as the background auto-launcher (<c>--watch</c>).</summary>
    public bool Watch { get; init; }

    public bool LaunchedByWatcher { get; init; }

    /// <summary>Wait for this process to exit before starting (used when restarting after an update).</summary>
    public int? WaitForPid { get; init; }

    /// <summary>Run as the update helper (<c>--finish-update</c>): install this exe, then relaunch.</summary>
    public bool FinishUpdate { get; init; }

    public string? DriveExe { get; init; }

    public string? LocalExe { get; init; }

    public bool RelaunchLocal { get; init; }

    public static LaunchOptions Parse(IReadOnlyList<string> args)
    {
        ArgumentNullException.ThrowIfNull(args);
        var o = new LaunchOptions();
        for (var i = 0; i < args.Count; i++)
        {
            var next = i + 1 < args.Count ? args[i + 1] : null;
            switch (args[i])
            {
                case "--root" when next is not null:
                    o = o with { Root = next };
                    i++;
                    break;
                case "--watch":
                    o = o with { Watch = true };
                    break;
                case "--launched-by-watcher":
                    o = o with { LaunchedByWatcher = true };
                    break;
                case "--wait-for-pid" when next is not null:
                    if (int.TryParse(next, NumberStyles.Integer, CultureInfo.InvariantCulture, out var pid))
                    {
                        o = o with { WaitForPid = pid };
                    }

                    i++;
                    break;
                case "--finish-update":
                    o = o with { FinishUpdate = true };
                    break;
                case "--drive-exe" when next is not null:
                    o = o with { DriveExe = next };
                    i++;
                    break;
                case "--local-exe" when next is not null:
                    o = o with { LocalExe = next };
                    i++;
                    break;
                case "--relaunch-local":
                    o = o with { RelaunchLocal = true };
                    break;
            }
        }

        return o;
    }
}
