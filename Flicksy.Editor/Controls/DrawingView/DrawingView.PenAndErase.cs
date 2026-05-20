using System.Windows;
using System.Windows.Controls;

namespace Flicksy.Editor.Controls;

public partial class DrawingView : UserControl
{
    private const double InputSmoothingAlpha = 0.5d;

    private Point? _lastAppendedPoint;
    private Point? _smoothedPoint;

    private Point SmoothInput(Point raw)
    {
        if (_smoothedPoint is not Point previous)
        {
            _smoothedPoint = raw;
            return raw;
        }

        var smoothed = new Point(
            (InputSmoothingAlpha * raw.X) + ((1d - InputSmoothingAlpha) * previous.X),
            (InputSmoothingAlpha * raw.Y) + ((1d - InputSmoothingAlpha) * previous.Y));
        _smoothedPoint = smoothed;
        return smoothed;
    }

    private void EraseAt(Point point)
    {
        if (ViewModel is null)
        {
            return;
        }

        for (var i = ViewModel.Items.Count - 1; i >= 0; i--)
        {
            var item = ViewModel.Items[i];
            if (IntersectsItem(item, point))
            {
                ViewModel.Items.RemoveAt(i);
                return;
            }
        }
    }
}
