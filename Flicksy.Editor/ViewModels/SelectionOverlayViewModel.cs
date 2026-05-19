using System;
using System.ComponentModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Flicksy.Editor.ViewModels;

public partial class SelectionOverlayViewModel : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsVisible))]
    private Stroke? selectedStroke;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsVisible))]
    private bool isActive;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsVisible))]
    private Rect canonicalBounds = Rect.Empty;

    public bool IsVisible => IsActive && SelectedStroke is not null && !CanonicalBounds.IsEmpty;

    public event EventHandler? TransformChanged;

    partial void OnSelectedStrokeChanged(Stroke? oldValue, Stroke? newValue)
    {
        if (oldValue is not null)
        {
            oldValue.PropertyChanged -= OnStrokePropertyChanged;
            oldValue.Transform.Changed -= OnStrokeTransformChanged;
        }

        if (newValue is not null)
        {
            newValue.PropertyChanged += OnStrokePropertyChanged;
            newValue.Transform.Changed += OnStrokeTransformChanged;
        }

        RecomputeCanonicalBounds();
        TransformChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnStrokePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(Stroke.Geometry) || e.PropertyName == nameof(Stroke.Thickness))
        {
            RecomputeCanonicalBounds();
        }
    }

    private void OnStrokeTransformChanged(object? sender, EventArgs e)
    {
        TransformChanged?.Invoke(this, EventArgs.Empty);
    }

    private void RecomputeCanonicalBounds()
    {
        CanonicalBounds = SelectedStroke?.CanonicalBounds ?? Rect.Empty;
    }
}
