using System.Collections.Generic;
using YourApp.Models;
using ArimaaAnalyzer.Maui.Services; // for Sides

namespace ArimaaAnalyzer.Maui.Services.Arimaa;

// Simple stateful service to drive the UI. Keeps a selected square and exposes move methods.
public sealed class ArimaaGameService
{
    public ArimaaGameService(GameState state)
    {
        State = state ?? throw new ArgumentNullException(nameof(state));
    }

    public GameState State { get; private set; }

    public GameTurn? CurrentNode { get; private set; }

    // Snapshot of the state when the current node was loaded; used to compute pending move(s)
    private GameState? _snapshotAtLoad;

    public Position? Selected { get; private set; }

    // Controls whether the board component renders outer UI around the inner 8x8 grid.
    // When false, the outer size matches the inner board size.
    public bool ShowOuterUi { get; set; } = false;

    public void Select(Position p)
    {
        if (!p.IsOnBoard) return;
        var piece = State.GetPiece(p);
        if (piece is not null)
        {
            Selected = p;
        }
    }

    // Drag-and-drop entry point: move directly from a known origin to target.
    public bool TryMove(Position from, Position to)
    {
        if (!from.IsOnBoard || !to.IsOnBoard) return false;
        var success = State.TryMove(from, to);
        if (success)
        {
            // After a successful user move, apply trap captures via CorrectMoveService
            CorrectMoveService.ApplyTrapCaptures(State);
        }
        return success;
    }

    public void ClearSelection() => Selected = null;

    // Load a GameTurn node and update the underlying GameState accordingly
    public void Load(GameTurn node)
    {
        if (node == null) throw new ArgumentNullException(nameof(node));
        CurrentNode = node;
        State = new GameState(node);
        // Refresh snapshot for pending-move computation
        _snapshotAtLoad = new GameState(node);
        var test = "test";
    }

    public bool CanPrev => CurrentNode?.Parent is not null;
    public bool CanNext => CurrentNode?.Children is { Count: > 0 };

    public void GoPrev()
    {
        if (CurrentNode?.Parent is { } p)
        {
            Load(p);
        }
    }

    public void GoNextMainLine()
    {
        if (CurrentNode is null) return;
        var next = CurrentNode.Children.FirstOrDefault(c => c.IsMainLine) ?? CurrentNode.Children.FirstOrDefault();
        if (next != null)
        {
            Load(next);
        }
    }

    // Indicates whether the board has diverged from the loaded node (i.e., there is a pending move to commit)
    public bool CanCommitMove => CurrentNode is not null && _snapshotAtLoad is not null &&
                                 !string.Equals(_snapshotAtLoad.localAeiSetPosition, State.localAeiSetPosition, StringComparison.Ordinal);

    // Create a new non-mainline child node from the pending move(s) and move the current position to that child
    public bool CommitMove()
    {
        if (!CanCommitMove || CurrentNode is null || _snapshotAtLoad is null) return false;

        // Compute the official move notation between the snapshot and the current state
        var sideToMove = _snapshotAtLoad.SideToMove;
        var notation = CorrectMoveService.ComputeMoveSequence(_snapshotAtLoad, State, sideToMove);
        if (string.IsNullOrWhiteSpace(notation) || notation == "error")
        {
            return false;
        }

        // Determine side in Sides enum
        var sidesEnum = sideToMove == Side.Gold ? Sides.Gold : Sides.Silver;

        // Determine the move number string
        var moveNumberStr = CurrentNode.MoveNumber;
        if (int.TryParse(CurrentNode.MoveNumber, out var curNum))
        {
            // Increment when a new Gold move starts a new full number (assuming Silver completes the previous)
            // Heuristic: if side to move is Gold, increment; else keep same number
            var newNum = sideToMove == Side.Gold ? curNum + 1 : curNum;
            moveNumberStr = newNum.ToString();
        }

        // Build child turn with IsMainLine = false
        var child = new GameTurn(CurrentNode.AEIstring, moveNumberStr, sidesEnum, new List<string> { notation }, isMainLine: false);
        CurrentNode.AddChild(child);

        // Load the newly created variation node
        Load(child);
        return true;
    }
}
