using System;
using System.Buffers;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Flicksy.Drawing.Media;
using Flicksy.VideoEditor.Project;
using SkiaSharp;

namespace Flicksy.VideoEditor.Composition;

/// <summary>
/// <see cref="ICompositor"/> backed by SkiaSharp's CPU surface. Wraps each call's target
/// <see cref="WriteableBitmap"/>'s back buffer via <see cref="SKBitmap.InstallPixels(SKImageInfo, IntPtr, int)"/>
/// so paints land directly in WPF-bindable memory — no intermediate Skia surface, no
/// extra copy. <see cref="CompositionPlanner.PlanFrame"/> supplies the layer list; this
/// class only owns paint dispatch, the decoder cache, and the audio mix.
/// <para>
/// Threading: per <see cref="ICompositor"/>, calls are single-call-in-flight on one
/// thread at a time. The class is not thread-safe across concurrent callers.
/// <see cref="RenderFrame"/> may run off the UI thread for media-only projects;
/// projects with <see cref="GraphicsClip"/>s currently require the UI thread because
/// <see cref="RenderTargetBitmap"/> needs a Dispatcher. Step 11 will revisit this if
/// off-UI-thread playback demands it.
/// </para>
/// </summary>
public sealed class SkiaCompositor : ICompositor
{
    private readonly Dictionary<Guid, IMediaDecoder> _decoders = new();
    private bool _disposed;

    public CompositedFrame RenderFrame(Project.Project project, int frame)
    {
        ArgumentNullException.ThrowIfNull(project);
        if (_disposed) throw new ObjectDisposedException(nameof(SkiaCompositor));

        int width = project.Settings.ResolutionWidth;
        int height = project.Settings.ResolutionHeight;
        int sampleRate = project.Settings.AudioSampleRate;

        // Pbgra32 + SKAlphaType.Premul: WPF's only fully blendable format pair. Allocating
        // fresh per call so the result can be Freeze()-d and crossed back to any caller
        // without aliasing the next frame. Future perf work (#11) can introduce pooling
        // via Clone()-into-unfrozen if 8 MB/frame at 1080p shows up in measurements.
        var bitmap = new WriteableBitmap(width, height, 96, 96, PixelFormats.Pbgra32, null);

        bitmap.Lock();
        try
        {
            var info = new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul);
            using var skBitmap = new SKBitmap();
            skBitmap.InstallPixels(info, bitmap.BackBuffer, bitmap.BackBufferStride);
            using var canvas = new SKCanvas(skBitmap);

            canvas.Clear(SKColors.Black);

            var layers = CompositionPlanner.PlanFrame(project, frame);
            foreach (var layer in layers)
            {
                // Audio-only layers don't contribute to the visual frame.
                if (layer.Track.Kind == TrackKind.Audio) continue;
                PaintLayer(canvas, layer, width, height, sampleRate);
            }
        }
        finally
        {
            bitmap.AddDirtyRect(new Int32Rect(0, 0, width, height));
            bitmap.Unlock();
        }

        bitmap.Freeze();
        return new CompositedFrame(bitmap);
    }

    public AudioBuffer RenderAudio(Project.Project project, int frame)
    {
        ArgumentNullException.ThrowIfNull(project);
        if (_disposed) throw new ObjectDisposedException(nameof(SkiaCompositor));

        int sampleRate = project.Settings.AudioSampleRate;
        int framerate = project.Settings.Framerate;
        int stereoFrames = framerate > 0 ? sampleRate / framerate : 0;
        var output = new float[stereoFrames * 2];

        if (stereoFrames == 0) return new AudioBuffer(output, sampleRate);

        var layers = CompositionPlanner.PlanFrame(project, frame);
        // Scratch buffer reused across all audio-eligible layers — each clip's samples
        // get scaled by Volume and accumulated into `output`.
        var scratch = new float[stereoFrames * 2];

        foreach (var layer in layers)
        {
            if (!IsAudibleLayer(layer)) continue;
            var mediaClip = (MediaClip)layer.Clip;

            var decoder = TryGetOrCreateDecoder(mediaClip, sampleRate);
            if (decoder is null || !decoder.HasAudio) continue;

            decoder.GetAudioSamplesAt(layer.SourceTime, scratch);

            float volume = (float)mediaClip.Volume;
            for (int i = 0; i < scratch.Length; i++)
            {
                output[i] += scratch[i] * volume;
            }
        }

        return new AudioBuffer(output, sampleRate);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        foreach (var decoder in _decoders.Values)
        {
            try { decoder.Dispose(); } catch { /* swallow — best-effort cleanup */ }
        }
        _decoders.Clear();
    }

    // ---- Paint dispatch -----------------------------------------------------

    private void PaintLayer(SKCanvas canvas, CompositionLayer layer, int projectWidth, int projectHeight, int sampleRate)
    {
        switch (layer.Clip)
        {
            case MediaClip mediaClip when mediaClip.Streams != ClipStreams.Audio:
                PaintMediaClip(canvas, layer, mediaClip, projectWidth, projectHeight, sampleRate);
                break;
            case GraphicsClip graphicsClip:
                PaintGraphicsClip(canvas, graphicsClip, projectWidth, projectHeight);
                break;
            // Audio-only MediaClips and unknown subtypes: no visual output.
        }
    }

    private void PaintMediaClip(SKCanvas canvas, CompositionLayer layer, MediaClip clip, int projectWidth, int projectHeight, int sampleRate)
    {
        if (clip.IsBroken) return;

        var decoder = TryGetOrCreateDecoder(clip, sampleRate);
        if (decoder is null || !decoder.HasVideo) return;

        var maybeFrame = decoder.GetVideoFrameAt(layer.SourceTime);
        if (maybeFrame is null) return;

        var videoFrame = maybeFrame.Value;
        try
        {
            // Pin the rented byte[] so Skia can read it directly. SKImage.FromPixels does
            // not copy — the memory must stay valid for the lifetime of the image.
            var handle = GCHandle.Alloc(videoFrame.Buffer, GCHandleType.Pinned);
            try
            {
                var srcInfo = new SKImageInfo(videoFrame.Width, videoFrame.Height, SKColorType.Bgra8888, SKAlphaType.Opaque);
                using var image = SKImage.FromPixels(srcInfo, handle.AddrOfPinnedObject(), videoFrame.Stride);

                var (matrix, srcRect) = BuildLayerMatrix(clip.Transform, videoFrame.Width, videoFrame.Height, projectWidth, projectHeight);

                canvas.Save();
                canvas.SetMatrix(matrix);
                if (srcRect is { } crop)
                {
                    canvas.ClipRect(crop);
                }
                canvas.DrawImage(image, 0, 0);
                canvas.Restore();
            }
            finally
            {
                handle.Free();
            }
        }
        finally
        {
            // VideoFrame.Buffer is rented from ArrayPool — return it.
            ArrayPool<byte>.Shared.Return(videoFrame.Buffer);
        }
    }

    private void PaintGraphicsClip(SKCanvas canvas, GraphicsClip clip, int projectWidth, int projectHeight)
    {
        if (clip.Items.Count == 0) return;

        // GraphicsClip items render through WPF's DrawingContext. We bounce through a
        // project-resolution RenderTargetBitmap, copy the pixels out, and hand them to
        // Skia. This is allocation-heavy (one full RTB + one byte[] per graphics layer
        // per frame); a future Skia-native render path would eliminate it. The bigger
        // structural constraint: RenderTargetBitmap.Render needs a Dispatcher, so this
        // path only works on the UI thread. The preview wiring (step 7) does call from
        // the UI thread; off-thread playback (step 11) may need to address this.
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            foreach (var item in clip.Items)
            {
                item.Render(dc);
            }
        }

        var rtb = new RenderTargetBitmap(projectWidth, projectHeight, 96, 96, PixelFormats.Pbgra32);
        rtb.Render(visual);

        int stride = projectWidth * 4;
        var pixels = new byte[stride * projectHeight];
        rtb.CopyPixels(pixels, stride, 0);

        var handle = GCHandle.Alloc(pixels, GCHandleType.Pinned);
        try
        {
            var srcInfo = new SKImageInfo(projectWidth, projectHeight, SKColorType.Bgra8888, SKAlphaType.Premul);
            using var image = SKImage.FromPixels(srcInfo, handle.AddrOfPinnedObject(), stride);

            // Graphics clips draw in project-resolution space already, so the layer
            // matrix maps from (projectWidth × projectHeight) source to the same project
            // frame. Default Transform2D yields the identity matrix.
            var (matrix, _) = BuildLayerMatrix(clip.Transform, projectWidth, projectHeight, projectWidth, projectHeight);

            canvas.Save();
            canvas.SetMatrix(matrix);
            canvas.DrawImage(image, 0, 0);
            canvas.Restore();
        }
        finally
        {
            handle.Free();
        }
    }

    // ---- Decoder cache ------------------------------------------------------

    private IMediaDecoder? TryGetOrCreateDecoder(MediaClip clip, int sampleRate)
    {
        if (_decoders.TryGetValue(clip.Id, out var existing)) return existing;

        var path = clip.Source?.SourcePath;
        if (string.IsNullOrEmpty(path)) return null;

        try
        {
            var decoder = new FFmpegMediaDecoder(path, sampleRate);
            _decoders[clip.Id] = decoder;
            return decoder;
        }
        catch
        {
            // Probe failure — render nothing for this clip. Logging hook lands with the
            // diagnostics work; silent for now matches the rest of the pipeline (broken
            // clip already reds-out in the timeline).
            return null;
        }
    }

    // ---- Matrix + helpers ---------------------------------------------------

    /// <summary>
    /// Build the source→project matrix for one layer. Transform2D semantics:
    /// <list type="bullet">
    ///   <item><c>Position</c> = clip-center offset from project-frame center, in project pixels.</item>
    ///   <item><c>Scale</c> = per-axis scaling of source pixels (1,1 = pixel-for-pixel).</item>
    ///   <item><c>RotationDegrees</c> = clockwise rotation around the clip's center.</item>
    ///   <item><c>CropRect</c> (optional) = source-space rect of the visible region. When set,
    ///         the clip's center becomes the crop's center and the painter clips drawing
    ///         to the crop rect in source space.</item>
    /// </list>
    /// Composition: <c>M = T_clipCenter * R * S * T_-sourceCenter</c>.
    /// </summary>
    private static (SKMatrix Matrix, SKRect? SrcClipRect) BuildLayerMatrix(
        Transform2D transform, int sourceWidth, int sourceHeight, int projectWidth, int projectHeight)
    {
        float sourceCenterX, sourceCenterY;
        SKRect? srcClip = null;

        if (transform.CropRect is { } crop)
        {
            sourceCenterX = (float)(crop.X + crop.Width * 0.5);
            sourceCenterY = (float)(crop.Y + crop.Height * 0.5);
            srcClip = new SKRect(
                (float)crop.X,
                (float)crop.Y,
                (float)(crop.X + crop.Width),
                (float)(crop.Y + crop.Height));
        }
        else
        {
            sourceCenterX = sourceWidth * 0.5f;
            sourceCenterY = sourceHeight * 0.5f;
        }

        float clipCenterX = projectWidth * 0.5f + (float)transform.Position.X;
        float clipCenterY = projectHeight * 0.5f + (float)transform.Position.Y;

        // M = T_clipCenter * R * S * T_-sourceCenter, computed via SKMatrix.Concat which
        // returns first * second. Read bottom-up: T_-sourceCenter applies first, T_clipCenter last.
        var m = SKMatrix.CreateTranslation(-sourceCenterX, -sourceCenterY);
        m = SKMatrix.Concat(SKMatrix.CreateScale((float)transform.Scale.X, (float)transform.Scale.Y), m);
        m = SKMatrix.Concat(SKMatrix.CreateRotationDegrees((float)transform.RotationDegrees), m);
        m = SKMatrix.Concat(SKMatrix.CreateTranslation(clipCenterX, clipCenterY), m);

        return (m, srcClip);
    }

    private static bool IsAudibleLayer(CompositionLayer layer)
    {
        if (layer.Track.Kind == TrackKind.Overlay) return false;
        if (layer.Track.Muted) return false;
        if (layer.Clip is not MediaClip mediaClip) return false;
        if (mediaClip.Streams != ClipStreams.Audio && mediaClip.Streams != ClipStreams.Both) return false;
        if (mediaClip.IsBroken) return false;
        return true;
    }
}
