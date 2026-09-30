import { apiFetch } from "./client";

export type Station = "Kitchen" | "Bar";

export interface RecipeItem {
  id: string;
  ingredientId: string;
  ingredientName: string;
  quantity: number;
  unit: string;
}

export interface Product {
  id: string;
  name: string;
  categoryId: string;
  price: number;
  station: Station;
  isActive: boolean;
  recipeItems: RecipeItem[];
}

export function listProducts(includeInactive = false): Promise<Product[]> {
  const query = includeInactive ? "?includeInactive=true" : "";
  return apiFetch<Product[]>(`/api/products${query}`);
}

export function createProduct(
  name: string,
  categoryId: string,
  price: number,
  station: Station,
  recipeItems: { ingredientId: string; quantity: number; unit: string }[],
): Promise<Product> {
  return apiFetch<Product>("/api/products", {
    method: "POST",
    body: JSON.stringify({ name, categoryId, price, station, recipeItems }),
  });
}

export function updateProduct(
  id: string,
  name: string,
  categoryId: string,
  price: number,
  station: Station,
  isActive: boolean,
  recipeItems: { ingredientId: string; quantity: number; unit: string }[],
): Promise<Product> {
  return apiFetch<Product>(`/api/products/${id}`, {
    method: "PUT",
    body: JSON.stringify({ name, categoryId, price, station, isActive, recipeItems }),
  });
}
