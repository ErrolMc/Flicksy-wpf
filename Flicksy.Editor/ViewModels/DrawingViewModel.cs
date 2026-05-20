using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Flicksy.Editor.Source;

namespace Flicksy.Editor.ViewModels;

public partial class DrawingViewModel : ObservableObject
{
    private PenStrokeItem? _currentPen;
    private ShapeItem? _currentShape;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelectedItem))]
    [NotifyPropertyChangedFor(nameof(CanMoveSelectedItemUp))]
    [NotifyPropertyChangedFor(nameof(CanMoveSelectedItemDown))]
    [NotifyCanExecuteChangedFor(nameof(MoveSelectedItemUpCommand))]
    [NotifyCanExecuteChangedFor(nameof(MoveSelectedItemDownCommand))]
    private DrawingItem? selectedItem;

    [ObservableProperty]
    private TextItem? editingTextItem;

    public DrawingViewModel()
    {
        Items.CollectionChanged += OnItemsChanged;
    }

    public ObservableCollection<DrawingItem> Items { get; } = new();

    public bool HasItems => Items.Count > 0;

    public bool HasSelectedItem => SelectedItem is not null;

    public bool CanMoveSelectedItemUp => SelectedItem is { } item && Items.IndexOf(item) < Items.Count - 1;

    public bool CanMoveSelectedItemDown => SelectedItem is { } item && Items.IndexOf(item) > 0;

    [RelayCommand(CanExecute = nameof(CanMoveSelectedItemUp))]
    private void MoveSelectedItemUp()
    {
        if (SelectedItem is not { } item)
        {
            return;
        }

        var index = Items.IndexOf(item);
        if (index < 0 || index >= Items.Count - 1)
        {
            return;
        }

        Items.Move(index, index + 1);
    }

    [RelayCommand(CanExecute = nameof(CanMoveSelectedItemDown))]
    private void MoveSelectedItemDown()
    {
        if (SelectedItem is not { } item)
        {
            return;
        }

        var index = Items.IndexOf(item);
        if (index <= 0)
        {
            return;
        }

        Items.Move(index, index - 1);
    }

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

    public TextItem BeginText(Point origin, string fontFamily, double fontSize, Brush? fill, Brush? outline, double outlineThickness)
    {
        var item = new TextItem(origin, fontFamily, fontSize, fill, outline, outlineThickness);
        Items.Add(item);
        SelectedItem = item;
        return item;
    }

    public void BeginEditText(TextItem item)
    {
        if (EditingTextItem is { } current && !ReferenceEquals(current, item))
        {
            EndEditText(commit: true);
        }

        SelectedItem = item;
        item.IsEditing = true;
        EditingTextItem = item;
    }

    public void EndEditText(bool commit)
    {
        if (EditingTextItem is not { } item)
        {
            return;
        }

        item.IsEditing = false;
        EditingTextItem = null;

        if (commit && item.IsEmpty)
        {
            Items.Remove(item);
        }
    }

    public void Clear()
    {
        _currentPen = null;
        _currentShape = null;
        if (EditingTextItem is { } editing)
        {
            editing.IsEditing = false;
        }
        EditingTextItem = null;
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
                return;
            }
        }

        // The selected item's index may have shifted (move/add/remove), so the layer
        // commands' executability needs to be re-checked.
        OnPropertyChanged(nameof(CanMoveSelectedItemUp));
        OnPropertyChanged(nameof(CanMoveSelectedItemDown));
        MoveSelectedItemUpCommand.NotifyCanExecuteChanged();
        MoveSelectedItemDownCommand.NotifyCanExecuteChanged();
    }
}
