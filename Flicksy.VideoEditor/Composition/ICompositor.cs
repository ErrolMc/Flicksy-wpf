using System;
using System.Windows.Media.Imaging;

namespace Flicksy.VideoEditor.Composition;

/// <summary>
/// Produces a composited video frame and a mixed audio buffer for a
/// <c>(Project, frame)</c> input. Two independent calls so each caller composes what it
/// needs — playback wants audio in larger chunks than one frame's worth of samples,
/// scrubbing wants frames without audio, export wants both per frame.
/// <para>
/// Contract (per ADR 0004):
/// </para>
/// <list type="bullet">
///   <item>Synchronous and single-call-in-flight. The compositor does no internal
///         locking and must not be invoked concurrently from multiple threads.</item>
///   <item>Caller-owned output. <see cref="RenderFrame"/> paints into a
///         <see cref="WriteableBitmap"/> the caller supplies and reuses across frames, so
///         the compositor allocates no per-frame frame buffer. The bitmap is left
///         unfrozen, so the compositor and whoever presents it must share one thread (the
///         UI thread today). Cross-thread / decode-ahead playback is #11's job — it can
///         hand the compositor alternating buffers for ping-pong.</item>
///   <item>Decoder ownership is internal — the compositor maintains its own
///         <c>Clip.Id</c>-keyed cache of <c>IMediaDecoder</c>s. Callers see only the
///         pixels written into their bitmap and the audio buffers returned.</item>
/// </list>
/// </summary>
public interface ICompositor : IDisposable
{
    /// <summary>
    /// Paint one composited frame into <paramref name="target"/>, a caller-owned
    /// <see cref="WriteableBitmap"/> whose dimensions must equal the project resolution
    /// (<c>ProjectSettings.{ResolutionWidth, ResolutionHeight}</c>). The compositor
    /// <c>Lock</c>s the bitmap, blits the layer stack into its back buffer, marks it dirty,
    /// and <c>Unlock</c>s — the bound <c>Image</c> repaints in place, so the caller need
    /// not reassign its <c>Image.Source</c> between frames. Throws
    /// <see cref="System.ArgumentException"/> when <paramref name="target"/>'s size differs
    /// from the project resolution.
    /// </summary>
    void RenderFrame(Project.Project project, int frame, WriteableBitmap target);

    /// <summary>
    /// Render one video-frame's worth of mixed audio at the project's sample rate.
    /// <c>SampleRate / Framerate</c> stereo frames per call.
    /// </summary>
    AudioBuffer RenderAudio(Project.Project project, int frame);
}
