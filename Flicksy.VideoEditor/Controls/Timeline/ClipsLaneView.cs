using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Flicksy.VideoEditor.Project;
using Flicksy.VideoEditor.ViewModels;

namespace Flicksy.VideoEditor.Controls.Timeline;

/// <summary>
/// Renders one <see cref="ClipView"/> per <see cref="Project.Track.Clips"/> entry, positioned
/// on a <see cref="Canvas"/> at <c>TimelineStart × PixelsPerFrame</c> with width
/// <c>Duration × PixelsPerFrame</c>. Both inputs come in as DPs (<see cref="Track"/>,
/// <see cref="Timeline"/>) so the lane's parent <c>ItemTemplate</c> binds them explicitly —
/// no visual-tree walking. The lane subscribes to:
///  - <c>Track.Clips.CollectionChanged</c> — add/remove rebuilds children.
///  - Each <c>Clip.PropertyChanged</c> — <c>TimelineStart</c>/<c>Duration</c> re-layout.
///  - <c>Timeline.PropertyChanged</c> — <c>PixelsPerFrame</c> re-layouts all,
///    <c>SelectedClip</c> updates <see cref="ClipView.IsSelected"/> on the matching child.
/// </summary>
public sealed class ClipsLaneView : Canvas
{
    public const double TrackHeight = 56;
    private const double ClipVerticalPadding = 4;
    private const double MinClipWidth = 2;

    public static readonly DependencyProperty TrackProperty = DependencyProperty.Register(
        nameof(Track),
        typeof(Track),
        typeof(ClipsLaneView),
        new PropertyMetadata(null, OnTrackChanged));

    public static readonly DependencyProperty TimelineProperty = DependencyProperty.Register(
        nameof(Timeline),
        typeof(TimelineViewModel),
        typeof(ClipsLaneView),
        new PropertyMetadata(null, OnTimelineChanged));

    private readonly Dictionary<Clip, ClipView> _clipViews = new();
    private readonly PropertyChangedEventHandler _clipHandler;
    private readonly PropertyChangedEventHandler _timelineHandler;
    private readonly NotifyCollectionChangedEventHandler _clipsChangedHandler;

    public ClipsLaneView()
    {
        Background = new SolidColorBrush(Color.FromRgb(0x14, 0x14, 0x14));
        Height = TrackHeight;
        ClipToBounds = false;

        _clipHandler = OnClipPropertyChanged;
        _timelineHandler = OnTimelinePropertyChanged;
        _clipsChangedHandler = OnClipsCollectionChanged;
    }

    public Track? Track
    {
        get => (Track?)GetValue(TrackProperty);
        set => SetValue(TrackProperty, value);
    }

    public TimelineViewModel? Timeline
    {
        get => (TimelineViewModel?)GetValue(TimelineProperty);
        set => SetValue(TimelineProperty, value);
    }

    private static void OnTrackChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((ClipsLaneView)d).HandleTrackChanged((Track?)e.OldValue, (Track?)e.NewValue);
    }

    private static void OnTimelineChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((ClipsLaneView)d).HandleTimelineChanged((TimelineViewModel?)e.OldValue, (TimelineViewModel?)e.NewValue);
    }

    private void HandleTrackChanged(Track? oldTrack, Track? newTrack)
    {
        if (oldTrack is not null)
        {
            oldTrack.Clips.CollectionChanged -= _clipsChangedHandler;
        }
        if (newTrack is not null)
        {
            newTrack.Clips.CollectionChanged += _clipsChangedHandler;
        }
        RebuildClips();
    }

    private void HandleTimelineChanged(TimelineViewModel? oldVm, TimelineViewModel? newVm)
    {
        if (oldVm is not null)
        {
            oldVm.PropertyChanged -= _timelineHandler;
        }
        if (newVm is not null)
        {
            newVm.PropertyChanged += _timelineHandler;
        }
        UpdateAllLayouts();
        UpdateAllSelections();
        InvalidateMeasure();
    }

    private void OnTimelinePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(TimelineViewModel.PixelsPerFrame):
                UpdateAllLayouts();
                InvalidateMeasure();
                break;
            case nameof(TimelineViewModel.SelectedClip):
                UpdateAllSelections();
                break;
        }
    }

    private void OnClipsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        // Tracks hold a small number of clips — a full rebuild keeps the diff logic
        // trivial and isn't perf-relevant for this slice.
        RebuildClips();
    }

    private void OnClipPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not Clip clip) return;
        if (!_clipViews.TryGetValue(clip, out var view)) return;

        // Fully qualified: `Clip` alone resolves to UIElement.Clip (a Geometry property)
        // inside this Canvas-derived type, so nameof on it fails to compile.
        if (e.PropertyName == nameof(Project.Clip.TimelineStart) || e.PropertyName == nameof(Project.Clip.Duration))
        {
            UpdateClipLayout(view, clip);
            InvalidateMeasure();
        }
    }

    private void RebuildClips()
    {
        foreach (var (clip, _) in _clipViews)
        {
            clip.PropertyChanged -= _clipHandler;
        }
        _clipViews.Clear();
        Children.Clear();

        if (Track is null) return;

        foreach (var clip in Track.Clips)
        {
            AddClipView(clip);
        }
        UpdateAllSelections();
        InvalidateMeasure();
    }

    private void AddClipView(Clip clip)
    {
        var view = new ClipView { DataContext = clip };
        _clipViews[clip] = view;
        clip.PropertyChanged += _clipHandler;
        Children.Add(view);
        UpdateClipLayout(view, clip);
    }

    private void UpdateClipLayout(ClipView view, Clip clip)
    {
        var px = Timeline?.PixelsPerFrame ?? 1.0;
        Canvas.SetLeft(view, clip.TimelineStart * px);
        Canvas.SetTop(view, ClipVerticalPadding);
        view.Width = Math.Max(MinClipWidth, clip.Duration * px);
        view.Height = TrackHeight - (ClipVerticalPadding * 2);
    }

    private void UpdateAllLayouts()
    {
        foreach (var (clip, view) in _clipViews)
        {
            UpdateClipLayout(view, clip);
        }
    }

    private void UpdateAllSelections()
    {
        var selected = Timeline?.SelectedClip;
        foreach (var (clip, view) in _clipViews)
        {
            view.IsSelected = ReferenceEquals(clip, selected);
        }
    }

    protected override Size MeasureOverride(Size constraint)
    {
        // Canvas's default measure returns (0,0). Measure children so they layout, then
        // report the lane's natural content width so the host ScrollViewer can scroll.
        foreach (UIElement child in Children)
        {
            child.Measure(constraint);
        }

        var width = ComputeContentWidth();
        return new Size(width, TrackHeight);
    }

    private double ComputeContentWidth()
    {
        if (Timeline is null) return 0;
        var totalFrames = Math.Max(Timeline.Transport.TotalFrames, 1);
        return totalFrames * Timeline.PixelsPerFrame;
    }
}
