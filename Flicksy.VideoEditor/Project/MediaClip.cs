using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Flicksy.VideoEditor.Project;

/// <summary>
/// A clip that plays a region of an external video or audio file on the timeline.
/// <see cref="SourceIn"/>/<see cref="SourceOut"/> are framerate-independent
/// <see cref="System.TimeSpan"/> values into the source; <see cref="Duration"/> is the
/// resulting span on the timeline in integer frames after applying <see cref="Speed"/>
/// at the project's framerate. <see cref="Framerate"/> is a local mirror of
/// <see cref="ProjectSettings.Framerate"/> kept in sync by the parent <see cref="Project"/>
/// so <see cref="Duration"/> can be a parameterless getter without a back-reference.
/// </summary>
public partial class MediaClip : Clip
{
    [ObservableProperty]
    private string sourcePath = string.Empty;

    [ObservableProperty]
    private TimeSpan sourceIn;

    [ObservableProperty]
    private TimeSpan sourceOut;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Duration))]
    private double speed = 1.0;

    [ObservableProperty]
    private double volume = 1.0;

    // Mirrors the parent Project's framerate. Project is responsible for keeping
    // this in sync; storing it on the clip lets Duration be a parameterless getter
    // without a back-reference to Project.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Duration))]
    private int framerate = 30;

    public Transform2D Transform { get; } = new();

    public ObservableCollection<Filter> Filters { get; } = new();

    partial void OnSourceInChanged(TimeSpan value) => OnPropertyChanged(nameof(Duration));

    partial void OnSourceOutChanged(TimeSpan value) => OnPropertyChanged(nameof(Duration));

    public override int Duration
    {
        get
        {
            var sourceSeconds = (SourceOut - SourceIn).TotalSeconds;
            if (Speed <= 0 || sourceSeconds <= 0 || Framerate <= 0)
            {
                return 0;
            }

            var timelineSeconds = sourceSeconds / Speed;
            return (int)Math.Round(timelineSeconds * Framerate);
        }
    }
}
