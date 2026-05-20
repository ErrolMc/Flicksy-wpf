using System;
using System.Windows;
using System.Windows.Controls;
using Flicksy.Editor.ViewModels;

namespace Flicksy.Editor.Controls;

public partial class DrawingView : UserControl
{
    private bool _isDrawingShape;
    private Point _shapeStartPoint;
    private ShapeKind _shapeKindInProgress;

    private static Point ApplyShiftConstraint(ShapeKind kind, Point start, Point end)
    {
        switch (kind)
        {
            case ShapeKind.Square:
            case ShapeKind.Circle:
            {
                // Constrain to equal side lengths (perfect square / circle), preserving each axis sign.
                var dx = end.X - start.X;
                var dy = end.Y - start.Y;
                var size = Math.Max(Math.Abs(dx), Math.Abs(dy));
                var signX = dx < 0 ? -1 : 1;
                var signY = dy < 0 ? -1 : 1;
                return new Point(start.X + signX * size, start.Y + signY * size);
            }
            case ShapeKind.Line:
            case ShapeKind.Arrow:
            {
                // Snap angle to the nearest 45°, preserve length.
                var dx = end.X - start.X;
                var dy = end.Y - start.Y;
                var length = Math.Sqrt(dx * dx + dy * dy);
                if (length <= double.Epsilon)
                {
                    return end;
                }

                var step = Math.PI / 4.0;
                var angle = Math.Atan2(dy, dx);
                var snapped = Math.Round(angle / step) * step;
                return new Point(start.X + Math.Cos(snapped) * length, start.Y + Math.Sin(snapped) * length);
            }
            default:
                return end;
        }
    }
}
