using System.Windows.Controls;

namespace Flicksy.VideoEditor.Controls.Timeline;

/// <summary>
/// Left-side header for one track. <c>DataContext</c> is a <see cref="Project.Track"/>.
/// Shows the track name and stub mute/lock toggles — the toggles aren't bound to model
/// state yet because <see cref="Project.Track"/> doesn't expose Mute/Lock until a later
/// slice. Pure visual feedback for now.
/// </summary>
public partial class TrackHeaderView : UserControl
{
    public TrackHeaderView()
    {
        InitializeComponent();
    }
}
