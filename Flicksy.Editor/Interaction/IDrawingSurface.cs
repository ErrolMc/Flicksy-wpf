using System.Windows;
using System.Windows.Input;

namespace Flicksy.Editor.Interaction;

/// <summary>
/// Host surface a drawing tool interacts with. Abstracts the WPF control implementation so
/// gesture controllers (selection, pen, shape, text...) can be hosted by any UI that exposes
/// pointer capture, dimensions, content scale, cursor, and pointer-to-canvas mapping.
/// </summary>
public interface IDrawingSurface
{
    double ActualWidth { get; }

    double ActualHeight { get; }

    /// <summary>
    /// The current zoom factor of the host (1.0 = no zoom). Used by tools to keep pickup
    /// regions a constant screen-pixel size regardless of canvas scale.
    /// </summary>
    double ContentScale { get; }

    /// <summary>
    /// Cursor displayed by the surface. Setting <c>null</c> reverts to the default.
    /// </summary>
    Cursor? Cursor { get; set; }

    /// <summary>
    /// Capture pointer input so the active tool keeps receiving move/up events even when the
    /// pointer leaves the surface.
    /// </summary>
    void CapturePointer();

    /// <summary>
    /// Release a previously captured pointer.
    /// </summary>
    void ReleasePointer();

    /// <summary>
    /// Maps a raw <see cref="MouseEventArgs"/> position to a canvas-space point.
    /// </summary>
    /// <param name="clampToBounds">
    /// When <c>true</c>, the returned point is clamped inside <see cref="ActualWidth"/> /
    /// <see cref="ActualHeight"/>. When <c>false</c>, the method returns <c>false</c> if the
    /// pointer is outside the surface.
    /// </param>
    bool TryGetCanvasPoint(MouseEventArgs e, bool clampToBounds, out Point point);
}
