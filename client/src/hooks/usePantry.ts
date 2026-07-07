import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { api } from "../api/client";
import type { PantryItem, PantryItemStatus, PantryItemDraft } from "../types/pantry";

export function usePantry() {
  return useQuery({
    queryKey: ["pantry"],
    queryFn: () => api<PantryItem[]>("/pantry"),
  });
}

export function useUpdateItemStatus() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id, status }: { id: string; status: PantryItemStatus }) =>
      api<PantryItem>(`/pantry/${id}/status`, {
        method: "PATCH",
        body: JSON.stringify({ status }),
      }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["pantry"] });
      queryClient.invalidateQueries({ queryKey: ["analytics"] });
    },
  });
}

export function useDeleteItem() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => api<void>(`/pantry/${id}`, { method: "DELETE" }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["pantry"] });
      queryClient.invalidateQueries({ queryKey: ["analytics"] });
    },
  });
}

export function useAddItem() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (draft: PantryItemDraft) =>
      api<PantryItem>("/pantry/items", {
        method: "POST",
        body: JSON.stringify(draft),
      }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["pantry"] }),
  });
}
