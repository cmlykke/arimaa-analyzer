using System.Collections.ObjectModel;

namespace ArimaaAnalyzer.Maui.Services.Arimaa;

public sealed class GameState
{
    // 8x8 board (Arimaa is 8x8). Null means empty square.
    private readonly Piece?[,] _board = new Piece?[8, 8];

    public string localAeiSetPosition { get; }
    
    public Side SideToMove { get; }

    /// <summary>
    /// Initialize the state from an AEI "setposition" command string.
    /// Example: setposition g "rrrrrrrrhdcemcdh                                HDCMECDHRRRRRRRR"
    /// Where the quoted part is 64 characters (spaces for empty, letters for pieces),
    /// and the side is 'g' (gold) or 's' (silver).
    /// </summary>
    public GameState(string aeiSetPosition)
    {
        localAeiSetPosition = aeiSetPosition;
        
        if (string.IsNullOrWhiteSpace(aeiSetPosition))
            throw new ArgumentException("Value cannot be null or whitespace.", nameof(aeiSetPosition));

        // Expect: setposition <g|s> "<64 chars>"
        // We'll parse leniently but validate essentials.
        var trimmed = aeiSetPosition.Trim();
        if (!trimmed.StartsWith("setposition ", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Expected string starting with 'setposition'.", nameof(aeiSetPosition));

        // Remove leading keyword
        var rest = trimmed.Substring("setposition ".Length).TrimStart();
        if (rest.Length < 3)
            throw new ArgumentException("Malformed setposition string.", nameof(aeiSetPosition));

        // First non-space char must be side
        var sideChar = char.ToLowerInvariant(rest[0]);
        SideToMove = sideChar switch
        {
            'g' => Side.Gold,
            's' => Side.Silver,
            _ => throw new ArgumentException("Side must be 'g' or 's'.", nameof(aeiSetPosition))
        };

        // Find the first quote and last quote to extract the 64-char flat board string
        var firstQuote = rest.IndexOf('"');
        var lastQuote = rest.LastIndexOf('"');
        if (firstQuote < 0 || lastQuote <= firstQuote)
            throw new ArgumentException("Board string must be quoted.", nameof(aeiSetPosition));

        var flat = rest.Substring(firstQuote + 1, lastQuote - firstQuote - 1);
        if (flat.Length != 64)
            throw new ArgumentException("Board string must be exactly 64 characters.", nameof(aeiSetPosition));

        // Fill board row-major: rows 0..7, cols 0..7, as per UI expects top row is index 0
        for (var i = 0; i < 64; i++)
        {
            var r = i / 8;
            var c = i % 8;
            var ch = flat[i];
            _board[r, c] = CharToPiece(ch);
        }
    }

    private static Piece? CharToPiece(char ch)
    {
        if (ch == ' ')
            return null;

        var isUpper = char.IsUpper(ch);
        var side = isUpper ? Side.Gold : Side.Silver;
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

    public Piece? GetPiece(Position p) => p.IsOnBoard ? _board[p.Row, p.Col] : null;

    public bool IsEmpty(Position p) => GetPiece(p) is null;

    public IEnumerable<(Position pos, Piece piece)> AllPieces()
    {
        for (var r = 0; r < 8; r++)
        for (var c = 0; c < 8; c++)
        {
            var piece = _board[r, c];
            if (piece != null) yield return (new Position(r, c), piece);
        }
    }

    // Extremely simplified move: move a piece to any empty square on the board.
    public bool TryMove(Position from, Position to)
    {
        if (!from.IsOnBoard || !to.IsOnBoard) return false;
        if (from == to) return false;
        var piece = _board[from.Row, from.Col];
        if (piece is null) return false;
        if (_board[to.Row, to.Col] is not null) return false;

        _board[to.Row, to.Col] = piece;
        _board[from.Row, from.Col] = null;
        return true;
    }
}
