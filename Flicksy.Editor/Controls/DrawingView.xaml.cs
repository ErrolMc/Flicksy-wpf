using System;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Flicksy.Editor.Source;
using Flicksy.Editor.ViewModels;

namespace Flicksy.Editor.Controls;

public partial class DrawingView : UserControl
{
    public static readonly DependencyProperty StrokeBrushProperty =
        DependencyProperty.Register(
            nameof(StrokeBrush),
            typeof(Brush),
            typeof(DrawingView),
            new PropertyMetadata(Brushes.Black));

    public static readonly DependencyProperty StrokeThicknessProperty =
        DependencyProperty.Register(
            nameof(StrokeThickness),
            typeof(double),
            typeof(DrawingView),
            new PropertyMetadata(1.0));

    public static readonly DependencyProperty IsErasingProperty =
        DependencyProperty.Register(
            nameof(IsErasing),
            typeof(bool),
            typeof(DrawingView),
            new PropertyMetadata(false));

    public static readonly DependencyProperty IsSelectActiveProperty =
        DependencyProperty.Register(
            nameof(IsSelectActive),
            typeof(bool),
            typeof(DrawingView),
            new PropertyMetadata(false));

    public static readonly DependencyProperty IsShapeActiveProperty =
        DependencyProperty.Register(
            nameof(IsShapeActive),
            typeof(bool),
            typeof(DrawingView),
            new PropertyMetadata(false));

    public static readonly DependencyProperty IsTextActiveProperty =
        DependencyProperty.Register(
            nameof(IsTextActive),
            typeof(bool),
            typeof(DrawingView),
            new PropertyMetadata(false));

    public static readonly DependencyProperty TextFontFamilyProperty =
        DependencyProperty.Register(
            nameof(TextFontFamily),
            typeof(string),
            typeof(DrawingView),
            new PropertyMetadata("Segoe UI", OnSelectedTextStyleChanged));

    public static readonly DependencyProperty TextFontSizeProperty =
        DependencyProperty.Register(
            nameof(TextFontSize),
            typeof(double),
            typeof(DrawingView),
            new PropertyMetadata(24.0, OnSelectedTextStyleChanged));

    public static readonly DependencyProperty TextFillBrushProperty =
        DependencyProperty.Register(
            nameof(TextFillBrush),
            typeof(Brush),
            typeof(DrawingView),
            new PropertyMetadata(null, OnSelectedTextStyleChanged));

    public static readonly DependencyProperty TextOutlineBrushProperty =
        DependencyProperty.Register(
            nameof(TextOutlineBrush),
            typeof(Brush),
            typeof(DrawingView),
            new PropertyMetadata(null, OnSelectedTextStyleChanged));

    public static readonly DependencyProperty TextOutlineThicknessProperty =
        DependencyProperty.Register(
            nameof(TextOutlineThickness),
            typeof(double),
            typeof(DrawingView),
            new PropertyMetadata(4.0, OnSelectedTextStyleChanged));

    public static readonly DependencyProperty ActiveShapeProperty =
        DependencyProperty.Register(
            nameof(ActiveShape),
            typeof(ShapeKind),
            typeof(DrawingView),
            new PropertyMetadata(ShapeKind.Square));

    public static readonly DependencyProperty FillBrushProperty =
        DependencyProperty.Register(
            nameof(FillBrush),
            typeof(Brush),
            typeof(DrawingView),
            new PropertyMetadata(null));

    public static readonly DependencyProperty OutlineBrushProperty =
        DependencyProperty.Register(
            nameof(OutlineBrush),
            typeof(Brush),
            typeof(DrawingView),
            new PropertyMetadata(null));

    public static readonly DependencyProperty OutlineThicknessProperty =
        DependencyProperty.Register(
            nameof(OutlineThickness),
            typeof(double),
            typeof(DrawingView),
            new PropertyMetadata(4.0));

    public static readonly DependencyProperty ContentScaleProperty =
        DependencyProperty.Register(
            nameof(ContentScale),
            typeof(double),
            typeof(DrawingView),
            new PropertyMetadata(1.0));

    public DrawingView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    public Brush StrokeBrush
    {
        get => (Brush)GetValue(StrokeBrushProperty);
        set => SetValue(StrokeBrushProperty, value);
    }

    public double StrokeThickness
    {
        get => (double)GetValue(StrokeThicknessProperty);
        set => SetValue(StrokeThicknessProperty, value);
    }

    public bool IsErasing
    {
        get => (bool)GetValue(IsErasingProperty);
        set => SetValue(IsErasingProperty, value);
    }

    public bool IsSelectActive
    {
        get => (bool)GetValue(IsSelectActiveProperty);
        set => SetValue(IsSelectActiveProperty, value);
    }

    public bool IsShapeActive
    {
        get => (bool)GetValue(IsShapeActiveProperty);
        set => SetValue(IsShapeActiveProperty, value);
    }

    public bool IsTextActive
    {
        get => (bool)GetValue(IsTextActiveProperty);
        set => SetValue(IsTextActiveProperty, value);
    }

    public string TextFontFamily
    {
        get => (string)GetValue(TextFontFamilyProperty);
        set => SetValue(TextFontFamilyProperty, value);
    }

    public double TextFontSize
    {
        get => (double)GetValue(TextFontSizeProperty);
        set => SetValue(TextFontSizeProperty, value);
    }

    public Brush? TextFillBrush
    {
        get => (Brush?)GetValue(TextFillBrushProperty);
        set => SetValue(TextFillBrushProperty, value);
    }

    public Brush? TextOutlineBrush
    {
        get => (Brush?)GetValue(TextOutlineBrushProperty);
        set => SetValue(TextOutlineBrushProperty, value);
    }

    public double TextOutlineThickness
    {
        get => (double)GetValue(TextOutlineThicknessProperty);
        set => SetValue(TextOutlineThicknessProperty, value);
    }

    public ShapeKind ActiveShape
    {
        get => (ShapeKind)GetValue(ActiveShapeProperty);
        set => SetValue(ActiveShapeProperty, value);
    }

    public Brush? FillBrush
    {
        get => (Brush?)GetValue(FillBrushProperty);
        set => SetValue(FillBrushProperty, value);
    }

    public Brush? OutlineBrush
    {
        get => (Brush?)GetValue(OutlineBrushProperty);
        set => SetValue(OutlineBrushProperty, value);
    }

    public double OutlineThickness
    {
        get => (double)GetValue(OutlineThicknessProperty);
        set => SetValue(OutlineThicknessProperty, value);
    }

    public double ContentScale
    {
        get => (double)GetValue(ContentScaleProperty);
        set => SetValue(ContentScaleProperty, value);
    }

    public DrawingViewModel? ViewModel => DataContext as DrawingViewModel;

    private enum CornerKind { None, TopLeft, TopRight, BottomLeft, BottomRight }

    private Point? _lastAppendedPoint;
    private Point? _smoothedPoint;
    private const double InputSmoothingAlpha = 0.5d;
    private DrawingItem? _draggingItem;
    private Matrix _moveBaseMatrix;
    private Point _moveStartPoint;
    private DrawingItem? _scalingItem;
    private Matrix _scaleBaseMatrix;
    private Point _scaleAnchorWorld;
    private Point _scaleOriginalGrabbedWorld;
    private bool _isDrawingShape;
    private Point _shapeStartPoint;
    private ShapeKind _shapeKindInProgress;

    private void OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (ViewModel is null || !TryGetPoint(e, clampToBounds: false, out var point))
        {
            return;
        }

        // Double-click on a TextItem (in Select mode) starts editing without dragging.
        if (e.ClickCount >= 2 && IsSelectActive && HitTestItem(point) is TextItem doubleClickedText)
        {
            ViewModel.BeginEditText(doubleClickedText);
            e.Handled = true;
            return;
        }

        if (IsSelectActive)
        {
            if (ViewModel.SelectedItem is { } current && GetCornerHit(current, point) is var corner && corner != CornerKind.None)
            {
                var canonical = current.CanonicalBounds;
                var (anchorCanonical, grabbedCanonical) = GetAnchorAndGrabbed(canonical, corner);
                var m = current.Transform.Matrix;
                _scalingItem = current;
                _scaleBaseMatrix = m;
                _scaleAnchorWorld = m.Transform(anchorCanonical);
                _scaleOriginalGrabbedWorld = m.Transform(grabbedCanonical);
                CaptureMouse();
                e.Handled = true;
                return;
            }

            var hit = HitTestItem(point);
            if (hit is not null)
            {
                if (hit == ViewModel.SelectedItem)
                {
                    _draggingItem = hit;
                    _moveBaseMatrix = hit.Transform.Matrix;
                    _moveStartPoint = point;
                    CaptureMouse();
                }
                else
                {
                    ViewModel.SelectedItem = hit;
                }
            }
            else if (ViewModel.SelectedItem is { } selected && IsInsideSelectionBounds(selected, point))
            {
                _draggingItem = selected;
                _moveBaseMatrix = selected.Transform.Matrix;
                _moveStartPoint = point;
                CaptureMouse();
            }
            else
            {
                ViewModel.SelectedItem = null;
            }

            e.Handled = true;
            return;
        }

        if (IsErasing)
        {
            EraseAt(point);
            CaptureMouse();
            e.Handled = true;
            return;
        }

        if (IsShapeActive)
        {
            _shapeKindInProgress = ActiveShape;
            _shapeStartPoint = point;
            _isDrawingShape = true;
            ViewModel.BeginShape(point, _shapeKindInProgress, FillBrush, OutlineBrush, OutlineThickness);
            CaptureMouse();
            e.Handled = true;
            return;
        }

        if (IsTextActive)
        {
            // If clicking on existing text, edit it; otherwise create a new text item.
            var hit = HitTestItem(point);
            if (hit is TextItem existing)
            {
                ViewModel.BeginEditText(existing);
            }
            else
            {
                var created = ViewModel.BeginText(
                    point,
                    TextFontFamily,
                    TextFontSize,
                    TextFillBrush,
                    TextOutlineBrush,
                    TextOutlineThickness);
                ViewModel.BeginEditText(created);
            }
            e.Handled = true;
            return;
        }

        ViewModel.BeginPenStroke(point, StrokeBrush, StrokeThickness);
        _lastAppendedPoint = point;
        _smoothedPoint = point;
        CaptureMouse();
        e.Handled = true;
    }

    private DrawingItem? HitTestItem(Point point)
    {
        if (ViewModel is null)
        {
            return null;
        }

        for (var i = ViewModel.Items.Count - 1; i >= 0; i--)
        {
            var item = ViewModel.Items[i];
            if (IntersectsItem(item, point))
            {
                return item;
            }
        }

        return null;
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        if (ViewModel is null)
        {
            return;
        }

        if (e.LeftButton != MouseButtonState.Pressed)
        {
            UpdateHoverCursor(e);
            return;
        }

        if (_scalingItem is not null)
        {
            var cursor = e.GetPosition(this);
            var oldDiag = _scaleOriginalGrabbedWorld - _scaleAnchorWorld;
            var oldLengthSq = oldDiag.X * oldDiag.X + oldDiag.Y * oldDiag.Y;
            if (oldLengthSq > double.Epsilon)
            {
                var offset = cursor - _scaleAnchorWorld;
                var factor = (offset.X * oldDiag.X + offset.Y * oldDiag.Y) / oldLengthSq;
                if (Math.Abs(factor) < 0.01d)
                {
                    factor = factor < 0 ? -0.01d : 0.01d;
                }
                _scalingItem.ScaleFrom(_scaleBaseMatrix, factor, _scaleAnchorWorld);
            }
            return;
        }

        if (_draggingItem is not null)
        {
            var current = e.GetPosition(this);
            var totalDelta = current - _moveStartPoint;
            _draggingItem.TranslateFrom(_moveBaseMatrix, totalDelta);
            return;
        }

        if (_isDrawingShape)
        {
            // Allow shape drag outside bounds so the cursor stays responsive at edges.
            var rawCurrent = e.GetPosition(this);
            var endPoint = (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift
                ? ApplyShiftConstraint(_shapeKindInProgress, _shapeStartPoint, rawCurrent)
                : rawCurrent;
            ViewModel.UpdateShapeEndPoint(endPoint);
            return;
        }

        if (!TryGetPoint(e, clampToBounds: true, out var point))
        {
            return;
        }

        if (IsErasing)
        {
            EraseAt(point);
            return;
        }

        var smoothed = SmoothInput(point);

        if (_lastAppendedPoint is Point lastPoint)
        {
            var minDistance = Math.Max(1.5d, StrokeThickness * 0.5d);
            var dx = smoothed.X - lastPoint.X;
            var dy = smoothed.Y - lastPoint.Y;
            if ((dx * dx) + (dy * dy) < (minDistance * minDistance))
            {
                return;
            }
        }

        ViewModel.AppendPenPoint(smoothed);
        _lastAppendedPoint = smoothed;
    }

    private Point SmoothInput(Point raw)
    {
        if (_smoothedPoint is not Point previous)
        {
            _smoothedPoint = raw;
            return raw;
        }

        var smoothed = new Point(
            (InputSmoothingAlpha * raw.X) + ((1d - InputSmoothingAlpha) * previous.X),
            (InputSmoothingAlpha * raw.Y) + ((1d - InputSmoothingAlpha) * previous.Y));
        _smoothedPoint = smoothed;
        return smoothed;
    }

    private void OnMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_scalingItem is not null)
        {
            _scalingItem = null;
            ReleaseMouseCapture();
            e.Handled = true;
            return;
        }

        if (_draggingItem is not null)
        {
            _draggingItem = null;
            ReleaseMouseCapture();
            e.Handled = true;
            return;
        }

        if (_isDrawingShape)
        {
            if (ViewModel is not null)
            {
                var rawCurrent = e.GetPosition(this);
                var endPoint = (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift
                    ? ApplyShiftConstraint(_shapeKindInProgress, _shapeStartPoint, rawCurrent)
                    : rawCurrent;
                ViewModel.UpdateShapeEndPoint(endPoint);
                ViewModel.EndShape();
            }

            _isDrawingShape = false;
            ReleaseMouseCapture();
            e.Handled = true;
            return;
        }

        if (IsErasing)
        {
            if (ViewModel is not null && TryGetPoint(e, clampToBounds: true, out var erasePoint))
            {
                EraseAt(erasePoint);
            }

            ReleaseMouseCapture();
            _lastAppendedPoint = null;
            _smoothedPoint = null;
            return;
        }

        if (ViewModel is not null && TryGetPoint(e, clampToBounds: true, out var point))
        {
            ViewModel.AppendPenPoint(SmoothInput(point));
        }

        ViewModel?.EndPenStroke();
        ReleaseMouseCapture();
        _lastAppendedPoint = null;
        _smoothedPoint = null;
    }

    private static Point ApplyShiftConstraint(ShapeKind kind, Point start, Point end)
    {
        switch (kind)
        {
            case ShapeKind.Square:
            case ShapeKind.Circle:
            {
                // Constrain to equal side lengths (perfect square / circle), preserving each axis sign.
                var dx = end.X - start.X;
                var dy = end.Y - start.Y;
                var size = Math.Max(Math.Abs(dx), Math.Abs(dy));
                var signX = dx < 0 ? -1 : 1;
                var signY = dy < 0 ? -1 : 1;
                return new Point(start.X + signX * size, start.Y + signY * size);
            }
            case ShapeKind.Line:
            case ShapeKind.Arrow:
            {
                // Snap angle to the nearest 45°, preserve length.
                var dx = end.X - start.X;
                var dy = end.Y - start.Y;
                var length = Math.Sqrt(dx * dx + dy * dy);
                if (length <= double.Epsilon)
                {
                    return end;
                }

                var step = Math.PI / 4.0;
                var angle = Math.Atan2(dy, dx);
                var snapped = Math.Round(angle / step) * step;
                return new Point(start.X + Math.Cos(snapped) * length, start.Y + Math.Sin(snapped) * length);
            }
            default:
                return end;
        }
    }

    private bool TryGetPoint(MouseEventArgs e, bool clampToBounds, out Point point)
    {
        var x = ActualWidth;
        var y = ActualHeight;
        if (x <= 0 || y <= 0)
        {
            point = default;
            return false;
        }

        var position = e.GetPosition(this);
        if (!clampToBounds && (position.X < 0 || position.Y < 0 || position.X > x || position.Y > y))
        {
            point = default;
            return false;
        }

        point = new Point(
            Math.Clamp(position.X, 0, x),
            Math.Clamp(position.Y, 0, y));

        return true;
    }

    private void EraseAt(Point point)
    {
        if (ViewModel is null)
        {
            return;
        }

        for (var i = ViewModel.Items.Count - 1; i >= 0; i--)
        {
            var item = ViewModel.Items[i];
            if (IntersectsItem(item, point))
            {
                ViewModel.Items.RemoveAt(i);
                return;
            }
        }
    }

    private static bool IsInsideSelectionBounds(DrawingItem item, Point worldPoint)
    {
        var canonical = item.CanonicalBounds;
        if (canonical.IsEmpty)
        {
            return false;
        }

        var inverse = item.Transform.Matrix;
        if (!inverse.HasInverse)
        {
            return false;
        }
        inverse.Invert();
        var localPoint = inverse.Transform(worldPoint);
        return canonical.Contains(localPoint);
    }

    private CornerKind GetCornerHit(DrawingItem item, Point worldPoint)
    {
        var canonical = item.CanonicalBounds;
        if (canonical.IsEmpty || canonical.Width < 0.5d || canonical.Height < 0.5d)
        {
            return CornerKind.None;
        }

        var m = item.Transform.Matrix;
        var tl = m.Transform(new Point(canonical.Left, canonical.Top));
        var tr = m.Transform(new Point(canonical.Right, canonical.Top));
        var bl = m.Transform(new Point(canonical.Left, canonical.Bottom));
        var br = m.Transform(new Point(canonical.Right, canonical.Bottom));

        var scale = Math.Max(0.0001d, ContentScale);
        var pickup = 8.0d / scale;
        var pickupSquared = pickup * pickup;

        if (DistanceSquared(worldPoint, tl) <= pickupSquared) return CornerKind.TopLeft;
        if (DistanceSquared(worldPoint, tr) <= pickupSquared) return CornerKind.TopRight;
        if (DistanceSquared(worldPoint, bl) <= pickupSquared) return CornerKind.BottomLeft;
        if (DistanceSquared(worldPoint, br) <= pickupSquared) return CornerKind.BottomRight;

        return CornerKind.None;
    }

    private static (Point Anchor, Point Grabbed) GetAnchorAndGrabbed(Rect bounds, CornerKind corner) => corner switch
    {
        CornerKind.TopLeft => (new Point(bounds.Right, bounds.Bottom), new Point(bounds.Left, bounds.Top)),
        CornerKind.TopRight => (new Point(bounds.Left, bounds.Bottom), new Point(bounds.Right, bounds.Top)),
        CornerKind.BottomLeft => (new Point(bounds.Right, bounds.Top), new Point(bounds.Left, bounds.Bottom)),
        CornerKind.BottomRight => (new Point(bounds.Left, bounds.Top), new Point(bounds.Right, bounds.Bottom)),
        _ => (default, default),
    };

    private void UpdateHoverCursor(MouseEventArgs e)
    {
        if (!IsSelectActive || ViewModel?.SelectedItem is not { } selected)
        {
            Cursor = null;
            return;
        }

        var point = e.GetPosition(this);
        var corner = GetCornerHit(selected, point);
        if (corner == CornerKind.None)
        {
            Cursor = null;
            return;
        }

        // Use a unit diagonal based on which corner is grabbed (independent of the item's
        // aspect ratio), then rotate it by the item's rotation so cursors stay diagonal on
        // corners regardless of width/height — only the rotation changes the cursor bucket.
        var unitDiagonal = corner switch
        {
            CornerKind.TopLeft => new Vector(-1, -1),
            CornerKind.TopRight => new Vector(1, -1),
            CornerKind.BottomLeft => new Vector(-1, 1),
            CornerKind.BottomRight => new Vector(1, 1),
            _ => new Vector(1, 1),
        };

        var m = selected.Transform.Matrix;
        var rotation = Math.Atan2(m.M12, m.M11);
        var cos = Math.Cos(rotation);
        var sin = Math.Sin(rotation);
        var rotated = new Vector(
            unitDiagonal.X * cos - unitDiagonal.Y * sin,
            unitDiagonal.X * sin + unitDiagonal.Y * cos);

        Cursor = CursorForDiagonal(rotated);
    }

    private static Cursor CursorForDiagonal(Vector diagonal)
    {
        if (Math.Abs(diagonal.X) < double.Epsilon && Math.Abs(diagonal.Y) < double.Epsilon)
        {
            return Cursors.SizeNWSE;
        }

        var angle = Math.Atan2(diagonal.Y, diagonal.X);
        if (angle < 0) angle += Math.PI; // direction is bidirectional
        var deg = angle * 180.0 / Math.PI;

        // 4 buckets across [0, 180):
        //   [  0,  22.5) and [157.5, 180) → horizontal (WE)
        //   [ 22.5,  67.5) → top-left to bottom-right (NWSE)
        //   [ 67.5, 112.5) → vertical (NS)
        //   [112.5, 157.5) → top-right to bottom-left (NESW)
        if (deg < 22.5 || deg >= 157.5) return Cursors.SizeWE;
        if (deg < 67.5) return Cursors.SizeNWSE;
        if (deg < 112.5) return Cursors.SizeNS;
        return Cursors.SizeNESW;
    }

    private static bool IntersectsItem(DrawingItem item, Point worldPoint)
    {
        var inverse = item.Transform.Matrix;
        if (!inverse.HasInverse)
        {
            return false;
        }
        inverse.Invert();
        var localPoint = inverse.Transform(worldPoint);

        return item.HitTest(localPoint);
    }

    private static double DistanceSquared(Point a, Point b)
    {
        var dx = a.X - b.X;
        var dy = a.Y - b.Y;
        return (dx * dx) + (dy * dy);
    }

    // ---------- Text editing ----------

    private DrawingViewModel? _subscribedViewModel;
    private TextItem? _editingItem;
    private bool _suppressEditTextSync;

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_subscribedViewModel is not null)
        {
            _subscribedViewModel.PropertyChanged -= OnViewModelPropertyChanged;
            _subscribedViewModel = null;
        }

        if (e.NewValue is DrawingViewModel vm)
        {
            _subscribedViewModel = vm;
            vm.PropertyChanged += OnViewModelPropertyChanged;
        }

        ApplyEditingTextItem(ViewModel?.EditingTextItem);
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(DrawingViewModel.EditingTextItem))
        {
            ApplyEditingTextItem(ViewModel?.EditingTextItem);
        }
    }

    private void ApplyEditingTextItem(TextItem? item)
    {
        if (ReferenceEquals(_editingItem, item))
        {
            return;
        }

        if (_editingItem is not null)
        {
            _editingItem.PropertyChanged -= OnEditingItemPropertyChanged;
            _editingItem.Transform.Changed -= OnEditingItemTransformChanged;
        }

        _editingItem = item;

        if (item is null)
        {
            EditTextBox.Visibility = Visibility.Collapsed;
            EditTextBox.RenderTransform = Transform.Identity;
            _suppressEditTextSync = true;
            EditTextBox.Text = string.Empty;
            _suppressEditTextSync = false;
            return;
        }

        item.PropertyChanged += OnEditingItemPropertyChanged;
        item.Transform.Changed += OnEditingItemTransformChanged;

        ConfigureEditTextBoxForItem(item);

        _suppressEditTextSync = true;
        EditTextBox.Text = item.Text;
        _suppressEditTextSync = false;

        EditTextBox.Visibility = Visibility.Visible;
        PositionEditTextBox(item);

        Dispatcher.BeginInvoke(new Action(() =>
        {
            if (!ReferenceEquals(_editingItem, item))
            {
                return;
            }

            Keyboard.Focus(EditTextBox);
            EditTextBox.Focus();
            EditTextBox.CaretIndex = EditTextBox.Text.Length;
            EditTextBox.SelectAll();
        }), System.Windows.Threading.DispatcherPriority.Input);
    }

    private void ConfigureEditTextBoxForItem(TextItem item)
    {
        EditTextBox.FontFamily = new FontFamily(item.FontFamily);
        EditTextBox.FontSize = item.FontSize;
        // Keep the textbox content invisible while editing so the live-rendered geometry
        // (with fill AND outline) shows through; the caret remains visible via CaretBrush.
        EditTextBox.Foreground = Brushes.Transparent;
        EditTextBox.CaretBrush = item.Fill ?? item.Outline ?? Brushes.Black;
    }

    private void PositionEditTextBox(TextItem item)
    {
        var matrix = item.Transform.Matrix;

        // Align the textbox's first-line baseline with the geometry's baseline.
        // Geometry baseline in canonical Y = item.Origin.Y + FormattedText.Baseline.
        // TextBox first-line baseline in textbox-local Y = MeasureFirstLineBaseline(...).
        // Canvas position is in DrawingView coords; the item's linear (rotation/scale) part
        // is applied as RenderTransform around the textbox's top-left.
        var formattedBaseline = GetFormattedTextBaseline(item.FontFamily, item.FontSize);
        var textBoxBaseline = MeasureFirstLineBaseline(item.FontFamily, item.FontSize);
        var baselineDelta = formattedBaseline - textBoxBaseline;

        var worldOrigin = matrix.Transform(item.Origin);
        var offsetX = matrix.M21 * baselineDelta;
        var offsetY = matrix.M22 * baselineDelta;

        Canvas.SetLeft(EditTextBox, worldOrigin.X + offsetX);
        Canvas.SetTop(EditTextBox, worldOrigin.Y + offsetY);

        // Give the textbox a sensible minimum size so the caret stays visible when empty.
        EditTextBox.MinWidth = Math.Max(item.CanonicalBounds.Width, item.FontSize * 0.5);
        EditTextBox.MinHeight = Math.Max(item.CanonicalBounds.Height, item.FontSize * 1.2);

        var rotationScale = new Matrix(matrix.M11, matrix.M12, matrix.M21, matrix.M22, 0, 0);
        EditTextBox.RenderTransformOrigin = new Point(0, 0);
        EditTextBox.RenderTransform = rotationScale.IsIdentity
            ? Transform.Identity
            : new MatrixTransform(rotationScale);
    }

    private static double GetFormattedTextBaseline(string fontFamilyName, double fontSize)
    {
        var typeface = new Typeface(
            new FontFamily(fontFamilyName),
            FontStyles.Normal,
            FontWeights.Normal,
            FontStretches.Normal);
        var formatted = new FormattedText(
            "Hg",
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            typeface,
            fontSize,
            Brushes.Black,
            1.0);
        return formatted.Baseline;
    }

    private static double MeasureFirstLineBaseline(string fontFamilyName, double fontSize)
    {
        var probe = new TextBlock
        {
            Text = "Hg",
            FontFamily = new FontFamily(fontFamilyName),
            FontSize = fontSize,
        };
        probe.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        return probe.BaselineOffset;
    }

    private void OnEditingItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_editingItem is null)
        {
            return;
        }

        if (e.PropertyName is nameof(TextItem.Text)
            or nameof(TextItem.FontFamily)
            or nameof(TextItem.FontSize)
            or nameof(TextItem.Fill)
            or nameof(TextItem.Outline)
            or nameof(TextItem.OutlineThickness)
            or nameof(TextItem.CanonicalBounds)
            or nameof(DrawingItem.Geometry))
        {
            ConfigureEditTextBoxForItem(_editingItem);
            PositionEditTextBox(_editingItem);
        }
    }

    private void OnEditingItemTransformChanged(object? sender, EventArgs e)
    {
        if (_editingItem is not null)
        {
            PositionEditTextBox(_editingItem);
        }
    }

    private void OnEditTextBoxTextChanged(object sender, TextChangedEventArgs e)
    {
        if (_suppressEditTextSync || _editingItem is null)
        {
            return;
        }

        _editingItem.SetText(EditTextBox.Text);
    }

    private void OnEditTextBoxLostFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        // Keep the editor alive when focus moves into the tools panel or any of its popups,
        // so the user can change font/size/fill/outline of the editing text without losing it.
        if (e.NewFocus is DependencyObject focusTarget && IsWithinImageEditTools(focusTarget))
        {
            return;
        }

        ViewModel?.EndEditText(commit: true);
    }

    private static bool IsWithinImageEditTools(DependencyObject element)
    {
        DependencyObject? current = element;
        while (current is not null)
        {
            if (current is ImageEditToolsView)
            {
                return true;
            }

            // Walk up the logical tree first (handles popup content), then fall back to visual tree.
            var next = LogicalTreeHelper.GetParent(current);
            if (next is null && current is Visual visual)
            {
                next = VisualTreeHelper.GetParent(visual);
            }
            current = next;
        }
        return false;
    }

    private void OnEditTextBoxPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            ViewModel?.EndEditText(commit: true);
            e.Handled = true;
        }
        else if (e.Key == Key.Enter && (Keyboard.Modifiers & ModifierKeys.Shift) != ModifierKeys.Shift)
        {
            ViewModel?.EndEditText(commit: true);
            e.Handled = true;
        }
    }

    private static void OnSelectedTextStyleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not DrawingView view || view.ViewModel?.SelectedItem is not TextItem text)
        {
            return;
        }

        if (e.Property == TextFontFamilyProperty && e.NewValue is string family)
        {
            text.SetFontFamily(family);
        }
        else if (e.Property == TextFontSizeProperty && e.NewValue is double size)
        {
            text.SetFontSize(size);
        }
        else if (e.Property == TextFillBrushProperty)
        {
            text.SetFill(e.NewValue as Brush);
        }
        else if (e.Property == TextOutlineBrushProperty)
        {
            text.SetOutline(e.NewValue as Brush, view.TextOutlineThickness);
        }
        else if (e.Property == TextOutlineThicknessProperty && e.NewValue is double thickness)
        {
            text.SetOutline(view.TextOutlineBrush, thickness);
        }
    }
}
