using System.Threading.Tasks;
using ArimaaAnalyzer.Maui.Services;
using System;
using System.IO;
using FluentAssertions;
using Xunit;
using YourApp.Models;

namespace ArimaaAnalyzer.Tests.Services;


public class ArimaaGameServiceTests
{
    [Fact(DisplayName = "ArimaaGameService can handle four move steps" )]
    public void TryParse_ValidGame_ReturnsTrueAndPopulatesGame()
    {
        var notation = @"1w Ed2 Mb2 Ha2 Hg2
1b ra7 hb7 cc7
2w Ed2n Ed3n";

        var success = ArimaaGameService.TryParse(notation, out var game, out var error);

        Assert.True(success);
        Assert.NotNull(game);
        Assert.Null(error);
        Assert.Equal(3, game.Moves.Count);
        Assert.Contains("1w Ed2 Mb2 Ha2 Hg2", game.RawNotation);
    }
    
    [Fact(DisplayName = "ArimaaGameService invalidates plain text input" )]
    public void TryParse_InvalidLine_ReturnsFalseWithError()
    {
        var notation = @"1w Valid move
Invalid line here
2b another move";

        var success = ArimaaGameService.TryParse(notation, out var game, out var error);

        Assert.False(success);
        Assert.Null(game);
        Assert.Contains("Invalid move line format", error);
    }

    [Fact(DisplayName = "ArimaaGameService invalidates the empty string" )]
    public void TryParse_Empty_ReturnsFalse()
    {
        var success = ArimaaGameService.TryParse("", out var game, out var error);
        Assert.False(success);
        Assert.Contains("cannot be null or empty", error);
    }
}





