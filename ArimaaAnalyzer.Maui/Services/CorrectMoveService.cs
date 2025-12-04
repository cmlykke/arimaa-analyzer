using System.Text;
using ArimaaAnalyzer.Maui.Services.Arimaa;

namespace ArimaaAnalyzer.Maui.Services;

/// <summary>
/// Attempts to validate and reconstruct a legal Arimaa move sequence (up to 4 steps)
/// that transforms a position "before" into a position "after".
///
/// Limitations (initial implementation):
/// - Supports only slide steps (orthogonal move by one square into empty).
/// - Enforces rabbit non-backward rule and freezing (cannot move a frozen piece).
/// - Applies trap captures after each step.
/// - Does NOT implement push/pull, goal, repetition, or advanced rules yet.
///
/// If a matching sequence is found, returns the official step notation like
/// "Re2n Hh2w Rc2n Rd2n" (1 to 4 steps). Otherwise returns "error".
/// </summary>
public sealed class CorrectMoveService
{
    /// <summary>
    /// Try to compute a legal sequence (1..4 steps) for <paramref name="sideToMove"/> that transforms
    /// <paramref name="before"/> into <paramref name="after"/>. Returns official notation string or "error".
    /// </summary>
    public string ComputeMoveSequence(GameState before, GameState after, Side sideToMove)
    {
        if (before == null || after == null) return "error";

        // Quick sanity: side after a completed turn should be the opponent.
        // We don't hard-fail on mismatch, but it helps prune impossible cases.
        var expectedAfterSide = Opposite(sideToMove);

        // Build boards
        var start = BoardState.From(before, sideToMove);
        var goal = BoardState.From(after, expectedAfterSide);

        // If already equal (no change), no legal sequence (must spend at least one step)
        if (start.BoardEquals(goal)) return "error";

        // BFS over sequences up to 4 steps
        var seen = new HashSet<string> { start.Hash }; // hash excludes step history
        var q = new Queue<BoardState>();
        q.Enqueue(start);

        while (q.Count > 0)
        {
            var cur = q.Dequeue();

            if (cur.StepsCount is > 0 and <= 4 && cur.BoardEquals(goal))
            {
                return cur.RenderNotation();
            }

            if (cur.StepsCount >= 4) continue;

            // Generate legal slide moves
            foreach (var next in GenerateSlides(cur))
            {
                if (seen.Add(next.Hash))
                {
                    q.Enqueue(next);
                }
            }
        }

        return "error";
    }

    private static IEnumerable<BoardState> GenerateSlides(BoardState state)
    {
        // Directions: N,E,S,W
        var dirs = new (int dr, int dc, char dch)[] { (-1, 0, 'n'), (0, 1, 'e'), (1, 0, 's'), (0, -1, 'w') };

        for (var r = 0; r < 8; r++)
        for (var c = 0; c < 8; c++)
        {
            var piece = state.Board[r, c];
            if (piece is null || piece.Side != state.SideToMove) continue;

            // Frozen pieces can't move
            if (IsFrozen(state.Board, r, c)) continue;

            foreach (var (dr, dc, dch) in dirs)
            {
                var r2 = r + dr;
                var c2 = c + dc;
                if (r2 < 0 || r2 > 7 || c2 < 0 || c2 > 7) continue;
                if (state.Board[r2, c2] is not null) continue; // slide into empty only

                // Rabbit cannot move backward
                if (piece.Type == PieceType.Rabbit)
                {
                    if (piece.Side == Side.Gold && dr == 1) continue; // south
                    if (piece.Side == Side.Silver && dr == -1) continue; // north
                }

                // Apply step
                var next = state.CloneForNext();
                next.Board[r2, c2] = piece;
                next.Board[r, c] = null;
                next.AppendStep(piece, r, c, r2, c2, dch);

                // Apply immediate trap captures
                ApplyTrapCaptures(next.Board);

                yield return next;
            }
        }
    }

    private static bool IsFrozen(Piece?[,] board, int r, int c)
    {
        var p = board[r, c];
        if (p is null) return false;

        // Friendly neighbor unfroze
        if (HasAdjacentFriendly(board, r, c, p.Side)) return false;

        // Adjacent stronger enemy => frozen
        return HasAdjacentStrongerEnemy(board, r, c, p.Side, p.Type);
    }

    private static bool HasAdjacentFriendly(Piece?[,] b, int r, int c, Side side)
    {
        for (var k = 0; k < 4; k++)
        {
            var r2 = r + ((k == 0) ? -1 : (k == 2) ? 1 : 0);
            var c2 = c + ((k == 1) ? 1 : (k == 3) ? -1 : 0);
            if (r2 < 0 || r2 > 7 || c2 < 0 || c2 > 7) continue;
            var q = b[r2, c2];
            if (q is { } && q.Side == side) return true;
        }
        return false;
    }

    private static bool HasAdjacentStrongerEnemy(Piece?[,] b, int r, int c, Side side, PieceType type)
    {
        var myStr = Strength(type);
        for (var k = 0; k < 4; k++)
        {
            var r2 = r + ((k == 0) ? -1 : (k == 2) ? 1 : 0);
            var c2 = c + ((k == 1) ? 1 : (k == 3) ? -1 : 0);
            if (r2 < 0 || r2 > 7 || c2 < 0 || c2 > 7) continue;
            var q = b[r2, c2];
            if (q is { } && q.Side != side && Strength(q.Type) > myStr) return true;
        }
        return false;
    }

    private static int Strength(PieceType t) => t switch
    {
        PieceType.Elephant => 6,
        PieceType.Camel => 5,
        PieceType.Horse => 4,
        PieceType.Dog => 3,
        PieceType.Cat => 2,
        PieceType.Rabbit => 1,
        _ => 0
    };

    private static void ApplyTrapCaptures(Piece?[,] board)
    {
        // Traps: (2,2), (2,5), (5,2), (5,5)
        var traps = new (int r, int c)[] { (2, 2), (2, 5), (5, 2), (5, 5) };
        foreach (var (r, c) in traps)
        {
            var p = board[r, c];
            if (p is null) continue;
            if (!HasAdjacentFriendly(board, r, c, p.Side))
            {
                board[r, c] = null; // captured
            }
        }
    }

    private static Side Opposite(Side s) => s == Side.Gold ? Side.Silver : Side.Gold;

    private sealed class BoardState
    {
        public Piece?[,] Board { get; }
        public Side SideToMove { get; }
        private readonly List<string> _steps = new();

        public int StepsCount => _steps.Count;
        public string Hash => ComputeHash(Board, SideToMove);

        private BoardState(Piece?[,] board, Side side, IEnumerable<string>? steps = null)
        {
            Board = board;
            SideToMove = side;
            if (steps != null) _steps.AddRange(steps);
        }

        public static BoardState From(GameState s, Side side)
        {
            var b = new Piece?[8, 8];
            foreach (var (pos, piece) in s.AllPieces())
            {
                b[pos.Row, pos.Col] = new Piece(piece.Type, piece.Side);
            }
            // Do not apply trap captures here; the input states are considered authoritative.
            // Trap captures are applied only as a consequence of steps during search.
            return new BoardState(b, side);
        }

        public BoardState CloneForNext()
        {
            var copy = new Piece?[8, 8];
            for (var r = 0; r < 8; r++)
            for (var c = 0; c < 8; c++)
                copy[r, c] = Board[r, c] is { } p ? new Piece(p.Type, p.Side) : null;

            return new BoardState(copy, SideToMove, _steps);
        }

        public void AppendStep(Piece piece, int r1, int c1, int r2, int c2, char dir)
        {
            // Notation: PieceLetter + origin square + dir
            var sb = new StringBuilder(4);
            sb.Append(PieceLetter(piece.Type));
            sb.Append(SquareString(r1, c1));
            sb.Append(dir);
            _steps.Add(sb.ToString());

            // After 1..4 steps, turn ends; we keep the side to move unchanged during building
            // and only compare to the goal board (which is built with the opponent side).
        }

        public string RenderNotation() => string.Join(" ", _steps);

        public bool BoardEquals(BoardState other)
        {
            for (var r = 0; r < 8; r++)
            for (var c = 0; c < 8; c++)
            {
                var a = Board[r, c];
                var b = other.Board[r, c];
                if ((a is null) != (b is null)) return false;
                if (a is not null && b is not null && (a.Side != b.Side || a.Type != b.Type)) return false;
            }
            return true;
        }

        private static string ComputeHash(Piece?[,] board, Side side)
        {
            var sb = new StringBuilder(100);
            sb.Append(side == Side.Gold ? 'g' : 's');
            for (var r = 0; r < 8; r++)
            for (var c = 0; c < 8; c++)
            {
                var p = board[r, c];
                if (p is null) { sb.Append('.'); }
                else { sb.Append((char)('A' + Strength(p.Type))); sb.Append(p.Side == Side.Gold ? 'G' : 'S'); }
            }
            return sb.ToString();
        }

        private static char PieceLetter(PieceType t) => t switch
        {
            PieceType.Elephant => 'E',
            PieceType.Camel => 'M',
            PieceType.Horse => 'H',
            PieceType.Dog => 'D',
            PieceType.Cat => 'C',
            PieceType.Rabbit => 'R',
            _ => 'X'
        };

        private static string SquareString(int r, int c)
        {
            // file a..h = col 0..7; rank 1..8 bottom to top => rank = 8 - r
            char file = (char)('a' + c);
            int rank = 8 - r;
            return string.Create(2, (file, rank), (span, st) =>
            {
                span[0] = st.file;
                span[1] = (char)('0' + st.rank);
            });
        }
    }
}
