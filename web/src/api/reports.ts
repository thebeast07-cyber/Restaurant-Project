import { apiFetch } from "./client";

export interface SalesDailyReport {
  date: string;
  completedOrderCount: number;
  voidedOrderCount: number;
  cashTotal: number;
  nonCashTotal: number;
  totalRevenue: number;
}

export interface StockLevelReportItem {
  ingredientId: string;
  name: string;
  unit: string;
  currentStock: number;
  minimumStock: number;
  isBelowMinimum: boolean;
}

export interface StockLevelReport {
  asOf: string;
  belowMinimumCount: number;
  items: StockLevelReportItem[];
}

export interface WasteReportItem {
  ingredientId: string;
  name: string;
  unit: string;
  quantityWasted: number;
  wasteValue: number;
}

export interface WasteReport {
  year: number;
  month: number;
  totalWasteValue: number;
  items: WasteReportItem[];
}

export function getSalesDailyReport(date?: string): Promise<SalesDailyReport> {
  const query = date ? `?date=${date}` : "";
  return apiFetch<SalesDailyReport>(`/api/reports/sales-daily${query}`);
}

export function getStockLevelReport(): Promise<StockLevelReport> {
  return apiFetch<StockLevelReport>("/api/reports/stock-levels");
}

export function getWasteReport(year?: number, month?: number): Promise<WasteReport> {
  const query = year && month ? `?year=${year}&month=${month}` : "";
  return apiFetch<WasteReport>(`/api/reports/waste${query}`);
}

export interface SalesRangeReport {
  from: string;
  to: string;
  days: SalesDailyReport[];
}

export interface WasteRangeItem {
  year: number;
  month: number;
  totalWasteValue: number;
}

export interface WasteRangeReport {
  from: string;
  to: string;
  months: WasteRangeItem[];
}

export interface StockTrendPoint {
  date: string;
  balance: number;
}

export interface StockTrendItem {
  ingredientId: string;
  name: string;
  unit: string;
  minimumStock: number;
  belowMinimumDays: number;
  netChangePerDay: number;
  series: StockTrendPoint[];
}

export interface StockTrendReport {
  from: string;
  to: string;
  items: StockTrendItem[];
}

export function getSalesRangeReport(from: string, to: string): Promise<SalesRangeReport> {
  return apiFetch<SalesRangeReport>(`/api/reports/sales-range?from=${from}&to=${to}`);
}

export function getWasteRangeReport(from: string, to: string): Promise<WasteRangeReport> {
  return apiFetch<WasteRangeReport>(`/api/reports/waste-range?from=${from}&to=${to}`);
}

export function getStockTrendReport(from: string, to: string): Promise<StockTrendReport> {
  return apiFetch<StockTrendReport>(`/api/reports/stock-trend?from=${from}&to=${to}`);
}
