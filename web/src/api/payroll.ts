import { apiFetch } from "./client";
import type { ExpensePaymentStatus } from "./expenses";

export interface Payslip {
  id: string;
  employeeId: string;
  employeeName: string;
  periodYear: number;
  periodMonth: number;
  baseSalary: number;
  workingDaysInPeriod: number;
  alphaDays: number;
  attendanceDeduction: number;
  grossPay: number;
  kasbonDeduction: number;
  netPay: number;
  operatingExpenseId: string;
  amountPaid: number;
  remainingBalance: number;
  paymentStatus: ExpensePaymentStatus;
}

export function listPayslips(employeeId?: string, year?: number, month?: number): Promise<Payslip[]> {
  const params = new URLSearchParams();
  if (employeeId) params.set("employeeId", employeeId);
  if (year) params.set("year", String(year));
  if (month) params.set("month", String(month));
  const qs = params.toString();
  return apiFetch<Payslip[]>(`/api/payroll/payslips${qs ? `?${qs}` : ""}`);
}

export function runPayroll(employeeId: string, periodYear: number, periodMonth: number): Promise<Payslip> {
  return apiFetch<Payslip>("/api/payroll/run", {
    method: "POST",
    body: JSON.stringify({ employeeId, periodYear, periodMonth }),
  });
}
