using BerryMindful.Services.DTOs;

namespace BerryMindful.Services.RecipeServices;

// No-key fallback: matches canned recipes against the requested ingredients so
// the whole filter UX (ranking, ignorePantry, used/missed badges) is exercisable
// in dev. Ingredient overlap deliberately mirrors StubReceiptScanner's items.
// SourceUrl links are fake slugs and will 404 — dev-only.
public class StubRecipeService : IRecipeProvider
{
    private static readonly HashSet<string> PantryStaples =
        ["water", "salt", "pepper", "flour", "sugar", "butter", "olive oil"];

    private static readonly (int Id, string Title, string[] Ingredients, int Likes)[] Recipes =
    [
        (9001, "Banana Bread", ["bananas", "flour", "sugar", "butter", "eggs"], 412),
        (9002, "Strawberry Banana Smoothie", ["strawberries", "bananas", "milk", "honey"], 287),
        (9003, "Beef Tacos", ["ground beef", "tortillas", "cheddar cheese", "lettuce", "salsa"], 356),
        (9004, "Strawberries and Cream Oatmeal", ["strawberries", "milk", "oats", "sugar"], 149),
        (9005, "Spaghetti Bolognese", ["ground beef", "spaghetti", "tomato sauce", "onion", "garlic"], 501),
        (9006, "Classic Pancakes", ["flour", "milk", "eggs", "butter", "sugar"], 233),
    ];

    public Task<IReadOnlyList<RecipeSuggestionDto>> FindByIngredientsAsync(
        IReadOnlyList<string> ingredients,
        int ranking,
        bool ignorePantry,
        int number,
        CancellationToken cancellationToken = default)
    {
        var results = Recipes
            .Select(recipe =>
            {
                var effective = recipe.Ingredients
                    .Where(ing => !(ignorePantry && PantryStaples.Contains(ing)))
                    .ToList();
                var used = effective
                    .Where(ing => ingredients.Any(req => req.Contains(ing) || ing.Contains(req)))
                    .ToList();
                var missed = effective.Except(used).ToList();
                return new RecipeSuggestionDto(
                    recipe.Id,
                    recipe.Title,
                    ImageUrl: null,
                    SourceUrl: $"https://spoonacular.com/recipes/{recipe.Title.ToLowerInvariant().Replace(' ', '-')}-{recipe.Id}",
                    UsedIngredientCount: used.Count,
                    MissedIngredientCount: missed.Count,
                    UsedIngredients: used,
                    MissedIngredients: missed,
                    Likes: recipe.Likes);
            })
            .Where(recipe => recipe.UsedIngredientCount > 0)
            .OrderByDescending(recipe => ranking == 1 ? recipe.UsedIngredientCount : -recipe.MissedIngredientCount)
            .ThenByDescending(recipe => recipe.Likes)
            .Take(number)
            .ToList();

        return Task.FromResult<IReadOnlyList<RecipeSuggestionDto>>(results);
    }
}
