using System.Windows.Media.Imaging;

namespace Flicksy.VideoEditor.Composition;

/// <summary>
/// Output of <c>ICompositor.RenderFrame</c>: one composited image at the project's
/// resolution, wrapping a <see cref="WriteableBitmap"/> the compositor has already
/// <see cref="System.Windows.Freezable.Freeze">frozen</see>. Freezing is what makes the
/// result cross-thread safe — the compositor may run off the UI thread (single-call-in-
/// flight per ADR 0004) and the preview presents the bitmap on the UI thread without
/// dispatcher marshalling.
/// </summary>
/// <param name="Image">Frozen <see cref="WriteableBitmap"/> at
/// <c>ProjectSettings.{ResolutionWidth, ResolutionHeight}</c>.</param>
public sealed record CompositedFrame(WriteableBitmap Image)
{
    public int Width => Image.PixelWidth;
    public int Height => Image.PixelHeight;
}
