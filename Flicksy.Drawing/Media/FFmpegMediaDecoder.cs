using System;
using System.Buffers;
using System.IO;
using System.Threading;
using FFMediaToolkit.Decoding;
using FFMediaToolkit.Graphics;

namespace Flicksy.Drawing.Media;

/// <summary>
/// <see cref="IMediaDecoder"/> backed by FFMediaToolkit. One instance per
/// (clip, source) pair — see <see cref="IMediaDecoder"/> for cache-key rationale.
/// <para>
/// Video reads are synchronous seek-and-grab via <c>MediaFile.Video.GetFrame</c>, the same
/// path <see cref="FFmpegVideoPlayer"/> uses for inline-on-seek presentation. A
/// <see cref="Lock"/> serializes access because the compositor may invoke multiple
/// decoders concurrently and FFMediaToolkit's <c>MediaFile</c> is not thread-safe.
/// </para>
/// <para>
/// Audio decode is stubbed for this scaffolding slice: <see cref="GetAudioSamplesAt"/>
/// zero-fills the destination buffer. Real decode + resample/remix lands when the
/// compositor's audio mix pass (step 6 / issue #10) wires up <c>RenderAudio</c>; the
/// interface contract and metadata plumbing are in place so the compositor can be built
/// against this primitive immediately.
/// </para>
/// </summary>
public sealed class FFmpegMediaDecoder : IMediaDecoder
{
    private readonly Lock _gate = new();
    private readonly int _targetSampleRate;

    private MediaFile? _file;
    private bool _disposed;

    public bool HasVideo { get; private set; }
    public bool HasAudio { get; private set; }
    public TimeSpan Duration { get; private set; }
    public int VideoWidth { get; private set; }
    public int VideoHeight { get; private set; }

    /// <summary>Source-side sample rate, read from the file header. Kept for the audio resampler.</summary>
    private int _sourceSampleRate;
    /// <summary>Source-side channel count, read from the file header. Kept for the audio remixer.</summary>
    private int _sourceChannelCount;

    public FFmpegMediaDecoder(string path, int targetSampleRate)
    {
        if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Path is required.", nameof(path));
        if (targetSampleRate <= 0) throw new ArgumentOutOfRangeException(nameof(targetSampleRate));

        _targetSampleRate = targetSampleRate;

        var options = new MediaOptions
        {
            StreamsToLoad = MediaMode.AudioVideo,
            VideoPixelFormat = ImagePixelFormat.Bgra32,
        };
        _file = MediaFile.Open(Path.GetFullPath(path), options);

        HasVideo = _file.HasVideo;
        HasAudio = _file.HasAudio;

        var duration = TimeSpan.Zero;
        if (_file.HasVideo)
        {
            var info = _file.Video.Info;
            VideoWidth = info.FrameSize.Width;
            VideoHeight = info.FrameSize.Height;
            if (info.Duration > duration) duration = info.Duration;
        }
        if (_file.HasAudio)
        {
            var info = _file.Audio.Info;
            _sourceSampleRate = info.SampleRate;
            _sourceChannelCount = info.NumChannels;
            if (info.Duration > duration) duration = info.Duration;
        }
        Duration = duration;
    }

    public VideoFrame? GetVideoFrameAt(TimeSpan time)
    {
        if (_disposed || !HasVideo) return null;
        if (time < TimeSpan.Zero) time = TimeSpan.Zero;
        if (Duration > TimeSpan.Zero && time > Duration) return null;

        lock (_gate)
        {
            if (_file is null) return null;

            try
            {
                var image = _file.Video.GetFrame(time);
                var data = image.Data;
                var len = data.Length;
                var buf = ArrayPool<byte>.Shared.Rent(len);
                data.CopyTo(buf);
                return new VideoFrame(
                    buf, len,
                    image.ImageSize.Width, image.ImageSize.Height,
                    image.Stride,
                    time);
            }
            catch
            {
                // Decode failures are silent — the compositor renders black for the layer
                // and the caller treats the result as "no frame at t".
                return null;
            }
        }
    }

    public void GetAudioSamplesAt(TimeSpan time, Span<float> destination)
    {
        // Always start zeroed — every short-circuit path below leaves silence in place.
        destination.Clear();

        if (destination.Length == 0) return;
        if (_disposed || !HasAudio) return;
        if (time >= Duration) return;

        // TODO(#10 step 6): decode source packets via _file.Audio.GetFrame(time), then
        // remix N-channel → stereo and resample from _sourceSampleRate → _targetSampleRate,
        // writing into `destination` as interleaved [L0, R0, L1, R1, ...]. Stubbed silent
        // for the scaffolding slice; the interface contract + source metadata is captured
        // so the compositor's RenderAudio path can be built against this primitive and the
        // real decode wired in when the audio mix pass lands.
        _ = _sourceSampleRate;
        _ = _sourceChannelCount;
        _ = _targetSampleRate;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        lock (_gate)
        {
            try { _file?.Dispose(); } catch { /* ignore */ }
            _file = null;
        }
    }
}
