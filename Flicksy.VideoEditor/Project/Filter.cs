namespace Flicksy.VideoEditor.Project;

/// <summary>
/// Marker base for per-clip filters (e.g. color correction, blur, LUTs). Held by
/// <see cref="MediaClip.Filters"/> as an ordered chain applied during compositing.
/// </summary>
public abstract class Filter
{
}
