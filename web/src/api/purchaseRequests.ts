import { apiFetch } from "./client";

export type RequestedFor = "Kitchen" | "Bar" | "General";
export type PurchaseRequestStatus = "Pending" | "Approved" | "Rejected" | "Fulfilled";

export interface PurchaseRequestItem {
  ingredientId: string;
  ingredientName: string;
  quantity: number;
  unit: string;
}

export interface PurchaseRequest {
  id: string;
  requestedFor: RequestedFor;
  notes: string | null;
  status: PurchaseRequestStatus;
  reviewedByUserId: string | null;
  reviewedAt: string | null;
  reviewNotes: string | null;
  items: PurchaseRequestItem[];
}

export function listPurchaseRequests(status?: PurchaseRequestStatus): Promise<PurchaseRequest[]> {
  const query = status ? `?status=${status}` : "";
  return apiFetch<PurchaseRequest[]>(`/api/purchase-requests${query}`);
}

export function createPurchaseRequest(
  requestedFor: RequestedFor,
  notes: string | null,
  items: { ingredientId: string; quantity: number; unit: string }[],
): Promise<PurchaseRequest> {
  return apiFetch<PurchaseRequest>("/api/purchase-requests", {
    method: "POST",
    body: JSON.stringify({ requestedFor, notes, items }),
  });
}

export function approvePurchaseRequest(id: string, notes?: string): Promise<PurchaseRequest> {
  return apiFetch<PurchaseRequest>(`/api/purchase-requests/${id}/approve`, {
    method: "POST",
    body: JSON.stringify({ notes: notes ?? null }),
  });
}

export function rejectPurchaseRequest(id: string, notes?: string): Promise<PurchaseRequest> {
  return apiFetch<PurchaseRequest>(`/api/purchase-requests/${id}/reject`, {
    method: "POST",
    body: JSON.stringify({ notes: notes ?? null }),
  });
}
