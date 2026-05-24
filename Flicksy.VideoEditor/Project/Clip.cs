using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Flicksy.VideoEditor.Project;

/// <summary>
/// Abstract base for anything that occupies a span on a <see cref="Track"/>. Holds the
/// stable identity (<see cref="Id"/>, used by <see cref="Transition"/> to point at clip
/// pairs) and the timeline placement (<see cref="TimelineStart"/> in integer frames at
/// the project's framerate). Concrete subtypes (<see cref="MediaClip"/>,
/// <see cref="GraphicsClip"/>) supply their own <see cref="Duration"/> semantics — see
/// ADR 0002 for the rationale behind a discriminated clip hierarchy.
/// </summary>
public abstract partial class Clip : ObservableObject
{
    public Guid Id { get; init; } = Guid.NewGuid();

    [ObservableProperty]
    private int timelineStart;

    public abstract int Duration { get; }
}
