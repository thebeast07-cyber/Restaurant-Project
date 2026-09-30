import { apiFetch } from "./client";

export type StockMovementReason = "Sale" | "Void" | "ManualAdjustment" | "Opname" | "Purchase" | "Waste";

export interface Ingredient {
  id: string;
  name: string;
  unit: string;
  currentStock: number;
  minimumStock: number;
  averageCost: number;
}

export interface StockAdjustmentResult {
  ingredientId: string;
  quantityBefore: number;
  quantityAfter: number;
  changeQuantity: number;
  movementReason: string;
}

export function listIngredients(): Promise<Ingredient[]> {
  return apiFetch<Ingredient[]>("/api/ingredients");
}

export function createIngredient(name: string, unit: string, minimumStock: number): Promise<Ingredient> {
  return apiFetch<Ingredient>("/api/ingredients", {
    method: "POST",
    body: JSON.stringify({ name, unit, minimumStock }),
  });
}

export function updateMinimumStock(ingredientId: string, minimumStock: number): Promise<void> {
  return apiFetch<void>(`/api/ingredients/${ingredientId}/minimum-stock`, {
    method: "PUT",
    body: JSON.stringify({ minimumStock }),
  });
}

export function adjustStock(
  ingredientId: string,
  reason: string,
  options: { countedQuantity?: number; deltaQuantity?: number; deltaReason?: "ManualAdjustment" | "Waste" },
): Promise<StockAdjustmentResult> {
  return apiFetch<StockAdjustmentResult>(`/api/ingredients/${ingredientId}/stock-adjustment`, {
    method: "POST",
    body: JSON.stringify({
      countedQuantity: options.countedQuantity ?? null,
      deltaQuantity: options.deltaQuantity ?? null,
      deltaReason: options.deltaReason ?? null,
      reason,
    }),
  });
}
