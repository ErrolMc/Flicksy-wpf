using System.Windows.Controls;

namespace Flicksy.VideoEditor.Controls;

/// <summary>
/// Center-column timeline surface. <c>DataContext</c> is
/// <see cref="ViewModels.TimelineViewModel"/> — empty in this slice; populated in #7
/// when the real timeline (tracks, clips, ruler) lands.
/// </summary>
public partial class TimelineView : UserControl
{
    public TimelineView()
    {
        InitializeComponent();
    }
}
