using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Flicksy.Editor.ViewModels;

public sealed class Stroke : ObservableObject
{
    private PointCollection _points = new();
    private Geometry _geometry = Geometry.Empty;

    public Stroke(Brush brush, double thickness)
    {
        Brush = brush;
        Thickness = thickness;
    }

    public PointCollection Points
    {
        get => _points;
        private set => SetProperty(ref _points, value);
    }

    public Brush Brush { get; }

    public double Thickness { get; }

    public Geometry Geometry
    {
        get => _geometry;
        private set => SetProperty(ref _geometry, value);
    }

    public void AddPoint(Point point)
    {
        var updated = new PointCollection(Points)
        {
            point,
        };

        Points = updated;
        Geometry = BuildGeometry(updated);
    }

    public void Translate(Vector delta)
    {
        if (Points.Count == 0)
        {
            return;
        }

        var updated = new PointCollection(Points.Count);
        foreach (var p in Points)
        {
            updated.Add(new Point(p.X + delta.X, p.Y + delta.Y));
        }

        Points = updated;
        Geometry = BuildGeometry(updated);
    }

    private static Geometry BuildGeometry(PointCollection points)
    {
        if (points.Count == 0)
        {
            return Geometry.Empty;
        }

        var smoothedPoints = SmoothPoints(points);

        var figure = new PathFigure
        {
            StartPoint = smoothedPoints[0],
            IsClosed = false,
            IsFilled = false,
        };

        if (smoothedPoints.Count == 1)
        {
            figure.Segments.Add(new LineSegment(smoothedPoints[0], isStroked: true));
        }
        else if (smoothedPoints.Count == 2)
        {
            figure.Segments.Add(new LineSegment(smoothedPoints[1], isStroked: true));
        }
        else
        {
            const double tension = 1d;

            for (var i = 0; i < smoothedPoints.Count - 1; i++)
            {
                var p0 = i == 0 ? smoothedPoints[i] : smoothedPoints[i - 1];
                var p1 = smoothedPoints[i];
                var p2 = smoothedPoints[i + 1];
                var p3 = i + 2 < smoothedPoints.Count ? smoothedPoints[i + 2] : smoothedPoints[i + 1];

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

    private static List<Point> SmoothPoints(PointCollection points)
    {
        var smoothed = new List<Point>(points.Count);
        for (var i = 0; i < points.Count; i++)
        {
            smoothed.Add(points[i]);
        }

        if (smoothed.Count < 4)
        {
            return smoothed;
        }

        var iterations = smoothed.Count switch
        {
            > 24 => 3,
            > 10 => 2,
            _ => 1,
        };

        for (var iteration = 0; iteration < iterations; iteration++)
        {
            var current = smoothed;
            var next = new List<Point>(current.Count * 2)
            {
                current[0],
            };

            for (var i = 0; i < current.Count - 1; i++)
            {
                var p0 = current[i];
                var p1 = current[i + 1];

                var q = new Point(
                    (0.75 * p0.X) + (0.25 * p1.X),
                    (0.75 * p0.Y) + (0.25 * p1.Y));

                var r = new Point(
                    (0.25 * p0.X) + (0.75 * p1.X),
                    (0.25 * p0.Y) + (0.75 * p1.Y));

                next.Add(q);
                next.Add(r);
            }

            next.Add(current[^1]);
            smoothed = next;
        }

        return smoothed;
    }
}

public partial class DrawingViewModel : ObservableObject
{
    private Stroke? _current;

    [ObservableProperty]
    private Stroke? selectedStroke;

    public DrawingViewModel()
    {
        Strokes.CollectionChanged += OnStrokesChanged;
    }

    public ObservableCollection<Stroke> Strokes { get; } = new();

    public bool HasStrokes => Strokes.Count > 0;

    public void BeginStroke(Point point, Brush brush, double thickness)
    {
        _current = new Stroke(brush, thickness);
        _current.AddPoint(point);
        Strokes.Add(_current);
    }

    public void AppendPoint(Point point)
    {
        _current?.AddPoint(point);
    }

    public void EndStroke()
    {
        _current = null;
    }

    public void Clear()
    {
        _current = null;
        SelectedStroke = null;
        Strokes.Clear();
    }

    private void OnStrokesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (SelectedStroke is null)
        {
            return;
        }

        if (e.Action == NotifyCollectionChangedAction.Remove ||
            e.Action == NotifyCollectionChangedAction.Replace ||
            e.Action == NotifyCollectionChangedAction.Reset)
        {
            if (!Strokes.Contains(SelectedStroke))
            {
                SelectedStroke = null;
            }
        }
    }
}
