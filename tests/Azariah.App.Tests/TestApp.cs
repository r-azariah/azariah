using Avalonia;
using Avalonia.Headless;

[assembly: AvaloniaTestApplication(typeof(Azariah.App.Tests.TestAppBuilder))]

namespace Azariah.App.Tests;

public static class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UseSkia()
            .WithInterFont()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
}
