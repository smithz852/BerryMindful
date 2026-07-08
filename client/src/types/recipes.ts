export type RecipeRanking = 1 | 2; // 1 = maximize used ingredients, 2 = minimize missing

export interface RecipeFilters {
  ingredients: string[];
  ranking: RecipeRanking;
  ignorePantry: boolean;
}

export interface RecipeSuggestion {
  id: number;
  title: string;
  imageUrl: string | null;
  sourceUrl: string;
  usedIngredientCount: number;
  missedIngredientCount: number;
  usedIngredients: string[];
  missedIngredients: string[];
  likes: number;
}
