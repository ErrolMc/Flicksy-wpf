using Flicksy.Editor.Source;
using Flicksy.Editor.ViewModels;

namespace Flicksy.Editor.Undo.Commands;

public sealed class TextEditCommand : IUndoableCommand
{
    private readonly DrawingViewModel _viewModel;
    private readonly TextItem _item;
    private readonly string _oldText;
    private readonly string _newText;

    public TextEditCommand(DrawingViewModel viewModel, TextItem item, string oldText, string newText)
    {
        _viewModel = viewModel;
        _item = item;
        _oldText = oldText;
        _newText = newText;
    }

    public void Redo()
    {
        _item.SetText(_newText);
        _viewModel.SelectedItem = _item;
    }

    public void Undo()
    {
        _item.SetText(_oldText);
        _viewModel.SelectedItem = _item;
    }
}
