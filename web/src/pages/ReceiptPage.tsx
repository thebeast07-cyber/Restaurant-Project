import { useEffect, useState } from "react";
import { useParams } from "react-router-dom";
import { getReceipt, type SelfOrderReceipt } from "../api/selfOrder";
import { exportToPdf } from "../lib/export";
import { ApiError } from "../api/client";
import "./SelfOrderPage.css";

function formatRupiah(amount: number): string {
  return new Intl.NumberFormat("id-ID", { style: "currency", currency: "IDR", maximumFractionDigits: 0 }).format(
    amount,
  );
}

export function ReceiptPage() {
  const { orderId } = useParams<{ orderId: string }>();
  const [receipt, setReceipt] = useState<SelfOrderReceipt | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!orderId) return;
    getReceipt(orderId)
      .then(setReceipt)
      .catch((err) => setError(err instanceof ApiError ? err.message : "Tidak bisa terhubung ke server."));
  }, [orderId]);

  function handleDownload() {
    if (!receipt) return;
    exportToPdf(
      `struk-${receipt.orderId.slice(0, 8)}`,
      receipt.tableNumber ? `Struk — Meja ${receipt.tableNumber}` : "Struk",
      ["Produk", "Qty", "Harga", "Subtotal"],
      receipt.items.map((i) => [i.productName, i.quantity, formatRupiah(i.unitPrice), formatRupiah(i.subtotal)]),
    );
  }

  if (error) {
    return (
      <main className="self-order-page">
        <p className="self-order-error">{error}</p>
      </main>
    );
  }

  if (!receipt) {
    return (
      <main className="self-order-page">
        <p>Memuat struk...</p>
      </main>
    );
  }

  return (
    <main className="self-order-page">
      <h1>Struk{receipt.tableNumber ? ` — Meja ${receipt.tableNumber}` : ""}</h1>
      <p className="self-order-hint">{new Date(receipt.completedAt).toLocaleString("id-ID")}</p>

      <ul className="self-order-category" style={{ listStyle: "none", padding: 0 }}>
        {receipt.items.map((item, idx) => (
          <li key={idx} className="self-order-menu-item" style={{ borderBottom: "1px solid #eee" }}>
            <div>
              <div className="self-order-menu-item-name">
                {item.quantity}x {item.productName}
              </div>
              <div className="self-order-menu-item-price">{formatRupiah(item.unitPrice)} / item</div>
            </div>
            <div>{formatRupiah(item.subtotal)}</div>
          </li>
        ))}
      </ul>

      <div className="self-order-total" style={{ marginTop: 16 }}>
        Total: {formatRupiah(receipt.totalAmount)}
      </div>

      <button type="button" onClick={handleDownload} style={{ marginTop: 20 }}>
        Unduh PDF
      </button>
    </main>
  );
}
