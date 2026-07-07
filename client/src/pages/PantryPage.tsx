import { Link, useNavigate } from "react-router-dom";
import { useAuth } from "../hooks/useAuth";
import { usePantry, useUpdateItemStatus, useDeleteItem } from "../hooks/usePantry";
import type { PantryItem } from "../types/pantry";

function urgencyClass(expiresAt: string): string {
  const daysLeft = (new Date(expiresAt).getTime() - Date.now()) / 86_400_000;
  if (daysLeft < 2) return "urgency-red";
  if (daysLeft <= 5) return "urgency-yellow";
  return "urgency-green";
}

function expiryLabel(expiresAt: string): string {
  const daysLeft = Math.ceil(
    (new Date(expiresAt).getTime() - Date.now()) / 86_400_000,
  );
  if (daysLeft < 0) return "expired";
  if (daysLeft === 0) return "expires today";
  if (daysLeft === 1) return "1 day left";
  return `${daysLeft} days left`;
}

function ItemCard({ item }: { item: PantryItem }) {
  const updateStatus = useUpdateItemStatus();
  const deleteItem = useDeleteItem();

  return (
    <li className={`item-card ${urgencyClass(item.expiresAt)}`}>
      <div className="item-info">
        <strong>{item.name}</strong>
        <span className="item-meta">
          {item.category} · {expiryLabel(item.expiresAt)}
        </span>
      </div>
      <div className="item-actions">
        <button
          onClick={() => updateStatus.mutate({ id: item.id, status: "Used" })}
          disabled={updateStatus.isPending}
          title="Mark as used"
        >
          ✓ Used
        </button>
        <button
          onClick={() => updateStatus.mutate({ id: item.id, status: "Tossed" })}
          disabled={updateStatus.isPending}
          title="Mark as tossed"
        >
          🗑 Tossed
        </button>
        <button
          className="danger"
          onClick={() => deleteItem.mutate(item.id)}
          disabled={deleteItem.isPending}
          title="Delete entry"
        >
          ✕
        </button>
      </div>
    </li>
  );
}

export function PantryPage() {
  const { user, logout } = useAuth();
  const navigate = useNavigate();
  const { data: items, isLoading, error } = usePantry();

  async function handleLogout() {
    await logout();
    navigate("/login");
  }

  return (
    <main className="pantry">
      <header className="pantry-header">
        <h1>🫐 My Pantry</h1>
        <div>
          <span>{user?.email}</span>
          <button onClick={handleLogout}>Log out</button>
        </div>
      </header>

      <nav className="pantry-nav">
        <Link to="/scan" className="button-link">
          📷 Scan receipt
        </Link>
        <Link to="/pantry/add" className="button-link">
          + Add item
        </Link>
        <Link to="/analytics" className="button-link">
          📊 Analytics
        </Link>
      </nav>

      {isLoading && <p className="loading">Loading pantry…</p>}
      {error && <p className="error">Couldn't load pantry: {error.message}</p>}
      {items && items.length === 0 && (
        <p className="empty">
          Nothing here yet — scan a receipt or add an item to get started.
        </p>
      )}
      {items && items.length > 0 && (
        <ul className="item-list">
          {items.map((item) => (
            <ItemCard key={item.id} item={item} />
          ))}
        </ul>
      )}
    </main>
  );
}
