using ArimaaAnalyzer.Maui.Services;
using ArimaaAnalyzer.Maui.Services.Arimaa;
using FluentAssertions;
using Xunit;

namespace ArimaaAnalyzer.Tests.Services;

public class ArimaaGameServiceTrapCaptureTests
{

    private static string[] EmptyBoard() => new[]
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

    [Fact(DisplayName = "Move onto trap without support → captured")]
    public void MoveOntoTrap_NoSupport_Captured()
    {
        // Trap: c6 -> (row2,col2)
        var board = EmptyBoard();
        // Place silver rabbit at c7 -> (row1,col2)
        board[1] = ReplaceChar(board[1], 2, 'r');

        var state = new GameState(NotationService.BoardToAei(board, "s"));
        var game = new ArimaaGameService(state);

        var from = new Position(1, 2); // c7
        var to = new Position(2, 2);   // c6 (trap)
        var ok = game.TryMove(from, to);
        ok.Should().BeTrue();

        // After move, piece on trap without support should be removed
        state.GetPiece(new Position(2, 2)).Should().BeNull();
    }

    [Fact(DisplayName = "Move onto trap with adjacent friendly support → survives")]
    public void MoveOntoTrap_WithSupport_Survives()
    {
        // Trap: c6 -> (row2,col2)
        var board = EmptyBoard();
        // Silver rabbit starting at c7
        board[1] = ReplaceChar(board[1], 2, 'r');
        // Silver dog at d6 (row2,col3) provides support
        board[2] = ReplaceChar(board[2], 3, 'd');

        var state = new GameState(NotationService.BoardToAei(board, "s"));
        var game = new ArimaaGameService(state);

        var from = new Position(1, 2); // c7
        var to = new Position(2, 2);   // c6 (trap)
        var ok = game.TryMove(from, to);
        ok.Should().BeTrue();

        // Supported, should remain
        state.GetPiece(new Position(2, 2)).Should().NotBeNull();
    }

    [Fact(DisplayName = "Support moves away from trap piece → trapped piece captured")]
    public void SupportMovesAway_TrapPieceCaptured()
    {
        // Trap: c6 -> (row2,col2)
        var board = EmptyBoard();
        // Silver rabbit already on c6
        board[2] = ReplaceChar(board[2], 2, 'r');
        // Silver dog at d6 (row2,col3) provides support initially
        board[2] = ReplaceChar(board[2], 3, 'd');

        var state = new GameState(NotationService.BoardToAei(board, "s"));
        var game = new ArimaaGameService(state);

        // Move the supporting dog away: d6 -> d7 (row1,col3)
        var ok = game.TryMove(new Position(2, 3), new Position(1, 3));
        ok.Should().BeTrue();

        // After support leaves, the rabbit on trap should be captured
        state.GetPiece(new Position(2, 2)).Should().BeNull();
    }

    private static string ReplaceChar(string s, int index, char ch)
    {
        var arr = s.ToCharArray();
        arr[index] = ch;
        return new string(arr);
    }
}
