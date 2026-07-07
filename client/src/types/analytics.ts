import type { ItemCategory } from "./pantry";

export interface WasteTotals {
  used: number;
  tossed: number;
  wasteRate: number;
  tossedAfterExpiry: number;
}

export interface WeeklyWaste {
  weekStart: string; // Monday, ISO date
  used: number;
  tossed: number;
}

export interface CategoryWaste {
  category: ItemCategory;
  tossed: number;
  used: number;
}

export interface TossedItem {
  name: string;
  count: number;
}

export interface WasteAnalytics {
  totals: WasteTotals;
  weekly: WeeklyWaste[];
  byCategory: CategoryWaste[];
  mostTossed: TossedItem[];
}
