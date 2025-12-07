using System.Threading.Tasks;
using ArimaaAnalyzer.Maui.Services;
using System;
using System.IO;
using FluentAssertions;
using Xunit;
using YourApp.Models;

namespace ArimaaAnalyzer.Tests.Services;


public class NotationServiceTests
{
    
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
    
    private static string[] EmptyBoard => new[]
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

    private static string[] MixedBoard => new[]
    {
        ".rrr.rrr",
        "h.cemcdh",
        "........",
        ".d......",
        "....E...",
        "........",
        "HDCM.CDH",
        "R.RRR.RR"
    };
    
    
    [Fact(DisplayName = "NotationService.BoardToAei can convert to a string, and AeiToBoard back again on empty board")]
    public void ConvertToStringAndBack_emptyboard()
    {
        // before: base position, gold to move
        var singleString = NotationService.BoardToAei(EmptyBoard, "g");

        singleString.Should().Be("setposition g \"                                                                \"");

        var newShapeBoard = NotationService.AeiToBoard(singleString);
        
        newShapeBoard.Should().BeEquivalentTo(EmptyBoard);
        
    }
    
    [Fact(DisplayName = "NotationService.BoardToAei can convert to a string, and AeiToBoard back again ")]
    public void ConvertToStringAndBack_startingboard()
    {
        // before: base position, gold to move
        var singleString = NotationService.BoardToAei(BaseBoard, "g");

        singleString.Should().Be("setposition g \"rrrrrrrrhdcemcdh                                HDCMECDHRRRRRRRR\"");

        var newShapeBoard = NotationService.AeiToBoard(singleString);
        
        newShapeBoard.Should().BeEquivalentTo(BaseBoard);
        
    }
    
    [Fact(DisplayName = "NotationService.BoardToAei can convert to a string, and AeiToBoard back again On mixed Board")]
    public void ConvertToStringAndBack_mixedBoard()
    {
        // before: base position, gold to move
        var singleString = NotationService.BoardToAei(MixedBoard, "g");

        singleString.Should().Be("setposition g \" rrr rrrh cemcdh         d          E           HDCM CDHR RRR RR\"");

        var newShapeBoard = NotationService.AeiToBoard(singleString);
        
        newShapeBoard.Should().BeEquivalentTo(MixedBoard);
        
    }
}