using System;
using System.Windows;
using System.Windows.Controls;
using Flicksy.Editor.Media;

namespace Flicksy.Editor.Controls;

public partial class VideoPlaybackOverlay : UserControl
{
    private bool _isScrubbing;
    private bool _isInternalUpdate;
    private bool _shouldResumeAfterScrub;
    private IVideoPlayer? _player;

    public VideoPlaybackOverlay()
    {
        InitializeComponent();
        UpdateButtonText();
        UpdateTimeText();
        Unloaded += OnUnloaded;
    }

    public static readonly DependencyProperty PlayerProperty =
        DependencyProperty.Register(
            nameof(Player),
            typeof(IVideoPlayer),
            typeof(VideoPlaybackOverlay),
            new PropertyMetadata(null, OnPlayerChanged));

    public static readonly DependencyProperty IsPlayingProperty =
        DependencyProperty.Register(
            nameof(IsPlaying),
            typeof(bool),
            typeof(VideoPlaybackOverlay),
            new PropertyMetadata(false, OnIsPlayingChanged));

    public static readonly DependencyProperty DurationSecondsProperty =
        DependencyProperty.Register(
            nameof(DurationSeconds),
            typeof(double),
            typeof(VideoPlaybackOverlay),
            new PropertyMetadata(0d, OnDurationChanged));

    public static readonly DependencyProperty CurrentSecondsProperty =
        DependencyProperty.Register(
            nameof(CurrentSeconds),
            typeof(double),
            typeof(VideoPlaybackOverlay),
            new PropertyMetadata(0d, OnCurrentChanged));

    public IVideoPlayer? Player
    {
        get => (IVideoPlayer?)GetValue(PlayerProperty);
        set => SetValue(PlayerProperty, value);
    }

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

    public void ResetState()
    {
        IsPlaying = false;
        CurrentSeconds = 0;
        DurationSeconds = 0;
    }

    private static void OnPlayerChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var overlay = (VideoPlaybackOverlay)d;
        overlay.DetachPlayer(e.OldValue as IVideoPlayer);
        overlay.AttachPlayer(e.NewValue as IVideoPlayer);
    }

    private void AttachPlayer(IVideoPlayer? player)
    {
        _player = player;
        if (player is null)
        {
            ResetState();
            return;
        }

        player.StateChanged += OnPlayerStateChanged;
        player.PositionChanged += OnPlayerPositionChanged;
        player.MediaEnded += OnPlayerMediaEnded;

        SyncFromPlayer();
    }

    private void DetachPlayer(IVideoPlayer? player)
    {
        if (player is null) return;
        player.StateChanged -= OnPlayerStateChanged;
        player.PositionChanged -= OnPlayerPositionChanged;
        player.MediaEnded -= OnPlayerMediaEnded;
    }

    private void SyncFromPlayer()
    {
        if (_player is null) return;
        DurationSeconds = _player.Duration.TotalSeconds;
        CurrentSeconds = _player.Position.TotalSeconds;
        IsPlaying = _player.State == PlaybackState.Playing;
    }

    private void OnPlayerStateChanged(object? sender, EventArgs e)
    {
        DispatchSync(SyncFromPlayer);
    }

    private void OnPlayerPositionChanged(object? sender, EventArgs e)
    {
        DispatchSync(() =>
        {
            if (_player is null) return;
            if (_isScrubbing) return;
            CurrentSeconds = _player.Position.TotalSeconds;
            if (_player.Duration.TotalSeconds > 0 && Math.Abs(DurationSeconds - _player.Duration.TotalSeconds) > 0.001)
            {
                DurationSeconds = _player.Duration.TotalSeconds;
            }
        });
    }

    private void OnPlayerMediaEnded(object? sender, EventArgs e)
    {
        DispatchSync(() =>
        {
            IsPlaying = false;
            if (_player is not null)
            {
                CurrentSeconds = _player.Duration.TotalSeconds;
            }
        });
    }

    private void DispatchSync(Action action)
    {
        if (Dispatcher.CheckAccess())
        {
            action();
        }
        else
        {
            Dispatcher.BeginInvoke(action);
        }
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
        if (control._isScrubbing) return;
        control._isInternalUpdate = true;
        control.TimelineSlider.Value = Math.Clamp(control.CurrentSeconds, 0, Math.Max(0, control.DurationSeconds));
        control._isInternalUpdate = false;
        control.UpdateTimeText();
    }

    private void OnPlayPauseClick(object sender, RoutedEventArgs e)
    {
        if (_player is null) return;
        if (IsPlaying)
        {
            _player.Pause();
        }
        else
        {
            _player.Play();
        }
    }

    private void OnSliderMouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (_player is null) return;
        _shouldResumeAfterScrub = _player.State == PlaybackState.Playing;
        if (_shouldResumeAfterScrub)
        {
            _player.Pause();
        }
        _isScrubbing = true;
    }

    private void OnSliderMouseUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        _isScrubbing = false;
        if (_player is null) return;

        var target = TimeSpan.FromSeconds(Math.Max(0, TimelineSlider.Value));
        _player.Seek(target);

        if (_shouldResumeAfterScrub)
        {
            _shouldResumeAfterScrub = false;
            _player.Play();
        }
    }

    private void OnSliderValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isInternalUpdate) return;
        if (_isScrubbing || TimelineSlider.IsMouseCaptureWithin)
        {
            CurrentSeconds = Math.Clamp(TimelineSlider.Value, 0, Math.Max(0, DurationSeconds));
            UpdateTimeText();
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        DetachPlayer(_player);
        _player = null;
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
