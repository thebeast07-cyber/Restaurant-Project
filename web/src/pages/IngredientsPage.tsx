import { useEffect, useState, type FormEvent } from "react";
import { useNavigate } from "react-router-dom";
import { adjustStock, createIngredient, listIngredients, updateMinimumStock, type Ingredient } from "../api/ingredients";
import { useAuth } from "../context/AuthContext";
import { ApiError } from "../api/client";
import "./PurchasingShared.css";
import "./IngredientsPage.css";

const CAN_MANAGE_ROLES = ["Owner", "Manager"];

type AdjustMode = "Opname" | "ManualAdjustment" | "Waste";

export function IngredientsPage() {
  const navigate = useNavigate();
  const { user } = useAuth();
  const canManage = !!user && CAN_MANAGE_ROLES.includes(user.role);

  const [ingredients, setIngredients] = useState<Ingredient[]>([]);
  const [error, setError] = useState<string | null>(null);

  const [name, setName] = useState("");
  const [unit, setUnit] = useState("");
  const [minimumStock, setMinimumStock] = useState("0");
  const [isSubmitting, setIsSubmitting] = useState(false);

  const [adjustTarget, setAdjustTarget] = useState<Ingredient | null>(null);
  const [adjustMode, setAdjustMode] = useState<AdjustMode>("Opname");
  const [adjustValue, setAdjustValue] = useState("");
  const [adjustReason, setAdjustReason] = useState("");
  const [adjustError, setAdjustError] = useState<string | null>(null);
  const [isAdjusting, setIsAdjusting] = useState(false);

  useEffect(() => {
    refresh();
  }, []);

  function refresh() {
    listIngredients()
      .then(setIngredients)
      .catch((err) => setError(err instanceof ApiError ? err.message : "Tidak bisa terhubung ke server."));
  }

  async function handleCreate(event: FormEvent) {
    event.preventDefault();
    setError(null);
    setIsSubmitting(true);
    try {
      await createIngredient(name, unit, Number(minimumStock));
      setName("");
      setUnit("");
      setMinimumStock("0");
      refresh();
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Tidak bisa terhubung ke server.");
    } finally {
      setIsSubmitting(false);
    }
  }

  async function handleMinimumStockChange(ingredient: Ingredient, value: string) {
    const parsed = Number(value);
    if (Number.isNaN(parsed) || parsed < 0) {
      return;
    }
    try {
      await updateMinimumStock(ingredient.id, parsed);
      refresh();
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Tidak bisa terhubung ke server.");
    }
  }

  function openAdjust(ingredient: Ingredient) {
    setAdjustTarget(ingredient);
    setAdjustMode("Opname");
    setAdjustValue("");
    setAdjustReason("");
    setAdjustError(null);
  }

  async function handleAdjustSubmit(event: FormEvent) {
    event.preventDefault();
    if (!adjustTarget) {
      return;
    }
    setAdjustError(null);
    setIsAdjusting(true);
    try {
      if (adjustMode === "Opname") {
        await adjustStock(adjustTarget.id, adjustReason, { countedQuantity: Number(adjustValue) });
      } else {
        await adjustStock(adjustTarget.id, adjustReason, {
          deltaQuantity: Number(adjustValue),
          deltaReason: adjustMode,
        });
      }
      setAdjustTarget(null);
      refresh();
    } catch (err) {
      setAdjustError(err instanceof ApiError ? err.message : "Tidak bisa terhubung ke server.");
    } finally {
      setIsAdjusting(false);
    }
  }

  return (
    <main className="purchasing-page">
      <header className="purchasing-header">
        <button type="button" className="purchasing-back" onClick={() => navigate("/purchasing")}>
          ← Purchasing
        </button>
        <h1>Bahan Baku</h1>
      </header>

      {error && (
        <p className="purchasing-error" role="alert">
          {error}
        </p>
      )}

      {canManage && (
        <form className="purchasing-form" onSubmit={handleCreate}>
          <h2>Bahan Baru</h2>
          <label htmlFor="ingName">Nama</label>
          <input id="ingName" value={name} onChange={(event) => setName(event.target.value)} required />

          <label htmlFor="ingUnit">Satuan</label>
          <input
            id="ingUnit"
            placeholder="gram, pcs, ml, ..."
            value={unit}
            onChange={(event) => setUnit(event.target.value)}
            required
          />

          <label htmlFor="ingMin">Stok Minimum</label>
          <input
            id="ingMin"
            type="number"
            min="0"
            step="any"
            value={minimumStock}
            onChange={(event) => setMinimumStock(event.target.value)}
          />

          <button type="submit" disabled={isSubmitting}>
            {isSubmitting ? "Menyimpan..." : "Simpan"}
          </button>
        </form>
      )}

      <table className="purchasing-table">
        <thead>
          <tr>
            <th>Nama</th>
            <th>Stok</th>
            <th>Stok Minimum</th>
            <th>Harga Rata-rata</th>
            {canManage && <th></th>}
          </tr>
        </thead>
        <tbody>
          {ingredients.map((ingredient) => {
            const isLow = ingredient.currentStock < ingredient.minimumStock;
            return (
              <tr key={ingredient.id}>
                <td>{ingredient.name}</td>
                <td>
                  {ingredient.currentStock} {ingredient.unit}
                  {isLow && <span className="purchasing-badge purchasing-badge--warn"> Rendah</span>}
                </td>
                <td>
                  {canManage ? (
                    <input
                      className="ingredients-min-input"
                      type="number"
                      min="0"
                      step="any"
                      defaultValue={ingredient.minimumStock}
                      onBlur={(event) => handleMinimumStockChange(ingredient, event.target.value)}
                    />
                  ) : (
                    ingredient.minimumStock
                  )}
                </td>
                <td>{formatRupiah(ingredient.averageCost)}</td>
                {canManage && (
                  <td>
                    <div className="purchasing-row-actions">
                      <button type="button" onClick={() => openAdjust(ingredient)}>
                        Sesuaikan Stok
                      </button>
                    </div>
                  </td>
                )}
              </tr>
            );
          })}
        </tbody>
      </table>

      {adjustTarget && (
        <div className="purchasing-overlay">
          <form className="purchasing-modal" onSubmit={handleAdjustSubmit}>
            <h2>Sesuaikan Stok — {adjustTarget.name}</h2>
            <p className="purchasing-modal-meta">
              Stok saat ini: {adjustTarget.currentStock} {adjustTarget.unit}
            </p>

            <label htmlFor="adjustMode">Jenis Penyesuaian</label>
            <select
              id="adjustMode"
              value={adjustMode}
              onChange={(event) => setAdjustMode(event.target.value as AdjustMode)}
            >
              <option value="Opname">Opname (hasil hitung fisik)</option>
              <option value="ManualAdjustment">Koreksi Manual (+/-)</option>
              <option value="Waste">Waste / Rusak (+/-)</option>
            </select>

            <label htmlFor="adjustValue">
              {adjustMode === "Opname" ? `Jumlah Terhitung (${adjustTarget.unit})` : `Perubahan (${adjustTarget.unit}, boleh negatif)`}
            </label>
            <input
              id="adjustValue"
              type="number"
              step="any"
              value={adjustValue}
              onChange={(event) => setAdjustValue(event.target.value)}
              required
            />

            <label htmlFor="adjustReason">Alasan</label>
            <input
              id="adjustReason"
              value={adjustReason}
              onChange={(event) => setAdjustReason(event.target.value)}
              required
            />

            {adjustError && (
              <p className="purchasing-error" role="alert">
                {adjustError}
              </p>
            )}

            <div className="purchasing-modal-actions">
              <button type="button" className="purchasing-modal-cancel" onClick={() => setAdjustTarget(null)}>
                Batal
              </button>
              <button type="submit" disabled={isAdjusting}>
                {isAdjusting ? "Menyimpan..." : "Simpan"}
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
