import { useState } from "react";
import { Link, Navigate, useLocation, useNavigate } from "react-router-dom";
import { useConfirmReceipt } from "../hooks/useReceipts";
import { CATEGORIES, type ItemCategory, type PantryItemDraft, type ReceiptScanResult } from "../types/pantry";

export function ConfirmPage() {
  const location = useLocation();
  const navigate = useNavigate();
  const confirm = useConfirmReceipt();
  const scan = location.state as ReceiptScanResult | null;

  const [items, setItems] = useState<PantryItemDraft[]>(scan?.items ?? []);
  const [error, setError] = useState<string | null>(null);

  if (!scan) {
    return <Navigate to="/scan" replace />;
  }

  function updateItem(index: number, patch: Partial<PantryItemDraft>) {
    setItems((prev) => prev.map((it, i) => (i === index ? { ...it, ...patch } : it)));
  }

  function removeItem(index: number) {
    setItems((prev) => prev.filter((_, i) => i !== index));
  }

  async function handleConfirm() {
    setError(null);
    try {
      await confirm.mutateAsync({
        storeNameRaw: scan!.storeNameRaw,
        purchasedAt: scan!.purchasedAt,
        imageUrl: scan!.imageUrl,
        rawOcrText: scan!.rawOcrText,
        items,
      });
      navigate("/pantry");
    } catch (err) {
      setError(err instanceof Error ? err.message : "Couldn't save items.");
    }
  }

  return (
    <main className="confirm-page">
      <h1>Confirm items</h1>
      <p>
        {scan.storeNameRaw ?? "Unknown store"} ·{" "}
        {new Date(scan.purchasedAt).toLocaleDateString()}
      </p>

      {items.length === 0 && <p className="empty">No items — nothing to save.</p>}

      <ul className="confirm-list">
        {items.map((item, i) => (
          <li key={i} className="confirm-row">
            <input
              value={item.name}
              onChange={(e) => updateItem(i, { name: e.target.value })}
              aria-label="Item name"
            />
            <select
              value={item.category}
              onChange={(e) => updateItem(i, { category: e.target.value as ItemCategory })}
              aria-label="Category"
            >
              {CATEGORIES.map((c) => (
                <option key={c} value={c}>
                  {c}
                </option>
              ))}
            </select>
            <label className="days-input">
              <input
                type="number"
                min={0}
                max={3650}
                value={item.estimatedExpiryDays}
                onChange={(e) =>
                  updateItem(i, { estimatedExpiryDays: Number(e.target.value) })
                }
                aria-label="Days until expiry"
              />
              days
            </label>
            <button className="danger" onClick={() => removeItem(i)} title="Remove">
              ✕
            </button>
          </li>
        ))}
      </ul>

      {error && <p className="error">{error}</p>}

      <div className="confirm-actions">
        <button
          className="primary"
          onClick={handleConfirm}
          disabled={confirm.isPending || items.length === 0}
        >
          {confirm.isPending ? "Saving…" : `Save ${items.length} items to pantry`}
        </button>
        <Link to="/scan">Rescan</Link>
      </div>
    </main>
  );
}
