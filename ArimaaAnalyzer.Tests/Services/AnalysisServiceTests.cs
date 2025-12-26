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
    public async Task Sharp2015_Aei_EndToEnd_SmokeTest()
    {
        
        Console.WriteLine("Test best move 1");
        
        if (!File.Exists(ExePath))
        {
            // Can't truly mark as Skipped here without adding a new package.
            // Gracefully skip by returning early, but log a helpful message.
            Console.WriteLine($"[SKIP] Engine executable not found at '{ExePath}'. " +
                              "Place sharp2015.exe there to run this test.");
            false.Should().Be(true);
        }

        await using var svc = new AnalysisService();
        try
        {
            // Start the engine with the 'aei' argument
            // (matches Python helper) and the service will also send 'aei' over stdin
            await svc.StartAsync(ExePath, arguments: "aei");

            // Ensure engine is ready
            await svc.IsReadyAsync();

            // Start a new game first (some engines reset options on newgame)
            await svc.NewGameAsync();

            // Send setposition EXACTLY as in the working Python example to avoid formatting mismatches
            await svc.SendAsync("setposition g \"rrrrrrrrhcdmedch                                HCDMEDCHRRRRRRRR\"");

            // Now set per-move time AFTER setting up the position, matching Python order
            await svc.SetOptionAsync("tcmove", "2");

            // Ready check after setting position and options
            await svc.IsReadyAsync();

            // Ask engine to move; capture output until bestmove
            var (best, ponder, 
                log) = await svc.GoAsync(string.Empty);

            best.Should().NotBeNullOrWhiteSpace(
                "engine should return a bestmove sequence");

            Console.WriteLine("Test best move");
            
            // Validate our parsing matches the engine's raw line by reconstructing expected sequence
            best.Should().MatchRegex(
                @"^[A-Z][a-z]\d[a-z]( [A-Z][a-z]\d[a-z]){3}$");
        }
        finally
        {
            await svc.QuitAsync();
        }
    }

    [Fact(DisplayName = "BuildGameTurnTreeAsync throws on invalid args")]
    public async Task BuildGameTurnTreeAsync_InvalidArgs()
    {
        await using var svc = new AnalysisService();

        await Assert.ThrowsAsync<ArgumentNullException>(() => svc.BuildGameTurnTreeAsync("", 1));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => svc.BuildGameTurnTreeAsync("setposition g \"xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx\"", 0));
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
            int depth = 2;

            var root = await svc.BuildGameTurnTreeAsync(aei, depth);

            // Verify chain length equals depth
            int count = 0;
            var node = root;
            while (node is not null)
            {
                count++;
                // minimal validations
                node.Moves.Should().NotBeNull();
                node.Moves.Count.Should().BeGreaterThan(0);
                node.AEIstring.Should().NotBeNullOrWhiteSpace();
                node.IsMainLine.Should().BeFalse("AI-generated suggestions should not be flagged as main line");
                node = node.Children.FirstOrDefault();
            }
            count.Should().Be(depth);
        }
        finally
        {
            await svc.QuitAsync();
        }
    }

}
