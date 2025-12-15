using System.Collections.Generic;
using System.Linq;
using ArimaaAnalyzer.Maui.Services;
using ArimaaAnalyzer.Maui.Services.Arimaa;
using FluentAssertions;
using Xunit;

namespace ArimaaAnalyzer.Tests.Services;

public class CorrectMoveServiceTests
{

    private static string[] BaseBoard => new[]
    {
        "RRRRRRRR",
        "HDCEMCDH",
        "........",
        "........",
        "........",
        "........",
        "hdcmecdh",
        "rrrrrrrr"
    };

    [Fact(DisplayName = "ComputeMoveSequence finds single slide step (Hh2n)")]
    public void SingleStep_Horse_h2_to_h3()
    {
        // before: base position, gold to move
        var before = new GameState(NotationService.BoardToAei(BaseBoard, Sides.Gold));

        // after: move gold Horse from h2 -> h3 (row6,col7 -> row5,col7)
        var afterBoard = (string[])BaseBoard.Clone();
        // row indices: 0=top (rank8)..7=bottom (rank1)
        // row 6: "HDCMECDH" => set col 7 to '.'
        afterBoard[1] = ReplaceChar(afterBoard[1], 3, '.');
        // row 5: "........" => set col 7 to 'H'
        afterBoard[2] = ReplaceChar(afterBoard[2], 3, 'E');

        var after = new GameState(NotationService.BoardToAei(afterBoard, Sides.Silver));

        var result = CorrectMoveService.ComputeMoveSequence(before, after);

        Console.WriteLine("SingleStep_Horse_h2_to_h3()");
        NotationService.PrintBoard(before.localAeiSetPosition);
        NotationService.PrintBoard(after.localAeiSetPosition);
        Console.WriteLine("Ed2n");
        
        result.Item1.Should().Be("Ed2n");
    }

    [Fact(DisplayName = "ComputeMoveSequence finds two slide steps (order-insensitive)")]
    public void TwoSteps_Hh2n_and_Dg2n()
    {
        var before = new GameState(NotationService.BoardToAei(BaseBoard, Sides.Gold));

        // Move Horse h2 -> h3 and Dog g2 -> g3
        var afterBoard = (string[])BaseBoard.Clone();
        // h2 (row6,col7) to h3 (row5,col7)
        afterBoard[1] = ReplaceChar(afterBoard[1], 0, '.');
        afterBoard[2] = ReplaceChar(afterBoard[2], 0, 'H');
        // g2 (row6,col6) to g3 (row5,col6)
        afterBoard[1] = ReplaceChar(afterBoard[1], 1, '.');
        afterBoard[2] = ReplaceChar(afterBoard[2], 1, 'D');

        var after = new GameState(NotationService.BoardToAei(afterBoard, Sides.Silver));

        var result = CorrectMoveService.ComputeMoveSequence(before, after);

        // Accept either order since BFS may find any order of the two independent steps
        result.Item1.Should().Be("Ha2n Db2n");
    }

    [Fact(DisplayName = "ComputeMoveSequence returns error when no change or impossible")]
    public void NoChange_ReturnsError()
    {
        var before = new GameState(NotationService.BoardToAei(BaseBoard, Sides.Gold));
        // after equals before side switched to Silver
        var after = new GameState(NotationService.BoardToAei(BaseBoard, Sides.Silver));

        var result = CorrectMoveService.ComputeMoveSequence(before, after);

        result.Item1.Should().Be("error");
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

        var gsBefore = new GameState(NotationService.BoardToAei(before, Sides.Gold));
        var gsAfter = new GameState(NotationService.BoardToAei(after, Sides.Silver));

        // This is a silver move (before: 'setposition s', after: 'setposition g'),
        // so sideToMove must be Silver. Passing Gold would force the engine to
        // try to explain the change via gold actions (slides/push/pull), which is impossible
        // when only silver pieces have moved.
        var result = CorrectMoveService.ComputeMoveSequence(gsBefore, gsAfter);

        result.Item1.Should().Be("error");
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

        var gsBefore = new GameState(NotationService.BoardToAei(before, Sides.Gold));
        var gsAfter = new GameState(NotationService.BoardToAei(after, Sides.Silver));

        var result = CorrectMoveService.ComputeMoveSequence(gsBefore, gsAfter);

        Console.WriteLine("TrapCapture_FromSlide_RemovesPiece()");
        NotationService.PrintBoard(gsBefore.localAeiSetPosition);
        NotationService.PrintBoard(gsAfter.localAeiSetPosition);
        Console.WriteLine("Ed2n");
        
        result.Item1.Should().Be("Cc5n");
    }

    [Fact(DisplayName = "Push/Pull required transition is now supported (push)")]
    public void PushPull_Required_IsSupported_Push()
    {
        // Before: Gold Elephant at d4, Silver Rabbit at d5
        // After:  Elephant at d5, Rabbit at d6 (a legal push in real Arimaa)
        // Solver implements push/pull, expect two-step push sequence.

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

        var gsBefore = new GameState(NotationService.BoardToAei(before, Sides.Gold));
        var gsAfter = new GameState(NotationService.BoardToAei(after, Sides.Silver));

        var result = CorrectMoveService.ComputeMoveSequence(gsBefore, gsAfter);
        
        Console.WriteLine("PushPull_Required_IsSupported_Push()");
        NotationService.PrintBoard(gsBefore.localAeiSetPosition);
        NotationService.PrintBoard(gsAfter.localAeiSetPosition);
        Console.WriteLine("Rd4s Ed5s");
        
        result.Item1.Should().Be("Rd4s Ed5s");
    }
    
    [Fact(DisplayName = "An Elephant implicitly push and pull a rabbit, or walk arround it")]
    public void MoveAroundOrPushPull()
    {
        // Before: Gold Elephant at d5, Silver Rabbit at d4
        // After:  Elephant at d3, Rabbit at d4 (either walk around or a legal push and pull)

        var before = EmptyBoard();
        // E at d5
        before[4] = ReplaceChar(before[4], 3, 'E');
        // r at d4
        before[3] = ReplaceChar(before[3], 3, 'r');

        var after = EmptyBoard();
        // E moved to d3
        after[4] = ReplaceChar(after[4], 3, '.');
        after[3] = ReplaceChar(after[3], 3, 'r');
        after[2] = ReplaceChar(after[2], 3, 'E');

        var gsBefore = new GameState(NotationService.BoardToAei(before, Sides.Gold));
        var gsAfter = new GameState(NotationService.BoardToAei(after, Sides.Silver));

        Console.WriteLine("An Elephant implicitly push and pull a rabbit, or walk arround it");
        NotationService.PrintBoard(gsBefore.localAeiSetPosition);
        NotationService.PrintBoard(gsAfter.localAeiSetPosition);
        Console.WriteLine("Rd4s Ed5s");
        
        var result = CorrectMoveService.ComputeMoveSequence(gsBefore, gsAfter);
        
        result.Item1.Should().Be("Ed5e Ee5s Ee4s Ee3w");
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

    [Fact(DisplayName = "Push: Elephant pushes rabbit north (two-step)")]
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

        var gsBefore = new GameState(NotationService.BoardToAei(before, Sides.Gold));
        var gsAfter = new GameState(NotationService.BoardToAei(after, Sides.Silver));
        
        var result = CorrectMoveService.ComputeMoveSequence(gsBefore, gsAfter);

        result.Item1.Should().Be("Rd4s Ed5s"); // push = enemy moves first, then pusher
    }

    [Fact(DisplayName = "Push onto trap causes immediate capture")]
    public void Push_Onto_Trap_Captures_Immediately()
    {
        // Trap square: c3 -> (row5,col2)
        // Setup: Gold Elephant at a3 (row5,col0), Silver Rabbit at b3 (row5,col1), no silver support.
        // Push r b3->c3 (onto trap, no support) -> captured; then E moves a3->b3.
        // After: E at b3 (row5,col1), c3 empty.
        var before = EmptyBoard();
        before[5] = ReplaceChar(before[5], 0, 'E'); // a3
        before[5] = ReplaceChar(before[5], 1, 'r'); // b3

        var after = EmptyBoard();
        after[5] = ReplaceChar(after[5], 1, 'E'); // E ends on b3
        // c3 remains empty (captured)

        var gsBefore = new GameState(NotationService.BoardToAei(before, Sides.Gold));
        var gsAfter = new GameState(NotationService.BoardToAei(after, Sides.Silver));
        
        var result = CorrectMoveService.ComputeMoveSequence(gsBefore, gsAfter);

        result.Item1.Should().Be("Rb6e Ea6e");
    }

    [Fact(DisplayName = "Pull: Elephant pulls rabbit north (two-step)")]
    public void Pull_Elephant_Pulls_Rabbit_South_TwoSteps()
    {
        // Setup for a pull: Gold Elephant at e5 (row3,col4), Silver Rabbit at e4 (row4,col4).
        // Pull north: first E e5->e6, then r e4->e5.
        var before = EmptyBoard();
        before[3] = ReplaceChar(before[3], 4, 'E'); // e5
        before[4] = ReplaceChar(before[4], 4, 'r'); // e4

        var after = EmptyBoard();
        after[2] = ReplaceChar(after[2], 4, 'E'); // e6
        after[3] = ReplaceChar(after[3], 4, 'r'); // e5

        var gsBefore = new GameState(NotationService.BoardToAei(before, Sides.Gold));
        var gsAfter = new GameState(NotationService.BoardToAei(after, Sides.Silver));
        
        var result = CorrectMoveService.ComputeMoveSequence(gsBefore, gsAfter);

        result.Item1.Should().Be("Ee4s Re5s");
    }

    [Fact(DisplayName = "Illegal pull by weaker piece should return error")]
    public void IllegalPull_ByWeaker_ReturnsError()
    {
        // Gold Horse attempts to pull Silver Camel (stronger) — illegal.
        // Before: H at d4, m at d5. After (as if pull): H at d5, m at d4.
        var before = EmptyBoard();
        before[4] = ReplaceChar(before[4], 3, 'H'); // d4
        before[3] = ReplaceChar(before[3], 3, 'm'); // d5 (silver camel)

        var after = EmptyBoard();
        after[3] = ReplaceChar(after[3], 3, 'H'); // d5
        after[4] = ReplaceChar(after[4], 3, 'm'); // d4

        var gsBefore = new GameState(NotationService.BoardToAei(before, Sides.Gold));
        var gsAfter = new GameState(NotationService.BoardToAei(after, Sides.Silver));

        var result = CorrectMoveService.ComputeMoveSequence(gsBefore, gsAfter);

        result.Item1.Should().Be("error");
    }

    [Fact(DisplayName = "Rabbit cannot initiate pull by moving backward")]
    public void Rabbit_Cannot_Pull_Backward()
    {
        // Gold Rabbit at d4, Silver Cat at d5.
        // Attempted pull south (backward for gold rabbit): R d4->d3, then c d5->d4.
        var before = EmptyBoard();
        before[4] = ReplaceChar(before[4], 3, 'R'); // d4 gold rabbit
        before[3] = ReplaceChar(before[3], 3, 'c'); // d5 silver cat

        var after = EmptyBoard();
        after[5] = ReplaceChar(after[5], 3, 'R'); // d3
        after[4] = ReplaceChar(after[4], 3, 'c'); // d4

        var gsBefore = new GameState(NotationService.BoardToAei(before, Sides.Gold));
        var gsAfter = new GameState(NotationService.BoardToAei(after, Sides.Silver));

        var result = CorrectMoveService.ComputeMoveSequence(gsBefore, gsAfter);

        result.Item1.Should().Be("error");
    }

    [Fact(DisplayName = "Frozen pusher cannot push or pull")]
    public void FrozenPusher_CannotPushOrPull_ReturnsError()
    {
        // Gold Cat at d4 is frozen by adjacent stronger Silver Dog at c4 (no friendly support).
        // There is also a Silver Rabbit at d5. Attempt an after-state as if Cat pushed the rabbit: Rd5n Cd4n.
        var before = EmptyBoard();
        before[4] = ReplaceChar(before[4], 3, 'C'); // d4 gold cat (pusher, but frozen)
        before[4] = ReplaceChar(before[4], 2, 'd'); // c4 silver dog (freezes C)
        before[3] = ReplaceChar(before[3], 3, 'r'); // d5 silver rabbit (target to push)

        var after = EmptyBoard();
        after[2] = ReplaceChar(after[2], 3, 'r'); // d6 (as if pushed)
        after[3] = ReplaceChar(after[3], 3, 'C'); // d5 (as if cat advanced)

        var gsBefore = new GameState(NotationService.BoardToAei(before, Sides.Gold));
        var gsAfter = new GameState(NotationService.BoardToAei(after, Sides.Silver));

        var result = CorrectMoveService.ComputeMoveSequence(gsBefore, gsAfter);

        result.Item1.Should().Be("error");
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

        var gsBefore = new GameState(NotationService.BoardToAei(before, Sides.Gold));
        var gsAfter = new GameState(NotationService.BoardToAei(after, Sides.Silver));
        
        var result = CorrectMoveService.ComputeMoveSequence(gsBefore, gsAfter);

        result.Item1.Should().Be("error");
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

        var gsBefore = new GameState(NotationService.BoardToAei(before, Sides.Gold));
        var gsAfter = new GameState(NotationService.BoardToAei(after, Sides.Silver));
        
        var result = CorrectMoveService.ComputeMoveSequence(gsBefore, gsAfter);

        result.Item1.Should().Be("error");
    }
    
    // move two silver pieecs:
    // setposition s "RCRDRRRR  REDCHRHM                      rh        checmdrrrrdrrr"
    
    [Fact(DisplayName = "I had a failure where two states couldnt be computed after two siler pieces were moved")]
    public void Push_Blocked_Destination_TwoSilverOiecesMovedShouldNotReturnErrorr()
    {
        var before = "setposition s \"RCRDRRRR  REDCHRHM                              rhchecmdrrrrdrrr\"";
        var after = "setposition g \"RCRDRRRR  REDCHRHM                      rh        checmdrrrrdrrr\"";
        

        var gsBefore = new GameState(before);
        var gsAfter = new GameState(after);
        
        var result = CorrectMoveService.ComputeMoveSequence(gsBefore, gsAfter);

        Console.WriteLine("SingleStep_Horse_h2_to_h3()");
        NotationService.PrintBoard(before);
        NotationService.PrintBoard(after);
        Console.WriteLine("Hh7s");
        
        result.Item1.Should().Be("Ra7s Hb7s");
    }
    
    /*
    [Fact(DisplayName = "An Elephant must implicitly push and pull a rabbit")]
    public void ElephantMustImplicitlyPushPull()
    {
        // Before: Gold Elephant at d5, Silver Rabbit at d4, Gold rabbits at c3 and e3
        // After:  Elephant at d3, Rabbit at d4 (Elephant must push and pull)

        var before = EmptyBoard();
        // E at d5
        before[4] = ReplaceChar(before[4], 3, 'E');
        before[4] = ReplaceChar(before[4], 3, 'E');
        before[2] = ReplaceChar(before[2], 2, 'R');
        before[2] = ReplaceChar(before[2], 4, 'R');
        // r at d4
        before[3] = ReplaceChar(before[3], 3, 'r');

        var after = EmptyBoard();
        // E moved to d3
        after[4] = ReplaceChar(after[4], 3, '.');
        after[3] = ReplaceChar(after[3], 3, 'r');
        after[2] = ReplaceChar(after[2], 3, 'E');
        after[2] = ReplaceChar(after[2], 2, 'R');
        after[2] = ReplaceChar(after[2], 4, 'R');

        var gsBefore = new GameState(NotationService.BoardToAei(before, Sides.Gold));
        var gsAfter = new GameState(NotationService.BoardToAei(after, Sides.Silver));

        Console.WriteLine("An Elephant implicitly push and pull a rabbit, or walk arround it");
        NotationService.PrintBoard(gsBefore.localAeiSetPosition);
        NotationService.PrintBoard(gsAfter.localAeiSetPosition);
        Console.WriteLine("Rd4s Ed5s");
        
        var result = CorrectMoveService.ComputeMoveSequence(gsBefore, gsAfter);
        
        result.Item1.Should().NotBe("error");
    }*/
//setposition g "                  R R      r       E                            "
    //setposition s "                  RER      r                                    "
}
