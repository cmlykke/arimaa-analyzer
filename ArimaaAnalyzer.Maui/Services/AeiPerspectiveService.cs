using System;
using System.Text.RegularExpressions;

namespace ArimaaAnalyzer.Maui.Services;

/// <summary>
/// Service for transforming AEI position strings by perspective.
/// </summary>
public static class AeiPerspectiveService
{
    private static readonly Regex AeiRegex = new(
        "setposition\\s+([gs])\\s+\"([^\"]*)\"",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>
    /// Extracts the side to move from an AEI string.
    /// </summary>
    /// <param name="aei">AEI position string in the form: setposition g|s "<64-char flat>"</param>
    /// <returns>'g' for gold to move, or 's' for silver to move.</returns>
    /// <exception cref="ArgumentException">Thrown if the AEI format is invalid or input is empty.</exception>
    public static char GetSideToMove(string aei)
    {
        if (string.IsNullOrWhiteSpace(aei))
            throw new ArgumentException("AEI string is null or empty.", nameof(aei));

        var m = AeiRegex.Match(aei);
        if (!m.Success)
            throw new ArgumentException("Invalid AEI format. Expected: setposition g|s \"<64-chars>\"");

        return char.ToLowerInvariant(m.Groups[1].Value[0]); // guaranteed by regex to be g or s
    }

    /// <summary>
    /// Ensures that the side to move (per AEI) is positioned at the bottom of the board.
    /// - If the AEI is gold to move, the string is returned unchanged (gold already plays from the south).
    /// - If the AEI is silver to move, the board is flipped vertically so that silver pieces are at the bottom,
    ///   and the side-to-move remains silver.
    /// </summary>
    /// <param name="aei">AEI position string in the form: setposition g|s "<64-char flat>"</param>
    /// <returns>Transformed AEI string.</returns>
    /// <exception cref="ArgumentException">Thrown if the AEI format is invalid.</exception>
    public static string ensureMoverAtBottom(string aei)
    {
        if (string.IsNullOrWhiteSpace(aei))
            throw new ArgumentException("AEI string is null or empty.", nameof(aei));

        // Determine side to move using the extracted function
        var side = GetSideToMove(aei);

        // Extract board flat string
        var m = AeiRegex.Match(aei);
        if (!m.Success)
            throw new ArgumentException("Invalid AEI format. Expected: setposition g|s \"<64-chars>\"");

        var flat = m.Groups[2].Value; // spaces denote empty squares

        if (flat.Length != 64)
            throw new ArgumentException($"AEI board must be exactly 64 characters, but was {flat.Length}.");

        // If gold to move, return as-is.
        if (side == 'g')
            return aei;

        // Silver to move: flip vertically so silver is at the bottom.
        var flipped = FlipVertical(flat);
        var swappedcaps = SwapCase(flipped);
        var finalAEI = $"setposition g \"{swappedcaps}\"";
        return finalAEI;
    }

    /// <summary>
    /// Returns a copy of the provided AEI string with letter casing inverted.
    /// Uppercase letters become lowercase and vice versa; non-letters are unchanged.
    /// </summary>
    /// <param name="aei">Any AEI-related string.</param>
    /// <returns>String with inverted letter casing.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="aei"/> is null.</exception>
    public static string SwapCase(string aei)
    {
        if (aei is null) throw new ArgumentNullException(nameof(aei));

        var buffer = aei.ToCharArray();
        for (int i = 0; i < buffer.Length; i++)
        {
            char ch = buffer[i];
            if (char.IsLetter(ch))
            {
                buffer[i] = char.IsUpper(ch)
                    ? char.ToLowerInvariant(ch)
                    : char.ToUpperInvariant(ch);
            }
        }

        return new string(buffer);
    }

    /// <summary>
    /// Converts a gold-formatted move string into the equivalent silver-formatted move string.
    /// - Swaps piece letter case (gold pieces upper -> lower; silver lower -> upper).
    /// - Mirrors ranks using r' = 9 - r (so 1 <-> 8, 2 <-> 7, ...).
    /// - Inverts vertical directions: 'n' <-> 's'. Horizontal 'e'/'w' stay the same.
    /// - Tokens are expected to be space-separated and shaped like:
    ///   "Pfr" (setup/placement) or "Pfrd" (single step), where
    ///   P = piece letter, f = file a-h, r = rank 1-8, d = direction n/s/e/w.
    ///   Any token not matching this pattern is returned unchanged.
    /// This supports pushes and pulls naturally as they are just sequences of such tokens.
    /// </summary>
    /// <param name="goldMove">Gold-looking move string, e.g., "Ea2n Ea3n Ea4n Ea5n".</param>
    /// <returns>Silver-formatted move string, e.g., "ea7s ea6s ea5s ea4s".</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="goldMove"/> is null or empty.</exception>
    public static string ConvertGoldMoveToSilver(string goldMove)
    {
        if (string.IsNullOrWhiteSpace(goldMove))
            throw new ArgumentException("Move string is null or empty.", nameof(goldMove));

        // Regex for tokens like Pfr or Pfrd
        // Group1 = piece [A-Za-z], Group2 = file [a-hA-H], Group3 = rank [1-8], Group4 = optional dir [nsewNSEW]
        var tokenRegex = new Regex("^([A-Za-z])([a-hA-H])([1-8])([nsewNSEW])?$", RegexOptions.Compiled);

        string TransformToken(string tok)
        {
            if (string.IsNullOrWhiteSpace(tok)) return tok;

            var m = tokenRegex.Match(tok);
            if (!m.Success)
                return tok; // leave unknown tokens as-is

            char piece = m.Groups[1].Value[0];
            char file = char.ToLowerInvariant(m.Groups[2].Value[0]); // normalize file letter lower
            char rankCh = m.Groups[3].Value[0];
            char? dir = m.Groups[4].Success ? m.Groups[4].Value[0] : (char?)null;

            // Swap case of the piece letter
            piece = char.IsUpper(piece) ? char.ToLowerInvariant(piece) : char.ToUpperInvariant(piece);

            // Mirror rank: r' = 9 - r
            int rank = rankCh - '0';
            int mirrored = 9 - rank; // 1<->8, 2<->7, ...
            char mirroredRank = (char)('0' + mirrored);

            // Transform direction if present
            char outDir = '\0';
            if (dir.HasValue)
            {
                switch (char.ToLowerInvariant(dir.Value))
                {
                    case 'n': outDir = 's'; break;
                    case 's': outDir = 'n'; break;
                    case 'e': outDir = 'e'; break;
                    case 'w': outDir = 'w'; break;
                    default: outDir = char.ToLowerInvariant(dir.Value); break;
                }
            }

            return dir.HasValue
                ? new string(new[] { piece, file, mirroredRank, outDir })
                : new string(new[] { piece, file, mirroredRank });
        }

        var parts = goldMove.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < parts.Length; i++)
        {
            parts[i] = TransformToken(parts[i]);
        }
        return string.Join(" ", parts);
    }

    private static string FlipVertical(string flat)
    {
        // flat is 8 rows of 8 characters, top (north) to bottom (south)
        Span<char> buffer = stackalloc char[64];
        for (int r = 0; r < 8; r++)
        {
            int srcStart = r * 8;
            int dstStart = (7 - r) * 8;
            for (int c = 0; c < 8; c++)
            {
                buffer[dstStart + c] = flat[srcStart + c];
            }
        }
        return new string(buffer);
    }
}
