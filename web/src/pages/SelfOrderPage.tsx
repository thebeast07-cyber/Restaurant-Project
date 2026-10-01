import { useEffect, useRef, useState, type FormEvent } from "react";
import { useNavigate, useParams } from "react-router-dom";
import {
  addSelfOrderItem,
  getSelfOrder,
  getTableState,
  startSelfOrder,
  type SelfOrderMenuItem,
  type SelfOrderTableState,
} from "../api/selfOrder";
import type { Order } from "../api/orders";
import { ApiError } from "../api/client";
import "./SelfOrderPage.css";

function formatRupiah(amount: number): string {
  return new Intl.NumberFormat("id-ID", { style: "currency", currency: "IDR", maximumFractionDigits: 0 }).format(
    amount,
  );
}

type Phase = "loading" | "not-found" | "closed" | "identify" | "ordering" | "paid";

export function SelfOrderPage() {
  const { tableId } = useParams<{ tableId: string }>();
  const navigate = useNavigate();

  const [phase, setPhase] = useState<Phase>("loading");
  const [tableState, setTableState] = useState<SelfOrderTableState | null>(null);
  const [order, setOrder] = useState<Order | null>(null);
  const [error, setError] = useState<string | null>(null);

  const [name, setName] = useState("");
  const [phone, setPhone] = useState("");
  const [isSubmitting, setIsSubmitting] = useState(false);

  const pollRef = useRef<number | null>(null);

  function handleError(err: unknown) {
    setError(err instanceof ApiError ? err.message : "Tidak bisa terhubung ke server.");
  }

  useEffect(() => {
    if (!tableId) return;
    getTableState(tableId)
      .then((state) => {
        setTableState(state);
        setPhase(state.isOpen ? "identify" : "closed");
      })
      .catch((err) => {
        if (err instanceof ApiError && err.status === 404) {
          setPhase("not-found");
        } else {
          handleError(err);
        }
      });
  }, [tableId]);

  // Poll order status while actively ordering, so "sudah dibayar di kasir" is
  // reflected here automatically without the customer needing to refresh.
  useEffect(() => {
    if (phase !== "ordering" || !order) return;

    pollRef.current = window.setInterval(() => {
      getSelfOrder(order.id).then((updated) => {
        setOrder(updated);
        if (updated.status === "Completed") {
          setPhase("paid");
        }
      });
    }, 5000);

    return () => {
      if (pollRef.current) window.clearInterval(pollRef.current);
    };
  }, [phase, order]);

  async function handleStart(event: FormEvent) {
    event.preventDefault();
    if (!tableId) return;
    setError(null);
    setIsSubmitting(true);
    try {
      const started = await startSelfOrder(tableId, name, phone);
      setOrder(started);
      setPhase(started.status === "Completed" ? "paid" : "ordering");
    } catch (err) {
      handleError(err);
    } finally {
      setIsSubmitting(false);
    }
  }

  async function handleAdd(item: SelfOrderMenuItem) {
    if (!order) return;
    setError(null);
    try {
      const updated = await addSelfOrderItem(order.id, item.productId, 1);
      setOrder(updated);
    } catch (err) {
      handleError(err);
    }
  }

  if (phase === "loading") {
    return (
      <main className="self-order-page">
        <p>Memuat...</p>
      </main>
    );
  }

  if (phase === "not-found") {
    return (
      <main className="self-order-page">
        <p className="self-order-error">Meja tidak ditemukan. Pastikan link/QR yang discan benar.</p>
      </main>
    );
  }

  if (phase === "closed") {
    return (
      <main className="self-order-page">
        <h1>Meja {tableState?.tableNumber}</h1>
        <p className="self-order-notice">Resto belum buka — silakan panggil staff.</p>
      </main>
    );
  }

  if (phase === "paid" && order) {
    return (
      <main className="self-order-page">
        <h1>Meja {tableState?.tableNumber}</h1>
        <p className="self-order-notice">Pesanan sudah dibayar. Terima kasih!</p>
        <button type="button" onClick={() => navigate(`/receipt/${order.id}`)}>
          Lihat Struk
        </button>
      </main>
    );
  }

  if (phase === "identify") {
    return (
      <main className="self-order-page">
        <h1>Meja {tableState?.tableNumber}</h1>
        <form className="self-order-form" onSubmit={handleStart}>
          <label htmlFor="soName">Nama</label>
          <input id="soName" value={name} onChange={(event) => setName(event.target.value)} required autoFocus />

          <label htmlFor="soPhone">No. WhatsApp</label>
          <input
            id="soPhone"
            type="tel"
            value={phone}
            onChange={(event) => setPhone(event.target.value)}
            placeholder="08xxxxxxxxxx"
            required
          />

          {error && <p className="self-order-error">{error}</p>}

          <button type="submit" disabled={isSubmitting}>
            {isSubmitting ? "Memproses..." : "Mulai Pesan"}
          </button>
        </form>
      </main>
    );
  }

  // phase === "ordering"
  const menu = tableState?.menu ?? [];
  const byCategory = menu.reduce<Record<string, SelfOrderMenuItem[]>>((acc, item) => {
    (acc[item.categoryName] ??= []).push(item);
    return acc;
  }, {});

  return (
    <main className="self-order-page">
      <h1>Meja {tableState?.tableNumber}</h1>

      {error && <p className="self-order-error">{error}</p>}

      {Object.entries(byCategory).map(([categoryName, items]) => (
        <section key={categoryName} className="self-order-category">
          <h2>{categoryName}</h2>
          {items.map((item) => (
            <div key={item.productId} className="self-order-menu-item">
              <div>
                <div className="self-order-menu-item-name">{item.name}</div>
                <div className="self-order-menu-item-price">{formatRupiah(item.price)}</div>
              </div>
              <button type="button" onClick={() => handleAdd(item)}>
                Tambah
              </button>
            </div>
          ))}
        </section>
      ))}

      <div className="self-order-cart">
        <h2>Pesanan Anda</h2>
        {!order || order.items.length === 0 ? (
          <p>Belum ada item.</p>
        ) : (
          <>
            <ul>
              {order.items.map((item) => (
                <li key={item.id}>
                  {item.quantity}x {item.productName} — {formatRupiah(item.subtotal)}
                </li>
              ))}
            </ul>
            <div className="self-order-total">Total: {formatRupiah(order.totalAmount)}</div>
          </>
        )}
        <p className="self-order-hint">Sudah selesai pesan? Panggil staff untuk bayar di kasir.</p>
      </div>
    </main>
  );
}
