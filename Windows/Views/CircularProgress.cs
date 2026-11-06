using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;

namespace FileHustle.Views;

/// A ring-shaped progress indicator. `Value` is 0..1. When
/// `IsIndeterminate` is true it ignores Value and spins a short arc instead
/// — used for the phases where there's no byte count to show yet
/// (zipping a folder, waiting on the other device to tap Accept).
public class CircularProgress : Control
{
    public static readonly StyledProperty<double> ValueProperty =
        AvaloniaProperty.Register<CircularProgress, double>(nameof(Value));

    public static readonly StyledProperty<bool> IsIndeterminateProperty =
        AvaloniaProperty.Register<CircularProgress, bool>(nameof(IsIndeterminate));

    public static readonly StyledProperty<double> ThicknessProperty =
        AvaloniaProperty.Register<CircularProgress, double>(nameof(Thickness), 8);

    public static readonly StyledProperty<IBrush?> TrackBrushProperty =
        AvaloniaProperty.Register<CircularProgress, IBrush?>(nameof(TrackBrush), new SolidColorBrush(Color.FromArgb(0x33, 0x80, 0x80, 0x80)));

    public static readonly StyledProperty<IBrush?> IndicatorBrushProperty =
        AvaloniaProperty.Register<CircularProgress, IBrush?>(nameof(IndicatorBrush), Brushes.DodgerBlue);

    public double Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
    public bool IsIndeterminate { get => GetValue(IsIndeterminateProperty); set => SetValue(IsIndeterminateProperty, value); }
    public double Thickness { get => GetValue(ThicknessProperty); set => SetValue(ThicknessProperty, value); }
    public IBrush? TrackBrush { get => GetValue(TrackBrushProperty); set => SetValue(TrackBrushProperty, value); }
    public IBrush? IndicatorBrush { get => GetValue(IndicatorBrushProperty); set => SetValue(IndicatorBrushProperty, value); }

    private readonly DispatcherTimer _spinTimer;
    private double _spinAngle;
    private bool _attached;

    static CircularProgress()
    {
        AffectsRender<CircularProgress>(ValueProperty, IsIndeterminateProperty, ThicknessProperty, TrackBrushProperty, IndicatorBrushProperty);
    }

    public CircularProgress()
    {
        _spinTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _spinTimer.Tick += (_, _) =>
        {
            _spinAngle = (_spinAngle + 6) % 360;
            InvalidateVisual();
        };
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsIndeterminateProperty || change.Property == IsVisibleProperty) UpdateSpinTimer();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _attached = true;
        UpdateSpinTimer();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _attached = false;
        _spinTimer.Stop();
    }

    // Only burn a 60fps timer while actually spinning and on screen.
    private void UpdateSpinTimer()
    {
        if (IsIndeterminate && IsEffectivelyVisible && _attached) _spinTimer.Start();
        else _spinTimer.Stop();
    }

    public override void Render(DrawingContext context)
    {
        var size = Math.Min(Bounds.Width, Bounds.Height);
        if (size <= Thickness) return;

        var center = new Point(Bounds.Width / 2, Bounds.Height / 2);
        var radius = (size - Thickness) / 2;

        context.DrawEllipse(null, new Pen(TrackBrush, Thickness), center, radius, radius);

        double startDeg, sweepDeg;
        if (IsIndeterminate)
        {
            startDeg = _spinAngle;
            sweepDeg = 90;
        }
        else
        {
            startDeg = 0;
            sweepDeg = Math.Clamp(Value, 0, 1) * 360;
        }
        if (sweepDeg <= 0) return;

        var pen = new Pen(IndicatorBrush, Thickness, lineCap: PenLineCap.Round);

        // A single ArcTo can't draw a full 360° circle (start == end), so a
        // finished ring is just a full ellipse.
        if (sweepDeg >= 359.99)
        {
            context.DrawEllipse(null, pen, center, radius, radius);
            return;
        }

        // 0° = 12 o'clock, sweeping clockwise.
        Point PointAt(double deg)
        {
            var rad = (deg - 90) * Math.PI / 180;
            return new Point(center.X + radius * Math.Cos(rad), center.Y + radius * Math.Sin(rad));
        }

        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(PointAt(startDeg), isFilled: false);
            ctx.ArcTo(PointAt(startDeg + sweepDeg), new Size(radius, radius), 0, sweepDeg > 180, SweepDirection.Clockwise);
            ctx.EndFigure(isClosed: false);
        }
        context.DrawGeometry(null, pen, geometry);
    }
}
