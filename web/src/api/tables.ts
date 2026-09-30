import { apiFetch } from "./client";

export type TableStatus = "Available" | "Occupied";

export interface RestaurantTable {
  id: string;
  number: string;
  status: TableStatus;
  currentOrderId: string | null;
}

export function listTables(): Promise<RestaurantTable[]> {
  return apiFetch<RestaurantTable[]>("/api/tables");
}
