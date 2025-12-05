public static class PieceHierarchy
{
    // Direct char-to-hierarchy lookup: O(1), no conversions needed
    private static readonly int[] CharToHierarchy = new int[256];

    static PieceHierarchy()
    {
        // Initialize lookup table for all 256 ASCII values
        Array.Fill(CharToHierarchy, -1); // -1 = invalid
        
        // Both uppercase (Gold) and lowercase (Silver) map to same hierarchy
        CharToHierarchy['R'] = CharToHierarchy['r'] = 1; // Rabbit
        CharToHierarchy['C'] = CharToHierarchy['c'] = 2; // Cat
        CharToHierarchy['D'] = CharToHierarchy['d'] = 3; // Dog
        CharToHierarchy['H'] = CharToHierarchy['h'] = 4; // Horse
        CharToHierarchy['M'] = CharToHierarchy['m'] = 5; // Camel
        CharToHierarchy['E'] = CharToHierarchy['e'] = 6; // Elephant
    }

    /// <summary>
    /// Get hierarchy value directly from character (1=Rabbit, 6=Elephant).
    /// Returns -1 for invalid pieces.
    /// </summary>
    public static int GetHierarchy(char ch) => CharToHierarchy[ch];

    /// <summary>
    /// Check if attacker can capture defender based on character hierarchy.
    /// </summary>
    public static bool CanPushOrPull(char attacker, char defender)
    {
        var attackerHierarchy = GetHierarchy(attacker);
        var defenderHierarchy = GetHierarchy(defender);
        return attackerHierarchy > 0 && defenderHierarchy > 0 && attackerHierarchy > defenderHierarchy;
    }
}