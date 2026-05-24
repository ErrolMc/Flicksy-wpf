using System.Windows.Controls;

namespace Flicksy.VideoEditor.Controls;

/// <summary>
/// Center-column transport bar: prev/play-pause/next buttons flanked by current and total
/// timecode labels. All commands resolve from <see cref="ViewModels.VideoEditorViewModel"/>
/// and are no-op stubs in this slice — play/pause flips <c>IsPlaying</c> for visual
/// feedback, prev/next step <c>Playhead</c> by one frame. Real playback wiring lands in
/// #11.
/// </summary>
public partial class TransportView : UserControl
{
    public TransportView()
    {
        InitializeComponent();
    }
}
