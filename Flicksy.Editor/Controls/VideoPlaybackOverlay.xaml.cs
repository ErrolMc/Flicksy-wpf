using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace Flicksy.Editor.Controls;

public partial class VideoPlaybackOverlay : UserControl
{
    private bool _isScrubbing;
    private bool _isInternalUpdate;
    private readonly DispatcherTimer _playbackTimer;
    private MediaElement? _mediaElement;

    public VideoPlaybackOverlay()
    {
        InitializeComponent();

        _playbackTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(200)
        };
        _playbackTimer.Tick += OnPlaybackTimerTick;

        UpdateButtonText();
        UpdateTimeText();
    }

    public static readonly DependencyProperty IsPlayingProperty =
        DependencyProperty.Register(nameof(IsPlaying), typeof(bool), typeof(VideoPlaybackOverlay),
            new PropertyMetadata(false, OnIsPlayingChanged));

    public static readonly DependencyProperty DurationSecondsProperty =
        DependencyProperty.Register(nameof(DurationSeconds), typeof(double), typeof(VideoPlaybackOverlay),
            new PropertyMetadata(0d, OnDurationChanged));

    public static readonly DependencyProperty CurrentSecondsProperty =
        DependencyProperty.Register(nameof(CurrentSeconds), typeof(double), typeof(VideoPlaybackOverlay),
            new PropertyMetadata(0d, OnCurrentChanged));

    public bool IsPlaying
    {
        get => (bool)GetValue(IsPlayingProperty);
        set => SetValue(IsPlayingProperty, value);
    }

    public double DurationSeconds
    {
        get => (double)GetValue(DurationSecondsProperty);
        set => SetValue(DurationSecondsProperty, value);
    }

    public double CurrentSeconds
    {
        get => (double)GetValue(CurrentSecondsProperty);
        set => SetValue(CurrentSecondsProperty, value);
    }

    public void AttachMediaElement(MediaElement? mediaElement)
    {
        if (ReferenceEquals(_mediaElement, mediaElement))
        {
            return;
        }

        if (_mediaElement is not null)
        {
            _mediaElement.MediaOpened -= OnMediaOpened;
            _mediaElement.MediaEnded -= OnMediaEnded;
        }

        _mediaElement = mediaElement;

        if (_mediaElement is not null)
        {
            _mediaElement.MediaOpened += OnMediaOpened;
            _mediaElement.MediaEnded += OnMediaEnded;
        }

        ResetState();
    }

    public void Play()
    {
        if (_mediaElement is null)
        {
            return;
        }

        if (_mediaElement.NaturalDuration.HasTimeSpan)
        {
            var duration = _mediaElement.NaturalDuration.TimeSpan;
            var durationSeconds = duration.TotalSeconds;
            var endThresholdSeconds = 0.1;
            var isAtEnd = durationSeconds > 0 &&
                          (_mediaElement.Position >= duration ||
                           CurrentSeconds >= durationSeconds - endThresholdSeconds);

            if (isAtEnd)
            {
                _mediaElement.Position = TimeSpan.Zero;
                CurrentSeconds = 0;
            }
        }

        _mediaElement.Play();
        IsPlaying = true;

        if (_mediaElement.NaturalDuration.HasTimeSpan)
        {
            _playbackTimer.Start();
        }
    }

    public void Pause()
    {
        if (_mediaElement is null)
        {
            return;
        }

        _mediaElement.Pause();
        _playbackTimer.Stop();
        IsPlaying = false;
    }

    public void Stop()
    {
        _playbackTimer.Stop();

        if (_mediaElement is not null)
        {
            _mediaElement.Stop();
        }

        ResetState();
    }

    public void ResetState()
    {
        _playbackTimer.Stop();
        IsPlaying = false;
        CurrentSeconds = 0;
        DurationSeconds = 0;
    }

    private static void OnIsPlayingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((VideoPlaybackOverlay)d).UpdateButtonText();
    }

    private static void OnDurationChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (VideoPlaybackOverlay)d;
        control._isInternalUpdate = true;
        control.TimelineSlider.Maximum = Math.Max(0, control.DurationSeconds);
        control._isInternalUpdate = false;
        control.UpdateTimeText();
    }

    private static void OnCurrentChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (VideoPlaybackOverlay)d;
        if (control._isScrubbing)
        {
            return;
        }

        control._isInternalUpdate = true;
        control.TimelineSlider.Value = Math.Clamp(control.CurrentSeconds, 0, Math.Max(0, control.DurationSeconds));
        control._isInternalUpdate = false;
        control.UpdateTimeText();
    }

    private void OnPlayPauseClick(object sender, RoutedEventArgs e)
    {
        if (IsPlaying)
        {
            Pause();
        }
        else
        {
            Play();
        }
    }

    private void OnSliderMouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (IsPlaying)
        {
            Pause();
        }

        _isScrubbing = true;
    }

    private void OnSliderMouseUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        _isScrubbing = false;

        if (_mediaElement is not null)
        {
            _mediaElement.Position = TimeSpan.FromSeconds(Math.Max(0, TimelineSlider.Value));
        }
    }

    private void OnMediaOpened(object sender, RoutedEventArgs e)
    {
        if (_mediaElement is null)
        {
            return;
        }

        DurationSeconds = _mediaElement.NaturalDuration.HasTimeSpan
            ? _mediaElement.NaturalDuration.TimeSpan.TotalSeconds
            : 0;

        CurrentSeconds = _mediaElement.Position.TotalSeconds;

        if (IsPlaying)
        {
            _playbackTimer.Start();
        }
    }

    private void OnMediaEnded(object sender, RoutedEventArgs e)
    {
        if (_mediaElement is not null)
        {
            _mediaElement.Pause();

            if (_mediaElement.NaturalDuration.HasTimeSpan)
            {
                CurrentSeconds = _mediaElement.NaturalDuration.TimeSpan.TotalSeconds;
            }
        }

        IsPlaying = false;
        _playbackTimer.Stop();
    }

    private void OnPlaybackTimerTick(object? sender, EventArgs e)
    {
        if (_mediaElement is null || !_mediaElement.NaturalDuration.HasTimeSpan)
        {
            return;
        }

        CurrentSeconds = _mediaElement.Position.TotalSeconds;
    }

    private void OnSliderValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isInternalUpdate)
        {
            return;
        }

        if (IsPlaying)
        {
            Pause();
        }

        if (_isScrubbing || TimelineSlider.IsMouseCaptureWithin)
        {
            var newPositionSeconds = Math.Clamp(TimelineSlider.Value, 0, Math.Max(0, DurationSeconds));

            if (_mediaElement is not null)
            {
                _mediaElement.Position = TimeSpan.FromSeconds(newPositionSeconds);
            }

            CurrentSeconds = newPositionSeconds;
            UpdateTimeText();
        }
    }

    private void UpdateButtonText()
    {
        PlayPauseButton.Content = IsPlaying ? "Pause" : "Play";
    }

    private void UpdateTimeText()
    {
        var current = TimeSpan.FromSeconds(Math.Max(0, CurrentSeconds));
        var total = TimeSpan.FromSeconds(Math.Max(0, DurationSeconds));
        TimeText.Text = $"{current:mm\\:ss} / {total:mm\\:ss}";
    }
}
