import { apiFetch } from "./client";

export type PurchasePaymentStatus = "Unpaid" | "PartiallyPaid" | "Paid";

export interface PurchaseItem {
  ingredientId: string;
  ingredientName: string;
  quantity: number;
  unit: string;
  unitCost: number;
  subtotal: number;
}

export interface Purchase {
  id: string;
  supplierId: string;
  supplierName: string;
  purchaseRequestId: string | null;
  totalAmount: number;
  amountPaid: number;
  remainingBalance: number;
  paymentStatus: PurchasePaymentStatus;
  journalEntryId: string;
  items: PurchaseItem[];
}

export interface PurchasePaymentResult {
  purchaseId: string;
  amountPaid: number;
  remainingBalance: number;
  paymentStatus: PurchasePaymentStatus;
  journalEntryId: string;
}

export function listPurchases(): Promise<Purchase[]> {
  return apiFetch<Purchase[]>("/api/purchases");
}

export function createPurchase(
  supplierId: string,
  purchaseRequestId: string | null,
  items: { ingredientId: string; quantity: number; unit: string; unitCost: number }[],
): Promise<Purchase> {
  return apiFetch<Purchase>("/api/purchases", {
    method: "POST",
    body: JSON.stringify({ supplierId, purchaseRequestId, items }),
  });
}

export function recordPurchasePayment(purchaseId: string, amount: number): Promise<PurchasePaymentResult> {
  return apiFetch<PurchasePaymentResult>(`/api/purchases/${purchaseId}/payments`, {
    method: "POST",
    body: JSON.stringify({ amount }),
  });
}
