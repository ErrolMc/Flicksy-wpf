namespace Flicksy.VideoEditor.Project;

/// <summary>
/// Categorizes a <see cref="Track"/> by what its clips contribute to the final composition.
/// Determines which clip subtypes are valid on the track and how the compositor layers it
/// (Video = base layers, Overlay = composited on top, Audio = mixed into the audio bus).
/// </summary>
public enum TrackKind
{
    Video,
    Overlay,
    Audio,
}
