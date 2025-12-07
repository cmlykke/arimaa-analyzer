using System.Collections.Generic;

namespace YourApp.Models;

/// <summary>
/// Represents a single parsed turn in an Arimaa game.
/// </summary>
public record GameTurn(string MoveNumber, string Side, IReadOnlyList<string> Moves);
