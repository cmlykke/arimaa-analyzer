namespace ArimaaAnalyzer.Maui.Services.Arimaa;

// Simple stateful service to drive the UI. Keeps a selected square and exposes move methods.
public sealed class ArimaaGameService
{
    public GameState State { get; } = new();

    public Position? Selected { get; private set; }

    public void Select(Position p)
    {
        if (!p.IsOnBoard) return;
        var piece = State.GetPiece(p);
        if (piece is not null)
        {
            Selected = p;
        }
    }

    public bool TryMoveTo(Position target)
    {
        if (Selected is null) return false;
        var from = Selected.Value;
        var success = State.TryMove(from, target);
        if (success)
        {
            Selected = null;
        }
        return success;
    }

    // Drag-and-drop entry point: move directly from a known origin to target.
    public bool TryMove(Position from, Position to)
    {
        if (!from.IsOnBoard || !to.IsOnBoard) return false;
        return State.TryMove(from, to);
    }

    public void ClearSelection() => Selected = null;
}
