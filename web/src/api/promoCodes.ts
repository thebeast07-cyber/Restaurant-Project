import { apiFetch } from "./client";

export interface PromoCode {
  id: string;
  code: string;
  percentageOff: number;
  minimumPurchase: number | null;
  expiresAt: string | null;
  usageLimit: number | null;
  usageCount: number;
  isActive: boolean;
}

export function listPromoCodes(): Promise<PromoCode[]> {
  return apiFetch<PromoCode[]>("/api/promo-codes");
}

export function createPromoCode(
  code: string,
  percentageOff: number,
  minimumPurchase: number | null,
  expiresAt: string | null,
  usageLimit: number | null,
): Promise<PromoCode> {
  return apiFetch<PromoCode>("/api/promo-codes", {
    method: "POST",
    body: JSON.stringify({ code, percentageOff, minimumPurchase, expiresAt, usageLimit }),
  });
}

export function updatePromoCode(
  id: string,
  percentageOff: number,
  minimumPurchase: number | null,
  expiresAt: string | null,
  usageLimit: number | null,
  isActive: boolean,
): Promise<PromoCode> {
  return apiFetch<PromoCode>(`/api/promo-codes/${id}`, {
    method: "PUT",
    body: JSON.stringify({ percentageOff, minimumPurchase, expiresAt, usageLimit, isActive }),
  });
}
