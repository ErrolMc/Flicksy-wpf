using CommunityToolkit.Mvvm.ComponentModel;

namespace Flicksy.VideoEditor.Project;

/// <summary>
/// Project-wide rendering parameters. <see cref="Framerate"/> defines the frame grid that
/// all timeline positions and clip durations integer-quantize to (per ADR 0002). The
/// resolution/sample-rate fields define the compositor's output canvas and audio bus.
/// Changing <see cref="Framerate"/> after clips exist is a lossy remap — out of scope here,
/// handled in a later slice.
/// </summary>
public partial class ProjectSettings : ObservableObject
{
    [ObservableProperty]
    private int framerate = 30;

    [ObservableProperty]
    private int resolutionWidth = 1920;

    [ObservableProperty]
    private int resolutionHeight = 1080;

    [ObservableProperty]
    private int audioSampleRate = 48000;
}
