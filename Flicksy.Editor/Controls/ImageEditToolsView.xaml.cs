using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace Flicksy.Editor.Controls;

public partial class ImageEditToolsView : UserControl
{
    public ImageEditToolsView()
    {
        InitializeComponent();
    }

    private void OnShapeSettingsPopupOpened(object sender, System.EventArgs e)
    {
        if (sender is not Popup popup || popup.Child is not FrameworkElement child)
        {
            return;
        }

        if (popup.PlacementTarget is not FrameworkElement target)
        {
            return;
        }

        child.UpdateLayout();
        var childWidth = child.ActualWidth > 0 ? child.ActualWidth : child.DesiredSize.Width;
        popup.HorizontalOffset = (target.ActualWidth - childWidth) / 2;
    }
}
