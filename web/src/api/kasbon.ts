import { apiFetch } from "./client";

export type KasbonStatus = "Pending" | "Approved" | "Rejected" | "Settled";

export interface Kasbon {
  id: string;
  employeeId: string;
  employeeName: string;
  amount: number;
  installmentCount: number;
  status: KasbonStatus;
  amountRepaid: number;
  remainingBalance: number;
  notes: string | null;
}

export function listKasbons(employeeId?: string, status?: KasbonStatus): Promise<Kasbon[]> {
  const params = new URLSearchParams();
  if (employeeId) params.set("employeeId", employeeId);
  if (status) params.set("status", status);
  const qs = params.toString();
  return apiFetch<Kasbon[]>(`/api/kasbons${qs ? `?${qs}` : ""}`);
}

export function createKasbon(
  employeeId: string,
  amount: number,
  installmentCount: number,
  notes?: string,
): Promise<Kasbon> {
  return apiFetch<Kasbon>("/api/kasbons", {
    method: "POST",
    body: JSON.stringify({ employeeId, amount, installmentCount, notes: notes || null }),
  });
}

export function approveKasbon(id: string): Promise<Kasbon> {
  return apiFetch<Kasbon>(`/api/kasbons/${id}/approve`, { method: "POST" });
}

export function rejectKasbon(id: string): Promise<Kasbon> {
  return apiFetch<Kasbon>(`/api/kasbons/${id}/reject`, { method: "POST" });
}
