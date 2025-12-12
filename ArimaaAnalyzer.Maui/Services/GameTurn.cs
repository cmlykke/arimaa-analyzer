using System.Collections.Generic;
using System.Collections.ObjectModel;
using ArimaaAnalyzer.Maui.Services;

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
    public Sides Side { get; }

    /// <summary>
    /// The list of individual moves composing this turn.
    /// </summary>
    public IReadOnlyList<string> Moves { get; }

    /// <summary>
    /// True if this node belongs to the main line. Main-line nodes must not have a non-main-line parent.
    /// </summary>
    public bool IsMainLine { get; }

    public string AEIstring { get; }
    
    /// <summary>
    /// Child turns representing continuations/variations from this position.
    /// The first child can be considered the main line; additional children are alternatives.
    /// </summary>
    public List<GameTurn> Children { get; } = new();

    /// <summary>
    /// Optional reference to the parent turn (null for root-level turns).
    /// </summary>
    public GameTurn? Parent { get; private set; }

    public GameTurn(string oldAEIstring, string MoveNumber, Sides Side, IReadOnlyList<string> Moves, bool isMainLine = true)
    {
        this.MoveNumber = MoveNumber;
        this.Side = Side;
        this.Moves = Moves ?? new ReadOnlyCollection<string>(new List<string>());
        this.IsMainLine = isMainLine;
        this.AEIstring =
            NotationService.GamePlusMovesToAei(oldAEIstring, Moves);
    }

    /// <summary>
    /// Adds a child turn and sets its Parent reference.
    /// </summary>
    public void AddChild(GameTurn child)
    {
        if (child == null) return;
        // Enforce: Any main-line node cannot have a non-mainline parent.
        // i.e., if the child is main-line, its parent (this) must also be main-line.
        if (child.IsMainLine && !this.IsMainLine)
            throw new InvalidOperationException("A main-line node cannot be attached under a non-main-line parent.");

        child.Parent = this;
        Children.Add(child);
    }
}
