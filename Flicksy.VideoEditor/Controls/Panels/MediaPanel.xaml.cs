using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Flicksy.VideoEditor.ViewModels;

namespace Flicksy.VideoEditor.Controls.Panels;

/// <summary>
/// Media bin panel — the Media tab's left-panel content. Replaces the
/// <c>StubSurface</c> placeholder. Hosts a toolbar (Import) plus a
/// <see cref="WrapPanel"/> of bin cells over <see cref="MediaBinViewModel.MediaSources"/>,
/// with an empty-state hint when the project has no imported sources. Files dropped from
/// Windows Explorer onto the panel surface route through the same
/// <see cref="MediaBinViewModel.TryImportFiles"/> path so dedupe + probe-failure handling
/// stay uniform with the Import button. Right-click on a cell offers Reveal in Explorer;
/// rename / relocate / remove arrive in step 1b.
/// </summary>
public partial class MediaPanel : UserControl
{
    public MediaPanel()
    {
        InitializeComponent();
    }

    private void OnPanelDragOver(object sender, DragEventArgs e)
    {
        e.Effects = IsAcceptedFileDrop(e) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void OnPanelDrop(object sender, DragEventArgs e)
    {
        if (DataContext is not MediaBinViewModel vm) return;
        if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;

        if (e.Data.GetData(DataFormats.FileDrop) is string[] paths)
        {
            vm.TryImportFiles(paths);
        }
        e.Handled = true;
    }

    private static bool IsAcceptedFileDrop(DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return false;
        if (e.Data.GetData(DataFormats.FileDrop) is not string[] paths) return false;
        foreach (var path in paths)
        {
            if (MediaBinViewModel.IsAcceptedMediaPath(path)) return true;
        }
        return false;
    }

    // Click anywhere inside the panel that isn't a cell clears the bin selection
    // (toolbar, empty grid area, scrollbar, padding around cells). Clicks on a cell
    // bubble through unchanged — the ListBox handles the re-select naturally.
    private void OnPanelPreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is not MediaBinViewModel vm) return;
        if (vm.SelectedSource is null) return;
        if (e.OriginalSource is not DependencyObject d) return;
        if (FindAncestor<ListBoxItem>(d) is not null) return;

        vm.SelectedSource = null;
    }

    private static T? FindAncestor<T>(DependencyObject start) where T : DependencyObject
    {
        var current = start;
        while (current is not null)
        {
            if (current is T match) return match;
            current = VisualTreeHelper.GetParent(current) ?? LogicalTreeHelper.GetParent(current);
        }
        return null;
    }
}
