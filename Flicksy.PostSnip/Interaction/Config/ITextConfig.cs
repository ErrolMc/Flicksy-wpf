using System.Windows.Media;

namespace Flicksy.PostSnip.Interaction.Config;

/// <summary>
/// Per-tool settings consumed by the text tool (font / size / fill / outline of newly
/// created text items).
/// </summary>
public interface ITextConfig
{
    string TextFontFamily { get; }

    double TextFontSize { get; }

    Brush? TextFillBrush { get; }

    Brush? TextOutlineBrush { get; }

    double TextOutlineThickness { get; }
}
