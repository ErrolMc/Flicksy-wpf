using Flicksy.PostSnip.Source;
using Flicksy.PostSnip.ViewModels;

namespace Flicksy.PostSnip.Undo.Commands;

public sealed class MoveLayerCommand : IUndoableCommand
{
    private readonly DrawingViewModel _viewModel;
    private readonly DrawingItem _item;
    private readonly int _oldIndex;
    private readonly int _newIndex;

    public MoveLayerCommand(DrawingViewModel viewModel, DrawingItem item, int oldIndex, int newIndex)
    {
        _viewModel = viewModel;
        _item = item;
        _oldIndex = oldIndex;
        _newIndex = newIndex;
    }

    public void Redo()
    {
        var currentIndex = _viewModel.Items.IndexOf(_item);
        if (currentIndex < 0 || currentIndex == _newIndex)
        {
            return;
        }
        _viewModel.Items.Move(currentIndex, _newIndex);
        _viewModel.SelectedItem = _item;
    }

    public void Undo()
    {
        var currentIndex = _viewModel.Items.IndexOf(_item);
        if (currentIndex < 0 || currentIndex == _oldIndex)
        {
            return;
        }
        _viewModel.Items.Move(currentIndex, _oldIndex);
        _viewModel.SelectedItem = _item;
    }
}
