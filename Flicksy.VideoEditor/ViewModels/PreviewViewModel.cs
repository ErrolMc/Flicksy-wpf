using System;
using System.ComponentModel;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using Flicksy.VideoEditor.Composition;
using Flicksy.VideoEditor.Project;

namespace Flicksy.VideoEditor.ViewModels;

/// <summary>
/// State for the Preview surface. Drives the composited image displayed in
/// <c>PreviewView</c>: subscribes to <see cref="TransportViewModel.Playhead"/> and the
/// project's resolution settings, and on each change asks <see cref="ICompositor"/> for a
/// fresh frame. <see cref="CurrentFrame"/> is the frozen <c>WriteableBitmap</c> the view's
/// <c>&lt;Image Source="…"&gt;</c> binds to.
/// <para>
/// Threading: every PropertyChanged path that triggers <see cref="Render"/> originates on
/// the UI thread (Transport commands, Settings edits via the inspector), so the call into
/// <c>SkiaCompositor</c> stays on the UI thread — required for the
/// <c>GraphicsClip</c> render path that bounces through <c>RenderTargetBitmap</c>.
/// Off-UI playback is #11's problem.
/// </para>
/// </summary>
public partial class PreviewViewModel : ObservableObject
{
    private readonly Project.Project _project;
    private readonly TransportViewModel _transport;
    private readonly ICompositor _compositor;

    /// <summary>
    /// The most recently composited frame. <see cref="ImageSource"/> rather than
    /// <c>WriteableBitmap</c> so future backends could supply a different concrete type
    /// without touching the binding.
    /// </summary>
    [ObservableProperty]
    private ImageSource? currentFrame;

    public PreviewViewModel(Project.Project project, TransportViewModel transport, ICompositor compositor)
    {
        _project = project;
        _transport = transport;
        _compositor = compositor;
        ProjectSettings = project.Settings;

        _transport.PropertyChanged += OnTransportPropertyChanged;
        _project.Settings.PropertyChanged += OnSettingsPropertyChanged;

        // Render once so CurrentFrame is non-null at construction — the view's
        // Stretch=Uniform needs a sized source to letterbox correctly even on an empty
        // project (SkiaCompositor clears to black, so the first frame is a black fill at
        // project resolution).
        Render();
    }

    public ProjectSettings ProjectSettings { get; }

    private void OnTransportPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(TransportViewModel.Playhead))
        {
            Render();
        }
    }

    private void OnSettingsPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ProjectSettings.ResolutionWidth)
            || e.PropertyName == nameof(ProjectSettings.ResolutionHeight))
        {
            Render();
        }
    }

    private void Render()
    {
        try
        {
            var frame = _compositor.RenderFrame(_project, _transport.Playhead);
            CurrentFrame = frame.Image;
        }
        catch (Exception ex)
        {
            // Compositor failures shouldn't crash the editor — log and leave the
            // previous frame on-screen. Production-grade surfacing lands with #11's
            // playback loop, which needs a proper status channel anyway.
            System.Diagnostics.Debug.WriteLine($"PreviewViewModel.Render failed: {ex}");
        }
    }
}
