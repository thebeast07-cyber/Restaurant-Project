import { apiFetch, apiUpload } from "./client";

export type Station = "Kitchen" | "Bar";

export interface RecipeItem {
  id: string;
  ingredientId: string;
  ingredientName: string;
  quantity: number;
  unit: string;
}

export interface RecipeItemInput {
  ingredientId: string;
  quantity: number;
  unit: string;
}

export interface ProductVariant {
  id: string;
  name: string;
  price: number;
  recipeItems: RecipeItem[];
}

export interface ProductVariantInput {
  id?: string;
  name: string;
  price: number;
  recipeItems: RecipeItemInput[];
}

export interface Product {
  id: string;
  name: string;
  categoryId: string;
  price: number;
  station: Station;
  isActive: boolean;
  imageUrl: string | null;
  recipeItems: RecipeItem[];
  variants: ProductVariant[];
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
  recipeItems: RecipeItemInput[],
  variants: ProductVariantInput[] = [],
): Promise<Product> {
  return apiFetch<Product>("/api/products", {
    method: "POST",
    body: JSON.stringify({ name, categoryId, price, station, recipeItems, variants }),
  });
}

export function updateProduct(
  id: string,
  name: string,
  categoryId: string,
  price: number,
  station: Station,
  isActive: boolean,
  recipeItems: RecipeItemInput[],
  variants: ProductVariantInput[] = [],
): Promise<Product> {
  return apiFetch<Product>(`/api/products/${id}`, {
    method: "PUT",
    body: JSON.stringify({ name, categoryId, price, station, isActive, recipeItems, variants }),
  });
}

export function uploadProductImage(id: string, file: File): Promise<Product> {
  const formData = new FormData();
  formData.append("file", file);
  return apiUpload<Product>(`/api/products/${id}/image`, formData);
}
