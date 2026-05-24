using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Flicksy.VideoEditor.Controls.Timeline;
using Flicksy.VideoEditor.ViewModels;

namespace Flicksy.VideoEditor.Controls;

/// <summary>
/// Center-column timeline surface. <c>DataContext</c> is
/// <see cref="ViewModels.TimelineViewModel"/>. Layout is a 2×2 grid with three
/// <see cref="ScrollViewer"/>s: a top ruler scroller, a left pinned-headers scroller, and
/// a main lanes scroller in the bottom-right. The main scroller owns the visible
/// scrollbars; the other two have hidden scrollbars and are slaved to its H/V offsets via
/// <see cref="OnMainScrollerScrollChanged"/>. Wheel + click handlers attach to the outer
/// border so they fire over any sub-scroller. The wheel handler implements:
/// <list type="bullet">
///   <item><description>Plain wheel: horizontal pan.</description></item>
///   <item><description>Shift + wheel: vertical pan.</description></item>
///   <item><description>Ctrl + wheel: zoom <see cref="TimelineViewModel.PixelsPerFrame"/>
///       centered on the current playhead.</description></item>
/// </list>
/// The deselect-on-click handler walks up from the click's <c>OriginalSource</c>; if a
/// <see cref="ClipView"/> is on the path the click is a clip selection and selection is
/// left alone, otherwise <c>SelectedClip</c> is cleared.
/// </summary>
public partial class TimelineView : UserControl
{
    private const double ZoomStep = 1.15;
    private const double PanLinesPerNotch = 3;

    public TimelineView()
    {
        InitializeComponent();
    }

    private TimelineViewModel? ViewModel => DataContext as TimelineViewModel;

    private void OnMainScrollerScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        // Push H/V offsets from MainScroller into the slave scrollers so ruler stays
        // aligned with lanes horizontally and headers stay aligned with lanes vertically.
        if (e.HorizontalChange != 0)
        {
            RulerScroller.ScrollToHorizontalOffset(MainScroller.HorizontalOffset);
        }
        if (e.VerticalChange != 0)
        {
            HeadersScroller.ScrollToVerticalOffset(MainScroller.VerticalOffset);
        }
    }

    private void OnRootPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var vm = ViewModel;
        if (vm is null || vm.SelectedClip is null) return;
        if (e.OriginalSource is DependencyObject source && IsInsideClipView(source)) return;
        vm.SelectedClip = null;
    }

    private static bool IsInsideClipView(DependencyObject source)
    {
        DependencyObject? current = source;
        while (current is not null)
        {
            if (current is ClipView) return true;
            current = VisualTreeHelper.GetParent(current) ?? LogicalTreeHelper.GetParent(current);
        }
        return false;
    }

    private void OnRootPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        var vm = ViewModel;
        if (vm is null) return;

        var mods = Keyboard.Modifiers;

        if ((mods & ModifierKeys.Control) == ModifierKeys.Control)
        {
            ZoomCenteredOnPlayhead(vm, e.Delta > 0 ? ZoomStep : 1.0 / ZoomStep);
            e.Handled = true;
            return;
        }

        if ((mods & ModifierKeys.Shift) == ModifierKeys.Shift)
        {
            MainScroller.ScrollToVerticalOffset(MainScroller.VerticalOffset - e.Delta);
            e.Handled = true;
            return;
        }

        // Plain wheel = horizontal pan. WPF reports wheel delta in multiples of 120;
        // scale to feel like a normal scroll step.
        MainScroller.ScrollToHorizontalOffset(MainScroller.HorizontalOffset - e.Delta * PanLinesPerNotch / 120.0 * 16);
        e.Handled = true;
    }

    private void ZoomCenteredOnPlayhead(TimelineViewModel vm, double factor)
    {
        // Headers are pinned outside MainScroller, so MainScroller's content coordinate
        // for the playhead is just playhead × PixelsPerFrame (no header offset). Capture
        // the playhead's current screen X, apply the zoom, then re-derive the scroll
        // offset so the playhead lands at the same screen X.
        var oldPxPerFrame = vm.PixelsPerFrame;
        var playhead = vm.Transport.Playhead;

        var oldContentX = playhead * oldPxPerFrame;
        var screenX = oldContentX - MainScroller.HorizontalOffset;

        vm.ZoomBy(factor);

        var newContentX = playhead * vm.PixelsPerFrame;
        var newOffset = Math.Max(0, newContentX - screenX);
        MainScroller.ScrollToHorizontalOffset(newOffset);
    }
}
