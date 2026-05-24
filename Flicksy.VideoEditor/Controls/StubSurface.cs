using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Flicksy.VideoEditor.Controls;

/// <summary>
/// Shared base for the placeholder panel and inspector content controls. Each subclass
/// just passes its tab name; the body is a centered label so the shell layout is visible
/// before the real content lands. Real implementations replace these as later issues
/// (#9 media bin, #13 graphics, #15–#18 inspectors) land.
/// </summary>
public abstract class StubSurface : UserControl
{
    protected StubSurface(string title)
    {
        Background = new SolidColorBrush(Color.FromRgb(0x18, 0x18, 0x18));
        Content = new TextBlock
        {
            Text = title,
            Foreground = new SolidColorBrush(Color.FromRgb(0xBD, 0xBD, 0xBD)),
            FontSize = 16,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
    }
}
