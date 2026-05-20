using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Flicksy.Editor.Controllers;
using Flicksy.Editor.Source;
using Flicksy.Editor.ViewModels;

namespace Flicksy.Editor.Controls;

public partial class DrawingView : UserControl
{
    private readonly TextEditingController _textEditing;

    public DrawingView()
    {
        InitializeComponent();
        _textEditing = new TextEditingController(EditTextBox, () => ViewModel);
        DataContextChanged += OnDataContextChanged;
    }

    public DrawingViewModel? ViewModel => DataContext as DrawingViewModel;

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        _textEditing.OnHostDataContextChanged(e.OldValue as DrawingViewModel, e.NewValue as DrawingViewModel);
    }

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
            HandleSelectMouseDown(point);
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
            if (HitTestItem(point) is TextItem existing)
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

    private void HandleSelectMouseDown(Point point)
    {
        if (ViewModel is null)
        {
            return;
        }

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
}
