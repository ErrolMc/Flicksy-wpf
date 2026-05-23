using System.Windows.Media;
using Flicksy.Drawing.Source;
using Flicksy.Drawing.ViewModels;

namespace Flicksy.Drawing.Undo.Commands;

public readonly record struct TextStyleSnapshot(
    string FontFamily,
    double FontSize,
    Brush? Fill,
    Brush? Outline,
    double OutlineThickness)
{
    public static TextStyleSnapshot Capture(TextItem item) => new(
        item.FontFamily,
        item.FontSize,
        item.Fill,
        item.Outline,
        item.OutlineThickness);

    public void ApplyTo(TextItem item)
    {
        item.SetFontFamily(FontFamily);
        item.SetFontSize(FontSize);
        item.SetFill(Fill);
        item.SetOutline(Outline, OutlineThickness);
    }
}

public sealed class TextStyleCommand : IUndoableCommand
{
    private readonly DrawingViewModel _viewModel;
    private readonly TextItem _item;
    private readonly TextStyleSnapshot _before;
    private readonly TextStyleSnapshot _after;

    public TextStyleCommand(DrawingViewModel viewModel, TextItem item, TextStyleSnapshot before, TextStyleSnapshot after)
    {
        _viewModel = viewModel;
        _item = item;
        _before = before;
        _after = after;
    }

    public void Redo()
    {
        _after.ApplyTo(_item);
        _viewModel.SelectedItem = _item;
    }

    public void Undo()
    {
        _before.ApplyTo(_item);
        _viewModel.SelectedItem = _item;
    }
}
