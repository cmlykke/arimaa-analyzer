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
    /// <param name="root">The root (first node) of the parsed main-line turn tree from ExtractTurnsWithMoves</param>
    /// <param name="upToTurnIndex">
    /// The 0-based index in the turns list. 
    /// -1 means the final position (all turns applied).
    /// Any valid index applies moves up to and including that turn.
    /// </param>
    /// <returns>The AEI string (setposition ...) for the position after the specified turn.</returns>
    public static string GameToAeiAtTurn(
        GameTurn root,
        int upToTurnIndex = -1)
    {
        if (root == null) throw new ArgumentNullException(nameof(root));

        string[] board = InitializeEmptyBoard();
        Sides sideToMove = Sides.Gold; // Gold always starts

        // Build the main-line sequence by traversing children where IsMainLine is true.
        var mainLine = new List<GameTurn>();
        var node = root;
        while (node != null)
        {
            mainLine.Add(node);
            node = node.Children.FirstOrDefault(c => c.IsMainLine);
        }

        int lastIndex = upToTurnIndex == -1 ? mainLine.Count - 1 : upToTurnIndex;
        if (lastIndex < -1 || (mainLine.Count > 0 && lastIndex >= mainLine.Count))
            throw new ArgumentOutOfRangeException(nameof(upToTurnIndex));

        // If upToTurnIndex is -1 and there are no turns, return initial position
        if (mainLine.Count == 0 || lastIndex < -1)
            return BoardToAei(board, sideToMove);

        // Apply moves up to and including the specified turn
        for (int i = 0; i <= lastIndex; i++)
        {
            var turn = mainLine[i];
            Sides moveSide = turn.Side == Sides.Gold ? Sides.Gold : Sides.Silver;

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
            sideToMove = sideToMove == Sides.Gold ? Sides.Silver : Sides.Gold;
        }

        return BoardToAei(board, sideToMove);
    }
    
    /// <summary>
    /// Parses an Arimaa game text and returns a list of turns, each containing
    /// the move number, side ('w' or 'b'), and the list of individual moves in that turn.
    /// </summary>
    /// <param name="gameText">The full game notation text.</param>
    /// <returns>The root node of the main-line turn tree (first turn). Returns null if no turns found.</returns>
    public static GameTurn? ExtractTurnsWithMoves(string gameText)
    {
        GameTurn? root = null;
        GameTurn? current = null;

        if (string.IsNullOrWhiteSpace(gameText))
            return null;

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

        string currentAei = NotationService.BoardToAei(NotationService.InitializeEmptyBoard(), Sides.Gold);
        root = new GameTurn(currentAei, "0", Sides.Silver, Array.Empty<string>(), isMainLine: true);
        current = root;
        foreach (var line in lines)
        {
            var match = regex.Match(line);
            if (!match.Success)
                continue;

            string moveNumber = match.Groups[1].Value;
            string sideStr = match.Groups[2].Value;
            Sides sideToMove = sideStr switch
            {
                "g" => Sides.Gold,
                "w" => Sides.Gold,
                "s" => Sides.Silver,
                "b" => Sides.Silver,
                _ => throw new ArgumentException($"Invalid side code in AEI: {sideStr}")
            };
            string movesPart = match.Groups[3].Value.Trim();

            IReadOnlyList<string> individualMoves;
            if (string.IsNullOrWhiteSpace(movesPart))
            {
                individualMoves = Array.Empty<string>();
            }
            else
            {
                individualMoves = movesPart
                    .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                    .Select(m => m.Trim())
                    .Where(m => !string.IsNullOrEmpty(m))
                    .ToArray();
            }

            var node = new GameTurn(currentAei, moveNumber, sideToMove, individualMoves, isMainLine: true);
            currentAei = node.AEIstring;
            current!.AddChild(node);
            current = node;
        }

        return root;
    }
    
    
    public static string GameToAei(string gameText)
    {
        string[] board = InitializeEmptyBoard();
        Sides sideToMove = Sides.Gold; // gold (white) starts

        var lines = gameText.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                            .Select(l => l.Trim())
                            .Where(l => !string.IsNullOrWhiteSpace(l));

        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;

            // Match move number + side: e.g., "1w", "41w", "1b"
            var sideMatch = System.Text.RegularExpressions.Regex.Match(line, @"^\d+[wb]");
            if (!sideMatch.Success) continue;
            
            char code = sideMatch.Value[^1]; // last char: 'g' or 's'
            Sides moveSide = code switch
            {
                'w' => Sides.Gold,
                'g' => Sides.Gold,
                'b' => Sides.Silver,
                's' => Sides.Silver,
                _   => throw new InvalidOperationException($"Unknown side code: {code}")
            }; // last char: 'w' or 'b'

            if (moveSide != sideToMove)
                continue; // out of order, skip (shouldn't happen)

            string movesPart = line.Substring(sideMatch.Length).Trim();
            var moves = movesPart.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            foreach (var move in moves)
            {
                ApplyMove(ref board, move, moveSide);
            }

            // Switch turn
            sideToMove = sideToMove == Sides.Gold ? Sides.Silver : Sides.Gold;
        }

        return BoardToAei(board, sideToMove);
    }

    public static string[] InitializeEmptyBoard()
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

    private static void ApplyMove(ref string[] board, string move, Sides side)
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

            // Preserve the case of the piece from notation to determine color
            b[row, col] = piece;
        }
        else if (move.Length >= 4)
        {
            // Regular move: e.g., "Ed4n", "hb5s", "rc5x"
            char piece = move[0];
            int startCol = char.ToLower(move[1]) - 'a';
            int startRow = move[2] - '1';
            char dirChar = move[^1] == 'x' ? move[^2] : move[^1]; // direction is last or second-last if 'x'
            
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
                    // Normal step: move piece to target, preserving original case from notation
                    b[targetRow, targetCol] = piece;
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
    
    public static string BoardToAei(string[] b, Sides side)
    {
        var flat = string.Join(string.Empty, System.Array.ConvertAll(b, r => r.Replace('.', ' ')));
        return $"setposition {side.GetCode()} \"{flat}\"";
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

    /// <summary>
    /// Applies a list of moves to an existing AEI position and returns the new AEI string.
    /// Assumes the moves constitute a complete turn for the current side to move.
    /// </summary>
    /// <param name="aei">The starting AEI position string (e.g., "setposition g \"...\"").</param>
    /// <param name="moves">The list of moves to apply.</param>
    /// <returns>The new AEI string after applying the moves and switching sides.</returns>
    public static string GamePlusMovesToAei(string aei, IReadOnlyList<string> moves)
    {
        if (string.IsNullOrWhiteSpace(aei)) throw new ArgumentNullException(nameof(aei));
        if (moves == null) throw new ArgumentNullException(nameof(moves));

        // Extract side to move from AEI
        int setPosIndex = aei.IndexOf("setposition", StringComparison.OrdinalIgnoreCase);
        if (setPosIndex == -1) throw new ArgumentException("Invalid AEI format: missing 'setposition'.");
        
        string afterSetPos = aei.Substring(setPosIndex + "setposition".Length).Trim();
        int spaceIndex = afterSetPos.IndexOf(' ');
        if (spaceIndex == -1) spaceIndex = afterSetPos.IndexOf('"') - 1; // in case no space before "
        string sideCode = afterSetPos.Substring(0, spaceIndex + 1).Trim();
        Sides sideToMove = sideCode switch
        {
            "g" => Sides.Gold,
            "s" => Sides.Silver,
            _ => throw new ArgumentException($"Invalid side code in AEI: {sideCode}")
        };

        // Extract the flat string
        int quoteStart = aei.IndexOf('"');
        int quoteEnd = aei.LastIndexOf('"');
        if (quoteStart == -1 || quoteEnd == -1 || quoteEnd <= quoteStart)
            throw new ArgumentException("Invalid AEI format: missing quotes.");
        string flat = aei.Substring(quoteStart + 1, quoteEnd - quoteStart - 1);

        // Apply each move directly to the flat string
        foreach (var move in moves)
        {
            flat = ApplyMoveToFlat(flat, move);
        }

        // Switch side after the turn
        sideToMove = sideToMove == Sides.Gold ? Sides.Silver : Sides.Gold;

        // Return new AEI
        return $"setposition {sideToMove.GetCode()} \"{flat}\"";
    }

    /// <summary>
    /// Helper function to apply a single move directly to the flat AEI board string (with spaces for empties).
    /// Returns the updated flat string.
    /// Note: Directions corrected based on standard Arimaa notation ('n' increases row towards higher ranks).
    /// Piece case preserved from notation (no forcing based on side).
    /// </summary>
    private static string ApplyMoveToFlat(string flat, string move)
    {
        if (flat.Length != 64) throw new ArgumentException("Flat board string must be 64 characters.");

        char[] b = flat.ToCharArray(); // spaces for empty

        if (move.Length == 3 && char.IsLetter(move[0]) && char.IsLetter(move[1]) && char.IsDigit(move[2]))
        {
            // Setup move: e.g., "Ra1", "ra8"
            char piece = move[0];
            int col = char.ToLower(move[1]) - 'a';
            int row = move[2] - '1'; // 0 for '1' (bottom), 7 for '8' (top)
            if (row < 0 || row > 7 || col < 0 || col > 7) return flat; // invalid

            int index = row * 8 + col;
            b[index] = piece;
        }
        else if (move.Length >= 4 && char.IsLetter(move[0]) && char.IsLetter(move[1]) && char.IsDigit(move[2]))
        {
            // Regular move or removal: e.g., "Ed4n", "hb5s", "rc5x"
            char piece = move[0];
            int col = char.ToLower(move[1]) - 'a';
            int row = move[2] - '1';
            if (row < 0 || row > 7 || col < 0 || col > 7) return flat; // invalid

            int index = row * 8 + col;

            // Clear starting position (for moves and removals)
            b[index] = ' ';

            if (!move.EndsWith('x'))
            {
                // Normal step: compute target and place piece there
                char dirChar = move[^1];
                int dr = 0, dc = 0;
                switch (dirChar)
                {
                    case 'n': dr = 1; break; // north: higher row (towards rank 8)
                    case 's': dr = -1; break; // south: lower row (towards rank 1)
                    case 'e': dc = 1; break;
                    case 'w': dc = -1; break;
                }

                int targetRow = row + dr;
                int targetCol = col + dc;

                if (targetRow >= 0 && targetRow < 8 && targetCol >= 0 && targetCol < 8)
                {
                    int targetIndex = targetRow * 8 + targetCol;
                    b[targetIndex] = piece;
                }
            }
            // If ends with 'x', it's a removal: already cleared the position, no further action
        }

        return new string(b);
    }
}