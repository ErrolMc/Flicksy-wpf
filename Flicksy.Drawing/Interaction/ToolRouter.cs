using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;

namespace Flicksy.Drawing.Interaction;

/// <summary>
/// Dispatches pointer events to the tool the user has currently selected, or — when a
/// gesture is in progress — to whichever tool is mid-gesture, so move/up always reach the
/// same tool that received the down event.
/// </summary>
public sealed class ToolRouter
{
    private readonly List<IDrawingTool> _tools = new();
    private readonly Func<IDrawingTool?> _selectActiveTool;

    public ToolRouter(Func<IDrawingTool?> selectActiveTool)
    {
        _selectActiveTool = selectActiveTool ?? throw new ArgumentNullException(nameof(selectActiveTool));
    }

    /// <summary>
    /// Registers a tool. Tools are stored in registration order and inspected via
    /// <see cref="IDrawingTool.IsActive"/> to find the in-progress one during move/up routing.
    /// </summary>
    public void Register(IDrawingTool tool)
    {
        if (tool is null) throw new ArgumentNullException(nameof(tool));
        _tools.Add(tool);
    }

    /// <summary>
    /// Drops all registered tools. Use when the host rebuilds its tool instances (e.g. on a
    /// view-model swap) so the registration list doesn't accumulate stale entries.
    /// </summary>
    public void ClearRegistrations()
    {
        _tools.Clear();
    }

    /// <summary>
    /// True while any registered tool is mid-gesture. Hosts use this to decide whether to
    /// forward move/up to the router or fall through to host-owned (not-yet-extracted) logic.
    /// </summary>
    public bool HasActiveGesture => _tools.Any(t => t.IsActive);

    public bool OnPointerDown(System.Windows.Point point, MouseButtonEventArgs e)
    {
        var tool = ActiveOrSelected();
        return tool is not null && tool.OnPointerDown(point, e);
    }

    public void OnPointerMove(System.Windows.Point point, MouseEventArgs e)
    {
        var tool = ActiveOrSelected();
        tool?.OnPointerMove(point, e);
    }

    public void OnPointerUp(System.Windows.Point point, MouseButtonEventArgs e)
    {
        var tool = ActiveOrSelected();
        tool?.OnPointerUp(point, e);
    }

    public void OnPointerHover(System.Windows.Point point, MouseEventArgs e)
    {
        // Hover routes to the currently *selected* tool only; an in-progress gesture would
        // never be in hover state by definition.
        _selectActiveTool()?.OnPointerHover(point, e);
    }

    private IDrawingTool? ActiveOrSelected()
    {
        // Prefer whichever tool is mid-gesture, so a Move/Up that started on a tool still
        // reaches that tool even if the user clicked a different tool button in between.
        return _tools.FirstOrDefault(t => t.IsActive) ?? _selectActiveTool();
    }
}
