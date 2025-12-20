namespace ArimaaAnalyzer.Maui.Components;

/// <summary>
/// Configuration for an Arimaa board display.
/// Allows different board instances to have different sizes, behaviors, and visual settings.
/// </summary>
public class ArimaaBoardSettings
{
    /// <summary>
    /// Size of each square in pixels. Board will be 8x this size (plus outer UI if enabled).
    /// </summary>
    public int SquareSizePx { get; set; } = 56;

    private int? _innerSquareSizePx;

    /// <summary>
    /// Size of each square in pixels when the Outer UI is shown (inner board).
    /// Default is chosen so that: left button column (1 inner square) +
    /// inner board (8 inner squares) + right button column (1 inner square)
    /// has the same total width as the full-size outer board (8 * SquareSizePx).
    /// That is, 10 * InnerSquareSizePx == 8 * SquareSizePx =>
    /// InnerSquareSizePx = 0.8 * SquareSizePx.
    /// </summary>
    public int InnerSquareSizePx
    {
        get => _innerSquareSizePx ?? Math.Max(1, (SquareSizePx * 4) / 5);
        set => _innerSquareSizePx = value;
    }

    /// <summary>
    /// Size of piece images in pixels.
    /// </summary>
    public int PieceSizePx { get; set; } = 44;

    /// <summary>
    /// Whether to show the outer UI frame (top/bottom/left/right buttons/labels).
    /// </summary>
    public bool ShowOuterUi { get; set; } = false;

    /// <summary>
    /// Interactive behavior of the board (playable, spectator, etc.).
    /// </summary>
    public BoardBehavior Behavior { get; set; } = BoardBehavior.Playable;

    /// <summary>
    /// If true, the board will scale responsively to fill available viewport space.
    /// If false, board size is fixed based on SquareSizePx.
    /// </summary>
    public bool IsResponsive { get; set; } = true;

    /// <summary>
    /// Small responsive board (useful for sidebars or analysis panels).
    /// Size: ~40px per square, responsive, read-only.
    /// </summary>
    public static ArimaaBoardSettings Compact => new()
    {
        SquareSizePx = 40,
        PieceSizePx = 32,
        ShowOuterUi = false,
        Behavior = BoardBehavior.Spectator,
        IsResponsive = true
    };

    /// <summary>
    /// Large playable board (main game board).
    /// Size: ~56px per square, responsive, interactive.
    /// </summary>
    public static ArimaaBoardSettings PlayableLarge => new()
    {
        SquareSizePx = 56,
        PieceSizePx = 44,
        ShowOuterUi = false,
        Behavior = BoardBehavior.Playable,
        IsResponsive = true
    };

    /// <summary>
    /// Large board with outer UI controls enabled.
    /// Size: ~56px per square, responsive, interactive, with toolbars.
    /// </summary>
    public static ArimaaBoardSettings PlayableLargeWithUi => new()
    {
        SquareSizePx = 56,
        PieceSizePx = 44,
        ShowOuterUi = true,
        Behavior = BoardBehavior.Playable,
        IsResponsive = true
    };

    /// <summary>
    /// Fixed-size board (does not scale with viewport).
    /// Size: ~56px per square, fixed, interactive.
    /// </summary>
    public static ArimaaBoardSettings PlayableFixed => new()
    {
        SquareSizePx = 56,
        PieceSizePx = 44,
        ShowOuterUi = false,
        Behavior = BoardBehavior.Playable,
        IsResponsive = false
    };

    /// <summary>
    /// Tiny board for embedded display in lists or grids.
    /// Size: ~24px per square, responsive, read-only.
    /// </summary>
    public static ArimaaBoardSettings Tiny => new()
    {
        SquareSizePx = 24,
        PieceSizePx = 18,
        ShowOuterUi = false,
        Behavior = BoardBehavior.Spectator,
        IsResponsive = true
    };

    /// <summary>
    /// Medium board, good for tablets.
    /// Size: ~48px per square, responsive, interactive.
    /// </summary>
    public static ArimaaBoardSettings PlayableMedium => new()
    {
        SquareSizePx = 48,
        PieceSizePx = 38,
        ShowOuterUi = false,
        Behavior = BoardBehavior.Playable,
        IsResponsive = true
    };

    /// <summary>
    /// Default settings (same as PlayableLarge).
    /// </summary>
    public static ArimaaBoardSettings Default => PlayableLarge;
}
