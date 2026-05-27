using System;

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
///   <item>Callable from any single thread. The <see cref="CompositedFrame.Image"/>
///         the compositor returns is <see cref="System.Windows.Freezable.Freeze">frozen</see>,
///         so the consumer (typically the UI thread) can read it without dispatcher
///         marshalling even if the compositor was driven from a worker thread.</item>
///   <item>Decoder ownership is internal — the compositor maintains its own
///         <c>Clip.Id</c>-keyed cache of <c>IMediaDecoder</c>s. Callers see only frames
///         and audio buffers.</item>
/// </list>
/// </summary>
public interface ICompositor : IDisposable
{
    /// <summary>
    /// Render one composited frame at the project's resolution. Returns a frozen
    /// <see cref="CompositedFrame"/> safe to hand to <c>Image.Source</c> from any thread.
    /// </summary>
    CompositedFrame RenderFrame(Project.Project project, int frame);

    /// <summary>
    /// Render one video-frame's worth of mixed audio at the project's sample rate.
    /// <c>SampleRate / Framerate</c> stereo frames per call.
    /// </summary>
    AudioBuffer RenderAudio(Project.Project project, int frame);
}
