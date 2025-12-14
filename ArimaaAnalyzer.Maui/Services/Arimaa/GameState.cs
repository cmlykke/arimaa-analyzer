using System.Collections.ObjectModel;
using YourApp.Models;

namespace ArimaaAnalyzer.Maui.Services.Arimaa;

public sealed class GameState
{
    // Board state is now represented only as the AEI setposition string
    // No internal _board array needed for performance
    private string _aeiSetPosition;
    
    public string localAeiSetPosition
    {
        get => _aeiSetPosition;
        private set => _aeiSetPosition = value;
    }
    
    public Sides SideToMove { get; }

    public BoardOrientation boardorientation { get; set; }

    // Optional: track current node when initialized from a GameTurn
    public GameTurn? CurrentNode { get; }

    /// <summary>
    /// Initialize the state from an AEI "setposition" command string.
    /// Example: setposition g "rrrrrrrrhdcemcdh                                HDCMECDHRRRRRRRR"
    /// Where the quoted part is 64 characters (spaces for empty, letters for pieces),
    /// and the side is 'g' (gold) or 's' (silver).
    /// </summary>
    public GameState(string aeiSetPosition)
    {
        if (string.IsNullOrWhiteSpace(aeiSetPosition))
            throw new ArgumentException("Value cannot be null or whitespace.", nameof(aeiSetPosition));

        // Validate and parse the AEI string
        ValidateAndParseAei(aeiSetPosition, out var side, out var boardString);

        boardorientation = getBoardOrientationFromAEI(aeiSetPosition);
        SideToMove = side;
        _aeiSetPosition = aeiSetPosition;
    }

    public BoardOrientation getBoardOrientationFromAEI(string AEIstring)
    {
        // Treat incoming AEI as already normalized with Gold homebase on the left
        // (GoldWestSIlverEast). Orientation is a view concern.
        return BoardOrientation.GoldWestSIlverEast;
    }
    
    /// <summary>
    /// Initialize the state from a parsed GameTurn node. Uses the node's AEIstring.
    /// </summary>
    public GameState(GameTurn node)
    {
        if (node == null) throw new ArgumentNullException(nameof(node));
        CurrentNode = node;
        ValidateAndParseAei(node.AEIstring, out var side, out _);
        SideToMove = side;
        _aeiSetPosition = node.AEIstring;
        boardorientation = getBoardOrientationFromAEI(node.AEIstring);;
        var test = "test";
    }

    /// <summary>
    /// Move a piece from one position to another.
    /// Updates the internal AEI string directly (no intermediate _board).
    /// </summary>
    public bool TryMove(Position from, Position to)
    {
        if (!from.IsOnBoard || !to.IsOnBoard) return false;
        if (from == to) return false;

        // Map display-space positions to normalized board coordinates
        var (nrFrom, ncFrom) = BoardRotationService.MapDisplayToNormalized(from.Row, from.Col, boardorientation);
        var (nrTo, ncTo) = BoardRotationService.MapDisplayToNormalized(to.Row, to.Col, boardorientation);

        // Extract the current board string from AEI (normalized orientation)
        var boardChars = ExtractBoardString();
        if (boardChars == null) return false;

        // Check source and destination
        var fromIdx = nrFrom * 8 + ncFrom;
        var toIdx = nrTo * 8 + ncTo;

        var sourcePiece = boardChars[fromIdx];
        if (sourcePiece == ' ') return false; // no piece at source
        if (boardChars[toIdx] != ' ') return false; // destination not empty

        // Perform move by modifying the board string
        var newBoardChars = boardChars.ToCharArray();
        newBoardChars[toIdx] = sourcePiece;
        newBoardChars[fromIdx] = ' ';

        // Rebuild AEI string
        RebuildAei(new string(newBoardChars));
        return true;
    }

    /// <summary>
    /// Remove a piece at the given position.
    /// Used after trap captures or other removals.
    /// </summary>
    public void RemovePieceAt(Position p)
    {
        if (!p.IsOnBoard) return;

        var boardChars = ExtractBoardString();
        if (boardChars == null) return;

        var idx = p.Row * 8 + p.Col;
        if (boardChars[idx] == ' ') return; // already empty

        var newBoardChars = boardChars.ToCharArray();
        newBoardChars[idx] = ' ';

        RebuildAei(new string(newBoardChars));
    }

    /// <summary>
    /// Get the character at a position (for direct AEI-based queries).
    /// </summary>
    public char GetPieceChar(Position p)
    {
        if (!p.IsOnBoard) return ' ';
        var boardChars = ExtractBoardString();
        if (boardChars == null) return ' ';
        var (nr, nc) = BoardRotationService.MapDisplayToNormalized(p.Row, p.Col, boardorientation);
        var idx = nr * 8 + nc;
        return boardChars[idx];
    }

    /// <summary>
    /// Get the character at a raw board index (0-63).
    /// </summary>
    public char GetPieceCharAtIndex(int index)
    {
        if (index < 0 || index >= 64) return ' ';
        var boardChars = ExtractBoardString();
        return boardChars?[index] ?? ' ';
    }

    /// <summary>
    /// Legacy method for UI compatibility.
    /// Reconstructs a Piece from the AEI string on demand.
    /// </summary>
    public Piece? GetPiece(Position p)
    {
        var ch = GetPieceChar(p);
        return CharToPiece(ch);
    }

    public bool IsEmpty(Position p) => GetPieceChar(p) == ' ';

    /// <summary>
    /// Iterate all pieces by reading the AEI string directly.
    /// </summary>
    public IEnumerable<(Position pos, Piece piece)> AllPieces()
    {
        var boardChars = ExtractBoardString();
        if (boardChars == null) yield break;

        for (var i = 0; i < 64; i++)
        {
            var ch = boardChars[i];
            if (ch != ' ')
            {
                var r = i / 8;
                var c = i % 8;
                var piece = CharToPiece(ch);
                if (piece != null)
                    yield return (new Position(r, c), piece);
            }
        }
    }

    /// <summary>
    /// Extract the 64-character board string from the AEI setposition format.
    /// Returns null if the AEI string is malformed.
    /// </summary>
    private string? ExtractBoardString()
    {
        if (string.IsNullOrWhiteSpace(_aeiSetPosition)) return null;

        var trimmed = _aeiSetPosition.Trim();
        var firstQuote = trimmed.IndexOf('"');
        var lastQuote = trimmed.LastIndexOf('"');

        if (firstQuote < 0 || lastQuote <= firstQuote || lastQuote - firstQuote - 1 != 64)
            return null;

        return trimmed.Substring(firstQuote + 1, 64);
    }

    /// <summary>
    /// Internal helper for consumers that require a guaranteed valid board string.
    /// Throws when the AEI string is not present or malformed.
    /// </summary>
    internal string GetBoardStringOrThrow()
    {
        var board = ExtractBoardString();
        if (board is null)
            throw new ArgumentException("Malformed AEI setposition: board string must be exactly 64 characters.");
        if (board.Length != 64)
            throw new ArgumentException("Malformed AEI setposition: board string must be exactly 64 characters.");
        return board;
    }

    /// <summary>
    /// Rebuild the AEI setposition string with the new board configuration.
    /// </summary>
    private void RebuildAei(string newBoardString)
    {
        if (newBoardString.Length != 64)
            throw new ArgumentException("Board string must be exactly 64 characters.", nameof(newBoardString));

        var sideChar = SideToMove == Sides.Gold ? 'g' : 's';
        localAeiSetPosition = $"setposition {sideChar} \"{newBoardString}\"";
    }

    private static void ValidateAndParseAei(string aeiSetPosition, out Sides side, out string boardString)
    {
        var trimmed = aeiSetPosition.Trim();
        if (!trimmed.StartsWith("setposition ", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Expected string starting with 'setposition'.", nameof(aeiSetPosition));

        var rest = trimmed.Substring("setposition ".Length).TrimStart();
        if (rest.Length < 3)
            throw new ArgumentException("Malformed setposition string.", nameof(aeiSetPosition));

        var sideChar = char.ToLowerInvariant(rest[0]);
        side = sideChar switch
        {
            'g' => Sides.Gold,
            's' => Sides.Silver,
            _ => throw new ArgumentException("Side must be 'g' or 's'.", nameof(aeiSetPosition))
        };

        var firstQuote = rest.IndexOf('"');
        var lastQuote = rest.LastIndexOf('"');
        if (firstQuote < 0 || lastQuote <= firstQuote)
            throw new ArgumentException("Board string must be quoted.", nameof(aeiSetPosition));

        boardString = rest.Substring(firstQuote + 1, lastQuote - firstQuote - 1);
        if (boardString.Length != 64)
            throw new ArgumentException("Board string must be exactly 64 characters.", nameof(aeiSetPosition));
    }

    private static Piece? CharToPiece(char ch)
    {
        if (ch == ' ')
            return null;

        var isUpper = char.IsUpper(ch);
        var side = isUpper ? Sides.Gold : Sides.Silver;
        var up = char.ToUpperInvariant(ch);
        return up switch
        {
            'R' => new Piece(PieceType.Rabbit, side),
            'C' => new Piece(PieceType.Cat, side),
            'D' => new Piece(PieceType.Dog, side),
            'H' => new Piece(PieceType.Horse, side),
            'M' => new Piece(PieceType.Camel, side),
            'E' => new Piece(PieceType.Elephant, side),
            _ => null
        };
    }
}
