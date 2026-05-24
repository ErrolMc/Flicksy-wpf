namespace Flicksy.VideoEditor;

public abstract record StartupMode
{
    public sealed record Welcome : StartupMode;

    public sealed record EmptyEditor : StartupMode;

    public sealed record EditorWithSource(string Path) : StartupMode;
}
