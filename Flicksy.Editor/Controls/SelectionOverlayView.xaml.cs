using System.ComponentModel;
using System.Drawing.Imaging;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Flicksy.Editor.ViewModels;

namespace Flicksy.Editor.Controls;

public partial class SelectionOverlayView : UserControl
{
    private const double HandleRadius = 4.0;
    private const double RotateHandleSize = 26.0;
    private const double RotateHandleGap = 18.0;

    private static readonly ImageSource RotateIconSource = LoadRotateIcon();

    public static readonly DependencyProperty ContentToViewportProperty =
        DependencyProperty.Register(
            nameof(ContentToViewport),
            typeof(Transform),
            typeof(SelectionOverlayView),
            new PropertyMetadata(null, OnContentToViewportChanged));

    public SelectionOverlayView()
    {
        InitializeComponent();
        RotateHandleIcon.Source = RotateIconSource;
        DataContextChanged += OnDataContextChanged;
        Loaded += (_, _) => UpdateLayoutFromState();
    }

    public Transform? ContentToViewport
    {
        get => (Transform?)GetValue(ContentToViewportProperty);
        set => SetValue(ContentToViewportProperty, value);
    }

    private SelectionOverlayViewModel? ViewModel => DataContext as SelectionOverlayViewModel;

    private static void OnContentToViewportChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var view = (SelectionOverlayView)d;
        if (e.OldValue is Transform oldTransform)
        {
            oldTransform.Changed -= view.OnTransformChanged;
        }

        if (e.NewValue is Transform newTransform)
        {
            newTransform.Changed += view.OnTransformChanged;
        }

        view.UpdateLayoutFromState();
    }

    private void OnTransformChanged(object? sender, System.EventArgs e)
    {
        UpdateLayoutFromState();
    }

    private void OnDataContextChanged(object? sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is INotifyPropertyChanged oldVm)
        {
            oldVm.PropertyChanged -= OnViewModelPropertyChanged;
        }

        if (e.NewValue is INotifyPropertyChanged newVm)
        {
            newVm.PropertyChanged += OnViewModelPropertyChanged;
        }

        UpdateLayoutFromState();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(SelectionOverlayViewModel.ContentBounds)
            or nameof(SelectionOverlayViewModel.IsVisible)
            or nameof(SelectionOverlayViewModel.SelectedStroke)
            or nameof(SelectionOverlayViewModel.IsActive))
        {
            UpdateLayoutFromState();
        }
    }

    private void UpdateLayoutFromState()
    {
        var vm = ViewModel;
        if (vm is null || !vm.IsVisible)
        {
            Visibility = Visibility.Collapsed;
            return;
        }

        Visibility = Visibility.Visible;

        var bounds = vm.ContentBounds;
        var transform = ContentToViewport;

        var topLeft = new Point(bounds.X, bounds.Y);
        var topRight = new Point(bounds.X + bounds.Width, bounds.Y);
        var bottomLeft = new Point(bounds.X, bounds.Y + bounds.Height);
        var bottomRight = new Point(bounds.X + bounds.Width, bounds.Y + bounds.Height);

        if (transform is not null)
        {
            topLeft = transform.Transform(topLeft);
            topRight = transform.Transform(topRight);
            bottomLeft = transform.Transform(bottomLeft);
            bottomRight = transform.Transform(bottomRight);
        }

        var minX = System.Math.Min(System.Math.Min(topLeft.X, topRight.X), System.Math.Min(bottomLeft.X, bottomRight.X));
        var minY = System.Math.Min(System.Math.Min(topLeft.Y, topRight.Y), System.Math.Min(bottomLeft.Y, bottomRight.Y));
        var maxX = System.Math.Max(System.Math.Max(topLeft.X, topRight.X), System.Math.Max(bottomLeft.X, bottomRight.X));
        var maxY = System.Math.Max(System.Math.Max(topLeft.Y, topRight.Y), System.Math.Max(bottomLeft.Y, bottomRight.Y));

        SelectionBox.Width = System.Math.Max(0, maxX - minX);
        SelectionBox.Height = System.Math.Max(0, maxY - minY);
        Canvas.SetLeft(SelectionBox, minX);
        Canvas.SetTop(SelectionBox, minY);

        PositionHandle(HandleTopLeft, topLeft);
        PositionHandle(HandleTopRight, topRight);
        PositionHandle(HandleBottomLeft, bottomLeft);
        PositionHandle(HandleBottomRight, bottomRight);

        var topMidX = (topLeft.X + topRight.X) / 2.0;
        var topMidY = (topLeft.Y + topRight.Y) / 2.0;
        Canvas.SetLeft(RotateHandle, topMidX - RotateHandleSize / 2.0);
        Canvas.SetTop(RotateHandle, topMidY - RotateHandleGap - RotateHandleSize);
    }

    private static void PositionHandle(Ellipse handle, Point point)
    {
        Canvas.SetLeft(handle, point.X - HandleRadius);
        Canvas.SetTop(handle, point.Y - HandleRadius);
    }

    private static ImageSource LoadRotateIcon()
    {
        using var stream = new MemoryStream();
        Properties.Resources.rotate.Save(stream, ImageFormat.Png);
        stream.Position = 0;

        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.StreamSource = stream;
        image.EndInit();
        image.Freeze();
        return image;
    }
}
