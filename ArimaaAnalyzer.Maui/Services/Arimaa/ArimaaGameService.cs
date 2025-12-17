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

    // Raised whenever the CurrentNode changes (e.g., navigation or commit)
    public event Action? CurrentNodeChanged;

    // Snapshots of the state across user-made steps (index 0 = state when the node was loaded).
    // Each successful single-step user move appends a new snapshot.
    private List<GameState>? _snapshots;

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
            OnStateMutated();
        }
        return success;
    }

    public void ClearSelection() => Selected = null;

    // Load a GameTurn node and update the underlying GameState accordingly
    public void Load(GameTurn node)
    {
        if (node == null) throw new ArgumentNullException(nameof(node));
        CurrentNode = node;
        // Preserve current board orientation across loads so UI rotation stays consistent
        // Default to canonical orientation: GoldSouth (bottom) vs SilverNorth (top)
        var orientation = State?.boardorientation ?? BoardOrientation.GoldSouthSilverNorth;
        State = new GameState(node);
        State.boardorientation = orientation;
        // Initialize snapshots for pending-move computation (index 0 = loaded state)
        _snapshots = new List<GameState>
        {
            new GameState(node) { boardorientation = orientation }
        };
        var test = "test";

        // Notify listeners that the active node has changed
        CurrentNodeChanged?.Invoke();
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
    public bool CanCommitMove => CurrentNode is not null && _snapshots is { Count: >= 2 } &&
                                 !string.Equals(_snapshots[0].localAeiSetPosition, State.localAeiSetPosition, StringComparison.Ordinal);

    // Alias for UI clarity: we can reset whenever we have uncommitted changes
    public bool CanResetPendingMoves => CanCommitMove;

    // Human-readable notation of the pending move sequence (null when none)
    public string? PendingMoveText
    {
        get
        {
            if (!CanResetPendingMoves || _snapshots is null || _snapshots.Count < 2) return null;
            var notation = CorrectMoveService.ComputeMoveSequenceFromSnapshots(_snapshots);
            if (string.IsNullOrWhiteSpace(notation.Item1) || notation.Item2 == "error") return null;
            return notation.Item1;
        }
    }

    // Create a new non-mainline child node from the pending move(s) and move the current position to that child
    public bool CommitMove()
    {
        if (!CanCommitMove || CurrentNode is null || _snapshots is null) return false;

        // Compute the official move notation based on the accumulated snapshots
        var sideToMove = _snapshots[0].SideToMove;
        var notation = CorrectMoveService.ComputeMoveSequenceFromSnapshots(_snapshots);
        if (string.IsNullOrWhiteSpace(notation.Item1) || notation.Item2 == "error")
        {
            return false;
        }

        // Determine side in Sides enum
        var sidesEnum = sideToMove == Sides.Gold ? Sides.Gold : Sides.Silver;

        // Determine the move number string
        var moveNumberStr = CurrentNode.MoveNumber;
        if (int.TryParse(CurrentNode.MoveNumber, out var curNum))
        {
            // Increment when a new Gold move starts a new full number (assuming Silver completes the previous)
            // Heuristic: if side to move is Gold, increment; else keep same number
            var newNum = sideToMove == Sides.Gold ? curNum + 1 : curNum;
            moveNumberStr = newNum.ToString();
        }

        // Build child turn with IsMainLine = false
        var child = new GameTurn(CurrentNode.AEIstring, notation.Item2, moveNumberStr, sidesEnum, new List<string> { notation.Item1 }, isMainLine: false);
        CurrentNode.AddChild(child);

        // Load the newly created variation node
        Load(child);
        return true;
    }

    // Revert the board to the state when the current node was loaded (discard uncommitted moves)
    public void ResetPendingMoves()
    {
        if (CurrentNode is null || _snapshots is null || _snapshots.Count == 0) return;

        // Preserve current orientation while restoring the loaded snapshot
        var orientation = State?.boardorientation ?? BoardOrientation.GoldSouthSilverNorth;
        State = new GameState(_snapshots[0].localAeiSetPosition)
        {
            boardorientation = orientation
        };

        // Reset snapshots back to the initial state only
        _snapshots = new List<GameState> { new GameState(State.localAeiSetPosition) { boardorientation = orientation } };

        // Clear any UI selection
        Selected = null;

        // Notify listeners that state (and thus pending moves) changed
        StateChanged?.Invoke();
    }

    // Rotate the board orientation clockwise through the defined enum values
    public void RotateBoardClockwise()
    {
        var current = State.boardorientation;
        BoardOrientation next = current switch
        {
            BoardOrientation.GoldSouthSilverNorth => BoardOrientation.GoldWestSilverEast,
            BoardOrientation.GoldWestSilverEast => BoardOrientation.GoldNorthSilverSouth,
            BoardOrientation.GoldNorthSilverSouth => BoardOrientation.GoldEastSilverWest,
            _ => BoardOrientation.GoldSouthSilverNorth
        };
        State.boardorientation = next;
    }

    // Raised when the State changes without changing the CurrentNode (e.g., user makes or resets pending moves)
    public event Action? StateChanged;

    // Internal helper to raise StateChanged after successful state mutation
    private void OnStateMutated()
    {
        // Append a new snapshot capturing the current state after mutation
        if (_snapshots is not null)
        {
            var orientation = State?.boardorientation ?? BoardOrientation.GoldSouthSilverNorth;
            _snapshots.Add(new GameState(State.localAeiSetPosition) { boardorientation = orientation });
        }
        StateChanged?.Invoke();
    }

    // (No other duplicate TryMove definitions)
}
