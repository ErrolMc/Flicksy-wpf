using System;
using System.Windows;
using System.Windows.Media;

namespace Flicksy.Drawing.Source;

public sealed class PenStrokeItem : DrawingItem
{
    private PointCollection _basePoints = new();

    public PenStrokeItem(Brush brush, double thickness)
    {
        Brush = brush;
        Thickness = thickness;
    }

    public PointCollection BasePoints
    {
        get => _basePoints;
        private set => SetProperty(ref _basePoints, value);
    }

    public Brush Brush { get; }

    public double Thickness { get; }

    public override Rect CanonicalBounds
    {
        get
        {
            if (Geometry.Bounds.IsEmpty)
            {
                return Rect.Empty;
            }

            var b = Geometry.Bounds;
            var inflate = Thickness / 2.0;
            b.Inflate(inflate, inflate);
            return b;
        }
    }

    public void AddPoint(Point point)
    {
        var updated = new PointCollection(BasePoints)
        {
            point,
        };

        BasePoints = updated;
        Geometry = BuildGeometry(updated);
    }

    public override bool HitTest(Point localPoint)
    {
        var points = BasePoints;
        if (points.Count == 0)
        {
            return false;
        }

        var tolerance = Math.Max(1d, Thickness * 0.5d);
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

    public override void Render(DrawingContext dc)
    {
        if (Geometry == Geometry.Empty)
        {
            return;
        }

        var pen = new Pen(Brush, Thickness)
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round,
            LineJoin = PenLineJoin.Round,
        };

        dc.PushTransform(Transform);
        dc.DrawGeometry(null, pen, Geometry);
        dc.Pop();
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

    private static Geometry BuildGeometry(PointCollection points)
    {
        if (points.Count == 0)
        {
            return Geometry.Empty;
        }

        var figure = new PathFigure
        {
            StartPoint = points[0],
            IsClosed = false,
            IsFilled = false,
        };

        if (points.Count == 1)
        {
            figure.Segments.Add(new LineSegment(points[0], isStroked: true));
        }
        else if (points.Count == 2)
        {
            figure.Segments.Add(new LineSegment(points[1], isStroked: true));
        }
        else
        {
            const double tension = 1d;

            for (var i = 0; i < points.Count - 1; i++)
            {
                var p0 = i == 0 ? points[i] : points[i - 1];
                var p1 = points[i];
                var p2 = points[i + 1];
                var p3 = i + 2 < points.Count ? points[i + 2] : points[i + 1];

                var cp1 = new Point(
                    p1.X + ((p2.X - p0.X) * tension / 6d),
                    p1.Y + ((p2.Y - p0.Y) * tension / 6d));

                var cp2 = new Point(
                    p2.X - ((p3.X - p1.X) * tension / 6d),
                    p2.Y - ((p3.Y - p1.Y) * tension / 6d));

                figure.Segments.Add(new BezierSegment(cp1, cp2, p2, isStroked: true));
            }
        }

        var geometry = new PathGeometry(new[] { figure });
        geometry.Freeze();
        return geometry;
    }
}
