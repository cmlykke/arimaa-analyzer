using System.Threading.Tasks;
using ArimaaAnalyzer.Maui.Services;
using System;
using System.IO;
using FluentAssertions;
using Xunit;

namespace ArimaaAnalyzer.Tests.Services;

/// <summary>
/// Skeleton tests for AnalysisService.
/// These are placeholders to be filled in with real engine paths and scenarios.
/// </summary>
public class AnalysisServiceTests
{
    // Example: use IAsyncLifetime if you later want shared setup/teardown.
    // public class AnalysisServiceFixture : IAsyncLifetime { ... }
    // Point to the engine that lives under the MAUI project folder.
    // Tests run from bin/<config>/<tfm>, so we navigate up to solution root and into MAUI/Aiexecutables.
    private static readonly string ExePath = Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory,
        "..", "..", "..", "..", // up from bin/<cfg>/<tfm> to solution root
        "ArimaaAnalyzer.Maui", "Aiexecutables", "sharp2015.exe"));

    [Fact(DisplayName = "AnalysisService can be instantiated" )]
    public void CanCreateInstance()
    {
        var svc = new AnalysisService();
        svc.Should().NotBeNull();
        svc.IsRunning.Should().BeFalse();
    }

    [Fact(DisplayName = "StartAsync requires a valid engine executable", 
        Skip = "Skeleton only: provide a real engine exe path and remove Skip.")]
    public async Task StartAsync_RequiresValidEnginePath()
    {
        var svc = new AnalysisService();

        // Arrange: set an invalid path to demonstrate expected failure
        var invalidPath = "nonexistent-engine.exe";

        // Act
        var act = async () => await svc.StartAsync(invalidPath);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact(DisplayName = "Sharp2015 AEI smoke test: handshake, " +
                        "isready, setoption, setposition, go (expects bestmove)")]
    public async Task Sharp2015_Aei_EndToEnd_SmokeTest_goldToPlay()
    {
        var aei = $"setposition g \"rrrrrrrrhcdmedch                                HCDMEDCHRRRRRRRR\"";
        
        var move = await RunSharp2015AeiSmokeAsync(
            aeistring: aei,
            skipNote: string.Empty);

        move.Should().NotBeNullOrWhiteSpace("engine should return a bestmove sequence");
        move.Should().MatchRegex(@"^[A-Z][a-z]\d[a-z]( [A-Z][a-z]\d[a-z]){3}$");
    }
    
    [Fact( 
        DisplayName = "Sharp2015 AEI smoke test from the silver perspective.")]
    public async Task Sharp2015_Aei_EndToEnd_SmokeTest_silverToPlay()
    {
        var aei = $"setposition s \"rrrrrrrrecdmhdch                                HCDMRDCHERRRRRRR\"";
        
        var notflippedBoard = NotationService.AeiToBoard(aei);
        
        var test3 = "";
        
        var move = await RunSharp2015AeiSmokeAsync(
            aeistring: aei,
            skipNote: "Silver to play");
        
        move.Should().NotBeNullOrWhiteSpace("engine should return a bestmove sequence");
        move.Should().MatchRegex(@"^[a-z][a-z]\d[a-z]( [a-z][a-z]\d[a-z]){3}$");

    }
    

    [Fact(DisplayName = "BuildGameTurnTreeAsync throws on invalid args")]
    public async Task BuildGameTurnTreeAsync_InvalidArgs()
    {
        await using var svc = new AnalysisService();

        // null position node
        await Assert.ThrowsAsync<ArgumentNullException>(() => svc.BuildGameTurnTreeAsync(null!, 1));

        // depth <= 0
        var dummyAei = "setposition g \"xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx\"";
        var dummyNode = new GameTurn(dummyAei, dummyAei, "0", Sides.Gold, Array.Empty<string>());
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => svc.BuildGameTurnTreeAsync(dummyNode, 0));
    }

    [Fact(DisplayName = "BuildGameTurnTreeAsync builds a chain of requested depth when engine is available")]
    public async Task BuildGameTurnTreeAsync_Chain_WhenEnginePresent()
    {
        if (!File.Exists(ExePath))
        {
            Console.WriteLine($"[SKIP] Engine executable not found at '{ExePath}'. Place sharp2015.exe there to run this test.");
            false.Should().BeTrue();
        }

        await using var svc = new AnalysisService();
        try
        {
            // Start engine
            await svc.StartAsync(ExePath, arguments: "aei");

            // Use the same AEI position as the smoke test
            var aei = "setposition g \"rrrrrrrrhcdmedch                                HCDMEDCHRRRRRRRR\"";
            int depth = 4;

            var startNode = new GameTurn(aei, aei, "0", Sides.Gold, Array.Empty<string>());
            var root = await svc.BuildGameTurnTreeAsync(startNode, depth);

            root.Moves.Should().NotBeNull();
            root.Moves.Count.Should().BeGreaterThan(0);
            root.AEIstring.Should().NotBeNullOrWhiteSpace();
            root.IsMainLine.Should().BeFalse("AI-generated suggestions should not be flagged as main line");
            root.AEIstring.ToCharArray()[12].Should().Be('s');
            var rootmoves = string.Join(", ", root.Moves);
           
            
            var child1 = root.Children.FirstOrDefault();
            child1.Moves.Should().NotBeNull();
            child1.Moves.Count.Should().BeGreaterThan(0);
            child1.AEIstring.Should().NotBeNullOrWhiteSpace();
            child1.IsMainLine.Should().BeFalse("AI-generated suggestions should not be flagged as main line");
            child1.AEIstring.ToCharArray()[12].Should().Be('g');
            var child1moves = string.Join(", ", child1.Moves);
            
            
            var child2 = child1.Children.FirstOrDefault();
            child2.Moves.Should().NotBeNull();
            child2.Moves.Count.Should().BeGreaterThan(0);
            child2.AEIstring.Should().NotBeNullOrWhiteSpace();
            child2.IsMainLine.Should().BeFalse("AI-generated suggestions should not be flagged as main line");
            child2.AEIstring.ToCharArray()[12].Should().Be('s');
            var child2moves = string.Join(", ", child2.Moves);
            
            
            var child3 = child2.Children.FirstOrDefault();
            child3.Moves.Should().NotBeNull();
            child3.Moves.Count.Should().BeGreaterThan(0);
            child3.AEIstring.Should().NotBeNullOrWhiteSpace();
            child3.IsMainLine.Should().BeFalse("AI-generated suggestions should not be flagged as main line");
            child3.AEIstring.ToCharArray()[12].Should().Be('g');
            var child3moves = string.Join(", ", child3.Moves);
            
            
            child3.Children.Should().BeEmpty();
        }
        finally
        {
            await svc.QuitAsync();
        }
    }

    private static async Task<string> RunSharp2015AeiSmokeAsync(
        string aeistring, 
        string skipNote)
    {
        if (!File.Exists(ExePath))
        {
            // Can't truly mark as Skipped here without adding a new package.
            // Gracefully skip by returning early, but log a helpful message.
            var note = string.IsNullOrWhiteSpace(skipNote) ? string.Empty : $" {skipNote}";
            Console.WriteLine($"[SKIP] Engine executable not found at '{ExePath}'. Place sharp2015.exe there to run this test.{note}");
            false.Should().Be(true);
            return string.Empty;
        }

        await using var svc = new AnalysisService();
        try
        {
            await svc.StartAsync(ExePath, arguments: "aei");
            await svc.IsReadyAsync();
            await svc.NewGameAsync();
            var (best, ponder, log) = await svc.GetBestMoveAsync(aeistring, "tcmove", "2");
            return best;
        }
        finally
        {
            await svc.QuitAsync();
        }
    }
}
