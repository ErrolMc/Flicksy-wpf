using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using Flicksy.VideoEditor.Project;

namespace Flicksy.VideoEditor.ViewModels;

/// <summary>
/// View wrapper around a <see cref="MediaSource"/> for the media bin. Lets the panel bind
/// to a WPF <see cref="ImageSource"/> thumbnail without polluting the document model with
/// WPF types (Project POCOs hold no WPF types — see ADR 0002). The underlying
/// <see cref="Source"/> stays the source of truth for every model-level operation; the
/// wrapper exists solely to host transient view-state: the <see cref="Thumbnail"/> and
/// the inline-rename buffer (<see cref="IsEditing"/> / <see cref="EditingName"/>).
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

    // Inline-rename state. Flipped to true by MediaBinViewModel.BeginRename; the cell's
    // DataTemplate swaps TextBlock → TextBox on this property. EditingName is the buffer
    // the TextBox is bound to TwoWay — committed onto Source.DisplayName by CommitRename,
    // discarded by CancelRename.
    [ObservableProperty]
    private bool isEditing;

    [ObservableProperty]
    private string editingName = string.Empty;
}
