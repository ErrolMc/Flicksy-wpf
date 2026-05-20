using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Flicksy.Editor.Helpers;
using Flicksy.Editor.Source;

namespace Flicksy.Editor.Controls;

public partial class DrawingView : UserControl
{
    private enum CornerKind { None, TopLeft, TopRight, BottomLeft, BottomRight }

    // Drag / scale gesture state.
    private DrawingItem? _draggingItem;
    private Matrix _moveBaseMatrix;
    private Point _moveStartPoint;
    private DrawingItem? _scalingItem;
    private Matrix _scaleBaseMatrix;
    private Point _scaleAnchorWorld;
    private Point _scaleOriginalGrabbedWorld;

    private DrawingItem? HitTestItem(Point point)
    {
        if (ViewModel is null)
        {
            return null;
        }

        for (var i = ViewModel.Items.Count - 1; i >= 0; i--)
        {
            var item = ViewModel.Items[i];
            if (IntersectsItem(item, point))
            {
                return item;
            }
        }

        return null;
    }

    private static bool IsInsideSelectionBounds(DrawingItem item, Point worldPoint)
    {
        var canonical = item.CanonicalBounds;
        if (canonical.IsEmpty)
        {
            return false;
        }

        var inverse = item.Transform.Matrix;
        if (!inverse.HasInverse)
        {
            return false;
        }
        inverse.Invert();
        var localPoint = inverse.Transform(worldPoint);
        return canonical.Contains(localPoint);
    }

    private CornerKind GetCornerHit(DrawingItem item, Point worldPoint)
    {
        var canonical = item.CanonicalBounds;
        if (canonical.IsEmpty || canonical.Width < 0.5d || canonical.Height < 0.5d)
        {
            return CornerKind.None;
        }

        var m = item.Transform.Matrix;
        var tl = m.Transform(new Point(canonical.Left, canonical.Top));
        var tr = m.Transform(new Point(canonical.Right, canonical.Top));
        var bl = m.Transform(new Point(canonical.Left, canonical.Bottom));
        var br = m.Transform(new Point(canonical.Right, canonical.Bottom));

        var scale = Math.Max(0.0001d, ContentScale);
        var pickup = 8.0d / scale;
        var pickupSquared = pickup * pickup;

        if (DrawingMath.DistanceSquared(worldPoint, tl) <= pickupSquared) return CornerKind.TopLeft;
        if (DrawingMath.DistanceSquared(worldPoint, tr) <= pickupSquared) return CornerKind.TopRight;
        if (DrawingMath.DistanceSquared(worldPoint, bl) <= pickupSquared) return CornerKind.BottomLeft;
        if (DrawingMath.DistanceSquared(worldPoint, br) <= pickupSquared) return CornerKind.BottomRight;

        return CornerKind.None;
    }

    private static (Point Anchor, Point Grabbed) GetAnchorAndGrabbed(Rect bounds, CornerKind corner) => corner switch
    {
        CornerKind.TopLeft => (new Point(bounds.Right, bounds.Bottom), new Point(bounds.Left, bounds.Top)),
        CornerKind.TopRight => (new Point(bounds.Left, bounds.Bottom), new Point(bounds.Right, bounds.Top)),
        CornerKind.BottomLeft => (new Point(bounds.Right, bounds.Top), new Point(bounds.Left, bounds.Bottom)),
        CornerKind.BottomRight => (new Point(bounds.Left, bounds.Top), new Point(bounds.Right, bounds.Bottom)),
        _ => (default, default),
    };

    private void UpdateHoverCursor(MouseEventArgs e)
    {
        if (!IsSelectActive || ViewModel?.SelectedItem is not { } selected)
        {
            Cursor = null;
            return;
        }

        var point = e.GetPosition(this);
        var corner = GetCornerHit(selected, point);
        if (corner == CornerKind.None)
        {
            Cursor = null;
            return;
        }

        // Use a unit diagonal based on which corner is grabbed (independent of the item's
        // aspect ratio), then rotate it by the item's rotation so cursors stay diagonal on
        // corners regardless of width/height — only the rotation changes the cursor bucket.
        var unitDiagonal = corner switch
        {
            CornerKind.TopLeft => new Vector(-1, -1),
            CornerKind.TopRight => new Vector(1, -1),
            CornerKind.BottomLeft => new Vector(-1, 1),
            CornerKind.BottomRight => new Vector(1, 1),
            _ => new Vector(1, 1),
        };

        var m = selected.Transform.Matrix;
        var rotation = Math.Atan2(m.M12, m.M11);
        var cos = Math.Cos(rotation);
        var sin = Math.Sin(rotation);
        var rotated = new Vector(
            unitDiagonal.X * cos - unitDiagonal.Y * sin,
            unitDiagonal.X * sin + unitDiagonal.Y * cos);

        Cursor = DrawingMath.CursorForDiagonal(rotated);
    }

    private static bool IntersectsItem(DrawingItem item, Point worldPoint)
    {
        var inverse = item.Transform.Matrix;
        if (!inverse.HasInverse)
        {
            return false;
        }
        inverse.Invert();
        var localPoint = inverse.Transform(worldPoint);

        return item.HitTest(localPoint);
    }
}
