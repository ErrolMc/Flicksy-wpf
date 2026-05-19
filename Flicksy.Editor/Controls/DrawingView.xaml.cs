using System.Collections.Generic;
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

    public static readonly DependencyProperty IsSelectActiveProperty =
        DependencyProperty.Register(
            nameof(IsSelectActive),
            typeof(bool),
            typeof(DrawingView),
            new PropertyMetadata(false));

    public static readonly DependencyProperty ContentScaleProperty =
        DependencyProperty.Register(
            nameof(ContentScale),
            typeof(double),
            typeof(DrawingView),
            new PropertyMetadata(1.0));

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

    public bool IsSelectActive
    {
        get => (bool)GetValue(IsSelectActiveProperty);
        set => SetValue(IsSelectActiveProperty, value);
    }

    public double ContentScale
    {
        get => (double)GetValue(ContentScaleProperty);
        set => SetValue(ContentScaleProperty, value);
    }

    public DrawingViewModel? ViewModel => DataContext as DrawingViewModel;

    private enum CornerKind { None, TopLeft, TopRight, BottomLeft, BottomRight }

    private Point? _lastAppendedPoint;
    private Point? _smoothedPoint;
    private const double InputSmoothingAlpha = 0.5d;
    private Stroke? _draggingStroke;
    private Matrix _moveBaseMatrix;
    private Point _moveStartPoint;
    private Stroke? _scalingStroke;
    private Matrix _scaleBaseMatrix;
    private Point _scaleAnchorWorld;
    private Point _scaleOriginalGrabbedWorld;

    private void OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (ViewModel is null || !TryGetPoint(e, clampToBounds: false, out var point))
        {
            return;
        }

        if (IsSelectActive)
        {
            if (ViewModel.SelectedStroke is { } current && GetCornerHit(current, point) is var corner && corner != CornerKind.None)
            {
                var canonical = current.CanonicalBounds;
                var (anchorCanonical, grabbedCanonical) = GetAnchorAndGrabbed(canonical, corner);
                var m = current.Transform.Matrix;
                _scalingStroke = current;
                _scaleBaseMatrix = m;
                _scaleAnchorWorld = m.Transform(anchorCanonical);
                _scaleOriginalGrabbedWorld = m.Transform(grabbedCanonical);
                CaptureMouse();
                e.Handled = true;
                return;
            }

            var hit = HitTestStroke(point);
            if (hit is not null)
            {
                if (hit == ViewModel.SelectedStroke)
                {
                    _draggingStroke = hit;
                    _moveBaseMatrix = hit.Transform.Matrix;
                    _moveStartPoint = point;
                    CaptureMouse();
                }
                else
                {
                    ViewModel.SelectedStroke = hit;
                }
            }
            else if (ViewModel.SelectedStroke is { } selected && IsInsideSelectionBounds(selected, point))
            {
                _draggingStroke = selected;
                _moveBaseMatrix = selected.Transform.Matrix;
                _moveStartPoint = point;
                CaptureMouse();
            }
            else
            {
                ViewModel.SelectedStroke = null;
            }

            e.Handled = true;
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
        _smoothedPoint = point;
        CaptureMouse();
        e.Handled = true;
    }

    private Stroke? HitTestStroke(Point point)
    {
        if (ViewModel is null)
        {
            return null;
        }

        for (var i = ViewModel.Strokes.Count - 1; i >= 0; i--)
        {
            var stroke = ViewModel.Strokes[i];
            if (IntersectsStroke(stroke, point))
            {
                return stroke;
            }
        }

        return null;
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        if (ViewModel is null)
        {
            return;
        }

        if (e.LeftButton != MouseButtonState.Pressed)
        {
            UpdateHoverCursor(e);
            return;
        }

        if (_scalingStroke is not null)
        {
            var cursor = e.GetPosition(this);
            var oldDiag = _scaleOriginalGrabbedWorld - _scaleAnchorWorld;
            var oldLengthSq = oldDiag.X * oldDiag.X + oldDiag.Y * oldDiag.Y;
            if (oldLengthSq > double.Epsilon)
            {
                var offset = cursor - _scaleAnchorWorld;
                var factor = (offset.X * oldDiag.X + offset.Y * oldDiag.Y) / oldLengthSq;
                if (Math.Abs(factor) < 0.01d)
                {
                    factor = factor < 0 ? -0.01d : 0.01d;
                }
                _scalingStroke.ScaleFrom(_scaleBaseMatrix, factor, _scaleAnchorWorld);
            }
            return;
        }

        if (_draggingStroke is not null)
        {
            var current = e.GetPosition(this);
            var totalDelta = current - _moveStartPoint;
            _draggingStroke.TranslateFrom(_moveBaseMatrix, totalDelta);
            return;
        }

        if (!TryGetPoint(e, clampToBounds: true, out var point))
        {
            return;
        }

        if (IsErasing)
        {
            EraseAt(point);
            return;
        }

        var smoothed = SmoothInput(point);

        if (_lastAppendedPoint is Point lastPoint)
        {
            var minDistance = Math.Max(1.5d, StrokeThickness * 0.5d);
            var dx = smoothed.X - lastPoint.X;
            var dy = smoothed.Y - lastPoint.Y;
            if ((dx * dx) + (dy * dy) < (minDistance * minDistance))
            {
                return;
            }
        }

        ViewModel.AppendPoint(smoothed);
        _lastAppendedPoint = smoothed;
    }

    private Point SmoothInput(Point raw)
    {
        if (_smoothedPoint is not Point previous)
        {
            _smoothedPoint = raw;
            return raw;
        }

        var smoothed = new Point(
            (InputSmoothingAlpha * raw.X) + ((1d - InputSmoothingAlpha) * previous.X),
            (InputSmoothingAlpha * raw.Y) + ((1d - InputSmoothingAlpha) * previous.Y));
        _smoothedPoint = smoothed;
        return smoothed;
    }

    private void OnMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_scalingStroke is not null)
        {
            _scalingStroke = null;
            ReleaseMouseCapture();
            e.Handled = true;
            return;
        }

        if (_draggingStroke is not null)
        {
            _draggingStroke = null;
            ReleaseMouseCapture();
            e.Handled = true;
            return;
        }

        if (IsErasing)
        {
            if (ViewModel is not null && TryGetPoint(e, clampToBounds: true, out var erasePoint))
            {
                EraseAt(erasePoint);
            }

            ReleaseMouseCapture();
            _lastAppendedPoint = null;
            _smoothedPoint = null;
            return;
        }

        if (ViewModel is not null && TryGetPoint(e, clampToBounds: true, out var point))
        {
            ViewModel.AppendPoint(SmoothInput(point));
        }

        ViewModel?.EndStroke();
        ReleaseMouseCapture();
        _lastAppendedPoint = null;
        _smoothedPoint = null;
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

    private static bool IsInsideSelectionBounds(Stroke stroke, Point worldPoint)
    {
        var canonical = stroke.CanonicalBounds;
        if (canonical.IsEmpty)
        {
            return false;
        }

        var inverse = stroke.Transform.Matrix;
        if (!inverse.HasInverse)
        {
            return false;
        }
        inverse.Invert();
        var localPoint = inverse.Transform(worldPoint);
        return canonical.Contains(localPoint);
    }

    private CornerKind GetCornerHit(Stroke stroke, Point worldPoint)
    {
        var canonical = stroke.CanonicalBounds;
        if (canonical.IsEmpty || canonical.Width < 0.5d || canonical.Height < 0.5d)
        {
            return CornerKind.None;
        }

        var m = stroke.Transform.Matrix;
        var tl = m.Transform(new Point(canonical.Left, canonical.Top));
        var tr = m.Transform(new Point(canonical.Right, canonical.Top));
        var bl = m.Transform(new Point(canonical.Left, canonical.Bottom));
        var br = m.Transform(new Point(canonical.Right, canonical.Bottom));

        var scale = Math.Max(0.0001d, ContentScale);
        var pickup = 8.0d / scale;
        var pickupSquared = pickup * pickup;

        if (DistanceSquared(worldPoint, tl) <= pickupSquared) return CornerKind.TopLeft;
        if (DistanceSquared(worldPoint, tr) <= pickupSquared) return CornerKind.TopRight;
        if (DistanceSquared(worldPoint, bl) <= pickupSquared) return CornerKind.BottomLeft;
        if (DistanceSquared(worldPoint, br) <= pickupSquared) return CornerKind.BottomRight;

        return CornerKind.None;
    }

    private static (Point Anchor, Point Grabbed) GetAnchorAndGrabbed(Rect bounds, CornerKind corner) => corner switch
    {
        CornerKind.TopLeft => (new Point(bounds.Right, bounds.Bottom), new Point(bounds.Left, bounds.Top)),
        CornerKind.TopRight => (new Point(bounds.Left, bounds.Bottom), new Point(bounds.Right, bounds.Top)),
        CornerKind.BottomLeft => (new Point(bounds.Right, bounds.Top), new Point(bounds.Left, bounds.Bottom)),
        CornerKind.BottomRight => (new Point(bounds.Left, bounds.Top), new Point(bounds.Right, bounds.Bottom)),
        _ => (default, default),
    };

    private void UpdateHoverCursor(MouseEventArgs e)
    {
        if (!IsSelectActive || ViewModel?.SelectedStroke is not { } selected)
        {
            Cursor = null;
            return;
        }

        var point = e.GetPosition(this);
        var corner = GetCornerHit(selected, point);
        if (corner == CornerKind.None)
        {
            Cursor = null;
            return;
        }

        var canonical = selected.CanonicalBounds;
        var (anchorCanonical, grabbedCanonical) = GetAnchorAndGrabbed(canonical, corner);
        var m = selected.Transform.Matrix;
        var anchorWorld = m.Transform(anchorCanonical);
        var grabbedWorld = m.Transform(grabbedCanonical);
        Cursor = CursorForDiagonal(grabbedWorld - anchorWorld);
    }

    private static Cursor CursorForDiagonal(Vector diagonal)
    {
        if (Math.Abs(diagonal.X) < double.Epsilon && Math.Abs(diagonal.Y) < double.Epsilon)
        {
            return Cursors.SizeNWSE;
        }

        var angle = Math.Atan2(diagonal.Y, diagonal.X);
        if (angle < 0) angle += Math.PI; // direction is bidirectional
        var deg = angle * 180.0 / Math.PI;

        // 4 buckets across [0, 180):
        //   [  0,  22.5) and [157.5, 180) → horizontal (WE)
        //   [ 22.5,  67.5) → top-left to bottom-right (NWSE)
        //   [ 67.5, 112.5) → vertical (NS)
        //   [112.5, 157.5) → top-right to bottom-left (NESW)
        if (deg < 22.5 || deg >= 157.5) return Cursors.SizeWE;
        if (deg < 67.5) return Cursors.SizeNWSE;
        if (deg < 112.5) return Cursors.SizeNS;
        return Cursors.SizeNESW;
    }

    private static bool IntersectsStroke(Stroke stroke, Point worldPoint)
    {
        var points = stroke.BasePoints;
        if (points.Count == 0)
        {
            return false;
        }

        var inverse = stroke.Transform.Matrix;
        if (!inverse.HasInverse)
        {
            return false;
        }
        inverse.Invert();
        var localPoint = inverse.Transform(worldPoint);

        var tolerance = Math.Max(1d, stroke.Thickness * 0.5d);
        var toleranceSquared = tolerance * tolerance;

        if (points.Count == 1)
        {
            return DistanceSquared(points[0], localPoint) <= toleranceSquared;
        }

        for (var i = 1; i < points.Count; i++)
        {
            if (DistanceSquaredToSegment(localPoint, points[i - 1], points[i]) <= toleranceSquared)
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
