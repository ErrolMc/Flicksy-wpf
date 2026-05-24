namespace Flicksy.VideoEditor.Project;

/// <summary>
/// Identifies which blend the compositor should run across the overlap region of a
/// <see cref="Transition"/>. The minimal v1 set; richer transitions land later.
/// </summary>
public enum TransitionType
{
    Crossfade,
    FadeToBlack,
    FadeFromBlack,
    WipeLeft,
    WipeRight,
}
