import { apiFetch } from "./client";

export type ExpenseCategory = "Sewa" | "Gaji" | "Utilitas" | "Marketing" | "Lainnya";
export type ExpensePaymentStatus = "Unpaid" | "PartiallyPaid" | "Paid";

export interface OperatingExpense {
  id: string;
  category: ExpenseCategory;
  description: string;
  amount: number;
  incurredAt: string;
  amountPaid: number;
  remainingBalance: number;
  paymentStatus: ExpensePaymentStatus;
  journalEntryId: string;
}

export interface ExpensePaymentResult {
  operatingExpenseId: string;
  amountPaid: number;
  remainingBalance: number;
  paymentStatus: ExpensePaymentStatus;
  journalEntryId: string;
}

export function listExpenses(): Promise<OperatingExpense[]> {
  return apiFetch<OperatingExpense[]>("/api/expenses");
}

export function createExpense(
  category: ExpenseCategory,
  description: string,
  amount: number,
  incurredAt: string,
): Promise<OperatingExpense> {
  return apiFetch<OperatingExpense>("/api/expenses", {
    method: "POST",
    body: JSON.stringify({ category, description, amount, incurredAt }),
  });
}

export function recordExpensePayment(expenseId: string, amount: number): Promise<ExpensePaymentResult> {
  return apiFetch<ExpensePaymentResult>(`/api/expenses/${expenseId}/payments`, {
    method: "POST",
    body: JSON.stringify({ amount }),
  });
}
