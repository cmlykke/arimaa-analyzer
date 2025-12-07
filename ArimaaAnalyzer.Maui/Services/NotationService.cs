using System.Text;
using ArimaaAnalyzer.Maui.Services.Arimaa;

namespace ArimaaAnalyzer.Maui.Services;

public static class NotationService
{
    public static string BoardToAei(string[] b, string side)
    {
        var flat = string.Join(string.Empty, System.Array.ConvertAll(b, r => r.Replace('.', ' ')));
        return $"setposition {side} \"{flat}\"";
    }
    
    public static string[] AeiToBoard(string internalArimaaString)
    {
        // The format is: setposition {side} "{flat}"
        // We need the content inside the quotes
        int quoteStart = internalArimaaString.IndexOf('"');
        int quoteEnd   = internalArimaaString.LastIndexOf('"');

        if (quoteStart == -1 || quoteEnd == -1 || quoteEnd <= quoteStart)
            throw new ArgumentException("Invalid InternalArimaaString format: missing quotes.");

        string flatWithSpaces = internalArimaaString.Substring(quoteStart + 1, quoteEnd - quoteStart - 1);

        // Replace spaces with '.' to restore empty squares
        string flat = flatWithSpaces.Replace(' ', '.');

        if (flat.Length != 64)
            throw new ArgumentException($"Board string must contain exactly 64 characters, but has {flat.Length}.");

        // Split into 8 rows of 8 characters
        string[] board = new string[8];
        for (int i = 0; i < 8; i++)
        {
            board[i] = flat.Substring(i * 8, 8);
        }

        return board;
    }
}