using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Flicksy.VideoEditor.Project;

/// <summary>
/// Root of the video editor's document model — what a future .flicksy file will serialize
/// to. Holds project-wide <see cref="Settings"/> and the ordered collection of
/// <see cref="Tracks"/>. Use <see cref="CreateEmpty"/> for a fresh project with defaults
/// and a standard 4-track layout, or <see cref="CreateStub"/> for an in-memory project
/// pre-populated with sample clips for layout/render-pipeline development.
/// </summary>
public partial class Project : ObservableObject
{
    public ProjectSettings Settings { get; } = new();

    public ObservableCollection<Track> Tracks { get; } = new();

    public static Project CreateEmpty()
    {
        var project = new Project();
        project.Tracks.Add(new Track { Kind = TrackKind.Video, Name = "Video 1" });
        project.Tracks.Add(new Track { Kind = TrackKind.Video, Name = "Video 2" });
        project.Tracks.Add(new Track { Kind = TrackKind.Overlay, Name = "Overlay" });
        project.Tracks.Add(new Track { Kind = TrackKind.Audio, Name = "Audio" });
        return project;
    }

    public static Project CreateStub()
    {
        var project = CreateEmpty();
        var framerate = project.Settings.Framerate;

        var videoTrack = project.Tracks[0];
        videoTrack.Clips.Add(new MediaClip
        {
            SourcePath = @"C:\fake\clipA.mp4",
            SourceIn = TimeSpan.Zero,
            SourceOut = TimeSpan.FromSeconds(3),
            Framerate = framerate,
            TimelineStart = 0,
        });
        videoTrack.Clips.Add(new MediaClip
        {
            SourcePath = @"C:\fake\clipB.mp4",
            SourceIn = TimeSpan.Zero,
            SourceOut = TimeSpan.FromSeconds(2),
            Framerate = framerate,
            TimelineStart = 90,
        });

        var overlayTrack = project.Tracks[2];
        overlayTrack.Clips.Add(new GraphicsClip
        {
            DurationFrames = 60,
            TimelineStart = 30,
        });

        return project;
    }
}
