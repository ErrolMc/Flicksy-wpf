using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Flicksy.VideoEditor.ViewModels;

/// <summary>
/// State and commands for the Transport bar: the integer-frame <see cref="Playhead"/>,
/// the <see cref="IsPlaying"/> flag, and the play/pause + frame-step commands the
/// TransportView binds to. Timecode strings are computed off <see cref="Playhead"/> /
/// <see cref="TotalFrames"/> at the project framerate. All commands are no-op stubs in
/// this slice — real playback wiring (the compositor driving frame presentation) lands
/// in #11.
/// </summary>
public partial class TransportViewModel : ObservableObject
{
    private readonly int _framerate;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CurrentTimecode))]
    private int playhead;

    // Toggled by the play/pause button. No real playback yet — the flag exists so the
    // button can flip its label and future bindings have something to observe.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PlayPauseLabel))]
    private bool isPlaying;

    public TransportViewModel(Project.Project project)
    {
        _framerate = project.Settings.Framerate;
        TotalFrames = ComputeTotalFrames(project);
        TotalTimecode = FormatTimecode(TotalFrames, _framerate);
    }

    // Snapshotted at construction — the document model doesn't yet expose mutation events
    // the VM could subscribe to (media bin lands in #9, timeline editing in #12), so a
    // snapshot is sufficient for this slice.
    public int TotalFrames { get; }

    public string TotalTimecode { get; }

    public string CurrentTimecode => FormatTimecode(Playhead, _framerate);

    public string PlayPauseLabel => IsPlaying ? "Pause" : "Play";

    [RelayCommand]
    private void PlayPause()
    {
        IsPlaying = !IsPlaying;
    }

    [RelayCommand]
    private void PrevFrame()
    {
        Playhead = Math.Max(0, Playhead - 1);
    }

    [RelayCommand]
    private void NextFrame()
    {
        Playhead = TotalFrames > 0
            ? Math.Min(TotalFrames, Playhead + 1)
            : Playhead + 1;
    }

    private static int ComputeTotalFrames(Project.Project project)
    {
        int max = 0;
        foreach (var track in project.Tracks)
        {
            foreach (var clip in track.Clips)
            {
                var end = clip.TimelineStart + clip.Duration;
                if (end > max) max = end;
            }
        }
        return max;
    }

    // hh:mm:ss.ff where ff is the frame index inside the current second.
    private static string FormatTimecode(int frames, int framerate)
    {
        if (framerate <= 0) framerate = 1;
        var totalSeconds = frames / framerate;
        var frame = frames % framerate;
        var hours = totalSeconds / 3600;
        var minutes = (totalSeconds / 60) % 60;
        var seconds = totalSeconds % 60;
        return $"{hours:D2}:{minutes:D2}:{seconds:D2}.{frame:D2}";
    }
}
