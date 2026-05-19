using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Flicksy.Editor.Properties;

namespace Flicksy.Editor.ViewModels;

public enum ImageEditTool
{
    Select,
    Pen,
    Erase,
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

    public ImageEditToolsViewModel()
    {
        Cursor = BitmapToImageSource(Resources.cursor);
        PenBackground = BitmapToImageSource(Resources.pen_background);
        PenForeground = BitmapToImageSource(Resources.pen_foreground);
        Eraser = BitmapToImageSource(Resources.eraser);
    }

    public ImageSource Cursor { get; }

    public ImageSource PenBackground { get; }

    public ImageSource PenForeground { get; }

    public ImageSource Eraser { get; }

    public PenSettingsViewModel PenSettings { get; } = new();

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

    partial void OnSelectedToolChanged(ImageEditTool value)
    {
        if (value != ImageEditTool.Pen)
        {
            IsPenSettingsOpen = false;
        }
    }

    partial void OnIsPenSettingsOpenChanged(bool value)
    {
        if (!value)
        {
            _lastPopupCloseAt = DateTime.UtcNow;
        }
    }

    private static ImageSource BitmapToImageSource(Bitmap bitmap)
    {
        using var stream = new MemoryStream();
        bitmap.Save(stream, ImageFormat.Png);
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
