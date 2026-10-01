import { apiFetch } from "./client";
import type { Order } from "./orders";

export interface SelfOrderMenuVariant {
  id: string;
  name: string;
  price: number;
}

export interface SelfOrderMenuItem {
  productId: string;
  name: string;
  categoryName: string;
  price: number;
  imageUrl: string | null;
  variants: SelfOrderMenuVariant[];
}

export interface SelfOrderTableState {
  tableNumber: string;
  isOpen: boolean;
  activeOrder: Order | null;
  menu: SelfOrderMenuItem[];
}

export interface SelfOrderReceiptItem {
  productName: string;
  quantity: number;
  unitPrice: number;
  subtotal: number;
}

export interface SelfOrderReceipt {
  orderId: string;
  tableNumber: string | null;
  completedAt: string;
  items: SelfOrderReceiptItem[];
  totalAmount: number;
}

export function getTableState(tableId: string): Promise<SelfOrderTableState> {
  return apiFetch<SelfOrderTableState>(`/api/self-order/tables/${tableId}`);
}

export function startSelfOrder(tableId: string, name: string, phone: string): Promise<Order> {
  return apiFetch<Order>(`/api/self-order/tables/${tableId}/start`, {
    method: "POST",
    body: JSON.stringify({ name, phone }),
  });
}

export function addSelfOrderItem(
  orderId: string,
  productId: string,
  quantity: number,
  productVariantId?: string,
): Promise<Order> {
  return apiFetch<Order>(`/api/self-order/orders/${orderId}/items`, {
    method: "POST",
    body: JSON.stringify({ productId, quantity, productVariantId: productVariantId ?? null }),
  });
}

export function getSelfOrder(orderId: string): Promise<Order> {
  return apiFetch<Order>(`/api/self-order/orders/${orderId}`);
}

export function getReceipt(orderId: string): Promise<SelfOrderReceipt> {
  return apiFetch<SelfOrderReceipt>(`/api/self-order/receipts/${orderId}`);
}
