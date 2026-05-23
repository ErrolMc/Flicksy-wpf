using System.Windows;

namespace Flicksy.Drawing.Interaction;

/// <summary>
/// Single-pole exponential moving-average smoother for pointer input. Used by stroke-style
/// tools to take the edge off raw mouse jitter while still tracking the cursor closely.
///
/// <para>
/// State (<c>_smoothedPoint</c>) lives on the instance — one smoother per in-progress
/// gesture. Call <see cref="Seed"/> at gesture start to anchor the running average on the
/// first sample, <see cref="Smooth"/> on each subsequent sample, and <see cref="Reset"/>
/// when the gesture ends so the next gesture starts fresh.
/// </para>
/// </summary>
public sealed class InputSmoothing
{
    private const double DefaultAlpha = 0.5d;

    private readonly double _alpha;
    private Point? _smoothedPoint;

    public InputSmoothing()
        : this(DefaultAlpha) { }

    public InputSmoothing(double alpha)
    {
        _alpha = alpha;
    }

    /// <summary>
    /// Anchors the running average at the given point so the first <see cref="Smooth"/> call
    /// blends against this seed instead of treating the new sample as the start of the stroke.
    /// </summary>
    public void Seed(Point point)
    {
        _smoothedPoint = point;
    }

    /// <summary>
    /// Blends the new sample with the running average and returns the smoothed point. If no
    /// previous point exists (no seed and first call), the raw sample is returned and stored.
    /// </summary>
    public Point Smooth(Point raw)
    {
        if (_smoothedPoint is not Point previous)
        {
            _smoothedPoint = raw;
            return raw;
        }

        var smoothed = new Point(
            (_alpha * raw.X) + ((1d - _alpha) * previous.X),
            (_alpha * raw.Y) + ((1d - _alpha) * previous.Y));
        _smoothedPoint = smoothed;
        return smoothed;
    }

    /// <summary>
    /// Clears the running average. Call at the end of a gesture.
    /// </summary>
    public void Reset()
    {
        _smoothedPoint = null;
    }
}
