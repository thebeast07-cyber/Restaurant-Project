import { apiFetch } from "./client";

export interface Category {
  id: string;
  name: string;
}

export function listCategories(): Promise<Category[]> {
  return apiFetch<Category[]>("/api/categories");
}

export function createCategory(name: string): Promise<Category> {
  return apiFetch<Category>("/api/categories", {
    method: "POST",
    body: JSON.stringify({ name }),
  });
}

export function updateCategory(id: string, name: string): Promise<void> {
  return apiFetch<void>(`/api/categories/${id}`, {
    method: "PUT",
    body: JSON.stringify({ name }),
  });
}
