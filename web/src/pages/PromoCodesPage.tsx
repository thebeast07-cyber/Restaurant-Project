import { useEffect, useState, type FormEvent } from "react";
import { useNavigate } from "react-router-dom";
import { createPromoCode, listPromoCodes, updatePromoCode, type PromoCode } from "../api/promoCodes";
import { useAuth } from "../context/AuthContext";
import { ApiError } from "../api/client";
import "./PurchasingShared.css";

const CAN_MANAGE_ROLES = ["Owner", "Manager"];

interface PromoDraft {
  code: string;
  percentageOff: string;
  minimumPurchase: string;
  expiresAt: string;
  usageLimit: string;
  isActive: boolean;
}

function emptyDraft(): PromoDraft {
  return { code: "", percentageOff: "", minimumPurchase: "", expiresAt: "", usageLimit: "", isActive: true };
}

function draftFromPromoCode(promo: PromoCode): PromoDraft {
  return {
    code: promo.code,
    percentageOff: String(promo.percentageOff),
    minimumPurchase: promo.minimumPurchase === null ? "" : String(promo.minimumPurchase),
    expiresAt: promo.expiresAt ?? "",
    usageLimit: promo.usageLimit === null ? "" : String(promo.usageLimit),
    isActive: promo.isActive,
  };
}

function formatRupiah(amount: number): string {
  return new Intl.NumberFormat("id-ID", { style: "currency", currency: "IDR", maximumFractionDigits: 0 }).format(
    amount,
  );
}

export function PromoCodesPage() {
  const navigate = useNavigate();
  const { user } = useAuth();
  const canManage = !!user && CAN_MANAGE_ROLES.includes(user.role);

  const [promoCodes, setPromoCodes] = useState<PromoCode[]>([]);
  const [error, setError] = useState<string | null>(null);

  const [showCreate, setShowCreate] = useState(false);
  const [createDraft, setCreateDraft] = useState<PromoDraft>(emptyDraft());
  const [isSubmitting, setIsSubmitting] = useState(false);

  const [editingPromo, setEditingPromo] = useState<PromoCode | null>(null);
  const [editDraft, setEditDraft] = useState<PromoDraft>(emptyDraft());
  const [isSaving, setIsSaving] = useState(false);

  useEffect(() => {
    refresh();
  }, []);

  function refresh() {
    listPromoCodes()
      .then(setPromoCodes)
      .catch((err) => setError(err instanceof ApiError ? err.message : "Tidak bisa terhubung ke server."));
  }

  async function handleCreate(event: FormEvent) {
    event.preventDefault();
    setError(null);
    setIsSubmitting(true);
    try {
      await createPromoCode(
        createDraft.code,
        Number(createDraft.percentageOff),
        createDraft.minimumPurchase ? Number(createDraft.minimumPurchase) : null,
        createDraft.expiresAt || null,
        createDraft.usageLimit ? Number(createDraft.usageLimit) : null,
      );
      setCreateDraft(emptyDraft());
      setShowCreate(false);
      refresh();
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Tidak bisa terhubung ke server.");
    } finally {
      setIsSubmitting(false);
    }
  }

  function openEdit(promo: PromoCode) {
    setEditingPromo(promo);
    setEditDraft(draftFromPromoCode(promo));
  }

  async function handleEditSave(event: FormEvent) {
    event.preventDefault();
    if (!editingPromo) {
      return;
    }
    setError(null);
    setIsSaving(true);
    try {
      await updatePromoCode(
        editingPromo.id,
        Number(editDraft.percentageOff),
        editDraft.minimumPurchase ? Number(editDraft.minimumPurchase) : null,
        editDraft.expiresAt || null,
        editDraft.usageLimit ? Number(editDraft.usageLimit) : null,
        editDraft.isActive,
      );
      setEditingPromo(null);
      refresh();
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Tidak bisa terhubung ke server.");
    } finally {
      setIsSaving(false);
    }
  }

  return (
    <main className="purchasing-page">
      <header className="purchasing-header">
        <button type="button" className="purchasing-back" onClick={() => navigate("/dashboard")}>
          ← Dashboard
        </button>
        <h1>Kode Promo</h1>
      </header>

      {error && (
        <p className="purchasing-error" role="alert">
          {error}
        </p>
      )}

      {canManage && !showCreate && (
        <button type="button" className="purchasing-add-line" style={{ marginTop: 16 }} onClick={() => setShowCreate(true)}>
          + Kode Promo Baru
        </button>
      )}

      {canManage && showCreate && (
        <form className="purchasing-form" onSubmit={handleCreate} style={{ maxWidth: 420 }}>
          <h2>Kode Promo Baru</h2>

          <label htmlFor="createCode">Kode</label>
          <input
            id="createCode"
            value={createDraft.code}
            onChange={(event) => setCreateDraft({ ...createDraft, code: event.target.value })}
            placeholder="mis. HEMAT10"
            required
          />

          <label htmlFor="createPct">Diskon (%)</label>
          <input
            id="createPct"
            type="number"
            min="0"
            max="100"
            step="any"
            value={createDraft.percentageOff}
            onChange={(event) => setCreateDraft({ ...createDraft, percentageOff: event.target.value })}
            required
          />

          <label htmlFor="createMin">Minimal Belanja (opsional)</label>
          <input
            id="createMin"
            type="number"
            min="0"
            step="any"
            value={createDraft.minimumPurchase}
            onChange={(event) => setCreateDraft({ ...createDraft, minimumPurchase: event.target.value })}
          />

          <label htmlFor="createExpiry">Berlaku Sampai (opsional)</label>
          <input
            id="createExpiry"
            type="date"
            value={createDraft.expiresAt}
            onChange={(event) => setCreateDraft({ ...createDraft, expiresAt: event.target.value })}
          />

          <label htmlFor="createLimit">Kuota Pemakaian (opsional)</label>
          <input
            id="createLimit"
            type="number"
            min="1"
            step="1"
            value={createDraft.usageLimit}
            onChange={(event) => setCreateDraft({ ...createDraft, usageLimit: event.target.value })}
          />

          <div className="purchasing-modal-actions">
            <button
              type="button"
              className="purchasing-modal-cancel"
              onClick={() => {
                setShowCreate(false);
                setCreateDraft(emptyDraft());
              }}
            >
              Batal
            </button>
            <button type="submit" disabled={isSubmitting}>
              {isSubmitting ? "Menyimpan..." : "Simpan"}
            </button>
          </div>
        </form>
      )}

      <table className="purchasing-table">
        <thead>
          <tr>
            <th>Kode</th>
            <th>Diskon</th>
            <th>Min. Belanja</th>
            <th>Berlaku Sampai</th>
            <th>Pemakaian</th>
            <th>Status</th>
            <th></th>
          </tr>
        </thead>
        <tbody>
          {promoCodes.map((promo) => (
            <tr key={promo.id}>
              <td>{promo.code}</td>
              <td>{promo.percentageOff}%</td>
              <td>{promo.minimumPurchase !== null ? formatRupiah(promo.minimumPurchase) : "—"}</td>
              <td>{promo.expiresAt ?? "—"}</td>
              <td>
                {promo.usageCount}
                {promo.usageLimit !== null ? ` / ${promo.usageLimit}` : ""}
              </td>
              <td>
                <span className={`purchasing-badge ${promo.isActive ? "purchasing-badge--positive" : ""}`}>
                  {promo.isActive ? "Aktif" : "Nonaktif"}
                </span>
              </td>
              <td>
                {canManage && (
                  <div className="purchasing-row-actions">
                    <button type="button" onClick={() => openEdit(promo)}>
                      Ubah
                    </button>
                  </div>
                )}
              </td>
            </tr>
          ))}
        </tbody>
      </table>

      {editingPromo && (
        <div className="purchasing-overlay">
          <form className="purchasing-modal" onSubmit={handleEditSave} style={{ maxWidth: 420 }}>
            <h2>Ubah — {editingPromo.code}</h2>

            <label htmlFor="editPct">Diskon (%)</label>
            <input
              id="editPct"
              type="number"
              min="0"
              max="100"
              step="any"
              value={editDraft.percentageOff}
              onChange={(event) => setEditDraft({ ...editDraft, percentageOff: event.target.value })}
              required
            />

            <label htmlFor="editMin">Minimal Belanja (opsional)</label>
            <input
              id="editMin"
              type="number"
              min="0"
              step="any"
              value={editDraft.minimumPurchase}
              onChange={(event) => setEditDraft({ ...editDraft, minimumPurchase: event.target.value })}
            />

            <label htmlFor="editExpiry">Berlaku Sampai (opsional)</label>
            <input
              id="editExpiry"
              type="date"
              value={editDraft.expiresAt}
              onChange={(event) => setEditDraft({ ...editDraft, expiresAt: event.target.value })}
            />

            <label htmlFor="editLimit">Kuota Pemakaian (opsional)</label>
            <input
              id="editLimit"
              type="number"
              min="1"
              step="1"
              value={editDraft.usageLimit}
              onChange={(event) => setEditDraft({ ...editDraft, usageLimit: event.target.value })}
            />

            <label htmlFor="editActive">
              <input
                id="editActive"
                type="checkbox"
                checked={editDraft.isActive}
                onChange={(event) => setEditDraft({ ...editDraft, isActive: event.target.checked })}
                style={{ marginRight: 8 }}
              />
              Aktif
            </label>

            {error && (
              <p className="purchasing-error" role="alert">
                {error}
              </p>
            )}

            <div className="purchasing-modal-actions">
              <button type="button" className="purchasing-modal-cancel" onClick={() => setEditingPromo(null)}>
                Batal
              </button>
              <button type="submit" disabled={isSaving}>
                {isSaving ? "Menyimpan..." : "Simpan Perubahan"}
              </button>
            </div>
          </form>
        </div>
      )}
    </main>
  );
}
