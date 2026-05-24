using System;
using CommunityToolkit.Mvvm.ComponentModel;
using Flicksy.VideoEditor.Project;

namespace Flicksy.VideoEditor.ViewModels;

/// <summary>
/// State for the timeline surface: the document <see cref="Project"/> whose tracks/clips
/// it renders, the <see cref="Transport"/> whose <c>Playhead</c> drives the overlay, the
/// <see cref="PixelsPerFrame"/> zoom level, and the currently <see cref="SelectedClip"/>.
/// Selection is mirrored by <see cref="VideoEditorViewModel"/> so the right rail stays in
/// sync — the timeline writes the user's click here and the root forwards to its own
/// SelectedClip (and vice versa when selection is cleared elsewhere).
/// </summary>
public partial class TimelineViewModel : ObservableObject
{
    public const double MinPixelsPerFrame = 0.25;
    public const double MaxPixelsPerFrame = 60.0;

    [ObservableProperty]
    private double pixelsPerFrame = 6.0;

    [ObservableProperty]
    private Clip? selectedClip;

    public TimelineViewModel(Project.Project project, TransportViewModel transport)
    {
        Project = project;
        Transport = transport;
    }

    public Project.Project Project { get; }

    public TransportViewModel Transport { get; }

    /// <summary>
    /// Multiplies <see cref="PixelsPerFrame"/> by <paramref name="factor"/>, clamped to the
    /// supported range. The caller (timeline view's wheel handler) is responsible for
    /// restoring the scroll offset so the zoom appears centered on the playhead.
    /// </summary>
    public void ZoomBy(double factor)
    {
        if (factor <= 0 || double.IsNaN(factor) || double.IsInfinity(factor)) return;
        PixelsPerFrame = Math.Clamp(PixelsPerFrame * factor, MinPixelsPerFrame, MaxPixelsPerFrame);
    }

    /// <summary>
    /// Sets <see cref="TransportViewModel.Playhead"/> to <paramref name="frame"/>, clamped
    /// to <c>[0, TotalFrames]</c>. Used by scrub gestures on the ruler and the empty lane
    /// area; clip-internal scrubbing (drag the playhead handle itself) is a later slice.
    /// </summary>
    public void SeekToFrame(int frame)
    {
        var max = Math.Max(0, Transport.TotalFrames);
        Transport.Playhead = Math.Clamp(frame, 0, max);
    }

    /// <summary>
    /// Convenience: convert a lane-relative pixel offset to a frame and seek there.
    /// </summary>
    public void SeekToPixel(double laneX)
    {
        if (PixelsPerFrame <= 0) return;
        SeekToFrame((int)Math.Round(laneX / PixelsPerFrame));
    }
}
