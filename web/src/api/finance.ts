import { apiFetch } from "./client";
import type { ExpenseCategory } from "./expenses";

export interface ProfitLossExpenseLine {
  category: ExpenseCategory;
  amount: number;
}

export interface ProfitLossReport {
  from: string;
  to: string;
  revenue: number;
  cogs: number;
  grossProfit: number;
  grossMarginPct: number;
  operatingExpenses: ProfitLossExpenseLine[];
  totalOperatingExpenses: number;
  netProfit: number;
  netMarginPct: number;
}

export interface ProductMarginItem {
  productId: string;
  productName: string;
  quantitySold: number;
  revenue: number;
  cogs: number;
  grossProfit: number;
  grossMarginPct: number;
  cogsIsEstimated: boolean;
}

export interface ProductMarginReport {
  from: string;
  to: string;
  items: ProductMarginItem[];
}

export function getProfitLoss(from: string, to: string): Promise<ProfitLossReport> {
  return apiFetch<ProfitLossReport>(`/api/reports/profit-loss?from=${from}&to=${to}`);
}

export function getProductMargin(from: string, to: string): Promise<ProductMarginReport> {
  return apiFetch<ProductMarginReport>(`/api/reports/product-margin?from=${from}&to=${to}`);
}
