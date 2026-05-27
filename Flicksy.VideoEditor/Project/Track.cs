using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Flicksy.VideoEditor.Project;

/// <summary>
/// One horizontal lane on the timeline. Owns an ordered <see cref="Clips"/> collection and
/// a sibling <see cref="Transitions"/> list keyed by adjacent-clip pairs. The
/// <see cref="Kind"/> is fixed at construction and determines which clip subtypes are
/// valid here and how the compositor layers the track's output.
/// </summary>
public partial class Track : ObservableObject
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public TrackKind Kind { get; init; }

    [ObservableProperty]
    private string name = string.Empty;

    /// <summary>Excluded from the audio mix when true. UI hides the M toggle on Overlay headers (those tracks never carry audio); Video tracks keep it because their clips may have <c>Streams=Both</c>.</summary>
    [ObservableProperty]
    private bool muted;

    /// <summary>Editor refuses edits to clips on this track. Compositor unaffected.</summary>
    [ObservableProperty]
    private bool locked;

    /// <summary>Compositor skips the track entirely; the row ghosts in the timeline UI.</summary>
    [ObservableProperty]
    private bool disabled;

    public ObservableCollection<Clip> Clips { get; } = new();

    public List<Transition> Transitions { get; } = new();
}
