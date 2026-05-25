using System;
using System.Collections.ObjectModel;
using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Flicksy.VideoEditor.Project;

/// <summary>
/// A clip that plays a region of a <see cref="MediaSource"/> on the timeline. The clip
/// references its source by <see cref="MediaSourceId"/>; the file path lives on the
/// <see cref="MediaSource"/> only (see ADR 0003).
/// <para>
/// <see cref="SourceIn"/>/<see cref="SourceOut"/> are framerate-independent
/// <see cref="System.TimeSpan"/> values into the source; <see cref="Duration"/> is the
/// resulting span on the timeline in integer frames after applying <see cref="Speed"/> at
/// the project's framerate. <see cref="Framerate"/> is a local mirror of
/// <see cref="ProjectSettings.Framerate"/> kept in sync by the parent <see cref="Project"/>
/// so <see cref="Duration"/> can be a parameterless getter without a back-reference.
/// </para>
/// <para>
/// <see cref="Source"/> is a transient convenience reference, populated by
/// <see cref="Project"/> factories (and, in the future, the load resolver). It is
/// <see cref="JsonIgnoreAttribute">not serialized</see>. <strong>All mutations
/// (relocate, remove, etc.) must drive off <see cref="MediaSourceId"/> lookups in
/// <see cref="Project.MediaSources"/></strong> — never treat <see cref="Source"/> as
/// source of truth.
/// </para>
/// </summary>
public partial class MediaClip : Clip
{
    public Guid MediaSourceId { get; init; }

    [ObservableProperty]
    private TimeSpan sourceIn;

    [ObservableProperty]
    private TimeSpan sourceOut;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Duration))]
    private double speed = 1.0;

    [ObservableProperty]
    private double volume = 1.0;

    [ObservableProperty]
    private ClipStreams streams = ClipStreams.Both;

    // Mirrors the parent Project's framerate. Project is responsible for keeping
    // this in sync; storing it on the clip lets Duration be a parameterless getter
    // without a back-reference to Project.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Duration))]
    private int framerate = 30;

    // Transient convenience ref — populated by Project factories. Not the source of truth:
    // mutations always look up by MediaSourceId in Project.MediaSources.
    [JsonIgnore]
    [ObservableProperty]
    private MediaSource? source;

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
