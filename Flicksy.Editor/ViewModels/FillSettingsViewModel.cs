using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Flicksy.Editor.Extensions;
using Flicksy.Editor.Properties;

namespace Flicksy.Editor.ViewModels;

public partial class FillColorOption : ObservableObject
{
    public FillColorOption(Brush brush, bool isNone = false)
    {
        Brush = brush;
        IsNone = isNone;
    }

    public Brush Brush { get; }

    public bool IsNone { get; }

    [ObservableProperty]
    private bool isSelected;
}

public partial class FillSettingsViewModel : ObservableObject
{
    [ObservableProperty]
    private double opacity = 1;

    [ObservableProperty]
    private FillColorOption? selectedColor;

    public ObservableCollection<FillColorOption> Colors { get; }

    public ImageSource CrossCircle { get; } = Resources.cross_circle.ToImageSource();

    public FillSettingsViewModel()
    {
        Colors = new ObservableCollection<FillColorOption>(CreateColors());
        SelectColor(Colors[1]);
    }

    [RelayCommand]
    private void SelectColor(FillColorOption option)
    {
        if (SelectedColor is not null)
        {
            SelectedColor.IsSelected = false;
        }

        option.IsSelected = true;
        SelectedColor = option;
    }

    private static IEnumerable<FillColorOption> CreateColors()
    {
        yield return new FillColorOption(CreateBrush("#FFFFFF"), isNone: true);

        string[] hexes =
        {
            "#000000", "#FFFFFF", "#D9D9D9", "#ACACAC", "#767676",
            "#C2185B", "#E53935", "#FF6D00", "#FFA000", "#FFD600", "#FFEB3B",
            "#AEEA00", "#00C853", "#00695C", "#00B0FF", "#1976D2", "#1A237E",
            "#6200EA", "#4A148C", "#FFCCBC", "#BCAAA4", "#795548", "#5D4037",
            "#F48FB1", "#FFAB91", "#FFF59D", "#A5D6A7", "#81D4FA", "#B39DDB",
        };

        foreach (var hex in hexes)
        {
            yield return new FillColorOption(CreateBrush(hex));
        }
    }

    private static Brush CreateBrush(string hex)
    {
        var color = (Color)ColorConverter.ConvertFromString(hex);
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }
}
