using System.Collections.Generic;
using System.Linq;
using ArimaaAnalyzer.Maui.Services;
using ArimaaAnalyzer.Maui.Services.Arimaa;
using FluentAssertions;
using Xunit;

namespace ArimaaAnalyzer.Tests.Services;

public class CorrectMoveServiceTests
{
    private static string BoardToAei(string[] b, string side)
    {
        var flat = string.Join(string.Empty, System.Array.ConvertAll(b, r => r.Replace('.', ' ')));
        return $"setposition {side} \"{flat}\"";
    }

    private static string[] BaseBoard => new[]
    {
        "rrrrrrrr",
        "hdcemcdh",
        "........",
        "........",
        "........",
        "........",
        "HDCMECDH",
        "RRRRRRRR"
    };

    [Fact(DisplayName = "ComputeMoveSequence finds single slide step (Hh2n)")]
    public void SingleStep_Horse_h2_to_h3()
    {
        // before: base position, gold to move
        var before = new GameState(BoardToAei(BaseBoard, "g"));

        // after: move gold Horse from h2 -> h3 (row6,col7 -> row5,col7)
        var afterBoard = (string[])BaseBoard.Clone();
        // row indices: 0=top (rank8)..7=bottom (rank1)
        // row 6: "HDCMECDH" => set col 7 to '.'
        afterBoard[6] = ReplaceChar(afterBoard[6], 7, '.');
        // row 5: "........" => set col 7 to 'H'
        afterBoard[5] = ReplaceChar(afterBoard[5], 7, 'H');

        var after = new GameState(BoardToAei(afterBoard, "s"));

        var svc = new CorrectMoveService();
        var result = svc.ComputeMoveSequence(before, after, Side.Gold);

        result.Should().Be("Hh2n");
    }

    [Fact(DisplayName = "ComputeMoveSequence finds two slide steps (order-insensitive)")]
    public void TwoSteps_Hh2n_and_Dg2n()
    {
        var before = new GameState(BoardToAei(BaseBoard, "g"));

        // Move Horse h2 -> h3 and Dog g2 -> g3
        var afterBoard = (string[])BaseBoard.Clone();
        // h2 (row6,col7) to h3 (row5,col7)
        afterBoard[6] = ReplaceChar(afterBoard[6], 7, '.');
        afterBoard[5] = ReplaceChar(afterBoard[5], 7, 'H');
        // g2 (row6,col6) to g3 (row5,col6)
        afterBoard[6] = ReplaceChar(afterBoard[6], 6, '.');
        afterBoard[5] = ReplaceChar(afterBoard[5], 6, 'D');

        var after = new GameState(BoardToAei(afterBoard, "s"));

        var svc = new CorrectMoveService();
        var result = svc.ComputeMoveSequence(before, after, Side.Gold);

        // Accept either order since BFS may find any order of the two independent steps
        var steps = result.Split(' ', System.StringSplitOptions.RemoveEmptyEntries);
        steps.Should().HaveCount(2);
        steps.ToHashSet().Should().BeEquivalentTo(new HashSet<string> { "Hh2n", "Dg2n" });
    }

    [Fact(DisplayName = "ComputeMoveSequence returns error when no change or impossible")]
    public void NoChange_ReturnsError()
    {
        var before = new GameState(BoardToAei(BaseBoard, "g"));
        // after equals before side switched to Silver
        var after = new GameState(BoardToAei(BaseBoard, "s"));

        var svc = new CorrectMoveService();
        var result = svc.ComputeMoveSequence(before, after, Side.Gold);

        result.Should().Be("error");
    }

    [Fact(DisplayName = "Frozen piece cannot move (Cat frozen by stronger adjacent enemy)")]
    public void FrozenPiece_CannotMove_ReturnsError()
    {
        // Board setup:
        // - Gold Cat at d4 (row4,col3)
        // - Silver Dog at d5 (row3,col3) adjacent and stronger -> Cat is frozen
        // Attempted after-state moves Cat from d4 to d3; should be illegal -> "error"

        var before = EmptyBoard();
        // place Cat at d4
        before[4] = ReplaceChar(before[4], 3, 'C');
        // place silver Dog at d5
        before[3] = ReplaceChar(before[3], 3, 'd');

        var after = (string[])before.Clone();
        // try to move Cat d4 -> d3 (south)
        after[4] = ReplaceChar(after[4], 3, '.');
        after[5] = ReplaceChar(after[5], 3, 'C');

        var gsBefore = new GameState(BoardToAei(before, "g"));
        var gsAfter = new GameState(BoardToAei(after, "s"));

        var svc = new CorrectMoveService();
        var result = svc.ComputeMoveSequence(gsBefore, gsAfter, Side.Gold);

        result.Should().Be("error");
    }

    [Fact(DisplayName = "Trap capture from slide is applied (Cat to c3 without support)")]
    public void TrapCapture_FromSlide_RemovesPiece()
    {
        // Before: Gold Cat at c4 (row4,col2). No supporting friendlies around c3.
        // After: Cat steps c4->c3 and is immediately captured on trap (c3). Board should have no piece at c3.

        var before = EmptyBoard();
        // place Cat at c4
        before[4] = ReplaceChar(before[4], 2, 'C');

        // After: move to c3 then captured -> c4 empty, c3 empty
        var after = (string[])before.Clone();
        after[4] = ReplaceChar(after[4], 2, '.'); // vacate c4
        // ensure c3 empty
        after[5] = ReplaceChar(after[5], 2, '.');

        var gsBefore = new GameState(BoardToAei(before, "g"));
        var gsAfter = new GameState(BoardToAei(after, "s"));

        var svc = new CorrectMoveService();
        var result = svc.ComputeMoveSequence(gsBefore, gsAfter, Side.Gold);

        result.Should().Be("Cc4s");
    }

    [Fact(DisplayName = "Push/Pull required transition returns error (not implemented)")]
    public void PushPull_Required_ReturnsError()
    {
        // Before: Gold Elephant at d4, Silver Rabbit at d5
        // After:  Elephant at d5, Rabbit at d6 (a legal push in real Arimaa)
        // Our solver doesn't implement push/pull, so it must return "error".

        var before = EmptyBoard();
        // E at d4
        before[4] = ReplaceChar(before[4], 3, 'E');
        // r at d5
        before[3] = ReplaceChar(before[3], 3, 'r');

        var after = EmptyBoard();
        // E moved to d5
        after[3] = ReplaceChar(after[3], 3, 'E');
        // r pushed to d6
        after[2] = ReplaceChar(after[2], 3, 'r');

        var gsBefore = new GameState(BoardToAei(before, "g"));
        var gsAfter = new GameState(BoardToAei(after, "s"));

        var svc = new CorrectMoveService();
        var result = svc.ComputeMoveSequence(gsBefore, gsAfter, Side.Gold);

        result.Should().Be("error");
    }

    private static string ReplaceChar(string s, int index, char ch)
    {
        var arr = s.ToCharArray();
        arr[index] = ch;
        return new string(arr);
    }

    private static string[] EmptyBoard()
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

    // =============================
    // Push / Pull mechanics tests
    // =============================

    [Fact(DisplayName = "Push: Elephant pushes rabbit north (two-step)", Skip = "Push/Pull not implemented yet")]
    public void Push_Elephant_Pushes_Rabbit_North_TwoSteps()
    {
        // Before: Gold Elephant at d4 (row4,col3), Silver Rabbit at d5 (row3,col3)
        // Target push (legal in Arimaa): move r d5->d6, then E d4->d5 (two steps, order can vary)
        var before = EmptyBoard();
        before[4] = ReplaceChar(before[4], 3, 'E'); // d4
        before[3] = ReplaceChar(before[3], 3, 'r'); // d5

        var after = EmptyBoard();
        after[2] = ReplaceChar(after[2], 3, 'r'); // d6
        after[3] = ReplaceChar(after[3], 3, 'E'); // d5

        var gsBefore = new GameState(BoardToAei(before, "g"));
        var gsAfter = new GameState(BoardToAei(after, "s"));
        var svc = new CorrectMoveService();
        var result = svc.ComputeMoveSequence(gsBefore, gsAfter, Side.Gold);

        // Expected in the future (either order): "rd5n Ed4n" OR "Ed4n rd5n"
        // Once implemented, replace Skip with assertions like below:
        // var steps = result.Split(' ', System.StringSplitOptions.RemoveEmptyEntries);
        // steps.Should().HaveCount(2);
        // steps.ToHashSet().Should().BeEquivalentTo(new HashSet<string> { "rd5n", "Ed4n" });
        result.Should().NotBeNull(); // placeholder to avoid warnings
    }

    [Fact(DisplayName = "Push onto trap causes immediate capture", Skip = "Push/Pull not implemented yet")]
    public void Push_Onto_Trap_Captures_Immediately()
    {
        // Trap square: c3 -> (row5,col2)
        // Setup: Gold Elephant at c4 (row4,col2), Silver Rabbit at b3 (row5,col1), no silver support.
        // Push r b3->c3 (onto trap, no support) -> captured; then E moves b3.
        // After: E at b3 (row5,col1), c3 empty.
        var before = EmptyBoard();
        before[4] = ReplaceChar(before[4], 2, 'E'); // c4
        before[5] = ReplaceChar(before[5], 1, 'r'); // b3

        var after = EmptyBoard();
        after[5] = ReplaceChar(after[5], 1, 'E'); // E ends on b3
        // c3 remains empty (captured)

        var gsBefore = new GameState(BoardToAei(before, "g"));
        var gsAfter = new GameState(BoardToAei(after, "s"));
        var svc = new CorrectMoveService();
        var result = svc.ComputeMoveSequence(gsBefore, gsAfter, Side.Gold);

        // Expected later (either order): "rb3e Eb3w" OR "Eb3w rb3e" with capture at c3 after rb3e.
        result.Should().NotBeNull();
    }

    [Fact(DisplayName = "Pull: Elephant pulls rabbit south (two-step)", Skip = "Push/Pull not implemented yet")]
    public void Pull_Elephant_Pulls_Rabbit_South_TwoSteps()
    {
        // Setup for a pull: Gold Elephant at e5 (row3,col4), Silver Rabbit at e4 (row4,col4).
        // Pull south: first E e5->e4, then r e4->e3 (or the reverse order depending on convention).
        var before = EmptyBoard();
        before[3] = ReplaceChar(before[3], 4, 'E'); // e5
        before[4] = ReplaceChar(before[4], 4, 'r'); // e4

        var after = EmptyBoard();
        after[4] = ReplaceChar(after[4], 4, 'E'); // e4
        after[5] = ReplaceChar(after[5], 4, 'r'); // e3

        var gsBefore = new GameState(BoardToAei(before, "g"));
        var gsAfter = new GameState(BoardToAei(after, "s"));
        var svc = new CorrectMoveService();
        var result = svc.ComputeMoveSequence(gsBefore, gsAfter, Side.Gold);

        // Expected later: two steps, order-insensitive: {"Ee5s", "re4s"}
        result.Should().NotBeNull();
    }

    [Fact(DisplayName = "Attempt to pull off trap is illegal and should be error")]
    public void Pull_Off_Trap_Should_Return_Error()
    {
        // Enemy on trap c3 supported only by our pulling piece at c4.
        // If we move off c4 as first step, enemy on c3 loses all support and is captured immediately,
        // so the subsequent pull step cannot be made. Thus the target after-state (where enemy relocates) is illegal.
        // Set desired after-state as if a pull were attempted: E c4->b4, r c3->c4 (but this is impossible).
        var before = EmptyBoard();
        before[4] = ReplaceChar(before[4], 2, 'E'); // c4 (our support)
        before[5] = ReplaceChar(before[5], 2, 'r'); // c3 (enemy on trap)

        var after = EmptyBoard();
        after[4] = ReplaceChar(after[4], 1, 'E'); // pretend E moved to b4
        after[5] = ReplaceChar(after[5], 2, 'r'); // pretend r stayed or would move later (keep same to force impossibility)

        var gsBefore = new GameState(BoardToAei(before, "g"));
        var gsAfter = new GameState(BoardToAei(after, "s"));
        var svc = new CorrectMoveService();
        var result = svc.ComputeMoveSequence(gsBefore, gsAfter, Side.Gold);

        result.Should().Be("error");
    }

    [Fact(DisplayName = "Illegal push by weaker piece should return error")]
    public void IllegalPush_ByWeaker_ReturnsError()
    {
        // Horse (gold) adjacent to Elephant (silver); attempt after-state consistent with a push by horse.
        // Since a horse cannot push a stronger enemy (elephant), this should be impossible.
        var before = EmptyBoard();
        before[4] = ReplaceChar(before[4], 3, 'H'); // d4 gold horse
        before[3] = ReplaceChar(before[3], 3, 'e'); // d5 silver elephant

        var after = EmptyBoard();
        after[2] = ReplaceChar(after[2], 3, 'e'); // d6 (as if pushed)
        after[3] = ReplaceChar(after[3], 3, 'H'); // d5 (as if pusher advanced)

        var gsBefore = new GameState(BoardToAei(before, "g"));
        var gsAfter = new GameState(BoardToAei(after, "s"));
        var svc = new CorrectMoveService();
        var result = svc.ComputeMoveSequence(gsBefore, gsAfter, Side.Gold);

        result.Should().Be("error");
    }

    [Fact(DisplayName = "Push blocked because destination is occupied should return error")]
    public void Push_Blocked_Destination_ReturnsError()
    {
        // Setup: E at d4, r at d5, and a blocking piece at d6.
        // Attempted after-state matches a push to d6, which is impossible due to blockage.
        var before = EmptyBoard();
        before[4] = ReplaceChar(before[4], 3, 'E'); // d4
        before[3] = ReplaceChar(before[3], 3, 'r'); // d5
        before[2] = ReplaceChar(before[2], 3, 'h'); // d6 occupied by silver horse

        var after = EmptyBoard();
        after[2] = ReplaceChar(after[2], 3, 'r'); // pretend r at d6
        after[3] = ReplaceChar(after[3], 3, 'E'); // pretend E at d5

        var gsBefore = new GameState(BoardToAei(before, "g"));
        var gsAfter = new GameState(BoardToAei(after, "s"));
        var svc = new CorrectMoveService();
        var result = svc.ComputeMoveSequence(gsBefore, gsAfter, Side.Gold);

        result.Should().Be("error");
    }
}
