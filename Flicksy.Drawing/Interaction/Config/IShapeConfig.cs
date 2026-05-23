using System.Windows.Media;
using Flicksy.Drawing.Source;

namespace Flicksy.Drawing.Interaction.Config;

/// <summary>
/// Per-tool settings consumed by the shape tool (square / circle / line / arrow).
/// </summary>
public interface IShapeConfig
{
    ShapeKind ActiveShape { get; }

    Brush? FillBrush { get; }

    Brush? OutlineBrush { get; }

    double OutlineThickness { get; }
}
