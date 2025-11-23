using System.Threading.Tasks;
using ArimaaAnalyzer.Maui.Services;
using System;
using System.IO;
using FluentAssertions;
using Xunit;

namespace ArimaaAnalyzer.Tests.Services;

/// <summary>
/// Skeleton tests for AnalysisService. These are placeholders to be filled in with real engine paths and scenarios.
/// </summary>
public class AnalysisServiceTests
{
    // Example: use IAsyncLifetime if you later want shared setup/teardown.
    // public class AnalysisServiceFixture : IAsyncLifetime { ... }

    [Fact(DisplayName = "AnalysisService can be instantiated" )]
    public void CanCreateInstance()
    {
        var svc = new AnalysisService();
        svc.Should().NotBeNull();
        svc.IsRunning.Should().BeFalse();
    }

    [Fact(DisplayName = "StartAsync requires a valid engine executable", Skip = "Skeleton only: provide a real engine exe path and remove Skip.")]
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

    [Fact(DisplayName = "Sharp2015 AEI smoke test: handshake, isready, setoption, setposition, go (expects bestmove)")]
    public async Task Sharp2015_Aei_EndToEnd_SmokeTest()
    {
        // Absolute path provided by user
        var exePath = @"C:\\Users\\hjlyk\\RiderProjects\\arimaa-analyzer\\ArimaaAnalyzer.Maui\\Aiexecutables\\sharp2015.exe";

        if (!File.Exists(exePath))
        {
            // Can't truly mark as Skipped here without adding a conditional Fact or extra packages.
            // Treat as a no-op pass with a helpful message.
            Console.WriteLine($"[SKIP] Engine executable not found at '{exePath}'. Place sharp2015.exe there to run this test.");
            return;
        }

        static string BoardToAei(string[] board, string side)
        {
            // Replace dots with spaces and concatenate rows
            var flat = string.Join(string.Empty, Array.ConvertAll(board, r => r.Replace('.', ' ')));
            return $"setposition {side} \"{flat}\"";
        }

        await using var svc = new AnalysisService();
        try
        {
            // Start the engine with the 'aei' argument (matches Python helper) and the service will also send 'aei' over stdin
            await svc.StartAsync(exePath, arguments: "aei");

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
            await svc.SendAsync(BoardToAei(board, "g"));

            // Ready check after setting position
            await svc.IsReadyAsync();

            // Ask engine to move; capture output until bestmove
            var (best, ponder, log) = await svc.GoAsync(string.Empty);

            best.Should().NotBeNullOrWhiteSpace("engine should return a bestmove");
        }
        finally
        {
            await svc.QuitAsync();
        }
    }
}
