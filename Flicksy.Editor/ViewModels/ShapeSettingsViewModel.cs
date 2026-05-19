using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Flicksy.Editor.Extensions;
using Flicksy.Editor.Properties;

namespace Flicksy.Editor.ViewModels;

public enum ShapeKind
{
    Square,
    Circle,
    Line,
    Arrow,
}

public partial class ShapeOption : ObservableObject
{
    public ShapeOption(ShapeKind kind, string name, ImageSource icon)
    {
        Kind = kind;
        Name = name;
        Icon = icon;
    }

    public ShapeKind Kind { get; }

    public string Name { get; }

    public ImageSource Icon { get; }

    [ObservableProperty]
    private bool isSelected;
}

public partial class ShapeSettingsViewModel : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsFillEnabled))]
    private ShapeOption? selectedShape;

    public bool IsFillEnabled => SelectedShape?.Kind is not (ShapeKind.Line or ShapeKind.Arrow);

    [ObservableProperty]
    private Brush fillBrush = CreateBrush(Colors.Black);

    [ObservableProperty]
    private Brush outlineBrush = CreateBrush(Colors.Black);

    public double MinStrokeWidth { get; } = 1;

    public double MaxStrokeWidth { get; } = 30;

    [ObservableProperty]
    private double strokeWidth = 4;

    public ObservableCollection<ShapeOption> Shapes { get; }

    public ImageSource CircleOutside { get; } = Resources.circle.ToImageSource();

    public ImageSource CircleInside { get; } = Resources.circle_inside.ToImageSource();

    public ImageSource DonutOutside { get; } = Resources.donut_outside.ToImageSource();

    public ImageSource DonutInside { get; } = Resources.donut_inside.ToImageSource();

    public ShapeSettingsViewModel()
    {
        Shapes = new ObservableCollection<ShapeOption>(CreateShapes());
        SelectShape(Shapes[0]);
    }

    private static Brush CreateBrush(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    [RelayCommand]
    private void SelectShape(ShapeOption option)
    {
        if (SelectedShape is not null)
        {
            SelectedShape.IsSelected = false;
        }

        option.IsSelected = true;
        SelectedShape = option;
    }

    private static IEnumerable<ShapeOption> CreateShapes()
    {
        yield return new ShapeOption(ShapeKind.Square, "Square", Resources.square.ToImageSource());
        yield return new ShapeOption(ShapeKind.Circle, "Circle", Resources.circle.ToImageSource());
        yield return new ShapeOption(ShapeKind.Line, "Line", Resources.line.ToImageSource());
        yield return new ShapeOption(ShapeKind.Arrow, "Arrow", Resources.arrow.ToImageSource());
    }
}
