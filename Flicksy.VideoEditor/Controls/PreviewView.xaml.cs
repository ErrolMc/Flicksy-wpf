using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Flicksy.VideoEditor.Project;
using Flicksy.VideoEditor.ViewModels;

namespace Flicksy.VideoEditor.Controls;

/// <summary>
/// Top-of-center-column preview surface. The <see cref="Image"/>'s <c>Stretch=Uniform</c>
/// combined with a source bitmap sized to <see cref="ProjectSettings.ResolutionWidth"/> ×
/// <see cref="ProjectSettings.ResolutionHeight"/> letterboxes the content against the
/// control's dark background. Until the compositor lands (#10/#11) the source is a solid
/// fill at the project resolution; replacing it with a real frame is a `PreviewImage.Source`
/// assignment.
/// </summary>
public partial class PreviewView : UserControl
{
    private ProjectSettings? _subscribedSettings;

    public PreviewView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Unloaded += OnUnloaded;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        DetachSettings();

        if (e.NewValue is PreviewViewModel vm)
        {
            _subscribedSettings = vm.ProjectSettings;
            _subscribedSettings.PropertyChanged += OnSettingsPropertyChanged;
            UpdatePlaceholder();
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        DetachSettings();
    }

    private void DetachSettings()
    {
        if (_subscribedSettings is null) return;
        _subscribedSettings.PropertyChanged -= OnSettingsPropertyChanged;
        _subscribedSettings = null;
    }

    private void OnSettingsPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ProjectSettings.ResolutionWidth)
            || e.PropertyName == nameof(ProjectSettings.ResolutionHeight))
        {
            UpdatePlaceholder();
        }
    }

    private void UpdatePlaceholder()
    {
        if (_subscribedSettings is null) return;
        var width = _subscribedSettings.ResolutionWidth;
        var height = _subscribedSettings.ResolutionHeight;
        if (width <= 0 || height <= 0)
        {
            PreviewImage.Source = null;
            return;
        }

        // Vector placeholder: a single filled rect sized to the project resolution. Its
        // bounds are what Stretch=Uniform aspect-locks against — no raster allocation, no
        // pixel data to manage.
        var brush = new SolidColorBrush(Color.FromRgb(0x18, 0x18, 0x20));
        brush.Freeze();
        var geometry = new RectangleGeometry(new Rect(0, 0, width, height));
        geometry.Freeze();
        var drawing = new GeometryDrawing(brush, pen: null, geometry);
        drawing.Freeze();
        var image = new DrawingImage(drawing);
        image.Freeze();
        PreviewImage.Source = image;
    }
}
