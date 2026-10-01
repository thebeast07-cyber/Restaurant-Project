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

export interface CashFlowSourceLine {
  source: string;
  cashIn: number;
  cashOut: number;
}

export interface CashFlowReport {
  from: string;
  to: string;
  openingCash: number;
  cashIn: number;
  cashOut: number;
  netCashFlow: number;
  closingCash: number;
  sources: CashFlowSourceLine[];
}

export function getCashFlow(from: string, to: string): Promise<CashFlowReport> {
  return apiFetch<CashFlowReport>(`/api/reports/cash-flow?from=${from}&to=${to}`);
}

export interface ApAgingItem {
  type: string;
  name: string;
  referenceId: string;
  incurredDate: string;
  outstandingAmount: number;
  daysOutstanding: number;
  bucket: string;
}

export interface ApAgingReport {
  asOf: string;
  totalOutstanding: number;
  bucket0To30: number;
  bucket31To60: number;
  bucket61To90: number;
  bucketOver90: number;
  items: ApAgingItem[];
}

export function getApAging(asOf?: string): Promise<ApAgingReport> {
  return apiFetch<ApAgingReport>(`/api/reports/ap-aging${asOf ? `?asOf=${asOf}` : ""}`);
}

export interface BalanceSheetLine {
  code: string;
  name: string;
  balance: number;
}

export interface BalanceSheetReport {
  asOf: string;
  assets: BalanceSheetLine[];
  totalAssets: number;
  liabilities: BalanceSheetLine[];
  totalLiabilities: number;
  retainedEarnings: number;
  totalEquity: number;
  totalLiabilitiesAndEquity: number;
  isBalanced: boolean;
}

export function getBalanceSheet(asOf?: string): Promise<BalanceSheetReport> {
  return apiFetch<BalanceSheetReport>(`/api/reports/balance-sheet${asOf ? `?asOf=${asOf}` : ""}`);
}
