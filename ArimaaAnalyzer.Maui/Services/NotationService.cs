using System.Text;
using System.Text.RegularExpressions;
using ArimaaAnalyzer.Maui.Services.Arimaa;

namespace ArimaaAnalyzer.Maui.Services;

public static class NotationService
{
    
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