import { useMutation, useQueryClient } from "@tanstack/react-query";
import { api } from "../api/client";
import { downscaleImage } from "../utils/downscaleImage";
import type { PantryItem, PantryItemDraft, ReceiptScanResult } from "../types/pantry";

export function useScanReceipt() {
  return useMutation({
    mutationFn: async (file: File) => {
      const image = await downscaleImage(file);
      const form = new FormData();
      form.append("image", image, "receipt.jpg");
      return api<ReceiptScanResult>("/receipts/scan", {
        method: "POST",
        body: form,
      });
    },
  });
}

export interface ConfirmReceiptPayload {
  storeNameRaw: string | null;
  purchasedAt: string;
  imageUrl: string | null;
  rawOcrText: string | null;
  items: PantryItemDraft[];
}

export function useConfirmReceipt() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (payload: ConfirmReceiptPayload) =>
      api<PantryItem[]>("/receipts/confirm", {
        method: "POST",
        body: JSON.stringify(payload),
      }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["pantry"] }),
  });
}
