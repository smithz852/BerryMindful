using BerryMindful.Services.DTOs;
using BerryMindful.Services.RecipeServices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Caching.Memory;

namespace BerryMindful.Api.Controllers;

[ApiController]
[Route("recipes")]
[Authorize]
public class RecipesController(
    IRecipeProvider recipeProvider,
    IMemoryCache cache,
    ILogger<RecipesController> logger) : ControllerBase
{
    // Spoonacular allows up to 100, but each result costs quota (0.01 pt) and the
    // grid doesn't need more than a couple dozen.
    private const int MaxResults = 24;
    private static readonly TimeSpan CacheTtl = TimeSpan.FromHours(24);

    [HttpGet("by-ingredients")]
    [EnableRateLimiting("recipes")]
    public async Task<ActionResult<IReadOnlyList<RecipeSuggestionDto>>> ByIngredients(
        [FromQuery] string? ingredients,
        [FromQuery] int ranking = 1,
        [FromQuery] bool ignorePantry = true,
        [FromQuery] int number = 12,
        CancellationToken cancellationToken = default)
    {
        if (ranking is not (1 or 2))
        {
            return BadRequest(new { error = "ranking must be 1 (maximize used) or 2 (minimize missing)." });
        }

        var normalized = IngredientNormalizer.Normalize((ingredients ?? string.Empty).Split(','));
        if (normalized.Count == 0)
        {
            return BadRequest(new { error = "At least one ingredient is required." });
        }
        number = Math.Clamp(number, 1, MaxResults);

        // Shared across users on purpose: the same search costs quota once per day.
        var cacheKey = $"recipes:{ranking}:{ignorePantry}:{number}:"
            + string.Join(",", normalized.OrderBy(n => n, StringComparer.Ordinal));
        if (cache.TryGetValue(cacheKey, out IReadOnlyList<RecipeSuggestionDto>? cached) && cached is not null)
        {
            logger.LogInformation("Recipe search served from cache ({Key})", cacheKey);
            return Ok(cached);
        }

        try
        {
            var results = await recipeProvider.FindByIngredientsAsync(
                normalized, ranking, ignorePantry, number, cancellationToken);
            cache.Set(cacheKey, results, CacheTtl);
            return Ok(results);
        }
        catch (RecipeSearchException ex)
        {
            logger.LogError(ex, "Recipe search failed");
            return StatusCode(StatusCodes.Status502BadGateway, new
            {
                error = "Couldn't fetch recipes right now — try again in a bit.",
            });
        }
    }
}
