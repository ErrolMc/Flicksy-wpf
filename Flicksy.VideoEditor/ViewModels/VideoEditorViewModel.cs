using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Flicksy.VideoEditor.Project;

namespace Flicksy.VideoEditor.ViewModels;

/// <summary>
/// Root view-model for the video editor shell. Owns the document <see cref="Project"/>
/// plus the per-surface sub-VMs (<see cref="Preview"/>, <see cref="Transport"/>,
/// <see cref="Timeline"/>, <see cref="Inspector"/>, <see cref="MediaBin"/>) and the shell
/// UI state (selection, panel open/closed, rail tab). Undo/redo commands are no-ops in
/// this slice; they exist so the shell's <c>Ctrl+Z</c>/<c>Ctrl+Y</c> input bindings have
/// something to invoke until the timeline-edit undo stack lands in #12.
/// </summary>
public partial class VideoEditorViewModel : ObservableObject
{
    [ObservableProperty]
    private string projectName = "Untitled Project";

    [ObservableProperty]
    private Clip? selectedClip;

    [ObservableProperty]
    private LeftRailTab currentLeftTab = LeftRailTab.Media;

    [ObservableProperty]
    private RightRailTab currentRightTab = RightRailTab.Speed;

    [ObservableProperty]
    private bool isLeftPanelOpen = true;

    // Right panel starts closed — its tabs are clip-scoped and there's no selection yet.
    [ObservableProperty]
    private bool isRightPanelOpen;

    public VideoEditorViewModel(Project.Project project)
    {
        Project = project;
        Preview = new PreviewViewModel(project);
        Transport = new TransportViewModel(project);
        Timeline = new TimelineViewModel();
        Inspector = new InspectorViewModel();
        MediaBin = new MediaBinViewModel();

        LeftRailItems = new[]
        {
            new RailItem { Label = "Media", Glyph = "M", Tag = LeftRailTab.Media },
            new RailItem { Label = "Text", Glyph = "T", Tag = LeftRailTab.Text },
            new RailItem { Label = "Shapes", Glyph = "S", Tag = LeftRailTab.Shapes },
            new RailItem { Label = "Pen", Glyph = "P", Tag = LeftRailTab.Pen },
            new RailItem { Label = "Transitions", Glyph = "Tr", Tag = LeftRailTab.Transitions },
        };

        RightRailItems = new[]
        {
            new RailItem { Label = "Speed", Glyph = "Sp", Tag = RightRailTab.Speed },
            new RailItem { Label = "Audio", Glyph = "Au", Tag = RightRailTab.Audio },
            new RailItem { Label = "Adjust colors", Glyph = "Co", Tag = RightRailTab.AdjustColors },
            new RailItem { Label = "Filters", Glyph = "Fi", Tag = RightRailTab.Filters },
            new RailItem { Label = "Fade", Glyph = "Fa", Tag = RightRailTab.Fade },
        };
    }

    public Project.Project Project { get; }

    public PreviewViewModel Preview { get; }

    public TransportViewModel Transport { get; }

    public TimelineViewModel Timeline { get; }

    public InspectorViewModel Inspector { get; }

    public MediaBinViewModel MediaBin { get; }

    public IReadOnlyList<RailItem> LeftRailItems { get; }

    public IReadOnlyList<RailItem> RightRailItems { get; }

    [RelayCommand]
    private void Undo()
    {
        // No-op in this slice. Real undo stack lands in #12 (timeline editing).
    }

    [RelayCommand]
    private void Redo()
    {
    }

    [RelayCommand]
    private void Export()
    {
        // No-op in this slice. Real exporter lands in #20.
    }
}
