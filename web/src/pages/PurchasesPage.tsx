import { useEffect, useState, type FormEvent } from "react";
import { useNavigate } from "react-router-dom";
import { createPurchase, listPurchases, recordPurchasePayment, type Purchase } from "../api/purchases";
import { listPurchaseRequests, type PurchaseRequest } from "../api/purchaseRequests";
import { listSuppliers, type Supplier } from "../api/suppliers";
import { listIngredients, type Ingredient } from "../api/ingredients";
import { IngredientSelect } from "../components/IngredientSelect";
import { useAuth } from "../context/AuthContext";
import { ApiError } from "../api/client";
import "./PurchasingShared.css";

interface DraftLine {
  ingredientId: string;
  quantity: string;
  unit: string;
  unitCost: string;
}

export function PurchasesPage() {
  const navigate = useNavigate();
  const { user } = useAuth();
  const canAccess = !!user && (user.role === "Owner" || user.role === "Manager");

  const [purchases, setPurchases] = useState<Purchase[]>([]);
  const [suppliers, setSuppliers] = useState<Supplier[]>([]);
  const [ingredients, setIngredients] = useState<Ingredient[]>([]);
  const [approvedRequests, setApprovedRequests] = useState<PurchaseRequest[]>([]);
  const [error, setError] = useState<string | null>(null);

  const [supplierId, setSupplierId] = useState("");
  const [purchaseRequestId, setPurchaseRequestId] = useState("");
  const [lines, setLines] = useState<DraftLine[]>([{ ingredientId: "", quantity: "", unit: "", unitCost: "" }]);
  const [isSubmitting, setIsSubmitting] = useState(false);

  const [payingId, setPayingId] = useState<string | null>(null);
  const [paymentAmount, setPaymentAmount] = useState("");
  const [paymentError, setPaymentError] = useState<string | null>(null);
  const [isPaying, setIsPaying] = useState(false);

  useEffect(() => {
    if (!canAccess) {
      return;
    }
    refresh();
    listSuppliers().then(setSuppliers).catch(() => {});
    listIngredients().then(setIngredients).catch(() => {});
    listPurchaseRequests("Approved").then(setApprovedRequests).catch(() => {});
  }, [canAccess]);

  function refresh() {
    listPurchases()
      .then(setPurchases)
      .catch((err) => setError(err instanceof ApiError ? err.message : "Tidak bisa terhubung ke server."));
  }

  function updateLine(index: number, patch: Partial<DraftLine>) {
    setLines((prev) => prev.map((line, i) => (i === index ? { ...line, ...patch } : line)));
  }

  function addLine() {
    setLines((prev) => [...prev, { ingredientId: "", quantity: "", unit: "", unitCost: "" }]);
  }

  function removeLine(index: number) {
    setLines((prev) => prev.filter((_, i) => i !== index));
  }


  async function handleSubmit(event: FormEvent) {
    event.preventDefault();
    setError(null);
    setIsSubmitting(true);
    try {
      const items = lines
        .filter((line) => line.ingredientId && line.quantity)
        .map((line) => ({
          ingredientId: line.ingredientId,
          quantity: Number(line.quantity),
          unit: line.unit,
          unitCost: Number(line.unitCost),
        }));
      await createPurchase(supplierId, purchaseRequestId || null, items);
      setSupplierId("");
      setPurchaseRequestId("");
      setLines([{ ingredientId: "", quantity: "", unit: "", unitCost: "" }]);
      refresh();
      listPurchaseRequests("Approved").then(setApprovedRequests).catch(() => {});
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Tidak bisa terhubung ke server.");
    } finally {
      setIsSubmitting(false);
    }
  }

  function openPayment(purchaseId: string) {
    setPayingId(purchaseId);
    setPaymentAmount("");
    setPaymentError(null);
  }

  async function handlePaymentSubmit(event: FormEvent) {
    event.preventDefault();
    if (!payingId) {
      return;
    }
    setPaymentError(null);
    setIsPaying(true);
    try {
      await recordPurchasePayment(payingId, Number(paymentAmount));
      setPayingId(null);
      refresh();
    } catch (err) {
      setPaymentError(err instanceof ApiError ? err.message : "Tidak bisa terhubung ke server.");
    } finally {
      setIsPaying(false);
    }
  }

  if (!canAccess) {
    return (
      <main className="purchasing-page">
        <header className="purchasing-header">
          <button type="button" className="purchasing-back" onClick={() => navigate("/purchasing")}>
            ← Purchasing
          </button>
          <h1>Pembelian</h1>
        </header>
        <p className="purchasing-error" role="alert">
          Halaman ini khusus untuk Manager dan Owner.
        </p>
      </main>
    );
  }

  const payingPurchase = purchases.find((p) => p.id === payingId);

  return (
    <main className="purchasing-page">
      <header className="purchasing-header">
        <button type="button" className="purchasing-back" onClick={() => navigate("/purchasing")}>
          ← Purchasing
        </button>
        <h1>Pembelian</h1>
      </header>

      {error && (
        <p className="purchasing-error" role="alert">
          {error}
        </p>
      )}

      <form className="purchasing-form" onSubmit={handleSubmit} style={{ maxWidth: 640 }}>
        <h2>Catat Pembelian</h2>

        <label htmlFor="supplierId">Supplier</label>
        <select id="supplierId" value={supplierId} onChange={(event) => setSupplierId(event.target.value)} required>
          <option value="">Pilih supplier</option>
          {suppliers.map((supplier) => (
            <option key={supplier.id} value={supplier.id}>
              {supplier.name}
            </option>
          ))}
        </select>

        <label htmlFor="purchaseRequestId">Terkait Permintaan Pembelian (opsional)</label>
        <select
          id="purchaseRequestId"
          value={purchaseRequestId}
          onChange={(event) => setPurchaseRequestId(event.target.value)}
        >
          <option value="">Tidak terkait</option>
          {approvedRequests.map((request) => (
            <option key={request.id} value={request.id}>
              {request.requestedFor} —{" "}
              {request.items.map((item) => `${item.ingredientName} (${item.quantity} ${item.unit})`).join(", ")}
            </option>
          ))}
        </select>

        <div className="purchasing-line-items">
          {lines.map((line, index) => (
            <div className="purchasing-line-item purchasing-line-item--purchase" key={index}>
              <IngredientSelect
                ingredients={ingredients}
                value={line.ingredientId}
                required
                onChange={(ingredientId, unit) => updateLine(index, { ingredientId, unit })}
                onCreated={(ingredient) => setIngredients((prev) => [...prev, ingredient])}
              />
              <input
                type="number"
                min="0"
                step="any"
                placeholder="Jumlah"
                value={line.quantity}
                onChange={(event) => updateLine(index, { quantity: event.target.value })}
                required
              />
              <input
                placeholder="Satuan"
                value={line.unit}
                onChange={(event) => updateLine(index, { unit: event.target.value })}
                required
              />
              <input
                type="number"
                min="0"
                step="any"
                placeholder="Harga/satuan"
                value={line.unitCost}
                onChange={(event) => updateLine(index, { unitCost: event.target.value })}
                required
              />
              <button
                type="button"
                className="purchasing-line-item-remove"
                onClick={() => removeLine(index)}
                disabled={lines.length === 1}
              >
                Hapus
              </button>
            </div>
          ))}
        </div>

        <button type="button" className="purchasing-add-line" onClick={addLine}>
          + Tambah Baris
        </button>

        <button type="submit" disabled={isSubmitting}>
          {isSubmitting ? "Menyimpan..." : "Simpan Pembelian"}
        </button>
      </form>

      <table className="purchasing-table">
        <thead>
          <tr>
            <th>Supplier</th>
            <th>Total</th>
            <th>Terbayar</th>
            <th>Sisa</th>
            <th>Status</th>
            <th></th>
          </tr>
        </thead>
        <tbody>
          {purchases.map((purchase) => (
            <tr key={purchase.id}>
              <td>{purchase.supplierName}</td>
              <td>{formatRupiah(purchase.totalAmount)}</td>
              <td>{formatRupiah(purchase.amountPaid)}</td>
              <td>{formatRupiah(purchase.remainingBalance)}</td>
              <td>
                <span
                  className={`purchasing-badge ${purchase.paymentStatus === "Paid" ? "purchasing-badge--positive" : ""}`}
                >
                  {translatePaymentStatus(purchase.paymentStatus)}
                </span>
              </td>
              <td>
                {purchase.paymentStatus !== "Paid" && (
                  <div className="purchasing-row-actions">
                    <button type="button" onClick={() => openPayment(purchase.id)}>
                      Bayar
                    </button>
                  </div>
                )}
              </td>
            </tr>
          ))}
        </tbody>
      </table>

      {payingPurchase && (
        <div className="purchasing-overlay">
          <form className="purchasing-modal" onSubmit={handlePaymentSubmit}>
            <h2>Bayar — {payingPurchase.supplierName}</h2>
            <p className="purchasing-modal-meta">Sisa tagihan: {formatRupiah(payingPurchase.remainingBalance)}</p>

            <label htmlFor="paymentAmount">Jumlah Bayar</label>
            <input
              id="paymentAmount"
              type="number"
              min="0"
              step="any"
              max={payingPurchase.remainingBalance}
              value={paymentAmount}
              onChange={(event) => setPaymentAmount(event.target.value)}
              required
              autoFocus
            />

            {paymentError && (
              <p className="purchasing-error" role="alert">
                {paymentError}
              </p>
            )}

            <div className="purchasing-modal-actions">
              <button type="button" className="purchasing-modal-cancel" onClick={() => setPayingId(null)}>
                Batal
              </button>
              <button type="submit" disabled={isPaying}>
                {isPaying ? "Memproses..." : "Konfirmasi"}
              </button>
            </div>
          </form>
        </div>
      )}
    </main>
  );
}

function translatePaymentStatus(status: Purchase["paymentStatus"]): string {
  switch (status) {
    case "Unpaid":
      return "Belum Bayar";
    case "PartiallyPaid":
      return "Sebagian";
    case "Paid":
      return "Lunas";
  }
}

function formatRupiah(amount: number): string {
  return new Intl.NumberFormat("id-ID", { style: "currency", currency: "IDR", maximumFractionDigits: 0 }).format(
    amount,
  );
}
