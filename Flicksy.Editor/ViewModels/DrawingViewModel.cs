using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Flicksy.Editor.ViewModels;

public sealed class Stroke : ObservableObject
{
    private PointCollection _basePoints = new();
    private Geometry _geometry = Geometry.Empty;

    public Stroke(Brush brush, double thickness)
    {
        Brush = brush;
        Thickness = thickness;
        Transform = new MatrixTransform(Matrix.Identity);
    }

    public PointCollection BasePoints
    {
        get => _basePoints;
        private set => SetProperty(ref _basePoints, value);
    }

    public Brush Brush { get; }

    public double Thickness { get; }

    public Geometry Geometry
    {
        get => _geometry;
        private set => SetProperty(ref _geometry, value);
    }

    public MatrixTransform Transform { get; }

    public Rect CanonicalBounds
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

    public void TranslateFrom(Matrix baseTransform, Vector totalDelta)
    {
        var m = baseTransform;
        m.Translate(totalDelta.X, totalDelta.Y);
        Transform.Matrix = m;
    }

    public void ScaleFrom(Matrix baseTransform, double factor, Point anchorWorld)
    {
        var m = baseTransform;
        m.ScaleAt(factor, factor, anchorWorld.X, anchorWorld.Y);
        Transform.Matrix = m;
    }

    public void RotateFrom(Matrix baseTransform, double angleDegrees, Point centerWorld)
    {
        var m = baseTransform;
        m.RotateAt(angleDegrees, centerWorld.X, centerWorld.Y);
        Transform.Matrix = m;
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
