using System.Text.RegularExpressions;

namespace YourApp.Models;

/// <summary>
/// Represents a validated Arimaa game in standard notation.
/// Immutable after creation.
/// </summary>
public record ArimaaGameService
{
    /// <summary>
    /// The original raw notation string (trimmed).
    /// </summary>
    public string RawNotation { get; init; }

    /// <summary>
    /// The individual moves/lines (split by newline and trimmed).
    /// </summary>
    public IReadOnlyList<string> Moves { get; init; } = Array.Empty<string>();

    private ArimaaGameService(string rawNotation, IReadOnlyList<string> moves)
    {
        RawNotation = rawNotation;
        Moves = moves;
    }

    /// <summary>
    /// Attempts to parse and validate the game notation.
    /// Returns true if valid, false otherwise.
    /// </summary>
    public static bool TryParse(string notation, out ArimaaGameService? game, out string? errorMessage)
    {
        game = null;
        errorMessage = null;

        if (string.IsNullOrWhiteSpace(notation))
        {
            errorMessage = "Game notation cannot be null or empty.";
            return false;
        }

        var trimmed = notation.Trim();
        var lines = trimmed.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                           .Select(l => l.Trim())
                           .Where(l => !string.IsNullOrEmpty(l))
                           .ToList();

        if (lines.Count == 0)
        {
            errorMessage = "No valid move lines found.";
            return false;
        }

        // Basic validation: each line should match the expected pattern for Arimaa notation
        // Example patterns:
        // "1w Ed2 Mb2 ..."          -> move number + side + spaces + moves
        // "1b ra7 hb7 ..."
        var moveLinePattern = new Regex(@"^\d+[wb]\s+.*", RegexOptions.Compiled);

        foreach (var line in lines)
        {
            if (!moveLinePattern.IsMatch(line))
            {
                errorMessage = $"Invalid move line format: '{line}'";
                return false;
            }

            // Additional per-line validation can go here (piece codes, coordinates, etc.)
            // For now, we accept basic structure.
        }

        game = new ArimaaGameService(trimmed, lines.AsReadOnly());
        return true;
    }

    /// <summary>
    /// Parses and throws if invalid. Use only when you're sure input is trusted.
    /// </summary>
    public static ArimaaGameService Parse(string notation)
    {
        if (TryParse(notation, out var game, out var error))
            return game!;

        throw new ArgumentException($"Invalid Arimaa game notation: {error}");
    }
}