import { useEffect, useMemo, useState } from "react";
import { Link } from "react-router-dom";
import { usePantry } from "../hooks/usePantry";
import { useRecipes } from "../hooks/useRecipes";
import type { PantryItem } from "../types/pantry";
import type {
  RecipeFilters,
  RecipeRanking,
  RecipeSuggestion,
} from "../types/recipes";

const PREFS_KEY = "recipeFilterPrefs";

interface FilterPrefs {
  ranking: RecipeRanking;
  ignorePantry: boolean;
}

function loadPrefs(): FilterPrefs {
  try {
    const raw = localStorage.getItem(PREFS_KEY);
    if (raw) {
      const parsed = JSON.parse(raw);
      if (
        (parsed.ranking === 1 || parsed.ranking === 2) &&
        typeof parsed.ignorePantry === "boolean"
      ) {
        return parsed;
      }
    }
  } catch {
    // corrupted prefs — fall through to defaults
  }
  return { ranking: 1, ignorePantry: true };
}

interface Ingredient {
  name: string;
  expiringSoon: boolean;
}

// Distinct active item names, soonest expiry first (nudges users toward
// at-risk items); duplicates keep their soonest-expiring entry.
function toIngredients(items: PantryItem[]): Ingredient[] {
  const seen = new Map<string, Ingredient>();
  const sorted = [...items].sort(
    (a, b) => new Date(a.expiresAt).getTime() - new Date(b.expiresAt).getTime(),
  );
  for (const item of sorted) {
    const key = item.name.toLowerCase();
    if (!seen.has(key)) {
      const daysLeft =
        (new Date(item.expiresAt).getTime() - Date.now()) / 86_400_000;
      seen.set(key, { name: item.name, expiringSoon: daysLeft <= 3 });
    }
  }
  return [...seen.values()];
}

function FilterModal({
  ingredients,
  initialSelected,
  initialPrefs,
  onApply,
  onClose,
}: {
  ingredients: Ingredient[];
  initialSelected: string[] | null; // null = all checked
  initialPrefs: FilterPrefs;
  onApply: (selected: string[], prefs: FilterPrefs) => void;
  onClose: () => void;
}) {
  const [checked, setChecked] = useState<Set<string>>(
    () => new Set(initialSelected ?? ingredients.map((i) => i.name)),
  );
  const [ranking, setRanking] = useState<RecipeRanking>(initialPrefs.ranking);
  const [ignorePantry, setIgnorePantry] = useState(initialPrefs.ignorePantry);
  const allChecked = checked.size === ingredients.length;

  useEffect(() => {
    function onKeyDown(event: KeyboardEvent) {
      if (event.key === "Escape") onClose();
    }
    window.addEventListener("keydown", onKeyDown);
    return () => window.removeEventListener("keydown", onKeyDown);
  }, [onClose]);

  function toggle(name: string) {
    setChecked((prev) => {
      const next = new Set(prev);
      if (next.has(name)) {
        next.delete(name);
      } else {
        next.add(name);
      }
      return next;
    });
  }

  return (
    <div className="modal-overlay" onClick={onClose}>
      <div
        className="modal"
        role="dialog"
        aria-modal="true"
        aria-label="Recipe filters"
        onClick={(event) => event.stopPropagation()}
      >
        <div className="modal-header">
          <h2>Recipe filters</h2>
          <button onClick={onClose} aria-label="Close">
            ✕
          </button>
        </div>

        <section>
          <div className="modal-section-header">
            <h3>Ingredients</h3>
            <button
              onClick={() =>
                setChecked(
                  allChecked
                    ? new Set()
                    : new Set(ingredients.map((i) => i.name)),
                )
              }
            >
              {allChecked ? "Clear all" : "Select all"}
            </button>
          </div>
          <ul className="ingredient-list">
            {ingredients.map((ingredient) => (
              <li key={ingredient.name}>
                <label>
                  <input
                    type="checkbox"
                    checked={checked.has(ingredient.name)}
                    onChange={() => toggle(ingredient.name)}
                  />
                  {ingredient.name}
                  {ingredient.expiringSoon && (
                    <span className="badge badge-expiring">expiring soon</span>
                  )}
                </label>
              </li>
            ))}
          </ul>
        </section>

        <section>
          <h3>Prioritize</h3>
          <label className="modal-choice">
            <input
              type="radio"
              name="ranking"
              checked={ranking === 1}
              onChange={() => setRanking(1)}
            />
            Use as many of my ingredients as possible
          </label>
          <label className="modal-choice">
            <input
              type="radio"
              name="ranking"
              checked={ranking === 2}
              onChange={() => setRanking(2)}
            />
            Need as few extra ingredients as possible
          </label>
        </section>

        <label className="modal-choice">
          <input
            type="checkbox"
            checked={ignorePantry}
            onChange={(event) => setIgnorePantry(event.target.checked)}
          />
          Ignore common pantry staples (water, salt, flour…)
        </label>

        <div className="modal-actions">
          <button
            className="primary"
            disabled={checked.size === 0}
            onClick={() =>
              onApply(
                ingredients
                  .filter((i) => checked.has(i.name))
                  .map((i) => i.name),
                { ranking, ignorePantry },
              )
            }
          >
            🔍 Find recipes
          </button>
        </div>
      </div>
    </div>
  );
}

function RecipeCard({ recipe }: { recipe: RecipeSuggestion }) {
  // Some Spoonacular image URLs 404 — fall back to the placeholder.
  const [imgFailed, setImgFailed] = useState(false);
  return (
    <li className="recipe-card">
      {recipe.imageUrl && !imgFailed ? (
        <img
          src={recipe.imageUrl}
          alt=""
          loading="lazy"
          onError={() => setImgFailed(true)}
        />
      ) : (
        <div className="recipe-image-placeholder">🍽️</div>
      )}
      <div className="recipe-body">
        <h3>{recipe.title}</h3>
        <div className="recipe-badges">
          <span className="badge badge-used">
            ✓ uses {recipe.usedIngredientCount}
          </span>
          {recipe.missedIngredientCount > 0 && (
            <span className="badge badge-missed">
              needs {recipe.missedIngredientCount} more
            </span>
          )}
        </div>
        {recipe.missedIngredients.length > 0 && (
          <p className="recipe-missing">
            Missing: {recipe.missedIngredients.join(", ")}
          </p>
        )}
        <div className="recipe-footer">
          <span>👍 {recipe.likes}</span>
          <a href={recipe.sourceUrl} target="_blank" rel="noreferrer">
            View recipe →
          </a>
        </div>
      </div>
    </li>
  );
}

export function RecipesPage() {
  const { data: items, isLoading: pantryLoading } = usePantry();
  const [modalOpen, setModalOpen] = useState(false);
  const [prefs, setPrefs] = useState<FilterPrefs>(loadPrefs);
  const [selected, setSelected] = useState<string[] | null>(null);
  const [applied, setApplied] = useState<RecipeFilters | null>(null);
  const { data: recipes, isLoading, error, refetch } = useRecipes(applied);

  const ingredients = useMemo(() => toIngredients(items ?? []), [items]);

  function applyFilters(names: string[], nextPrefs: FilterPrefs) {
    setPrefs(nextPrefs);
    localStorage.setItem(PREFS_KEY, JSON.stringify(nextPrefs));
    setSelected(names);
    setApplied({ ingredients: names, ...nextPrefs });
    setModalOpen(false);
  }

  return (
    <main className="pantry recipes">
      <header className="pantry-header">
        <h1>🍳 Recipes</h1>
      </header>

      <nav className="pantry-nav">
        <Link to="/pantry" className="button-link">
          ← Pantry
        </Link>
      </nav>

      {pantryLoading && <p className="loading">Loading pantry…</p>}

      {items && ingredients.length === 0 && (
        <p className="empty">
          Your pantry is empty — scan a receipt or add items first, then come
          back to find recipes that use them up.
        </p>
      )}

      {ingredients.length > 0 && (
        <div className="recipes-toolbar">
          <button onClick={() => setModalOpen(true)}>
            ⚙ Filters
            {applied &&
              ` (${applied.ingredients.length} of ${ingredients.length} ingredients)`}
          </button>
          {applied && (
            <span className="recipes-summary">
              {applied.ranking === 1
                ? "maximizing used ingredients"
                : "minimizing missing ingredients"}
            </span>
          )}
        </div>
      )}

      {ingredients.length > 0 && !applied && (
        <p className="empty">
          Pick which pantry ingredients to cook with and we'll find recipes
          that use them before they expire.
        </p>
      )}

      {isLoading && <p className="loading">Finding recipes…</p>}
      {error && (
        <p className="error">
          Couldn't load recipes: {error.message}{" "}
          <button onClick={() => refetch()}>Retry</button>
        </p>
      )}
      {recipes && recipes.length === 0 && (
        <p className="empty">
          No recipes matched those ingredients — try selecting more, or switch
          to "need as few extra ingredients as possible".
        </p>
      )}
      {recipes && recipes.length > 0 && (
        <ul className="recipe-grid">
          {recipes.map((recipe) => (
            <RecipeCard key={recipe.id} recipe={recipe} />
          ))}
        </ul>
      )}

      {modalOpen && (
        <FilterModal
          ingredients={ingredients}
          initialSelected={selected}
          initialPrefs={prefs}
          onApply={applyFilters}
          onClose={() => setModalOpen(false)}
        />
      )}
    </main>
  );
}
