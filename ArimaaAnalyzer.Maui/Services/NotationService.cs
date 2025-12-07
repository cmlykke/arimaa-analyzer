using System.Text;
using System.Text.RegularExpressions;
using ArimaaAnalyzer.Maui.Services.Arimaa;
using YourApp.Models;

namespace ArimaaAnalyzer.Maui.Services;

public static class NotationService
{
        
    /// <summary>
    /// Converts a parsed Arimaa game up to a specific turn into an AEI position string.
    /// </summary>
    /// <param name="turns">The list of parsed turns from ExtractTurnsWithMoves</param>
    /// <param name="upToTurnIndex">
    /// The 0-based index in the turns list. 
    /// -1 means the final position (all turns applied).
    /// Any valid index applies moves up to and including that turn.
    /// </param>
    /// <returns>The AEI string (setposition ...) for the position after the specified turn.</returns>
    public static string GameToAeiAtTurn(
        List<GameTurn> turns,
        int upToTurnIndex = -1)
    {
        if (turns == null) throw new ArgumentNullException(nameof(turns));

        string[] board = InitializeEmptyBoard();
        string sideToMove = "g"; // Gold always starts

        int lastIndex = upToTurnIndex == -1 ? turns.Count - 1 : upToTurnIndex;
        if (lastIndex < -1 || (turns.Count > 0 && lastIndex >= turns.Count))
            throw new ArgumentOutOfRangeException(nameof(upToTurnIndex));

        // If upToTurnIndex is -1 and turns is empty, return initial position
        if (turns.Count == 0 || lastIndex < -1)
            return BoardToAei(board, sideToMove);

        // Apply moves up to and including the specified turn
        for (int i = 0; i <= lastIndex; i++)
        {
            var turn = turns[i];
            string moveSide = turn.Side == "w" ? "g" : "b";

            // Optional safety: ensure turns are in expected order (alternating sides)
            // You can remove this check if you're confident in the input
            if (moveSide != sideToMove)
            {
                // In rare malformed games, you might want to force it or skip
                // Here we just proceed with the turn's side
                sideToMove = moveSide;
            }

            foreach (var move in turn.Moves)
            {
                ApplyMove(ref board, move, moveSide);
            }

            // Switch side for next turn
            sideToMove = sideToMove == "g" ? "b" : "g";
        }

        return BoardToAei(board, sideToMove);
    }
    
    /// <summary>
    /// Parses an Arimaa game text and returns a list of turns, each containing
    /// the move number, side ('w' or 'b'), and the list of individual moves in that turn.
    /// </summary>
    /// <param name="gameText">The full game notation text.</param>
    /// <returns>List of turns with move number, side, and moves.</returns>
    public static List<GameTurn> ExtractTurnsWithMoves(string gameText)
    {
        var turns = new List<GameTurn>();

        if (string.IsNullOrWhiteSpace(gameText))
            return turns;

        // Step 1: Replace literal "\n" (the text) with actual newline characters
        // This handles cases where the text contains "\n" as part of the string
        string textWithRealNewlines = gameText.Replace("\\n", "\n");

        // Step 2: Normalize all line endings (\r\n -> \n, lone \r -> \n)
        textWithRealNewlines = textWithRealNewlines.Replace("\r\n", "\n").Replace("\r", "\n");

        // Step 3: Now split on real newlines
        var lines = textWithRealNewlines
            .Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.Trim())
            .Where(l => !string.IsNullOrWhiteSpace(l));

        // Step 4: Parse each line normally
        var regex = new Regex(@"^(\d+)([wb])\s+(.*)$", RegexOptions.Compiled);

        foreach (var line in lines)
        {
            var match = regex.Match(line);
            if (!match.Success)
                continue;

            string moveNumber = match.Groups[1].Value;
            string side = match.Groups[2].Value;
            string movesPart = match.Groups[3].Value.Trim();

            if (string.IsNullOrWhiteSpace(movesPart))
            {
                turns.Add(new GameTurn(moveNumber, side, Array.Empty<string>()));
                continue;
            }

            var individualMoves = movesPart
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Select(m => m.Trim())
                .Where(m => !string.IsNullOrEmpty(m))
                .ToArray();

            turns.Add(new GameTurn(moveNumber, side, individualMoves));
        }

        return turns;
    }
    
    
    public static string GameToAei(string gameText)
    {
        string[] board = InitializeEmptyBoard();
        string sideToMove = "g"; // gold (white) starts

        var lines = gameText.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                            .Select(l => l.Trim())
                            .Where(l => !string.IsNullOrWhiteSpace(l));

        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;

            // Match move number + side: e.g., "1w", "41w", "1b"
            var sideMatch = System.Text.RegularExpressions.Regex.Match(line, @"^\d+[wb]");
            if (!sideMatch.Success) continue;

            char notationSide = sideMatch.Value[^1]; // last char: 'w' or 'b'
            string moveSide = notationSide == 'w' ? "g" : "b";

            if (moveSide != sideToMove)
                continue; // out of order, skip (shouldn't happen)

            string movesPart = line.Substring(sideMatch.Length).Trim();
            var moves = movesPart.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            foreach (var move in moves)
            {
                ApplyMove(ref board, move, moveSide);
            }

            // Switch turn
            sideToMove = sideToMove == "g" ? "b" : "g";
        }

        return BoardToAei(board, sideToMove);
    }

    private static string[] InitializeEmptyBoard()
    {
        return new[]
        {
            "........",
            "........",
            "........",
            "........",
            "........",
            "........",
            "........",
            "........"
        };
    }

    private static void ApplyMove(ref string[] board, string move, string side)
    {
        // We'll work on a mutable char[,] for ease, then copy back
        char[,] b = new char[8, 8];
        for (int r = 0; r < 8; r++)
            for (int c = 0; c < 8; c++)
                b[r, c] = board[r][c];

        if (move.Length == 3 && move[1] >= 'a' && move[1] <= 'h' && move[2] >= '1' && move[2] <= '8')
        {
            // Setup move: e.g., "Ra1", "ra8"
            char piece = move[0];
            int col = char.ToLower(move[1]) - 'a';
            int row = move[2] - '1'; // '1' -> 0, '8' -> 7

            char actualPiece = side == "g" ? char.ToUpper(piece) : char.ToLower(piece);
            b[row, col] = actualPiece;
        }
        else if (move.Length >= 4)
        {
            // Regular move: e.g., "Ed4n", "hb5s", "rc5x"
            char piece = move[0];
            int startCol = char.ToLower(move[1]) - 'a';
            int startRow = move[2] - '1';
            char dirChar = move[^1] == 'x' ? move[^2] : move[^1]; // direction is last or second-last if 'x'

            char actualPiece = side == "g" ? char.ToUpper(piece) : char.ToLower(piece);

            // Clear starting position
            b[startRow, startCol] = '.';

            // Compute target
            int dr = 0, dc = 0;
            switch (dirChar)
            {
                case 'n': dr = -1; break;
                case 's': dr = 1; break;
                case 'e': dc = 1; break;
                case 'w': dc = -1; break;
            }

            int targetRow = startRow + dr;
            int targetCol = startCol + dc;

            if (targetRow >= 0 && targetRow < 8 && targetCol >= 0 && targetCol < 8)
            {
                if (move.EndsWith('x'))
                {
                    // Capture/push: the target square becomes empty (opponent piece removed)
                    b[targetRow, targetCol] = '.';
                }
                else
                {
                    // Normal step: move piece to target
                    b[targetRow, targetCol] = actualPiece;
                }
            }
        }

        // Copy back to string[]
        for (int r = 0; r < 8; r++)
        {
            char[] rowChars = new char[8];
            for (int c = 0; c < 8; c++)
                rowChars[c] = b[r, c];
            board[r] = new string(rowChars);
        }
    }
    
    public static string BoardToAei(string[] b, string side)
    {
        var flat = string.Join(string.Empty, System.Array.ConvertAll(b, r => r.Replace('.', ' ')));
        return $"setposition {side} \"{flat}\"";
    }
    
    public static string[] AeiToBoard(string internalArimaaString)
    {
        // The format is: setposition {side} "{flat}"
        // We need the content inside the quotes
        int quoteStart = internalArimaaString.IndexOf('"');
        int quoteEnd   = internalArimaaString.LastIndexOf('"');

        if (quoteStart == -1 || quoteEnd == -1 || quoteEnd <= quoteStart)
            throw new ArgumentException("Invalid InternalArimaaString format: missing quotes.");

        string flatWithSpaces = internalArimaaString.Substring(quoteStart + 1, quoteEnd - quoteStart - 1);

        // Replace spaces with '.' to restore empty squares
        string flat = flatWithSpaces.Replace(' ', '.');

        if (flat.Length != 64)
            throw new ArgumentException($"Board string must contain exactly 64 characters, but has {flat.Length}.");

        // Split into 8 rows of 8 characters
        string[] board = new string[8];
        for (int i = 0; i < 8; i++)
        {
            board[i] = flat.Substring(i * 8, 8);
        }

        return board;
    }
}