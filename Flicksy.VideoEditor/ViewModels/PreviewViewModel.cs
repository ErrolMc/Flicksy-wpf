using CommunityToolkit.Mvvm.ComponentModel;
using Flicksy.VideoEditor.Project;

namespace Flicksy.VideoEditor.ViewModels;

/// <summary>
/// State for the Preview surface. Exposes the project's <see cref="ProjectSettings"/> so
/// the view can aspect-lock to <see cref="ProjectSettings.ResolutionWidth"/> /
/// <see cref="ProjectSettings.ResolutionHeight"/>. Future home for the rendered-frame
/// <c>ImageSource</c> the compositor writes (#10/#11).
/// </summary>
public partial class PreviewViewModel : ObservableObject
{
    public PreviewViewModel(Project.Project project)
    {
        ProjectSettings = project.Settings;
    }

    public ProjectSettings ProjectSettings { get; }
}
