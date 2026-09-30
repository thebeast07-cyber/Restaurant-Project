import { apiFetch } from "./client";

export interface Supplier {
  id: string;
  name: string;
  contactInfo: string | null;
  isActive: boolean;
}

export function listSuppliers(): Promise<Supplier[]> {
  return apiFetch<Supplier[]>("/api/suppliers");
}

export function createSupplier(name: string, contactInfo: string | null): Promise<Supplier> {
  return apiFetch<Supplier>("/api/suppliers", {
    method: "POST",
    body: JSON.stringify({ name, contactInfo }),
  });
}
