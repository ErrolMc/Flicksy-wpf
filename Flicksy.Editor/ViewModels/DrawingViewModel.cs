using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Flicksy.Editor.ViewModels;

public sealed class Stroke : ObservableObject
{
    private PointCollection _points = new();

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

    public void AddPoint(Point point)
    {
        var updated = new PointCollection(Points)
        {
            point,
        };

        Points = updated;
    }
}

public partial class DrawingViewModel : ObservableObject
{
    private Stroke? _current;

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
        Strokes.Clear();
    }
}
