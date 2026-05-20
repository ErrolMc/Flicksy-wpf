using System;
using System.ComponentModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using Flicksy.Editor.Source;

namespace Flicksy.Editor.ViewModels;

public partial class SelectionOverlayViewModel : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsVisible))]
    private DrawingItem? selectedItem;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsVisible))]
    private bool isActive;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsVisible))]
    private Rect canonicalBounds = Rect.Empty;

    [ObservableProperty]
    private bool showHandles = true;

    public bool IsVisible => IsActive && SelectedItem is not null && !CanonicalBounds.IsEmpty;

    public event EventHandler? TransformChanged;

    partial void OnSelectedItemChanged(DrawingItem? oldValue, DrawingItem? newValue)
    {
        if (oldValue is not null)
        {
            oldValue.PropertyChanged -= OnItemPropertyChanged;
            oldValue.Transform.Changed -= OnItemTransformChanged;
        }

        if (newValue is not null)
        {
            newValue.PropertyChanged += OnItemPropertyChanged;
            newValue.Transform.Changed += OnItemTransformChanged;
        }

        RecomputeCanonicalBounds();
        TransformChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // Recompute bounds whenever the item's geometry or any derived property changes.
        if (e.PropertyName == nameof(DrawingItem.Geometry))
        {
            RecomputeCanonicalBounds();
        }
    }

    private void OnItemTransformChanged(object? sender, EventArgs e)
    {
        TransformChanged?.Invoke(this, EventArgs.Empty);
    }

    private void RecomputeCanonicalBounds()
    {
        CanonicalBounds = SelectedItem?.CanonicalBounds ?? Rect.Empty;
    }
}
