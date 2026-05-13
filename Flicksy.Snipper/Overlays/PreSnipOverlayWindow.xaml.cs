using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DrawingRectangle = System.Drawing.Rectangle;
using WpfPoint = System.Windows.Point;

namespace Flicksy.Snipper.Overlays;

public partial class PreSnipOverlayWindow : Window
{
    private readonly DrawingRectangle _screenBounds;
    private readonly Action<Bitmap> _onSnipCaptured;
    private readonly Action<DrawingRectangle, DrawingRectangle> _onVideoAreaSelected;
    private readonly Bitmap _backgroundBitmap;
    private bool _isSelecting;
    private bool _hasSelection;
    private bool _isSnipMode = true;
    private WpfPoint _selectionStart;
    private Rect _selectionRect;

    public PreSnipOverlayWindow(
        DrawingRectangle bounds,
        Action<Bitmap> onSnipCaptured,
        Action<DrawingRectangle, DrawingRectangle> onVideoAreaSelected)
    {
        _screenBounds = bounds;
        _onSnipCaptured = onSnipCaptured;
        _onVideoAreaSelected = onVideoAreaSelected;

        InitializeComponent();

        Left = bounds.Left;
        Top = bounds.Top;
        Width = bounds.Width;
        Height = bounds.Height;

        _backgroundBitmap = new Bitmap(bounds.Width, bounds.Height);
        using (var graphics = Graphics.FromImage(_backgroundBitmap))
        {
            graphics.CopyFromScreen(bounds.Location, System.Drawing.Point.Empty, bounds.Size);
        }

        BackgroundImage.Source = CreateBitmapSource(_backgroundBitmap);
        SetMode(true);
        Loaded += (_, _) =>
        {
            Focus();
            UpdateSelectionVisuals();
        };
        SizeChanged += (_, _) => UpdateSelectionVisuals();
    }

    protected override void OnClosed(EventArgs e)
    {
        _backgroundBitmap.Dispose();
        base.OnClosed(e);
    }

    private void OnWindowKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close();
        }
    }

    private void OnSnipModeClick(object sender, RoutedEventArgs e)
    {
        SetMode(true);
    }

    private void OnRecordModeClick(object sender, RoutedEventArgs e)
    {
        SetMode(false);
    }

    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (IsInsideMenu(e.OriginalSource))
        {
            return;
        }

        _isSelecting = true;
        _hasSelection = true;
        _selectionStart = ClampToCanvas(e.GetPosition(OverlayCanvas));
        _selectionRect = Rect.Empty;
        Mouse.Capture(this);
        UpdateSelectionVisuals();
    }

    private void OnMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (!_isSelecting)
        {
            return;
        }

        var current = ClampToCanvas(e.GetPosition(OverlayCanvas));
        _selectionRect = CreateRect(_selectionStart, current);
        UpdateSelectionVisuals();
    }

    private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isSelecting)
        {
            return;
        }

        _isSelecting = false;
        Mouse.Capture(null);
        UpdateSelectionVisuals();

        var selection = GetSelectionRectangle();
        if (selection.Width < 2 || selection.Height < 2)
        {
            return;
        }

        if (_isSnipMode)
        {
            var captured = new Bitmap(selection.Width, selection.Height);
            using (var graphics = Graphics.FromImage(captured))
            {
                graphics.DrawImage(
                    _backgroundBitmap,
                    new DrawingRectangle(0, 0, selection.Width, selection.Height),
                    selection,
                    GraphicsUnit.Pixel);
            }

            _onSnipCaptured(captured);
            Close();
            return;
        }

        _onVideoAreaSelected(_screenBounds, selection);
        Close();
    }

    private void SetMode(bool isSnipMode)
    {
        _isSnipMode = isSnipMode;

        SetButtonStyle(SnipModeButton, isSnipMode);
        SetButtonStyle(RecordModeButton, !isSnipMode);

        SelectionBorder.Stroke = isSnipMode ? System.Windows.Media.Brushes.White : System.Windows.Media.Brushes.Red;
        InstructionText.Text = isSnipMode
            ? "Click and drag an area to snip"
            : "Click and drag an area to record";
    }

    private static void SetButtonStyle(System.Windows.Controls.Button button, bool selected)
    {
        button.Background = selected
            ? new SolidColorBrush(System.Windows.Media.Color.FromRgb(102, 102, 102))
            : new SolidColorBrush(System.Windows.Media.Color.FromRgb(70, 70, 70));
        button.BorderBrush = selected
            ? System.Windows.Media.Brushes.White
            : new SolidColorBrush(System.Windows.Media.Color.FromRgb(120, 120, 120));
        button.BorderThickness = new Thickness(1);
    }

    private void UpdateSelectionVisuals()
    {
        var canvasWidth = OverlayCanvas.ActualWidth;
        var canvasHeight = OverlayCanvas.ActualHeight;
        if (canvasWidth <= 0 || canvasHeight <= 0)
        {
            return;
        }

        if (!_hasSelection || _selectionRect.IsEmpty || _selectionRect.Width <= 0 || _selectionRect.Height <= 0)
        {
            SetCanvasRect(TopShade, 0, 0, canvasWidth, canvasHeight);
            SetCanvasRect(LeftShade, 0, 0, 0, 0);
            SetCanvasRect(RightShade, 0, 0, 0, 0);
            SetCanvasRect(BottomShade, 0, 0, 0, 0);
            SelectionBorder.Visibility = Visibility.Collapsed;
            return;
        }

        var rect = _selectionRect;
        SetCanvasRect(TopShade, 0, 0, canvasWidth, rect.Y);
        SetCanvasRect(LeftShade, 0, rect.Y, rect.X, rect.Height);
        SetCanvasRect(RightShade, rect.Right, rect.Y, canvasWidth - rect.Right, rect.Height);
        SetCanvasRect(BottomShade, 0, rect.Bottom, canvasWidth, canvasHeight - rect.Bottom);

        SetCanvasRect(SelectionBorder, rect.X, rect.Y, rect.Width, rect.Height);
        SelectionBorder.Visibility = Visibility.Visible;
    }

    private DrawingRectangle GetSelectionRectangle()
    {
        var maxWidth = _backgroundBitmap.Width;
        var maxHeight = _backgroundBitmap.Height;
        var x = Math.Clamp((int)Math.Floor(_selectionRect.X), 0, Math.Max(maxWidth - 1, 0));
        var y = Math.Clamp((int)Math.Floor(_selectionRect.Y), 0, Math.Max(maxHeight - 1, 0));
        var width = Math.Clamp((int)Math.Ceiling(_selectionRect.Width), 0, maxWidth - x);
        var height = Math.Clamp((int)Math.Ceiling(_selectionRect.Height), 0, maxHeight - y);
        return new DrawingRectangle(x, y, width, height);
    }

    private static bool IsInsideMenu(object? source)
    {
        if (source is not DependencyObject dependencyObject)
        {
            return false;
        }

        var current = dependencyObject;
        while (current is not null)
        {
            if (current is Border border && border.Name == "MenuBorder")
            {
                return true;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return false;
    }

    private WpfPoint ClampToCanvas(WpfPoint point)
    {
        var x = Math.Clamp(point.X, 0, OverlayCanvas.ActualWidth);
        var y = Math.Clamp(point.Y, 0, OverlayCanvas.ActualHeight);
        return new WpfPoint(x, y);
    }

    private static Rect CreateRect(WpfPoint a, WpfPoint b)
    {
        var left = Math.Min(a.X, b.X);
        var top = Math.Min(a.Y, b.Y);
        var width = Math.Abs(b.X - a.X);
        var height = Math.Abs(b.Y - a.Y);
        return new Rect(left, top, width, height);
    }

    private static void SetCanvasRect(FrameworkElement element, double x, double y, double width, double height)
    {
        Canvas.SetLeft(element, Math.Max(0, x));
        Canvas.SetTop(element, Math.Max(0, y));
        element.Width = Math.Max(0, width);
        element.Height = Math.Max(0, height);
    }

    private static BitmapSource CreateBitmapSource(Bitmap bitmap)
    {
        var hBitmap = bitmap.GetHbitmap();
        try
        {
            var source = Imaging.CreateBitmapSourceFromHBitmap(
                hBitmap,
                IntPtr.Zero,
                Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());
            source.Freeze();
            return source;
        }
        finally
        {
            DeleteObject(hBitmap);
        }
    }

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr hObject);
}
