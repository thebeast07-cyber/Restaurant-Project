import { apiFetch } from "./client";
import type { Station } from "./products";

export type OrderStatus = "Draft" | "Open" | "PendingPayment" | "Paid" | "Completed" | "Voided" | "Cancelled";
export type PaymentMethodCode = "Cash" | "Qris";

export interface OrderItem {
  id: string;
  productId: string;
  productName: string;
  quantity: number;
  unitPrice: number;
  subtotal: number;
  station: Station;
}

export interface Order {
  id: string;
  tableId: string | null;
  shiftId: string;
  status: OrderStatus;
  totalAmount: number;
  items: OrderItem[];
}

export interface CheckoutResult {
  order: Order;
  paymentId: string;
  amountPaid: number;
  journalEntryId: string;
}

export interface VoidResult {
  order: Order;
  reversalJournalEntryId: string | null;
}

export function createOrder(tableId?: string): Promise<Order> {
  return apiFetch<Order>("/api/orders", {
    method: "POST",
    body: JSON.stringify({ tableId: tableId ?? null }),
  });
}

export function getOrder(orderId: string): Promise<Order> {
  return apiFetch<Order>(`/api/orders/${orderId}`);
}

export function addOrderItem(orderId: string, productId: string, quantity: number): Promise<Order> {
  return apiFetch<Order>(`/api/orders/${orderId}/items`, {
    method: "POST",
    body: JSON.stringify({ productId, quantity }),
  });
}

export function checkoutOrder(orderId: string, paymentMethod: PaymentMethodCode): Promise<CheckoutResult> {
  return apiFetch<CheckoutResult>(`/api/orders/${orderId}/checkout`, {
    method: "POST",
    body: JSON.stringify({ paymentMethod }),
  });
}

export function voidOrder(orderId: string, pin: string, reason?: string): Promise<VoidResult> {
  return apiFetch<VoidResult>(`/api/orders/${orderId}/void`, {
    method: "POST",
    body: JSON.stringify({ pin, reason: reason ?? null }),
  });
}

export function cancelOrder(orderId: string): Promise<Order> {
  return apiFetch<Order>(`/api/orders/${orderId}/cancel`, {
    method: "POST",
  });
}
