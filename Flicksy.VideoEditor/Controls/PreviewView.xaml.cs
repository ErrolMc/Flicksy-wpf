using System.Windows.Controls;

namespace Flicksy.VideoEditor.Controls;

/// <summary>
/// Top-of-center-column preview surface. The <see cref="Image"/>'s <c>Stretch=Uniform</c>
/// combined with a <c>WriteableBitmap</c> source sized to the project resolution
/// (owned by <c>PreviewViewModel</c> and painted in place each frame by
/// <c>SkiaCompositor.RenderFrame</c>, surfaced as <c>PreviewViewModel.CurrentFrame</c>)
/// letterboxes the content against the control's dark background. No code-behind logic —
/// the binding plus the compositor's per-frame render path drive everything.
/// </summary>
public partial class PreviewView : UserControl
{
    public PreviewView()
    {
        InitializeComponent();
    }
}
