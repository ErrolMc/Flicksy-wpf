namespace Flicksy.Drawing.Undo;

/// <summary>
/// A user-visible edit that can be reversed and re-applied. Commands are assumed to be
/// pushed onto the undo stack <em>after</em> the change has already taken effect (gestures
/// mutate state as they happen), so <see cref="Redo"/> is only invoked when the user steps
/// forward through the redo stack.
/// </summary>
public interface IUndoableCommand
{
    void Redo();
    void Undo();
}
