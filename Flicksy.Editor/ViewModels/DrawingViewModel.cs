using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using Flicksy.Editor.Source;

namespace Flicksy.Editor.ViewModels;

public partial class DrawingViewModel : ObservableObject
{
    private PenStrokeItem? _currentPen;
    private ShapeItem? _currentShape;

    [ObservableProperty]
    private DrawingItem? selectedItem;

    public DrawingViewModel()
    {
        Items.CollectionChanged += OnItemsChanged;
    }

    public ObservableCollection<DrawingItem> Items { get; } = new();

    public bool HasItems => Items.Count > 0;

    public void BeginPenStroke(Point point, Brush brush, double thickness)
    {
        _currentPen = new PenStrokeItem(brush, thickness);
        _currentPen.AddPoint(point);
        Items.Add(_currentPen);
    }

    public void AppendPenPoint(Point point)
    {
        _currentPen?.AddPoint(point);
    }

    public void EndPenStroke()
    {
        _currentPen = null;
    }

    public void BeginShape(Point point, ShapeKind kind, Brush? fill, Brush? outline, double outlineThickness)
    {
        // Skip if nothing will be visible.
        if (outline is null && (kind is ShapeKind.Line or ShapeKind.Arrow || fill is null))
        {
            _currentShape = null;
            return;
        }

        _currentShape = new ShapeItem(kind, point, fill, outline, outlineThickness);
        Items.Add(_currentShape);
    }

    public void UpdateShapeEndPoint(Point point)
    {
        _currentShape?.UpdateEndPoint(point);
    }

    public void EndShape()
    {
        if (_currentShape is { } shape && shape.IsDegenerate)
        {
            Items.Remove(shape);
        }

        _currentShape = null;
    }

    public void Clear()
    {
        _currentPen = null;
        _currentShape = null;
        SelectedItem = null;
        Items.Clear();
    }

    public bool DeleteSelectedItem()
    {
        if (SelectedItem is not { } item)
        {
            return false;
        }

        // OnItemsChanged will null out SelectedItem when the collection no longer contains it.
        return Items.Remove(item);
    }

    private void OnItemsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(HasItems));

        if (SelectedItem is null)
        {
            return;
        }

        if (e.Action == NotifyCollectionChangedAction.Remove ||
            e.Action == NotifyCollectionChangedAction.Replace ||
            e.Action == NotifyCollectionChangedAction.Reset)
        {
            if (!Items.Contains(SelectedItem))
            {
                SelectedItem = null;
            }
        }
    }
}
