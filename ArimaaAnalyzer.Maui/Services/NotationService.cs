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
}