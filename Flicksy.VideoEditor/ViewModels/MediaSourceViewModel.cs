using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using Flicksy.VideoEditor.Project;

namespace Flicksy.VideoEditor.ViewModels;

/// <summary>
/// View wrapper around a <see cref="MediaSource"/> for the media bin. Lets the panel bind
/// to a WPF <see cref="ImageSource"/> thumbnail without polluting the document model with
/// WPF types (Project POCOs hold no WPF types — see ADR 0002). The underlying
/// <see cref="Source"/> stays the source of truth for every model-level operation; the
/// wrapper exists solely to host the transient <see cref="Thumbnail"/>.
/// </summary>
public partial class MediaSourceViewModel : ObservableObject
{
    public MediaSourceViewModel(MediaSource source)
    {
        Source = source;
    }

    public MediaSource Source { get; }

    [ObservableProperty]
    private ImageSource? thumbnail;
}
