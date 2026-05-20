using System;
using System.Windows;
using System.Windows.Input;
using Flicksy.Editor.Helpers;
using Flicksy.Editor.ViewModels;

namespace Flicksy.Editor.Interaction.Tools;

/// <summary>
/// Click / drag erase gesture: deletes the topmost item under the pointer on down, then
/// continues to delete items as the pointer drags across them. Depends only on
/// <see cref="IDrawingSurface"/> + <see cref="DrawingViewModel"/>.
/// </summary>
public sealed class EraseTool : IDrawingTool
{
    private readonly IDrawingSurface _surface;
    private readonly DrawingViewModel _viewModel;

    private bool _erasing;

    public EraseTool(IDrawingSurface surface, DrawingViewModel viewModel)
    {
        _surface = surface ?? throw new ArgumentNullException(nameof(surface));
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
    }

    public bool IsActive => _erasing;

    public bool OnPointerDown(Point point, MouseButtonEventArgs e)
    {
        EraseAt(point);
        _erasing = true;
        _surface.CapturePointer();
        return true;
    }

    public void OnPointerMove(Point point, MouseEventArgs e)
    {
        if (_surface.TryGetCanvasPoint(e, clampToBounds: true, out var clamped))
        {
            EraseAt(clamped);
        }
    }

    public void OnPointerUp(Point point, MouseButtonEventArgs e)
    {
        if (_surface.TryGetCanvasPoint(e, clampToBounds: true, out var clamped))
        {
            EraseAt(clamped);
        }

        _surface.ReleasePointer();
        _erasing = false;
    }

    public void OnPointerHover(Point point, MouseEventArgs e)
    {
        // Erase tool has no hover affordance.
    }

    private void EraseAt(Point point)
    {
        if (DrawingMath.HitTestTopmost(_viewModel.Items, point) is { } hit)
        {
            _viewModel.Items.Remove(hit);
        }
    }
}
