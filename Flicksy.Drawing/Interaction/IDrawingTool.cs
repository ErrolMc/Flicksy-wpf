using System.Windows;
using System.Windows.Input;

namespace Flicksy.Drawing.Interaction;

/// <summary>
/// A self-contained pointer-driven interaction (select / drag, pen stroke, shape drag,
/// erase, text create...). Tools own their gesture state and depend only on
/// <see cref="IDrawingSurface"/> + their config/view-model — never on the WPF host control.
/// </summary>
public interface IDrawingTool
{
    /// <summary>
    /// <c>true</c> while a gesture is in progress (between <see cref="OnPointerDown"/> and
    /// <see cref="OnPointerUp"/>). The router uses this to keep dispatching move/up events to
    /// the tool that started the gesture even if the active-tool selector has changed.
    /// </summary>
    bool IsActive { get; }

    /// <summary>
    /// Called when the user presses the primary pointer button. Return <c>true</c> if the
    /// tool consumed the event.
    /// </summary>
    bool OnPointerDown(Point point, MouseButtonEventArgs e);

    /// <summary>
    /// Called while the primary pointer button is held down.
    /// </summary>
    void OnPointerMove(Point point, MouseEventArgs e);

    /// <summary>
    /// Called when the user releases the primary pointer button.
    /// </summary>
    void OnPointerUp(Point point, MouseButtonEventArgs e);

    /// <summary>
    /// Called for pointer movement while no button is pressed. Tools that adjust the cursor
    /// on hover (e.g. corner handles) implement this; others may no-op.
    /// </summary>
    void OnPointerHover(Point point, MouseEventArgs e);
}
