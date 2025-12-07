using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace YourApp.Models;

/// <summary>
/// Represents a single parsed turn in an Arimaa game as a node in a tree.
/// This allows for variations by attaching alternative child turns.
/// </summary>
public class GameTurn
{
    /// <summary>
    /// Move number of this turn (e.g., "1", "2", ...).
    /// </summary>
    public string MoveNumber { get; }

    /// <summary>
    /// Side that played this turn: "w" (gold/white) or "b" (silver/black).
    /// </summary>
    public string Side { get; }

    /// <summary>
    /// The list of individual moves composing this turn.
    /// </summary>
    public IReadOnlyList<string> Moves { get; }

    /// <summary>
    /// Child turns representing continuations/variations from this position.
    /// The first child can be considered the main line; additional children are alternatives.
    /// </summary>
    public List<GameTurn> Children { get; } = new();

    /// <summary>
    /// Optional reference to the parent turn (null for root-level turns).
    /// </summary>
    public GameTurn? Parent { get; private set; }

    public GameTurn(string MoveNumber, string Side, IReadOnlyList<string> Moves)
    {
        this.MoveNumber = MoveNumber;
        this.Side = Side;
        this.Moves = Moves ?? new ReadOnlyCollection<string>(new List<string>());
    }

    /// <summary>
    /// Adds a child turn and sets its Parent reference.
    /// </summary>
    public void AddChild(GameTurn child)
    {
        if (child == null) return;
        child.Parent = this;
        Children.Add(child);
    }
}
