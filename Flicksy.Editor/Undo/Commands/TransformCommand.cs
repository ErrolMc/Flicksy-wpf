using System.Windows.Media;
using Flicksy.Editor.Source;
using Flicksy.Editor.ViewModels;

namespace Flicksy.Editor.Undo.Commands;

public sealed class TransformCommand : IUndoableCommand
{
    private readonly DrawingViewModel _viewModel;
    private readonly DrawingItem _item;
    private readonly Matrix _before;
    private readonly Matrix _after;

    public TransformCommand(DrawingViewModel viewModel, DrawingItem item, Matrix before, Matrix after)
    {
        _viewModel = viewModel;
        _item = item;
        _before = before;
        _after = after;
    }

    public void Redo()
    {
        _item.Transform.Matrix = _after;
        _viewModel.SelectedItem = _item;
    }

    public void Undo()
    {
        _item.Transform.Matrix = _before;
        _viewModel.SelectedItem = _item;
    }
}
