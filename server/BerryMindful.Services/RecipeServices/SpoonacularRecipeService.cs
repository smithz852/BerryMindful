using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using BerryMindful.Services.DTOs;

namespace BerryMindful.Services.RecipeServices;

// Spoonacular findByIngredients search. All calls go through the server so the
// API key never reaches the browser; RecipesController adds caching + rate
// limiting on top since the free tier is ~150 points/day.
public class SpoonacularRecipeService(HttpClient http, string apiKey) : IRecipeProvider
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public async Task<IReadOnlyList<RecipeSuggestionDto>> FindByIngredientsAsync(
        IReadOnlyList<string> ingredients,
        int ranking,
        bool ignorePantry,
        int number,
        CancellationToken cancellationToken = default)
    {
        var query = "recipes/findByIngredients"
            + $"?ingredients={Uri.EscapeDataString(string.Join(",", ingredients))}"
            + $"&number={number}"
            + $"&ranking={ranking}"
            + $"&ignorePantry={(ignorePantry ? "true" : "false")}"
            + $"&apiKey={Uri.EscapeDataString(apiKey)}";

        HttpResponseMessage response;
        try
        {
            response = await http.GetAsync(query, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            throw new RecipeSearchException("Could not reach the recipe service.", ex);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new RecipeSearchException("The recipe service timed out.", ex);
        }

        using (response)
        {
            // Spoonacular signals an exhausted daily quota with 402.
            if (response.StatusCode == HttpStatusCode.PaymentRequired)
            {
                throw new RecipeSearchException("Daily recipe search quota is exhausted.");
            }
            if (!response.IsSuccessStatusCode)
            {
                throw new RecipeSearchException($"Recipe service returned {(int)response.StatusCode}.");
            }

            List<SpoonacularRecipe>? recipes;
            try
            {
                recipes = await response.Content.ReadFromJsonAsync<List<SpoonacularRecipe>>(JsonOptions, cancellationToken);
            }
            catch (JsonException ex)
            {
                throw new RecipeSearchException("Recipe service returned an unexpected response.", ex);
            }

            return (recipes ?? []).Select(recipe => new RecipeSuggestionDto(
                recipe.Id,
                recipe.Title,
                recipe.Image,
                SourceUrl(recipe.Title, recipe.Id),
                recipe.UsedIngredientCount,
                recipe.MissedIngredientCount,
                Names(recipe.UsedIngredients),
                Names(recipe.MissedIngredients),
                recipe.Likes)).ToList();
        }
    }

    // findByIngredients returns no instructions or source link; Spoonacular's own
    // recipe pages follow a stable {title-slug}-{id} URL scheme. A proxied
    // /recipes/{id}/information call is the later upgrade for real instructions.
    private static string SourceUrl(string title, int id)
    {
        var slug = new StringBuilder(title.Length);
        foreach (var ch in title.ToLowerInvariant())
        {
            if (char.IsAsciiLetterOrDigit(ch))
            {
                slug.Append(ch);
            }
            else if (slug.Length > 0 && slug[^1] != '-')
            {
                slug.Append('-');
            }
        }
        return $"https://spoonacular.com/recipes/{slug.ToString().TrimEnd('-')}-{id}";
    }

    private static IReadOnlyList<string> Names(List<SpoonacularIngredient>? list) =>
        list?.Where(i => !string.IsNullOrWhiteSpace(i.Name)).Select(i => i.Name!).ToList() ?? [];

    private sealed record SpoonacularRecipe(
        int Id,
        string Title,
        string? Image,
        int UsedIngredientCount,
        int MissedIngredientCount,
        List<SpoonacularIngredient>? UsedIngredients,
        List<SpoonacularIngredient>? MissedIngredients,
        int Likes);

    private sealed record SpoonacularIngredient(string? Name);
}
