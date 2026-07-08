namespace BerryMindful.Services.DTOs;

public record RecipeSuggestionDto(
    int Id,
    string Title,
    string? ImageUrl,
    string SourceUrl,
    int UsedIngredientCount,
    int MissedIngredientCount,
    IReadOnlyList<string> UsedIngredients,
    IReadOnlyList<string> MissedIngredients,
    int Likes);
