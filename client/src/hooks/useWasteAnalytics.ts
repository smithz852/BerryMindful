import { useQuery } from "@tanstack/react-query";
import { api } from "../api/client";
import type { WasteAnalytics } from "../types/analytics";

/** @param days look-back window in days; 0 means all time */
export function useWasteAnalytics(days: number) {
  return useQuery({
    queryKey: ["analytics", "waste", days],
    queryFn: () => api<WasteAnalytics>(`/analytics/waste?days=${days}`),
  });
}
