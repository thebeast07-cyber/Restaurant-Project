import { apiFetch, ApiError } from "./client";

export type ShiftStatus = "Open" | "Closed";

export interface Shift {
  id: string;
  openedAt: string;
  openingCash: number;
  status: ShiftStatus;
}

export interface ShiftCloseResult {
  id: string;
  openedAt: string;
  closedAt: string;
  openingCash: number;
  cashSalesTotal: number;
  nonCashSalesTotal: number;
  expectedCash: number;
  closingCash: number;
  cashVariance: number;
  status: ShiftStatus;
}

export async function getCurrentShift(): Promise<Shift | null> {
  try {
    return await apiFetch<Shift>("/api/shifts/current");
  } catch (err) {
    if (err instanceof ApiError && err.status === 404) {
      return null;
    }
    throw err;
  }
}

export function openShift(openingCash: number): Promise<Shift> {
  return apiFetch<Shift>("/api/shifts/open", {
    method: "POST",
    body: JSON.stringify({ openingCash }),
  });
}

export function closeShift(shiftId: string, closingCash: number): Promise<ShiftCloseResult> {
  return apiFetch<ShiftCloseResult>(`/api/shifts/${shiftId}/close`, {
    method: "POST",
    body: JSON.stringify({ closingCash }),
  });
}
