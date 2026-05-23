using System.Windows.Media;

namespace Flicksy.PostSnip.Interaction.Config;

/// <summary>
/// Per-tool settings consumed by the pen tool. Implemented by the host (e.g.
/// <c>DrawingView</c>) so the tool can read the user's current pen colour/thickness without
/// taking a hard dependency on the host control type.
/// </summary>
public interface IPenConfig
{
    Brush StrokeBrush { get; }

    double StrokeThickness { get; }
}
