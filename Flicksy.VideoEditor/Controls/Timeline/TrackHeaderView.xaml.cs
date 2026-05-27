using System.Windows.Controls;

namespace Flicksy.VideoEditor.Controls.Timeline;

/// <summary>
/// Left-side header for one track. <c>DataContext</c> is a <see cref="Project.Track"/>.
/// Shows the track name and Mute/Lock/Disable toggles bound to the matching <c>Track</c>
/// flags. The Mute (M) button is collapsed on non-Audio kinds via a style trigger.
/// </summary>
public partial class TrackHeaderView : UserControl
{
    public TrackHeaderView()
    {
        InitializeComponent();
    }
}
