export const CATEGORIES = [
  "Produce",
  "Dairy",
  "Meat",
  "Seafood",
  "Bakery",
  "Frozen",
  "Pantry",
  "Beverage",
  "Other",
] as const;

export type ItemCategory = (typeof CATEGORIES)[number];

export type PantryItemStatus = "Active" | "Used" | "Tossed";

export interface PantryItemDraft {
  name: string;
  category: ItemCategory;
  estimatedExpiryDays: number;
}

export interface ReceiptScanResult {
  storeNameRaw: string | null;
  purchasedAt: string;
  imageUrl: string | null;
  rawOcrText: string | null;
  items: PantryItemDraft[];
}

export interface PantryItem {
  id: string;
  name: string;
  category: ItemCategory;
  purchasedAt: string;
  estimatedExpiryDays: number;
  expiresAt: string;
  status: PantryItemStatus;
  receiptId: string | null;
}

export interface ReceiptSummary {
  id: string;
  storeNameRaw: string | null;
  purchasedAt: string;
  createdAt: string;
  itemCount: number;
}
