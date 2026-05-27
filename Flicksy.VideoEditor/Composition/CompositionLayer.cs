using System;
using Flicksy.VideoEditor.Project;

namespace Flicksy.VideoEditor.Composition;

/// <summary>
/// Pure-data description of one clip's contribution to a single composited frame. Built
/// by <see cref="CompositionPlanner.PlanFrame"/> and consumed by every backend's paint
/// and audio-mix loops. Zero Skia / WPF dependencies so planning can be tested in
/// isolation from any backend.
/// </summary>
/// <param name="Clip">The active clip. Backends dispatch on subtype
/// (<see cref="MediaClip"/>, <see cref="GraphicsClip"/>, …).</param>
/// <param name="Track">The owning track. Carries <c>Kind</c> (paint-vs-mix routing) and
/// <c>Muted</c> (audio mix's per-track skip). <c>Disabled</c> tracks are filtered by the
/// planner and never appear here.</param>
/// <param name="ZIndex">Paint order — lower z paints first. Planner emits Video tracks
/// (lowest z), then Overlay tracks, then Audio tracks. Audio layers carry whatever index
/// they fell on; the audio mix pass does not consume z.</param>
/// <param name="SourceTime">For <see cref="MediaClip"/>: the speed-mapped source-side
/// time to decode. For <see cref="GraphicsClip"/> and other time-invariant clip types:
/// <see cref="TimeSpan.Zero"/>.</param>
public readonly record struct CompositionLayer(
    Clip Clip,
    Track Track,
    int ZIndex,
    TimeSpan SourceTime);
