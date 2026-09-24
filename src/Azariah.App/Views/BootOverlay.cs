using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Path = Avalonia.Controls.Shapes.Path;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;

namespace Azariah.App.Views;

/// <summary>
/// The [A] moments. Launch: black, the mark glitches in (RGB split, displaced slices, flicker),
/// settles, then the brackets slide apart and the app shows through. Drive removed: the brackets
/// close on the A, one glitch burst, "Disconnected", fade to black. Any key or click skips the
/// launch animation.
/// </summary>
public sealed class BootOverlay : Grid
{
    private const double MarkWidth = 541;
    private const double MarkHeight = 422;
    private const double OpenDistance = 150;
    private const double OpenDuration = 1150;
    private const double CloseDuration = 1750;

    private const string LeftData = "M0,0 H110 V50 H56 V372 H110 V422 H0 Z";
    private const string AData = "F0 M218,50 H311 L434,351 H360 L338,291 H200 L178,351 H104 Z M270,117 L317,237 H222 Z";
    private const string RightData = "M541,0 H431 V50 H485 V372 H431 V422 H541 Z";

    private readonly Border _backdrop = new() { Background = Brushes.Black };
    private readonly Canvas _stage = new() { Width = MarkWidth, Height = MarkHeight, ClipToBounds = false };
    private readonly Layer _red = new(Color.Parse("#FF2D55"));
    private readonly Layer _cyan = new(Color.Parse("#00E5FF"));
    private readonly Layer _main = new(Color.Parse("#F2F2EE"));
    private readonly Layer _sliceA = new(Color.Parse("#F2F2EE"));
    private readonly Layer _sliceB = new(Color.Parse("#F2F2EE"));
    private readonly TextBlock _caption = new()
    {
        Foreground = new SolidColorBrush(Color.Parse("#9A9A96")),
        FontSize = 13,
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center,
        Margin = new Thickness(0, 200, 0, 0),
        Opacity = 0,
    };

    private readonly Random _random = new();
    private DispatcherTimer? _timer;
    private TaskCompletionSource? _done;
    private bool _skip;

    public BootOverlay()
    {
        IsVisible = false;
        _stage.HorizontalAlignment = HorizontalAlignment.Center;
        _stage.VerticalAlignment = VerticalAlignment.Center;
        _stage.RenderTransformOrigin = RelativePoint.Center;
        _stage.RenderTransform = new ScaleTransform(0.32, 0.32);
        foreach (var layer in new[] { _red, _cyan, _main, _sliceA, _sliceB })
        {
            _stage.Children.Add(layer.Root);
        }

        Children.Add(_backdrop);
        Children.Add(_stage);
        Children.Add(_caption);
        PointerPressed += (_, _) => Skip();
    }

    public bool IsPlayingOpen { get; private set; }

    /// <summary>Covers the window in black before the first frame so the UI never flashes.</summary>
    public void PrepareOpen()
    {
        IsVisible = true;
        _backdrop.Opacity = 1;
        _stage.Opacity = 0;
        _caption.Opacity = 0;
    }

    public Task PlayOpenAsync()
    {
        IsPlayingOpen = true;
        return Run(OpenFrame, OpenDuration, onDone: () =>
        {
            IsPlayingOpen = false;
            IsVisible = false;
        });
    }

    public Task PlayCloseAsync(string caption, bool animate)
    {
        _caption.Text = caption;
        if (!animate)
        {
            IsVisible = true;
            _backdrop.Opacity = 1;
            _stage.Opacity = 1;
            _caption.Opacity = 1;
            Pose(0, 0, 1, 1);
            Glitch(0);
            return Task.Delay(900);
        }

        return Run(CloseFrame, CloseDuration, onDone: null);
    }

    /// <summary>Poses the overlay at a fixed point in time (for screenshots/tests).</summary>
    internal void RenderAt(bool open, double t, string? caption = null)
    {
        IsVisible = true;
        _caption.Text = caption ?? _caption.Text;
        if (open)
        {
            OpenFrame(t);
        }
        else
        {
            CloseFrame(t);
        }
    }

    public void Skip()
    {
        if (IsPlayingOpen)
        {
            _skip = true;
        }
    }

    private Task Run(Action<double> frame, double duration, Action? onDone)
    {
        _timer?.Stop();
        _skip = false;
        IsVisible = true;
        _done = new TaskCompletionSource();
        var done = _done;
        var clock = Stopwatch.StartNew();
        frame(0);
        _timer = new DispatcherTimer(TimeSpan.FromMilliseconds(16), DispatcherPriority.Render, (_, _) =>
        {
            var t = clock.Elapsed.TotalMilliseconds;
            if (_skip || t >= duration)
            {
                _timer?.Stop();
                frame(duration);
                onDone?.Invoke();
                done.TrySetResult();
                return;
            }

            frame(t);
        });
        _timer.Start();
        return done.Task;
    }

    private void OpenFrame(double t)
    {
        _stage.Opacity = t < 110 ? 0 : 1;

        // Glitch in, decaying.
        Glitch(t is >= 110 and < 470 ? 1 - ((t - 110) / 360) : 0);

        // Brackets open, the A fades, the app shows through.
        var p = EaseOutCubic(Clamp01((t - 700) / 350));
        Pose(-OpenDistance * p, OpenDistance * p, 1 - p, 1 - Clamp01((t - 820) / 230));
        _backdrop.Opacity = t < 760 ? 1 : 1 - EaseOutCubic(Clamp01((t - 760) / 340));
        if (t >= OpenDuration)
        {
            _backdrop.Opacity = 0;
            _stage.Opacity = 0;
        }
    }

    private void CloseFrame(double t)
    {
        _backdrop.Opacity = Clamp01(t / 160);

        // Brackets close on the A.
        var p = EaseInOutCubic(Clamp01((t - 160) / 260));
        Pose(-OpenDistance * (1 - p), OpenDistance * (1 - p), p, p);

        // One burst, then quiet.
        Glitch(t is >= 420 and < 780 ? 1 - ((t - 420) / 360) : 0);

        var fade = 1 - Clamp01((t - 1350) / 350);
        _stage.Opacity = fade;
        _caption.Opacity = Math.Min(Clamp01((t - 780) / 140), fade);
    }

    private void Pose(double leftX, double rightX, double aOpacity, double bracketOpacity)
    {
        foreach (var layer in new[] { _red, _cyan, _main, _sliceA, _sliceB })
        {
            layer.LeftShift.X = leftX;
            layer.RightShift.X = rightX;
            layer.A.Opacity = aOpacity;
            layer.Left.Opacity = bracketOpacity;
            layer.Right.Opacity = bracketOpacity;
        }
    }

    private void Glitch(double intensity)
    {
        if (intensity <= 0)
        {
            _red.Root.Opacity = 0;
            _cyan.Root.Opacity = 0;
            _sliceA.Root.Opacity = 0;
            _sliceB.Root.Opacity = 0;
            _main.Root.Opacity = 1;
            return;
        }

        var split = 30 * intensity;
        _red.Shift.X = Jitter(split);
        _red.Shift.Y = Jitter(3 * intensity);
        _cyan.Shift.X = -_red.Shift.X + Jitter(split / 3);
        _cyan.Shift.Y = Jitter(3 * intensity);
        _red.Root.Opacity = 0.85;
        _cyan.Root.Opacity = 0.85;
        Slice(_sliceA, intensity);
        Slice(_sliceB, intensity);
        _main.Root.Opacity = _random.NextDouble() < 0.14 * intensity ? 0.15 : 1;
    }

    private void Slice(Layer layer, double intensity)
    {
        if (_random.NextDouble() > 0.7 * intensity)
        {
            layer.Root.Opacity = 0;
            return;
        }

        var y = _random.NextDouble() * MarkHeight;
        var h = 14 + (_random.NextDouble() * 60);
        layer.Root.Clip = new RectangleGeometry(new Rect(-OpenDistance, y, MarkWidth + (2 * OpenDistance), h));
        layer.Shift.X = Jitter(70 * intensity);
        layer.Root.Opacity = 1;
    }

    private double Jitter(double amount) => (_random.NextDouble() - 0.5) * 2 * amount;

    private static double Clamp01(double v) => Math.Clamp(v, 0, 1);

    private static double EaseOutCubic(double x) => 1 - Math.Pow(1 - x, 3);

    private static double EaseInOutCubic(double x) => x < 0.5 ? 4 * x * x * x : 1 - (Math.Pow((-2 * x) + 2, 3) / 2);

    /// <summary>One copy of the mark: brackets and A as separate shapes so they can move apart.</summary>
    private sealed class Layer
    {
        public Layer(Color color)
        {
            var brush = new SolidColorBrush(color);
            Left = new Path { Data = Geometry.Parse(LeftData), Fill = brush, RenderTransform = LeftShift };
            A = new Path { Data = Geometry.Parse(AData), Fill = brush };
            Right = new Path { Data = Geometry.Parse(RightData), Fill = brush, RenderTransform = RightShift };
            Root = new Canvas { Width = MarkWidth, Height = MarkHeight, RenderTransform = Shift, Opacity = 0 };
            Root.Children.Add(Left);
            Root.Children.Add(A);
            Root.Children.Add(Right);
        }

        public Canvas Root { get; }
        public Path Left { get; }
        public Path A { get; }
        public Path Right { get; }
        public TranslateTransform Shift { get; } = new();
        public TranslateTransform LeftShift { get; } = new();
        public TranslateTransform RightShift { get; } = new();
    }
}
