using System;
using System.Windows;
using System.Windows.Input;

namespace Flicksy.Editor.Helpers;

/// <summary>
/// Stateless geometry helpers shared by the drawing/selection code.
/// </summary>
internal static class DrawingMath
{
    public static double DistanceSquared(Point a, Point b)
    {
        var dx = a.X - b.X;
        var dy = a.Y - b.Y;
        return (dx * dx) + (dy * dy);
    }

    public static double DistanceSquaredToSegment(Point point, Point start, Point end)
    {
        var vx = end.X - start.X;
        var vy = end.Y - start.Y;
        var lengthSquared = (vx * vx) + (vy * vy);
        if (lengthSquared <= double.Epsilon)
        {
            return DistanceSquared(point, start);
        }

        var t = ((point.X - start.X) * vx + (point.Y - start.Y) * vy) / lengthSquared;
        t = Math.Clamp(t, 0d, 1d);

        var closest = new Point(start.X + (t * vx), start.Y + (t * vy));
        return DistanceSquared(point, closest);
    }

    /// <summary>
    /// Picks the most appropriate resize cursor for the given diagonal direction vector
    /// (bidirectional). Buckets across the 4 cardinal/diagonal axes.
    /// </summary>
    public static Cursor CursorForDiagonal(Vector diagonal)
    {
        if (Math.Abs(diagonal.X) < double.Epsilon && Math.Abs(diagonal.Y) < double.Epsilon)
        {
            return Cursors.SizeNWSE;
        }

        var angle = Math.Atan2(diagonal.Y, diagonal.X);
        if (angle < 0) angle += Math.PI; // direction is bidirectional
        var deg = angle * 180.0 / Math.PI;

        // 4 buckets across [0, 180):
        //   [  0,  22.5) and [157.5, 180) → horizontal (WE)
        //   [ 22.5,  67.5) → top-left to bottom-right (NWSE)
        //   [ 67.5, 112.5) → vertical (NS)
        //   [112.5, 157.5) → top-right to bottom-left (NESW)
        if (deg < 22.5 || deg >= 157.5) return Cursors.SizeWE;
        if (deg < 67.5) return Cursors.SizeNWSE;
        if (deg < 112.5) return Cursors.SizeNS;
        return Cursors.SizeNESW;
    }
}
