using System.Collections.Generic;
using Flicksy.Editor.Source;
using Flicksy.Editor.ViewModels;

namespace Flicksy.Editor.Undo.Commands;

/// <summary>
/// Bundles several commands into one undo step. Used for batched gestures like drag-erase
/// where many <see cref="RemoveItemCommand"/>s accumulate during a single mouse drag.
///
/// <para>
/// Selection is preserved across the bundle — the manager snapshots <c>SelectedItem</c>
/// before invoking children and restores it afterward. This stops inner commands from
/// leaving the canvas with a per-step selection that wasn't part of the user's gesture.
/// </para>
/// </summary>
public sealed class CompositeCommand : IUndoableCommand
{
    private readonly DrawingViewModel _viewModel;
    private readonly IReadOnlyList<IUndoableCommand> _children;

    public CompositeCommand(DrawingViewModel viewModel, IReadOnlyList<IUndoableCommand> children)
    {
        _viewModel = viewModel;
        _children = children;
    }

    public int Count => _children.Count;

    public void Redo()
    {
        DrawingItem? selectionBefore = _viewModel.SelectedItem;
        foreach (var child in _children)
        {
            child.Redo();
        }
        _viewModel.SelectedItem = ResolveSelectionAfter(selectionBefore);
    }

    public void Undo()
    {
        DrawingItem? selectionBefore = _viewModel.SelectedItem;
        for (var i = _children.Count - 1; i >= 0; i--)
        {
            _children[i].Undo();
        }
        _viewModel.SelectedItem = ResolveSelectionAfter(selectionBefore);
    }

    private DrawingItem? ResolveSelectionAfter(DrawingItem? selectionBefore)
    {
        // Only restore the prior selection if it's still in the collection — children may
        // have inserted or removed it.
        if (selectionBefore is null)
        {
            return null;
        }
        return _viewModel.Items.Contains(selectionBefore) ? selectionBefore : null;
    }
}
