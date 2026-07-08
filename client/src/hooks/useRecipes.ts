import { useQuery } from "@tanstack/react-query";
import { api } from "../api/client";
import type { RecipeFilters, RecipeSuggestion } from "../types/recipes";

// Only fires once the user applies filters (filters stays null until then), and
// results stay fresh client-side to mirror the server's 24h response cache —
// browsing back and forth never re-spends Spoonacular quota.
export function useRecipes(filters: RecipeFilters | null) {
  return useQuery({
    queryKey: ["recipes", filters],
    enabled: filters !== null && filters.ingredients.length > 0,
    staleTime: 24 * 60 * 60 * 1000,
    queryFn: () => {
      const params = new URLSearchParams({
        ingredients: filters!.ingredients.join(","),
        ranking: String(filters!.ranking),
        ignorePantry: String(filters!.ignorePantry),
        number: "12",
      });
      return api<RecipeSuggestion[]>(`/recipes/by-ingredients?${params}`);
    },
  });
}
