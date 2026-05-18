using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Flicksy.Editor.ViewModels;

namespace Flicksy.Editor.Controls;

public partial class DrawingView : UserControl
{
    public static readonly DependencyProperty StrokeBrushProperty =
        DependencyProperty.Register(
            nameof(StrokeBrush),
            typeof(Brush),
            typeof(DrawingView),
            new PropertyMetadata(Brushes.Black));

    public static readonly DependencyProperty StrokeThicknessProperty =
        DependencyProperty.Register(
            nameof(StrokeThickness),
            typeof(double),
            typeof(DrawingView),
            new PropertyMetadata(1.0));

    public static readonly DependencyProperty IsErasingProperty =
        DependencyProperty.Register(
            nameof(IsErasing),
            typeof(bool),
            typeof(DrawingView),
            new PropertyMetadata(false));

    public DrawingView()
    {
        InitializeComponent();
    }

    public Brush StrokeBrush
    {
        get => (Brush)GetValue(StrokeBrushProperty);
        set => SetValue(StrokeBrushProperty, value);
    }

    public double StrokeThickness
    {
        get => (double)GetValue(StrokeThicknessProperty);
        set => SetValue(StrokeThicknessProperty, value);
    }

    public bool IsErasing
    {
        get => (bool)GetValue(IsErasingProperty);
        set => SetValue(IsErasingProperty, value);
    }

    public DrawingViewModel? ViewModel => DataContext as DrawingViewModel;

    private Point? _lastAppendedPoint;

    private void OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (ViewModel is null || !TryGetPoint(e, clampToBounds: false, out var point))
        {
            return;
        }

        if (IsErasing)
        {
            EraseAt(point);
            CaptureMouse();
            e.Handled = true;
            return;
        }

        ViewModel.BeginStroke(point, StrokeBrush, StrokeThickness);
        _lastAppendedPoint = point;
        CaptureMouse();
        e.Handled = true;
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        if (ViewModel is null || e.LeftButton != MouseButtonState.Pressed || !TryGetPoint(e, clampToBounds: true, out var point))
        {
            return;
        }

        if (IsErasing)
        {
            EraseAt(point);
            return;
        }

        if (_lastAppendedPoint is Point lastPoint)
        {
            var minDistance = Math.Max(0.75d, StrokeThickness * 0.2d);
            var dx = point.X - lastPoint.X;
            var dy = point.Y - lastPoint.Y;
            if ((dx * dx) + (dy * dy) < (minDistance * minDistance))
            {
                return;
            }
        }

        ViewModel.AppendPoint(point);
        _lastAppendedPoint = point;
    }

    private void OnMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (IsErasing)
        {
            if (ViewModel is not null && TryGetPoint(e, clampToBounds: true, out var erasePoint))
            {
                EraseAt(erasePoint);
            }

            ReleaseMouseCapture();
            _lastAppendedPoint = null;
            return;
        }

        if (ViewModel is not null && TryGetPoint(e, clampToBounds: true, out var point))
        {
            ViewModel.AppendPoint(point);
        }

        ViewModel?.EndStroke();
        ReleaseMouseCapture();
        _lastAppendedPoint = null;
    }

    private bool TryGetPoint(MouseEventArgs e, bool clampToBounds, out Point point)
    {
        var x = ActualWidth;
        var y = ActualHeight;
        if (x <= 0 || y <= 0)
        {
            point = default;
            return false;
        }

        var position = e.GetPosition(this);
        if (!clampToBounds && (position.X < 0 || position.Y < 0 || position.X > x || position.Y > y))
        {
            point = default;
            return false;
        }

        point = new Point(
            Math.Clamp(position.X, 0, x),
            Math.Clamp(position.Y, 0, y));

        return true;
    }

    private void EraseAt(Point point)
    {
        if (ViewModel is null)
        {
            return;
        }

        for (var i = ViewModel.Strokes.Count - 1; i >= 0; i--)
        {
            var stroke = ViewModel.Strokes[i];
            if (IntersectsStroke(stroke, point))
            {
                ViewModel.Strokes.RemoveAt(i);
                return;
            }
        }
    }

    private static bool IntersectsStroke(Stroke stroke, Point point)
    {
        var points = stroke.Points;
        if (points.Count == 0)
        {
            return false;
        }

        var tolerance = Math.Max(1d, stroke.Thickness * 0.5d);
        var toleranceSquared = tolerance * tolerance;

        if (points.Count == 1)
        {
            return DistanceSquared(points[0], point) <= toleranceSquared;
        }

        for (var i = 1; i < points.Count; i++)
        {
            if (DistanceSquaredToSegment(point, points[i - 1], points[i]) <= toleranceSquared)
            {
                return true;
            }
        }

        return false;
    }

    private static double DistanceSquared(Point a, Point b)
    {
        var dx = a.X - b.X;
        var dy = a.Y - b.Y;
        return (dx * dx) + (dy * dy);
    }

    private static double DistanceSquaredToSegment(Point point, Point start, Point end)
    {
        var vx = end.X - start.X;
        var vy = end.Y - start.Y;
        var lengthSquared = (vx * vx) + (vy * vy);
        if (lengthSquared <= double.Epsilon)
        {
            return DistanceSquared(point, start);
        }

        var t = ((point.X - start.X) * vx + (point.Y - start.Y) * vy) / lengthSquared;
        t = Math.Clamp(t, 0d, 1d);

        var closest = new Point(start.X + (t * vx), start.Y + (t * vy));
        return DistanceSquared(point, closest);
    }

}
