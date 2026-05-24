using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Flicksy.VideoEditor.Project;

/// <summary>
/// A blended region between two adjacent clips on the same <see cref="Track"/>. Stored on
/// the track (in <see cref="Track.Transitions"/>) and keyed by the pair
/// <see cref="LeftClipId"/>/<see cref="RightClipId"/> — a transition is intentionally not a
/// <see cref="Clip"/> because it has no independent existence: trim/split/delete on either
/// adjacent clip must keep the transition list consistent. See ADR 0002.
/// </summary>
public partial class Transition : ObservableObject
{
    public Guid LeftClipId { get; init; }

    public Guid RightClipId { get; init; }

    [ObservableProperty]
    private TransitionType type;

    [ObservableProperty]
    private int duration;
}
