using ArimaaAnalyzer.Maui.Services.Arimaa;

namespace ArimaaAnalyzer.Maui.Components;

public sealed class ArimaaBoardSettings
{
    // Make properties mutable so pages can toggle settings at runtime (e.g., ShowOuterUi)
    public double SquareSizePx { get; set; } = 56; // maps to --square-size
    public double PieceSizePx { get; set; } = 44;  // maps to --piece-size
    public bool ShowOuterUi { get; set; } = false;
    public BoardBehavior Behavior { get; set; } = BoardBehavior.Playable;

    // Presets
    public static ArimaaBoardSettings PlayableSmall => new()
    {
        SquareSizePx = 44,
        PieceSizePx = 36,
        ShowOuterUi = false,
        Behavior = BoardBehavior.Playable
    };

    public static ArimaaBoardSettings PlayableMedium => new()
    {
        SquareSizePx = 56,
        PieceSizePx = 44,
        ShowOuterUi = false,
        Behavior = BoardBehavior.Playable
    };

    public static ArimaaBoardSettings PlayableLarge => new()
    {
        SquareSizePx = 72,
        PieceSizePx = 58,
        ShowOuterUi = true,
        Behavior = BoardBehavior.Playable
    };

    public static ArimaaBoardSettings SpectatorSmall => new()
    {
        SquareSizePx = 36,
        PieceSizePx = 30,
        ShowOuterUi = false,
        Behavior = BoardBehavior.Spectator
    };

    public static ArimaaBoardSettings SpectatorMedium => new()
    {
        SquareSizePx = 48,
        PieceSizePx = 40,
        ShowOuterUi = false,
        Behavior = BoardBehavior.Spectator
    };

    public static ArimaaBoardSettings SpectatorLarge => new()
    {
        SquareSizePx = 64,
        PieceSizePx = 52,
        ShowOuterUi = true,
        Behavior = BoardBehavior.Spectator
    };

    // Default
    public static ArimaaBoardSettings Default => PlayableMedium;
}
