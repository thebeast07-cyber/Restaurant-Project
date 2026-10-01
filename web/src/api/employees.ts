import { apiFetch } from "./client";

export interface Employee {
  id: string;
  name: string;
  position: string;
  baseSalary: number;
  hireDate: string;
  isActive: boolean;
  userId: string | null;
  hasPin: boolean;
}

export function listEmployees(): Promise<Employee[]> {
  return apiFetch<Employee[]>("/api/employees");
}

export function createEmployee(
  name: string,
  position: string,
  baseSalary: number,
  hireDate: string,
  pin: string,
): Promise<Employee> {
  return apiFetch<Employee>("/api/employees", {
    method: "POST",
    body: JSON.stringify({ name, position, baseSalary, hireDate, userId: null, pin: pin || null }),
  });
}

export function updateEmployee(
  id: string,
  name: string,
  position: string,
  baseSalary: number,
  isActive: boolean,
): Promise<Employee> {
  return apiFetch<Employee>(`/api/employees/${id}`, {
    method: "PUT",
    body: JSON.stringify({ name, position, baseSalary, isActive, userId: null }),
  });
}

export function setEmployeePin(id: string, pin: string): Promise<Employee> {
  return apiFetch<Employee>(`/api/employees/${id}/pin`, {
    method: "POST",
    body: JSON.stringify({ pin }),
  });
}
