import { apiFetch } from "./client";

export interface StockOpnameLine {
  ingredientId: string;
  ingredientName: string;
  unit: string;
  quantityBefore: number;
  quantityAfter: number;
  changeQuantity: number;
}

export interface StockOpnameSession {
  id: string;
  date: string;
  notes: string | null;
  createdByUserId: string;
  lines: StockOpnameLine[];
}

export function listStockOpnameSessions(): Promise<StockOpnameSession[]> {
  return apiFetch<StockOpnameSession[]>("/api/stock-opname");
}

export function getStockOpnameSession(id: string): Promise<StockOpnameSession> {
  return apiFetch<StockOpnameSession>(`/api/stock-opname/${id}`);
}

export function createStockOpnameSession(
  date: string,
  notes: string,
  lines: { ingredientId: string; countedQuantity: number }[],
): Promise<StockOpnameSession> {
  return apiFetch<StockOpnameSession>("/api/stock-opname", {
    method: "POST",
    body: JSON.stringify({ date, notes: notes || null, lines }),
  });
}
