using System.Collections.ObjectModel;

namespace ArimaaAnalyzer.Maui.Services.Arimaa;

public sealed class GameState
{
    // 8x8 board for a starter demo (Arimaa is 8x8). Null means empty square.
    private readonly Piece?[,] _board = new Piece?[8, 8];

    public GameState()
    {
        // Minimal demo setup: a few pieces to interact with
        // Gold bottom two rabbits
        _board[6, 3] = new Piece(PieceType.Rabbit, Side.Gold);
        _board[6, 4] = new Piece(PieceType.Rabbit, Side.Gold);

        // Silver top two rabbits
        _board[1, 3] = new Piece(PieceType.Rabbit, Side.Silver);
        _board[1, 4] = new Piece(PieceType.Rabbit, Side.Silver);

        // A couple of stronger pieces
        _board[7, 0] = new Piece(PieceType.Elephant, Side.Gold);
        _board[0, 7] = new Piece(PieceType.Camel, Side.Silver);
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
