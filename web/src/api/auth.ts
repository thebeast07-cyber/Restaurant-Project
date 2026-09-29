import { apiFetch } from "./client";

export interface LoginResponse {
  token: string;
  name: string;
  role: string;
  tenantId: string;
  branchId: string;
}

export function login(username: string, password: string): Promise<LoginResponse> {
  return apiFetch<LoginResponse>("/api/auth/login", {
    method: "POST",
    body: JSON.stringify({ username, password }),
  });
}
