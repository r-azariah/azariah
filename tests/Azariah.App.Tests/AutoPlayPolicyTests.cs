using System.Runtime.Versioning;
using Azariah.App.Platform;
using Azariah.Core.Diagnostics;
using Azariah.Core.Launcher;
using Microsoft.Win32;

namespace Azariah.App.Tests;

/// <summary>Runs against a throwaway HKCU subkey, never the real AutoPlay settings.</summary>
public class AutoPlayPolicyTests
{
    private const string Handlers = @"Software\Microsoft\Windows\CurrentVersion\Explorer\AutoplayHandlers";

    [Fact]
    [SupportedOSPlatform("windows")]
    public void Apply_turns_off_explorer_and_restore_puts_the_old_choice_back()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        Run((root, store, policy) =>
        {
            SetChoice(root, "UserChosenExecuteHandlers", "MSOpenFolder");

            policy.Apply();
            Assert.Equal(AutoPlayPolicy.TakeNoAction, GetChoice(root, "UserChosenExecuteHandlers"));
            Assert.Equal(AutoPlayPolicy.TakeNoAction, GetChoice(root, "EventHandlersDefaultSelection"));
            Assert.True(store.Load().AutoPlayChanged);

            policy.Restore();
            Assert.Equal("MSOpenFolder", GetChoice(root, "UserChosenExecuteHandlers"));
            Assert.Null(GetChoice(root, "EventHandlersDefaultSelection"));
            Assert.False(store.Load().AutoPlayChanged);
        });
    }

    [Fact]
    [SupportedOSPlatform("windows")]
    public void A_choice_made_in_windows_afterwards_is_left_alone()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        Run((root, store, policy) =>
        {
            policy.Apply();
            SetChoice(root, "UserChosenExecuteHandlers", "MSPromptEachTime");

            policy.Apply();
            Assert.Equal("MSPromptEachTime", GetChoice(root, "UserChosenExecuteHandlers"));

            policy.Restore();
            Assert.Equal("MSPromptEachTime", GetChoice(root, "UserChosenExecuteHandlers"));
            Assert.False(store.Load().AutoPlayChanged);
        });
    }

    [SupportedOSPlatform("windows")]
    private static void Run(Action<RegistryKey, LauncherConfigStore, AutoPlayPolicy> test)
    {
        var name = @"Software\Azariah.Tests\" + Guid.NewGuid().ToString("N");
        var data = Directory.CreateTempSubdirectory("azariah-autoplay").FullName;
        try
        {
            using var root = Registry.CurrentUser.CreateSubKey(name, writable: true);
            var store = new LauncherConfigStore(new LauncherPaths(data));
            test(root, store, new AutoPlayPolicy(root, store, NullAppLog.Instance));
        }
        finally
        {
            Registry.CurrentUser.DeleteSubKeyTree(name, throwOnMissingSubKey: false);
            Directory.Delete(data, recursive: true);
        }
    }

    [SupportedOSPlatform("windows")]
    private static void SetChoice(RegistryKey root, string handler, string value)
    {
        using var key = root.CreateSubKey($@"{Handlers}\{handler}\StorageOnArrival", writable: true);
        key.SetValue(null, value, RegistryValueKind.String);
    }

    [SupportedOSPlatform("windows")]
    private static string? GetChoice(RegistryKey root, string handler)
    {
        using var key = root.OpenSubKey($@"{Handlers}\{handler}\StorageOnArrival", writable: false);
        return key?.GetValue(null) as string;
    }
}
