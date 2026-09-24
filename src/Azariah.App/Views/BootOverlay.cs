using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Path = Avalonia.Controls.Shapes.Path;

namespace Azariah.App.Views;

/// <summary>
/// The brand moments.
/// Launch (~2.6 s): black, [A] glitches in (RGB split, displaced slices, flicker), holds, a glitch
/// burst swaps it for [AZARIAH], which settles, then its brackets slide apart and the app shows
/// through. Drive removed: the [A] brackets close on the A, one glitch burst, "Disconnected",
/// fade to black. Any key or click skips the launch animation.
/// </summary>
public sealed class BootOverlay : Grid
{
    private const double OpenDuration = 2600;
    private const double CloseDuration = 1900;
    private const double MarkScale = 0.32;
    private const double WordScale = 0.12;
    private const double MarkOpen = 150;
    private const double WordOpen = 1000;

    private readonly Border _backdrop = new() { Background = Brushes.Black };
    private readonly Rig _mark = new(Brand.MarkWidth, Brand.MarkHeight, Brand.MarkLeftData, Brand.MarkAData, Brand.MarkRightData, MarkScale);
    private readonly Rig _word = new(Brand.WordWidth, Brand.WordHeight, Brand.WordLeftData, Brand.WordTextData, Brand.WordRightData, WordScale);
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
    private bool _skip;

    public BootOverlay()
    {
        IsVisible = false;
        Children.Add(_backdrop);
        Children.Add(_mark.Stage);
        Children.Add(_word.Stage);
        Children.Add(_caption);
        PointerPressed += (_, _) => Skip();
        SizeChanged += (_, e) => _word.SetScale(Math.Min(WordScale, e.NewSize.Width * 0.62 / Brand.WordWidth));
    }

    public bool IsPlayingOpen { get; private set; }

    /// <summary>Covers the window in black before the first frame so the UI never flashes.</summary>
    public void PrepareOpen()
    {
        IsVisible = true;
        IsPlayingOpen = true;
        _backdrop.Opacity = 1;
        _mark.Stage.Opacity = 0;
        _word.Stage.Opacity = 0;
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
        _word.Stage.Opacity = 0;
        if (!animate)
        {
            IsVisible = true;
            _backdrop.Opacity = 1;
            _mark.Stage.Opacity = 1;
            _caption.Opacity = 1;
            _mark.Pose(0, 0, 1, 1);
            _mark.Glitch(0, _random);
            return Task.Delay(900);
        }

        return Run(CloseFrame, CloseDuration, onDone: null);
    }

    public void Skip()
    {
        if (IsPlayingOpen)
        {
            _skip = true;
        }
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

    private Task Run(Action<double> frame, double duration, Action? onDone)
    {
        _timer?.Stop();
        _skip = false;
        IsVisible = true;
        var done = new TaskCompletionSource();
        Stopwatch? clock = null;
        frame(0);
        _timer = new DispatcherTimer(TimeSpan.FromMilliseconds(16), DispatcherPriority.Render, (_, _) =>
        {
            // The clock starts on the first tick, i.e. once the window is actually drawing.
            clock ??= Stopwatch.StartNew();
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
        // [A]: glitch in, hold, burst out.
        _mark.Stage.Opacity = t is >= 250 and < 1300 ? 1 : 0;
        _mark.Pose(0, 0, 1, 1);
        _mark.Glitch(
            t switch
            {
                >= 250 and < 750 => 1 - (0.85 * (t - 250) / 500),
                >= 1100 and < 1300 => (t - 1100) / 200,
                _ => 0,
            },
            _random);

        // [AZARIAH]: glitch in, hold, brackets open.
        _word.Stage.Opacity = t is >= 1300 and < OpenDuration ? 1 : 0;
        _word.Glitch(t is >= 1300 and < 1700 ? 1 - ((t - 1300) / 400) : 0, _random);
        var p = EaseOutCubic(Clamp01((t - 2050) / 400));
        _word.Pose(-WordOpen * p, WordOpen * p, 1 - p, 1 - Clamp01((t - 2200) / 250));

        _backdrop.Opacity = t < 2150 ? 1 : 1 - EaseOutCubic(Clamp01((t - 2150) / 450));
        if (t >= OpenDuration)
        {
            _backdrop.Opacity = 0;
            _mark.Stage.Opacity = 0;
            _word.Stage.Opacity = 0;
        }
    }

    private void CloseFrame(double t)
    {
        _word.Stage.Opacity = 0;
        _backdrop.Opacity = Clamp01(t / 180);

        // Brackets close on the A.
        var p = EaseInOutCubic(Clamp01((t - 180) / 300));
        _mark.Pose(-MarkOpen * (1 - p), MarkOpen * (1 - p), p, p);

        // One burst, then quiet.
        _mark.Glitch(t is >= 480 and < 880 ? 1 - ((t - 480) / 400) : 0, _random);

        var fade = 1 - Clamp01((t - 1500) / 400);
        _mark.Stage.Opacity = fade;
        _caption.Opacity = Math.Min(Clamp01((t - 880) / 160), fade);
    }

    private static double Clamp01(double v) => Math.Clamp(v, 0, 1);

    private static double EaseOutCubic(double x) => 1 - Math.Pow(1 - x, 3);

    private static double EaseInOutCubic(double x) => x < 0.5 ? 4 * x * x * x : 1 - (Math.Pow((-2 * x) + 2, 3) / 2);

    /// <summary>One brand shape (mark or wordmark) with its glitch copies.</summary>
    private sealed class Rig
    {
        private readonly double _width;
        private readonly double _height;
        private readonly Layer _red;
        private readonly Layer _cyan;
        private readonly Layer _main;
        private readonly Layer _sliceA;
        private readonly Layer _sliceB;
        private readonly ScaleTransform _scale;

        public Rig(double width, double height, string left, string middle, string right, double scale)
        {
            _width = width;
            _height = height;
            _scale = new ScaleTransform(scale, scale);
            Stage = new Canvas
            {
                Width = width,
                Height = height,
                ClipToBounds = false,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                RenderTransformOrigin = RelativePoint.Center,
                RenderTransform = _scale,
                Opacity = 0,
                IsHitTestVisible = false,
            };
            _red = new Layer(Color.Parse("#FF2D55"), left, middle, right, width, height);
            _cyan = new Layer(Color.Parse("#00E5FF"), left, middle, right, width, height);
            _main = new Layer(Color.Parse("#F2F2EE"), left, middle, right, width, height);
            _sliceA = new Layer(Color.Parse("#F2F2EE"), left, middle, right, width, height);
            _sliceB = new Layer(Color.Parse("#F2F2EE"), left, middle, right, width, height);
            foreach (var layer in All)
            {
                Stage.Children.Add(layer.Root);
            }
        }

        public Canvas Stage { get; }

        private Layer[] All => [_red, _cyan, _main, _sliceA, _sliceB];

        public void SetScale(double scale)
        {
            _scale.ScaleX = scale;
            _scale.ScaleY = scale;
        }

        public void Pose(double leftX, double rightX, double middleOpacity, double bracketOpacity)
        {
            foreach (var layer in All)
            {
                layer.LeftShift.X = leftX;
                layer.RightShift.X = rightX;
                layer.Middle.Opacity = middleOpacity;
                layer.Left.Opacity = bracketOpacity;
                layer.Right.Opacity = bracketOpacity;
            }
        }

        public void Glitch(double intensity, Random random)
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

            var unit = _height / Brand.MarkHeight; // glitch sizes are tuned on the [A] mark
            _red.Shift.X = Jitter(random, 30 * intensity * unit);
            _red.Shift.Y = Jitter(random, 3 * intensity * unit);
            _cyan.Shift.X = -_red.Shift.X + Jitter(random, 10 * intensity * unit);
            _cyan.Shift.Y = Jitter(random, 3 * intensity * unit);
            _red.Root.Opacity = 0.85;
            _cyan.Root.Opacity = 0.85;
            Slice(_sliceA, intensity, random, unit);
            Slice(_sliceB, intensity, random, unit);
            _main.Root.Opacity = random.NextDouble() < 0.14 * intensity ? 0.15 : 1;
        }

        private void Slice(Layer layer, double intensity, Random random, double unit)
        {
            if (random.NextDouble() > 0.7 * intensity)
            {
                layer.Root.Opacity = 0;
                return;
            }

            var y = random.NextDouble() * _height;
            var h = (14 + (random.NextDouble() * 60)) * unit;
            layer.Root.Clip = new RectangleGeometry(new Rect(-_width, y, _width * 3, h));
            layer.Shift.X = Jitter(random, 70 * intensity * unit);
            layer.Root.Opacity = 1;
        }

        private static double Jitter(Random random, double amount) => (random.NextDouble() - 0.5) * 2 * amount;
    }

    /// <summary>One copy of a shape: brackets and middle as separate paths so they can move apart.</summary>
    private sealed class Layer
    {
        public Layer(Color color, string left, string middle, string right, double width, double height)
        {
            var brush = new SolidColorBrush(color);
            Left = new Path { Data = Geometry.Parse(left), Fill = brush, RenderTransform = LeftShift };
            Middle = new Path { Data = Geometry.Parse(middle), Fill = brush };
            Right = new Path { Data = Geometry.Parse(right), Fill = brush, RenderTransform = RightShift };
            Root = new Canvas { Width = width, Height = height, RenderTransform = Shift, Opacity = 0 };
            Root.Children.Add(Left);
            Root.Children.Add(Middle);
            Root.Children.Add(Right);
        }

        public Canvas Root { get; }
        public Path Left { get; }
        public Path Middle { get; }
        public Path Right { get; }
        public TranslateTransform Shift { get; } = new();
        public TranslateTransform LeftShift { get; } = new();
        public TranslateTransform RightShift { get; } = new();
    }
}
