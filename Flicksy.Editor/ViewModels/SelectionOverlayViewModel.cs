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
    private Rect contentBounds = Rect.Empty;

    public bool IsVisible => IsActive && SelectedStroke is not null && !ContentBounds.IsEmpty;

    partial void OnSelectedStrokeChanged(Stroke? oldValue, Stroke? newValue)
    {
        if (oldValue is not null)
        {
            oldValue.PropertyChanged -= OnStrokePropertyChanged;
        }

        if (newValue is not null)
        {
            newValue.PropertyChanged += OnStrokePropertyChanged;
        }

        RecomputeBounds();
    }

    private void OnStrokePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(Stroke.Geometry) || e.PropertyName == nameof(Stroke.Thickness))
        {
            RecomputeBounds();
        }
    }

    private void RecomputeBounds()
    {
        if (SelectedStroke?.Geometry is { } geometry && !geometry.Bounds.IsEmpty)
        {
            var bounds = geometry.Bounds;
            var inflate = SelectedStroke.Thickness / 2.0;
            bounds.Inflate(inflate, inflate);
            ContentBounds = bounds;
        }
        else
        {
            ContentBounds = Rect.Empty;
        }
    }
}
