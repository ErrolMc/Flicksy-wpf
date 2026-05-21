using System.Windows;
using Flicksy.Editor.ViewModels;

namespace Flicksy.Editor.Undo.Commands;

public sealed class CropCommand : IUndoableCommand
{
    private readonly CropOverlayViewModel _viewModel;
    private readonly Rect _before;
    private readonly Rect _after;

    public CropCommand(CropOverlayViewModel viewModel, Rect before, Rect after)
    {
        _viewModel = viewModel;
        _before = before;
        _after = after;
    }

    public void Redo()
    {
        _viewModel.ApplyCommittedCrop(_after);
    }

    public void Undo()
    {
        _viewModel.ApplyCommittedCrop(_before);
    }
}
