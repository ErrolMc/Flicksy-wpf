namespace Flicksy.VideoEditor.ViewModels;

/// <summary>
/// A single button entry in a <c>RailView</c>. <see cref="Tag"/> is the enum value the
/// rail selects (a <see cref="LeftRailTab"/> or <see cref="RightRailTab"/>); the rail
/// uses object equality to map back to its <c>SelectedTag</c> dependency property.
/// </summary>
public sealed class RailItem
{
    public required string Label { get; init; }

    public required string Glyph { get; init; }

    public required object Tag { get; init; }
}
