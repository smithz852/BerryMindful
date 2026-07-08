namespace BerryMindful.Services.RecipeServices;

// Thrown when the recipe provider fails for API-level reasons (timeout, quota
// exhausted, unexpected response) — the controller maps this to a 502 so the
// client can show a retry-later message.
public class RecipeSearchException(string message, Exception? innerException = null)
    : Exception(message, innerException);
