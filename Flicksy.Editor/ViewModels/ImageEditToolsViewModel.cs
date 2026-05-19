using System;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Flicksy.Editor.Extensions;
using Flicksy.Editor.Properties;

namespace Flicksy.Editor.ViewModels;

public enum ImageEditTool
{
    Select,
    Pen,
    Erase,
    Shapes,
}

public partial class ImageEditToolsViewModel : ObservableObject
{
    private DateTime _lastPopupCloseAt = DateTime.MinValue;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPenActive))]
    [NotifyPropertyChangedFor(nameof(IsEraseActive))]
    [NotifyPropertyChangedFor(nameof(IsSelectActive))]
    [NotifyPropertyChangedFor(nameof(IsDrawingToolActive))]
    [NotifyPropertyChangedFor(nameof(IsImageToolActive))]
    private ImageEditTool selectedTool = ImageEditTool.Select;

    [ObservableProperty]
    private bool isPenSettingsOpen;

    [ObservableProperty]
    private bool isShapeSettingsOpen;

    private DateTime _lastShapePopupCloseAt = DateTime.MinValue;

    public ImageEditToolsViewModel()
    {
        Cursor = Resources.cursor.ToImageSource();
        PenBackground = Resources.pen_background.ToImageSource();
        PenForeground = Resources.pen_foreground.ToImageSource();
        Eraser = Resources.eraser.ToImageSource();
        Shapes = Resources.shapes.ToImageSource();
    }

    public ImageSource Cursor { get; }

    public ImageSource PenBackground { get; }

    public ImageSource PenForeground { get; }

    public ImageSource Eraser { get; }

    public ImageSource Shapes { get; }

    public PenSettingsViewModel PenSettings { get; } = new();

    public ShapeSettingsViewModel ShapeSettings { get; } = new();

    public bool IsPenActive => SelectedTool == ImageEditTool.Pen;

    public bool IsEraseActive => SelectedTool == ImageEditTool.Erase;

    public bool IsSelectActive => SelectedTool == ImageEditTool.Select;

    public bool IsDrawingToolActive => SelectedTool is ImageEditTool.Pen or ImageEditTool.Erase;

    public bool IsImageToolActive => SelectedTool is ImageEditTool.Pen or ImageEditTool.Erase or ImageEditTool.Select;

    [RelayCommand]
    private void Select()
    {
        SelectedTool = ImageEditTool.Select;
    }

    [RelayCommand]
    private void Pen()
    {
        if (SelectedTool == ImageEditTool.Pen)
        {
            if ((DateTime.UtcNow - _lastPopupCloseAt).TotalMilliseconds < 250)
            {
                return;
            }

            IsPenSettingsOpen = !IsPenSettingsOpen;
            return;
        }

        SelectedTool = ImageEditTool.Pen;
    }

    [RelayCommand]
    private void Erase()
    {
        SelectedTool = ImageEditTool.Erase;
    }

    [RelayCommand]
    private void ShapesTool()
    {
        if (SelectedTool == ImageEditTool.Shapes)
        {
            if ((DateTime.UtcNow - _lastShapePopupCloseAt).TotalMilliseconds < 250)
            {
                return;
            }

            IsShapeSettingsOpen = !IsShapeSettingsOpen;
            return;
        }

        SelectedTool = ImageEditTool.Shapes;
        IsShapeSettingsOpen = true;
    }

    partial void OnSelectedToolChanged(ImageEditTool value)
    {
        if (value != ImageEditTool.Pen)
        {
            IsPenSettingsOpen = false;
        }

        if (value != ImageEditTool.Shapes)
        {
            IsShapeSettingsOpen = false;
        }
    }

    partial void OnIsPenSettingsOpenChanged(bool value)
    {
        if (!value)
        {
            _lastPopupCloseAt = DateTime.UtcNow;
        }
    }

    partial void OnIsShapeSettingsOpenChanged(bool value)
    {
        if (!value)
        {
            _lastShapePopupCloseAt = DateTime.UtcNow;
        }
    }

}
