using System;
using System.Threading;
using System.Threading.Tasks;

namespace Flicksy.Editor.Media;

public interface IVideoPlayer : IDisposable
{
    PlaybackState State { get; }
    TimeSpan Position { get; }
    TimeSpan Duration { get; }
    int FrameWidth { get; }
    int FrameHeight { get; }

    event EventHandler<VideoFrame>? FrameReady;
    event EventHandler? PositionChanged;
    event EventHandler? StateChanged;
    event EventHandler? MediaEnded;

    Task OpenAsync(string path, CancellationToken cancellationToken = default);
    void Play();
    void Pause();
    void Seek(TimeSpan position);
    void Close();
}
