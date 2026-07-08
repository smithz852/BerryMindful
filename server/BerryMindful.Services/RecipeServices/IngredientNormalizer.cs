using System.Text.RegularExpressions;

namespace BerryMindful.Services.RecipeServices;

// Pantry item names come from receipts ("Ground Beef 93/7", "Bananas 3PK"), so
// strip size/count noise before sending them to a recipe API that expects plain
// ingredient names. Heuristic-only for MVP; a Claude-normalized name stored at
// scan time is a later upgrade.
public static partial class IngredientNormalizer
{
    // Pure numbers ("3"), ratios/percentages ("93/7", "2%"), and count/size
    // tokens ("3pk", "12ct", "16oz", "1 lb", "gal").
    [GeneratedRegex(@"^(\d+([./]\d+)?%?|\d*\s?(pk|pack|ct|oz|lb|lbs|gal|pt|qt|ml|l|g|kg))$")]
    private static partial Regex NoiseToken();

    public static IReadOnlyList<string> Normalize(IEnumerable<string> raw)
    {
        var seen = new HashSet<string>();
        var result = new List<string>();
        foreach (var entry in raw)
        {
            var words = entry
                .ToLowerInvariant()
                .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(word => !NoiseToken().IsMatch(word));
            var name = string.Join(' ', words);
            if (name.Length > 0 && seen.Add(name))
            {
                result.Add(name);
            }
        }
        return result;
    }
}
