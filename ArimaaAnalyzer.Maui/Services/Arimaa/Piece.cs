namespace ArimaaAnalyzer.Maui.Services.Arimaa;

public sealed class Piece
{
    public Piece(PieceType type, Sides side)
    {
        Type = type;
        Side = side;
    }

    public PieceType Type { get; }
    public Sides Side { get; }

    public string SvgFileName =>
        Side switch
        {
            Sides.Gold => $"white-{Type}.svg",
            Sides.Silver => $"black-{Type}.svg",
            _ => ""
        };
}
