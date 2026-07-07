import { useState, type FormEvent } from "react";
import { Link, useNavigate } from "react-router-dom";
import { useAddItem } from "../hooks/usePantry";
import { CATEGORIES, type ItemCategory } from "../types/pantry";

export function AddItemPage() {
  const navigate = useNavigate();
  const addItem = useAddItem();
  const [name, setName] = useState("");
  const [category, setCategory] = useState<ItemCategory>("Other");
  const [days, setDays] = useState(7);
  const [error, setError] = useState<string | null>(null);

  async function handleSubmit(e: FormEvent) {
    e.preventDefault();
    setError(null);
    try {
      await addItem.mutateAsync({
        name,
        category,
        estimatedExpiryDays: days,
      });
      navigate("/pantry");
    } catch (err) {
      setError(err instanceof Error ? err.message : "Couldn't add item.");
    }
  }

  return (
    <main className="add-item-page">
      <h1>Add an item</h1>
      <form onSubmit={handleSubmit}>
        <label>
          Name
          <input
            value={name}
            onChange={(e) => setName(e.target.value)}
            required
            maxLength={256}
            placeholder="e.g. Cheddar cheese"
          />
        </label>
        <label>
          Category
          <select
            value={category}
            onChange={(e) => setCategory(e.target.value as ItemCategory)}
          >
            {CATEGORIES.map((c) => (
              <option key={c} value={c}>
                {c}
              </option>
            ))}
          </select>
        </label>
        <label>
          Days until expiry
          <input
            type="number"
            min={0}
            max={3650}
            value={days}
            onChange={(e) => setDays(Number(e.target.value))}
            required
          />
        </label>
        {error && <p className="error">{error}</p>}
        <button type="submit" className="primary" disabled={addItem.isPending}>
          {addItem.isPending ? "Adding…" : "Add to pantry"}
        </button>
      </form>
      <p>
        <Link to="/pantry">← Back to pantry</Link>
      </p>
    </main>
  );
}
