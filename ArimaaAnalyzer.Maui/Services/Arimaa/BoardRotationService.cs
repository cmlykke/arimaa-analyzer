using System;

namespace ArimaaAnalyzer.Maui.Services.Arimaa;

/// <summary>
/// Helper utilities to map coordinates and rotate a 64-char board string
/// between different <see cref="BoardOrientation"/> values. The canonical
/// (normalized) orientation in code is treated as GoldWestSIlverEast.
/// </summary>
public static class BoardRotationService
{
    /// <summary>
    /// Map a display-space coordinate (row, col) to the normalized-space
    /// coordinate according to the provided <paramref name="orientation"/>.
    /// Rows/cols are 0..7, row 0 is top, col 0 is left in display space.
    /// </summary>
    public static (int row, int col) MapDisplayToNormalized(int row, int col, BoardOrientation orientation)
    {
        return orientation switch
        {
            // Display equals normalized
            BoardOrientation.GoldWestSIlverEast => (row, col),

            // Display was produced by rotating normalized +90° CW -> invert by CCW
            BoardOrientation.GoldNorthSilverSouth => (7 - col, row),

            // Display was produced by rotating normalized 180° -> invert is same
            BoardOrientation.GoldEastSilverWest => (7 - row, 7 - col),

            // Display was produced by rotating normalized +270° CW -> invert by +90° CW
            BoardOrientation.GoldSouthSilverNorth => (col, 7 - row),

            _ => (row, col)
        };
    }

    /// <summary>
    /// Map a normalized-space coordinate (row, col) to display-space according
    /// to the provided <paramref name="orientation"/>.
    /// </summary>
    public static (int row, int col) MapNormalizedToDisplay(int row, int col, BoardOrientation orientation)
    {
        return orientation switch
        {
            BoardOrientation.GoldWestSIlverEast => (row, col),
            // Apply +90° CW
            BoardOrientation.GoldNorthSilverSouth => (col, 7 - row),
            // Apply 180°
            BoardOrientation.GoldEastSilverWest => (7 - row, 7 - col),
            // Apply +270° CW (i.e., 90° CCW)
            BoardOrientation.GoldSouthSilverNorth => (7 - col, row),
            _ => (row, col)
        };
    }

    public static int ToIndex(int row, int col) => row * 8 + col;

    /// <summary>
    /// Compute the character to render at a display-space coordinate from a
    /// normalized board string and the current orientation.
    /// </summary>
    public static char GetCharForDisplay(string normalizedBoard64, int displayRow, int displayCol, BoardOrientation orientation)
    {
        var (nr, nc) = MapDisplayToNormalized(displayRow, displayCol, orientation);
        return normalizedBoard64[ToIndex(nr, nc)];
    }

    /// <summary>
    /// Rotate a board string from one orientation to another, returning a new 64-char string.
    /// Both <paramref name="from"/> and <paramref name="to"/> are relative to the normalized
    /// definition GoldWestSIlverEast.
    /// </summary>
    public static string RotateBoardString(string board64, BoardOrientation from, BoardOrientation to)
    {
        if (board64 is null) throw new ArgumentNullException(nameof(board64));
        if (board64.Length != 64) throw new ArgumentException("Board string must be 64 chars", nameof(board64));

        // If orientations are the same, return as-is
        if (from == to) return board64;

        // Strategy: map each normalized cell to display (using 'from'), then from that display to the target normalized (inverse of 'to').
        // Equivalent to computing the delta rotation.
        var result = new char[64];

        for (var nr = 0; nr < 8; nr++)
        {
            for (var nc = 0; nc < 8; nc++)
            {
                var ch = board64[ToIndex(nr, nc)];
                // Where would (nr,nc) appear in display if we showed 'from'?
                var (dr, dc) = MapNormalizedToDisplay(nr, nc, from);
                // For that display cell, what's the target normalized position under 'to'?
                var (tnr, tnc) = MapDisplayToNormalized(dr, dc, to);
                result[ToIndex(tnr, tnc)] = ch;
            }
        }

        return new string(result);
    }

    /// <summary>
    /// Extract the 64-char board string from an AEI setposition string.
    /// Returns null if malformed.
    /// </summary>
    public static string? ExtractBoard64FromAei(string aeiSetPosition)
    {
        if (string.IsNullOrWhiteSpace(aeiSetPosition)) return null;
        var trimmed = aeiSetPosition.Trim();
        var firstQuote = trimmed.IndexOf('"');
        var lastQuote = trimmed.LastIndexOf('"');
        if (firstQuote < 0 || lastQuote <= firstQuote) return null;
        var payload = trimmed.Substring(firstQuote + 1, lastQuote - firstQuote - 1);
        return payload.Length == 64 ? payload : null;
    }

    /// <summary>
    /// Build a 64-char display board string directly from an AEI string and the desired orientation.
    /// Assumes the AEI board string is in the canonical normalized orientation (GoldWestSIlverEast).
    /// </summary>
    public static string BuildDisplayBoard64(string aeiSetPosition, BoardOrientation orientation)
    {
        var board64 = ExtractBoard64FromAei(aeiSetPosition)
                       ?? throw new ArgumentException("Malformed AEI setposition string.", nameof(aeiSetPosition));

        var result = new char[64];
        for (var r = 0; r < 8; r++)
        {
            for (var c = 0; c < 8; c++)
            {
                result[ToIndex(r, c)] = GetCharForDisplay(board64, r, c, orientation);
            }
        }
        return new string(result);
    }
}
