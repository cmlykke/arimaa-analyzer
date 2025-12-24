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
    private static readonly string ExePath = Path.Combine(AppContext.BaseDirectory, "Aiexecutables", "sharp2015.exe");

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
            // Can't truly mark as Skipped here without adding a conditional Fact or extra packages.
            // Treat as a no-op pass with a helpful message.
            Console.WriteLine($"[SKIP] Engine executable not found at " +
                              $"'{ExePath}'. Place sharp2015.exe there to run this test.");
            false.Should().BeTrue("Test should fail if an Arimaa AI is not found");
        }

        await using var svc = new AnalysisService();
        try
        {
            // Start the engine with the 'aei' argument
            // (matches Python helper) and the service will also send 'aei' over stdin
            await svc.StartAsync(ExePath, arguments: "aei");

            // Ensure engine is ready
            await svc.IsReadyAsync();

            // Set depth like the Python example
            await svc.SetOptionAsync("depth", "6");
            await svc.NewGameAsync();

            var board = new[]
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

            // Send setposition in the exact format Sharp expects
            await svc.SendAsync(NotationService.BoardToAei(board, Sides.Gold));

            // Ready check after setting position
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
}
