using BerryMindful.Services.DTOs;

namespace BerryMindful.Services.RecipeServices;

public interface IRecipeProvider
{
    /// <param name="ingredients">Normalized ingredient names (lowercased, noise tokens stripped).</param>
    /// <param name="ranking">1 = maximize used ingredients, 2 = minimize missing ingredients.</param>
    /// <param name="ignorePantry">Skip common pantry staples (water, salt, flour…).</param>
    Task<IReadOnlyList<RecipeSuggestionDto>> FindByIngredientsAsync(
        IReadOnlyList<string> ingredients,
        int ranking,
        bool ignorePantry,
        int number,
        CancellationToken cancellationToken = default);
}
