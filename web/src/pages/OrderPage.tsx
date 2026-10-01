import { useEffect, useState, type FormEvent } from "react";
import { useNavigate, useParams } from "react-router-dom";
import QRCode from "qrcode";
import { listCategories, type Category } from "../api/categories";
import { listProducts, type Product } from "../api/products";
import {
  addOrderItem,
  applyPromoCode,
  cancelOrder,
  checkoutOrder,
  getOrder,
  removeOrderItem,
  removePromoCode,
  updateOrderItemNotes,
  voidOrder,
  type Order,
  type PaymentMethodCode,
} from "../api/orders";
import { useAuth } from "../context/AuthContext";
import { ApiError } from "../api/client";
import "./OrderPage.css";

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL as string;
const CAN_VOID_ROLES = ["Owner", "Manager"];

export function OrderPage() {
  const { orderId } = useParams<{ orderId: string }>();
  const navigate = useNavigate();
  const { user } = useAuth();

  const [order, setOrder] = useState<Order | null>(null);
  const [categories, setCategories] = useState<Category[]>([]);
  const [products, setProducts] = useState<Product[]>([]);
  const [activeCategoryId, setActiveCategoryId] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [isBusy, setIsBusy] = useState(false);
  const [showVoid, setShowVoid] = useState(false);
  const [voidPin, setVoidPin] = useState("");
  const [voidReason, setVoidReason] = useState("");
  const [voidError, setVoidError] = useState<string | null>(null);
  const [paymentConfirmation, setPaymentConfirmation] = useState<{
    paymentMethod: PaymentMethodCode;
    amount: number;
    changeDue: number;
  } | null>(null);
  const [receiptQrUrl, setReceiptQrUrl] = useState<string | null>(null);
  const [showCashModal, setShowCashModal] = useState(false);
  const [cashTendered, setCashTendered] = useState("");
  const [noteDrafts, setNoteDrafts] = useState<Record<string, string>>({});
  const [variantPickerProduct, setVariantPickerProduct] = useState<Product | null>(null);
  const [promoInput, setPromoInput] = useState("");
  const [promoError, setPromoError] = useState<string | null>(null);
  const [isApplyingPromo, setIsApplyingPromo] = useState(false);

  useEffect(() => {
    if (!paymentConfirmation || !orderId) return;
    QRCode.toDataURL(`${window.location.origin}/receipt/${orderId}`, { width: 200, margin: 1 }).then(setReceiptQrUrl);
  }, [paymentConfirmation, orderId]);

  useEffect(() => {
    if (!orderId) {
      return;
    }
    Promise.all([getOrder(orderId), listCategories(), listProducts()])
      .then(([orderData, categoryData, productData]) => {
        setOrder(orderData);
        setCategories(categoryData);
        setProducts(productData);
        setActiveCategoryId(categoryData[0]?.id ?? null);
      })
      .catch((err) => setError(err instanceof ApiError ? err.message : "Tidak bisa terhubung ke server."));
  }, [orderId]);

  function handleProductClick(product: Product) {
    if (product.variants.length > 0) {
      setVariantPickerProduct(product);
      return;
    }
    handleAddItem(product.id);
  }

  async function handleAddItem(productId: string, productVariantId?: string) {
    if (!orderId) {
      return;
    }
    setError(null);
    setIsBusy(true);
    try {
      const updated = await addOrderItem(orderId, productId, 1, productVariantId);
      setOrder(updated);
      setVariantPickerProduct(null);
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Tidak bisa terhubung ke server.");
    } finally {
      setIsBusy(false);
    }
  }

  async function handleCheckout(paymentMethod: PaymentMethodCode, amountTendered?: number) {
    if (!orderId) {
      return;
    }
    setError(null);
    setIsBusy(true);
    try {
      const result = await checkoutOrder(orderId, paymentMethod, amountTendered);
      setOrder(result.order);
      setShowCashModal(false);
      setPaymentConfirmation({ paymentMethod, amount: result.amountPaid, changeDue: result.changeDue });
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Tidak bisa terhubung ke server.");
    } finally {
      setIsBusy(false);
    }
  }

  async function handleRemoveItem(itemId: string) {
    if (!orderId) {
      return;
    }
    setError(null);
    setIsBusy(true);
    try {
      const updated = await removeOrderItem(orderId, itemId);
      setOrder(updated);
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Tidak bisa terhubung ke server.");
    } finally {
      setIsBusy(false);
    }
  }

  async function handleSaveNote(itemId: string, notes: string) {
    if (!orderId) {
      return;
    }
    setError(null);
    try {
      const updated = await updateOrderItemNotes(orderId, itemId, notes);
      setOrder(updated);
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Tidak bisa terhubung ke server.");
    }
  }

  async function handleApplyPromo() {
    if (!orderId || !promoInput.trim()) {
      return;
    }
    setPromoError(null);
    setIsApplyingPromo(true);
    try {
      const updated = await applyPromoCode(orderId, promoInput.trim());
      setOrder(updated);
      setPromoInput("");
    } catch (err) {
      setPromoError(err instanceof ApiError ? err.message : "Tidak bisa terhubung ke server.");
    } finally {
      setIsApplyingPromo(false);
    }
  }

  async function handleRemovePromo() {
    if (!orderId) {
      return;
    }
    setPromoError(null);
    setIsApplyingPromo(true);
    try {
      const updated = await removePromoCode(orderId);
      setOrder(updated);
    } catch (err) {
      setPromoError(err instanceof ApiError ? err.message : "Tidak bisa terhubung ke server.");
    } finally {
      setIsApplyingPromo(false);
    }
  }

  async function handleCancel() {
    if (!orderId) {
      return;
    }
    setError(null);
    setIsBusy(true);
    try {
      await cancelOrder(orderId);
      navigate("/tables");
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Tidak bisa terhubung ke server.");
    } finally {
      setIsBusy(false);
    }
  }

  async function handleVoid(event: FormEvent) {
    event.preventDefault();
    if (!orderId) {
      return;
    }
    setVoidError(null);
    setIsBusy(true);
    try {
      await voidOrder(orderId, voidPin, voidReason || undefined);
      navigate("/tables");
    } catch (err) {
      setVoidError(err instanceof ApiError ? err.message : "Tidak bisa terhubung ke server.");
    } finally {
      setIsBusy(false);
    }
  }

  if (!order) {
    return (
      <main className="order-page">
        {error ? (
          <p className="order-error" role="alert">
            {error}
          </p>
        ) : (
          <p className="order-meta">Memuat...</p>
        )}
      </main>
    );
  }

  if (paymentConfirmation) {
    return (
      <main className="order-page">
        <div className="order-payment-confirmation">
          <span className="order-payment-confirmation-icon">✓</span>
          <h1>Pembayaran Berhasil</h1>
          <p className="order-payment-confirmation-amount">{formatRupiah(paymentConfirmation.amount)}</p>
          <p className="order-payment-confirmation-method">
            {paymentConfirmation.paymentMethod === "Cash" ? "Tunai" : "QRIS"}
          </p>
          {paymentConfirmation.paymentMethod === "Cash" && paymentConfirmation.changeDue > 0 && (
            <p className="order-payment-confirmation-change">
              Kembalian: {formatRupiah(paymentConfirmation.changeDue)}
            </p>
          )}
          {receiptQrUrl && (
            <div className="order-receipt-qr">
              <p>Struk digital — scan untuk lihat/unduh</p>
              <img src={receiptQrUrl} alt="QR struk digital" width={160} height={160} />
            </div>
          )}
          <button type="button" onClick={() => navigate("/tables")}>
            Kembali ke Meja
          </button>
        </div>
      </main>
    );
  }

  const visibleProducts = activeCategoryId
    ? products.filter((p) => p.categoryId === activeCategoryId)
    : products;
  const canVoid = order.status === "Completed" && !!user && CAN_VOID_ROLES.includes(user.role);

  return (
    <main className="order-page">
      <header className="order-header">
        <button type="button" className="order-back" onClick={() => navigate("/tables")}>
          ← Meja
        </button>
        <h1>Order</h1>
        <span className={`order-status order-status--${order.status.toLowerCase()}`}>{order.status}</span>
      </header>

      {error && (
        <p className="order-error" role="alert">
          {error}
        </p>
      )}

      {order.status === "Completed" ? (
        <div className="order-complete">
          <p>Pesanan sudah dibayar. Total {formatRupiah(order.totalAmount)}.</p>
          {canVoid && (
            <button type="button" className="order-void-trigger" onClick={() => setShowVoid(true)}>
              Void Order
            </button>
          )}
        </div>
      ) : (
        <div className="order-layout">
          <section className="order-menu">
            <nav className="order-categories">
              {categories.map((category) => (
                <button
                  key={category.id}
                  type="button"
                  className={category.id === activeCategoryId ? "order-category--active" : ""}
                  onClick={() => setActiveCategoryId(category.id)}
                >
                  {category.name}
                </button>
              ))}
            </nav>

            <div className="order-products">
              {visibleProducts.map((product) => (
                <button
                  key={product.id}
                  type="button"
                  className="order-product"
                  disabled={isBusy}
                  onClick={() => handleProductClick(product)}
                >
                  {product.imageUrl && (
                    <img src={`${API_BASE_URL}${product.imageUrl}`} alt="" className="order-product-image" />
                  )}
                  <span>{product.name}</span>
                  <span className="order-product-price">
                    {product.variants.length > 0 ? "Pilih varian" : formatRupiah(product.price)}
                  </span>
                </button>
              ))}
            </div>
          </section>

          <aside className="order-cart">
            <h2>Keranjang</h2>
            <ul className="order-cart-items">
              {order.items.length === 0 && <li className="order-cart-empty">Belum ada item.</li>}
              {order.items.map((item) => (
                <li key={item.id} className="order-cart-item">
                  <div className="order-cart-item-row">
                    <span>
                      {item.quantity}× {item.productName}
                      {item.productVariantName && ` (${item.productVariantName})`}
                    </span>
                    <span>{formatRupiah(item.subtotal)}</span>
                    <button
                      type="button"
                      className="order-cart-item-remove"
                      disabled={isBusy}
                      onClick={() => handleRemoveItem(item.id)}
                      aria-label={`Hapus ${item.productName}`}
                    >
                      ×
                    </button>
                  </div>
                  <input
                    type="text"
                    className="order-cart-item-note"
                    placeholder="Catatan (mis. tanpa es)"
                    value={noteDrafts[item.id] ?? item.notes ?? ""}
                    disabled={isBusy}
                    onChange={(event) =>
                      setNoteDrafts((prev) => ({ ...prev, [item.id]: event.target.value }))
                    }
                    onBlur={(event) => {
                      const value = event.target.value.trim();
                      if (value !== (item.notes ?? "")) {
                        handleSaveNote(item.id, value);
                      }
                    }}
                  />
                </li>
              ))}
            </ul>

            <div className="order-promo">
              {order.promoCode ? (
                <div className="order-promo-applied">
                  <span>Kode: {order.promoCode}</span>
                  <button type="button" onClick={handleRemovePromo} disabled={isApplyingPromo}>
                    Hapus
                  </button>
                </div>
              ) : (
                <div className="order-promo-input">
                  <input
                    type="text"
                    placeholder="Kode promo"
                    value={promoInput}
                    onChange={(event) => setPromoInput(event.target.value)}
                    disabled={isApplyingPromo}
                  />
                  <button type="button" onClick={handleApplyPromo} disabled={isApplyingPromo || !promoInput.trim()}>
                    Pakai
                  </button>
                </div>
              )}
              {promoError && (
                <p className="order-error" role="alert">
                  {promoError}
                </p>
              )}
            </div>

            {order.discountAmount > 0 && (
              <div className="order-cart-subtotal">
                <span>Subtotal</span>
                <span>{formatRupiah(order.subtotal)}</span>
              </div>
            )}
            {order.discountAmount > 0 && (
              <div className="order-cart-discount">
                <span>Diskon</span>
                <span>-{formatRupiah(order.discountAmount)}</span>
              </div>
            )}
            <div className="order-cart-total">
              <span>Total</span>
              <span>{formatRupiah(order.totalAmount)}</span>
            </div>

            <div className="order-checkout-actions">
              <button
                type="button"
                disabled={isBusy || order.items.length === 0}
                onClick={() => {
                  setCashTendered("");
                  setShowCashModal(true);
                }}
              >
                Bayar Tunai
              </button>
              <button
                type="button"
                disabled={isBusy || order.items.length === 0}
                onClick={() => handleCheckout("Qris")}
              >
                Bayar QRIS
              </button>
              <button type="button" className="order-cancel-trigger" disabled={isBusy} onClick={handleCancel}>
                Batal Order
              </button>
            </div>
          </aside>
        </div>
      )}

      {variantPickerProduct && (
        <div className="order-void-overlay">
          <div className="order-void-card">
            <h2>Pilih Varian — {variantPickerProduct.name}</h2>
            <div className="order-variant-options">
              {variantPickerProduct.variants.map((variant) => (
                <button
                  key={variant.id}
                  type="button"
                  className="order-variant-option"
                  disabled={isBusy}
                  onClick={() => handleAddItem(variantPickerProduct.id, variant.id)}
                >
                  <span>{variant.name}</span>
                  <span>{formatRupiah(variant.price)}</span>
                </button>
              ))}
            </div>
            <div className="order-void-actions">
              <button type="button" className="order-void-cancel" onClick={() => setVariantPickerProduct(null)}>
                Batal
              </button>
            </div>
          </div>
        </div>
      )}

      {showCashModal && (
        <div className="order-void-overlay">
          <form
            className="order-void-card"
            onSubmit={(event) => {
              event.preventDefault();
              const tendered = Number(cashTendered);
              handleCheckout("Cash", Number.isFinite(tendered) && tendered > 0 ? tendered : order.totalAmount);
            }}
          >
            <h2>Bayar Tunai</h2>
            <p>Total: {formatRupiah(order.totalAmount)}</p>
            <label htmlFor="cashTendered">Uang Diterima</label>
            <input
              id="cashTendered"
              type="number"
              inputMode="numeric"
              min={order.totalAmount}
              value={cashTendered}
              onChange={(event) => setCashTendered(event.target.value)}
              autoFocus
            />
            {cashTendered !== "" && Number(cashTendered) >= order.totalAmount && (
              <p className="order-cash-change">
                Kembalian: {formatRupiah(Number(cashTendered) - order.totalAmount)}
              </p>
            )}
            {cashTendered !== "" && Number(cashTendered) < order.totalAmount && (
              <p className="order-error" role="alert">
                Uang diterima kurang dari total.
              </p>
            )}
            <div className="order-void-actions">
              <button type="button" className="order-void-cancel" onClick={() => setShowCashModal(false)}>
                Batal
              </button>
              <button
                type="submit"
                disabled={isBusy || (cashTendered !== "" && Number(cashTendered) < order.totalAmount)}
              >
                {isBusy ? "Memproses..." : "Konfirmasi"}
              </button>
            </div>
          </form>
        </div>
      )}

      {showVoid && (
        <div className="order-void-overlay">
          <form className="order-void-card" onSubmit={handleVoid}>
            <h2>Void Order</h2>
            <label htmlFor="voidPin">PIN</label>
            <input
              id="voidPin"
              type="password"
              inputMode="numeric"
              value={voidPin}
              onChange={(event) => setVoidPin(event.target.value)}
              required
              autoFocus
            />
            <label htmlFor="voidReason">Alasan (opsional)</label>
            <input
              id="voidReason"
              type="text"
              value={voidReason}
              onChange={(event) => setVoidReason(event.target.value)}
            />
            {voidError && (
              <p className="order-error" role="alert">
                {voidError}
              </p>
            )}
            <div className="order-void-actions">
              <button type="button" className="order-void-cancel" onClick={() => setShowVoid(false)}>
                Batal
              </button>
              <button type="submit" disabled={isBusy}>
                {isBusy ? "Memproses..." : "Konfirmasi Void"}
              </button>
            </div>
          </form>
        </div>
      )}
    </main>
  );
}

function formatRupiah(amount: number): string {
  return new Intl.NumberFormat("id-ID", { style: "currency", currency: "IDR", maximumFractionDigits: 0 }).format(
    amount,
  );
}
