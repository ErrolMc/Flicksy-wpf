using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Flicksy.VideoEditor.Project;
using Flicksy.VideoEditor.ViewModels;

namespace Flicksy.VideoEditor.Controls.Timeline;

/// <summary>
/// Visual for one <see cref="Clip"/> inside a <see cref="ClipsLaneView"/>. The
/// per-subtype look comes from typed DataTemplates in the XAML (one for
/// <see cref="MediaClip"/>, one for <see cref="GraphicsClip"/>) — extend by adding a new
/// DataTemplate keyed by the new <see cref="Clip"/> subtype.
///
/// <see cref="IsSelected"/> is pushed down by the parent <see cref="ClipsLaneView"/> when
/// the host's <see cref="TimelineViewModel.SelectedClip"/> changes; the border color
/// reflects it. Click bubbles up via <see cref="VisualTreeHelper"/> walk to find the host
/// <see cref="TimelineViewModel"/> and writes <see cref="TimelineViewModel.SelectedClip"/>
/// directly (the root VM mirrors back to its own SelectedClip).
/// </summary>
public partial class ClipView : UserControl
{
    public static readonly DependencyProperty IsSelectedProperty = DependencyProperty.Register(
        nameof(IsSelected),
        typeof(bool),
        typeof(ClipView),
        new PropertyMetadata(false, OnIsSelectedChanged));

    public bool IsSelected
    {
        get => (bool)GetValue(IsSelectedProperty);
        set => SetValue(IsSelectedProperty, value);
    }

    public ClipView()
    {
        InitializeComponent();
        ApplySelectionVisual();
    }

    private static void OnIsSelectedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((ClipView)d).ApplySelectionVisual();
    }

    private void ApplySelectionVisual()
    {
        var selectedBrush = (Brush)Resources["ClipSelectedBrush"];
        var defaultBrush = (Brush)Resources["ClipBorderBrush"];
        OuterBorder.BorderBrush = IsSelected ? selectedBrush : defaultBrush;
        OuterBorder.BorderThickness = new Thickness(IsSelected ? 2 : 1);
    }

    private void OnClipMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is not Clip clip) return;

        var timeline = FindTimelineViewModel();
        if (timeline is null) return;

        timeline.SelectedClip = clip;
        e.Handled = true;
    }

    private TimelineViewModel? FindTimelineViewModel()
    {
        DependencyObject? current = this;
        while (current is not null)
        {
            if (current is FrameworkElement fe && fe.DataContext is TimelineViewModel vm)
            {
                return vm;
            }
            current = VisualTreeHelper.GetParent(current);
        }
        return null;
    }
}
