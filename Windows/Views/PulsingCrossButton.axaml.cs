using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;

namespace FileHustle.Views;

/// Matches the pulsing-cross animation used on iOS/Android: scale 1.0 → 1.25,
/// 900ms ease-in-out, reversing forever. Avalonia's declarative Animation
/// syntax for RenderTransform properties is finicky to get right without a
/// real Windows box to eyeball it on, so this drives the same curve from a
/// DispatcherTimer instead — trivial to reason about and verify via `dotnet run`.
public partial class PulsingCrossButton : UserControl
{
    public event EventHandler? Clicked;

    private const double HalfPeriodMs = 900;
    private readonly DateTime _start = DateTime.UtcNow;
    private DispatcherTimer? _timer;

    public PulsingCrossButton()
    {
        InitializeComponent();
        Loaded += (_, _) => StartPulsing();
        Unloaded += (_, _) => StopPulsing();
    }

    private void StartPulsing()
    {
        if (_timer != null) return;
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _timer.Tick += (_, _) => Tick();
        _timer.Start();
    }

    private void StopPulsing()
    {
        _timer?.Stop();
        _timer = null;
    }

    private void Tick()
    {
        var elapsed = (DateTime.UtcNow - _start).TotalMilliseconds % (HalfPeriodMs * 2);
        var triangle = elapsed <= HalfPeriodMs ? elapsed / HalfPeriodMs : 2 - elapsed / HalfPeriodMs;
        var eased = (1 - Math.Cos(triangle * Math.PI)) / 2;
        var scale = 1.0 + 0.25 * eased;
        if (CrossPath.RenderTransform is ScaleTransform transform)
        {
            transform.ScaleX = scale;
            transform.ScaleY = scale;
        }
    }

    private void OnClicked(object? sender, RoutedEventArgs e) => Clicked?.Invoke(this, EventArgs.Empty);
}
