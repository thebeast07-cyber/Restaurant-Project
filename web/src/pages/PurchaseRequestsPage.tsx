import { useEffect, useState, type FormEvent } from "react";
import { useNavigate } from "react-router-dom";
import {
  approvePurchaseRequest,
  createPurchaseRequest,
  listPurchaseRequests,
  rejectPurchaseRequest,
  type PurchaseRequest,
  type RequestedFor,
} from "../api/purchaseRequests";
import { listIngredients, type Ingredient } from "../api/ingredients";
import { IngredientSelect } from "../components/IngredientSelect";
import { useAuth } from "../context/AuthContext";
import { ApiError } from "../api/client";
import "./PurchasingShared.css";

interface DraftLine {
  ingredientId: string;
  quantity: string;
  unit: string;
}

export function PurchaseRequestsPage() {
  const navigate = useNavigate();
  const { user } = useAuth();
  const isManager = user?.role === "Manager";
  const isOwner = user?.role === "Owner";

  const [requests, setRequests] = useState<PurchaseRequest[]>([]);
  const [ingredients, setIngredients] = useState<Ingredient[]>([]);
  const [error, setError] = useState<string | null>(null);

  const [requestedFor, setRequestedFor] = useState<RequestedFor>("General");
  const [notes, setNotes] = useState("");
  const [lines, setLines] = useState<DraftLine[]>([{ ingredientId: "", quantity: "", unit: "" }]);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [reviewingId, setReviewingId] = useState<string | null>(null);

  useEffect(() => {
    if (!isManager && !isOwner) {
      return;
    }
    refresh();
    listIngredients().then(setIngredients).catch(() => {});
  }, [isManager, isOwner]);

  function refresh() {
    listPurchaseRequests()
      .then(setRequests)
      .catch((err) => setError(err instanceof ApiError ? err.message : "Tidak bisa terhubung ke server."));
  }

  function updateLine(index: number, patch: Partial<DraftLine>) {
    setLines((prev) => prev.map((line, i) => (i === index ? { ...line, ...patch } : line)));
  }

  function addLine() {
    setLines((prev) => [...prev, { ingredientId: "", quantity: "", unit: "" }]);
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
        .map((line) => ({ ingredientId: line.ingredientId, quantity: Number(line.quantity), unit: line.unit }));
      await createPurchaseRequest(requestedFor, notes || null, items);
      setNotes("");
      setLines([{ ingredientId: "", quantity: "", unit: "" }]);
      refresh();
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Tidak bisa terhubung ke server.");
    } finally {
      setIsSubmitting(false);
    }
  }

  async function handleReview(id: string, action: "approve" | "reject") {
    setError(null);
    setReviewingId(id);
    try {
      if (action === "approve") {
        await approvePurchaseRequest(id);
      } else {
        await rejectPurchaseRequest(id);
      }
      refresh();
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Tidak bisa terhubung ke server.");
    } finally {
      setReviewingId(null);
    }
  }

  if (!isManager && !isOwner) {
    return (
      <main className="purchasing-page">
        <header className="purchasing-header">
          <button type="button" className="purchasing-back" onClick={() => navigate("/purchasing")}>
            ← Purchasing
          </button>
          <h1>Permintaan Pembelian</h1>
        </header>
        <p className="purchasing-error" role="alert">
          Halaman ini khusus untuk Manager dan Owner.
        </p>
      </main>
    );
  }

  return (
    <main className="purchasing-page">
      <header className="purchasing-header">
        <button type="button" className="purchasing-back" onClick={() => navigate("/purchasing")}>
          ← Purchasing
        </button>
        <h1>Permintaan Pembelian</h1>
      </header>

      {error && (
        <p className="purchasing-error" role="alert">
          {error}
        </p>
      )}

      {isManager && (
        <form className="purchasing-form" onSubmit={handleSubmit} style={{ maxWidth: 560 }}>
          <h2>Permintaan Baru</h2>

          <label htmlFor="requestedFor">Untuk</label>
          <select
            id="requestedFor"
            value={requestedFor}
            onChange={(event) => setRequestedFor(event.target.value as RequestedFor)}
          >
            <option value="General">Umum</option>
            <option value="Kitchen">Dapur</option>
            <option value="Bar">Bar</option>
          </select>

          <label htmlFor="notes">Catatan (opsional)</label>
          <input id="notes" value={notes} onChange={(event) => setNotes(event.target.value)} />

          <div className="purchasing-line-items">
            {lines.map((line, index) => (
              <div className="purchasing-line-item" key={index}>
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
            {isSubmitting ? "Mengirim..." : "Kirim Permintaan"}
          </button>
        </form>
      )}

      <table className="purchasing-table">
        <thead>
          <tr>
            <th>Untuk</th>
            <th>Item</th>
            <th>Status</th>
            {isOwner && <th></th>}
          </tr>
        </thead>
        <tbody>
          {requests.map((request) => (
            <tr key={request.id}>
              <td>{translateRequestedFor(request.requestedFor)}</td>
              <td>
                {request.items.map((item) => `${item.ingredientName} (${item.quantity} ${item.unit})`).join(", ")}
              </td>
              <td>
                <span
                  className={`purchasing-badge ${request.status === "Approved" || request.status === "Fulfilled" ? "purchasing-badge--positive" : request.status === "Rejected" ? "purchasing-badge--warn" : ""}`}
                >
                  {translateStatus(request.status)}
                </span>
              </td>
              {isOwner && (
                <td>
                  {request.status === "Pending" && (
                    <div className="purchasing-row-actions">
                      <button
                        type="button"
                        disabled={reviewingId === request.id}
                        onClick={() => handleReview(request.id, "approve")}
                      >
                        Setujui
                      </button>
                      <button
                        type="button"
                        disabled={reviewingId === request.id}
                        onClick={() => handleReview(request.id, "reject")}
                      >
                        Tolak
                      </button>
                    </div>
                  )}
                </td>
              )}
            </tr>
          ))}
        </tbody>
      </table>
    </main>
  );
}

function translateRequestedFor(value: RequestedFor): string {
  switch (value) {
    case "Kitchen":
      return "Dapur";
    case "Bar":
      return "Bar";
    default:
      return "Umum";
  }
}

function translateStatus(status: PurchaseRequest["status"]): string {
  switch (status) {
    case "Pending":
      return "Menunggu";
    case "Approved":
      return "Disetujui";
    case "Rejected":
      return "Ditolak";
    case "Fulfilled":
      return "Terpenuhi";
  }
}
