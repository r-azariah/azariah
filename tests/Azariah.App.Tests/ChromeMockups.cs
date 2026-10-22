using Avalonia;
using Avalonia.Controls;
using Shape = Avalonia.Controls.Shapes.Path;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Azariah.App.Views;

namespace Azariah.App.Tests;

/// <summary>
/// Design exploration, not a regular test: renders the real Home screen inside three window shapes
/// (own title bar, wavy edges, bracket frame) so the owner can pick. Runs only with AZARIAH_MOCKUPS=&lt;dir&gt;.
/// </summary>
public class ChromeMockups
{
    private const double W = 1280;
    private const double H = 800;

    [AvaloniaFact]
    public void Window_edge_options()
    {
        var output = Environment.GetEnvironmentVariable("AZARIAH_MOCKUPS");
        if (string.IsNullOrEmpty(output))
        {
            return;
        }

        Directory.CreateDirectory(output);
        var app = RenderHome();

        Save(Compose(app, Geometry.Parse($"M10,0 H{W - 10} Q{W},0 {W},10 V{H - 10} Q{W},{H} {W - 10},{H} H10 Q0,{H} 0,{H - 10} V10 Q0,0 10,0 Z"), null),
            Path.Combine(output, "edges-a-own-bar.png"));

        Save(Compose(app, Geometry.Parse(Wave()), null), Path.Combine(output, "edges-b-wave.png"));

        var brackets = Geometry.Parse(
            $"M0,0 H36 V4 H4 V{H - 4} H36 V{H} H0 Z M{W},0 H{W - 36} V4 H{W - 4} V{H - 4} H{W - 36} V{H} H{W} Z");
        Save(Compose(app, new RectangleGeometry(new Rect(0, 0, W, H)), brackets), Path.Combine(output, "edges-c-brackets.png"));
    }

    /// <summary>A soft wave along the top and bottom edges, straight sides.</summary>
    private static string Wave()
    {
        const double baseline = 14;
        const double amplitude = 7;
        const int waves = 5;
        var step = W / (waves * 2);
        var top = $"M0,{baseline} Q{step / 2},{baseline - (2 * amplitude)} {step},{baseline}";
        for (var x = step * 2; x <= W + 0.1; x += step)
        {
            top += $" T{x},{baseline}";
        }

        var bottom = $" L{W},{H - baseline} Q{W - (step / 2)},{H - baseline + (2 * amplitude)} {W - step},{H - baseline}";
        for (var x = W - (step * 2); x >= -0.1; x -= step)
        {
            bottom += $" T{x},{H - baseline}";
        }

        return top + bottom + " Z";
    }

    private static WriteableBitmap RenderHome()
    {
        var main = SeedData.OpenMain(out _);
        var window = new MainWindow { DataContext = main, Width = W, Height = H };
        main.AttachWindow(window);
        window.Show();
        for (var i = 0; i < 5; i++)
        {
            Dispatcher.UIThread.RunJobs();
            Thread.Sleep(30);
        }

        var frame = window.CaptureRenderedFrame()!;
        window.Close();
        return frame;
    }

    private static WriteableBitmap Compose(IImage app, Geometry shape, Geometry? frame)
    {
        var body = new Grid { Width = W, Height = H, Clip = shape };
        body.Children.Add(new Image { Source = app, Stretch = Stretch.None });

        // Our own caption buttons instead of the Windows title bar.
        var caption = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 22,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 22, 28, 0),
        };
        foreach (var glyph in new[] { "—", "□", "✕" })
        {
            caption.Children.Add(new TextBlock { Text = glyph, FontSize = 13, Foreground = new SolidColorBrush(Color.Parse("#9A9A96")) });
        }

        body.Children.Add(caption);
        body.Children.Add(new Shape { Data = shape, Stroke = new SolidColorBrush(Color.Parse("#2E2E2E")), StrokeThickness = 1 });
        if (frame is not null)
        {
            body.Children.Add(new Shape { Data = frame, Fill = new SolidColorBrush(Color.Parse("#EDEDEA")) });
        }

        var desk = new Grid
        {
            Background = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
                GradientStops = { new GradientStop(Color.Parse("#3A4150"), 0), new GradientStop(Color.Parse("#12151B"), 1) },
            },
        };
        desk.Children.Add(new Border
        {
            Child = body,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Effect = new DropShadowEffect { BlurRadius = 40, OffsetX = 0, OffsetY = 12, Opacity = 0.55, Color = Colors.Black },
        });

        var window = new Window { Width = W + 160, Height = H + 120, Content = desk };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        var shot = window.CaptureRenderedFrame()!;
        window.Close();
        return shot;
    }

    private static void Save(WriteableBitmap bitmap, string path)
    {
        using var stream = File.Create(path);
        bitmap.Save(stream, new PngBitmapEncoderOptions());
    }
}
