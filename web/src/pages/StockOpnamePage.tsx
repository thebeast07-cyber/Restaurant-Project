import { Fragment, useEffect, useState, type FormEvent } from "react";
import { useNavigate } from "react-router-dom";
import { listIngredients, type Ingredient } from "../api/ingredients";
import { createStockOpnameSession, listStockOpnameSessions, type StockOpnameSession } from "../api/stockOpname";
import { useAuth } from "../context/AuthContext";
import { ApiError } from "../api/client";
import "./PurchasingShared.css";

function todayIso(): string {
  return new Date().toISOString().slice(0, 10);
}

export function StockOpnamePage() {
  const navigate = useNavigate();
  const { user } = useAuth();
  const canView = !!user && (user.role === "Owner" || user.role === "Manager");

  const [ingredients, setIngredients] = useState<Ingredient[]>([]);
  const [counts, setCounts] = useState<Record<string, string>>({});
  const [date, setDate] = useState(todayIso());
  const [notes, setNotes] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);

  const [sessions, setSessions] = useState<StockOpnameSession[]>([]);
  const [expandedId, setExpandedId] = useState<string | null>(null);

  function handleError(err: unknown) {
    setError(err instanceof ApiError ? err.message : "Tidak bisa terhubung ke server.");
  }

  function refreshIngredients() {
    listIngredients().then((list) => {
      setIngredients(list);
      setCounts(Object.fromEntries(list.map((i) => [i.id, String(i.currentStock)])));
    });
  }

  function refreshSessions() {
    listStockOpnameSessions().then(setSessions).catch(handleError);
  }

  useEffect(() => {
    if (!canView) return;
    refreshIngredients();
    refreshSessions();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [canView]);

  function setCount(ingredientId: string, value: string) {
    setCounts((prev) => ({ ...prev, [ingredientId]: value }));
  }

  async function handleSubmit(event: FormEvent) {
    event.preventDefault();
    setError(null);
    setIsSubmitting(true);
    try {
      const lines = ingredients
        .map((i) => ({ ingredientId: i.id, countedQuantity: Number(counts[i.id]) }))
        .filter((l) => !Number.isNaN(l.countedQuantity));

      await createStockOpnameSession(date, notes, lines);
      setNotes("");
      refreshIngredients();
      refreshSessions();
    } catch (err) {
      handleError(err);
    } finally {
      setIsSubmitting(false);
    }
  }

  if (!canView) {
    return (
      <main className="purchasing-page">
        <header className="purchasing-header">
          <button type="button" className="purchasing-back" onClick={() => navigate("/purchasing")}>
            ← Purchasing
          </button>
          <h1>Stock Opname</h1>
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
        <h1>Stock Opname</h1>
      </header>

      {error && (
        <p className="purchasing-error" role="alert">
          {error}
        </p>
      )}

      <form className="purchasing-form" onSubmit={handleSubmit}>
        <h2>Hitung Fisik Semua Bahan</h2>
        <p className="purchasing-modal-meta">
          Angka sudah diisi otomatis dengan stok sistem saat ini — ubah hanya yang hasil hitung fisiknya berbeda.
          Bahan yang tidak diubah tidak akan dicatat sebagai penyesuaian.
        </p>

        <label htmlFor="opnameDate">Tanggal</label>
        <input id="opnameDate" type="date" value={date} onChange={(event) => setDate(event.target.value)} required />

        <label htmlFor="opnameNotes">Catatan</label>
        <input id="opnameNotes" value={notes} onChange={(event) => setNotes(event.target.value)} />

        <table className="purchasing-table">
          <thead>
            <tr>
              <th>Bahan</th>
              <th>Stok Sistem</th>
              <th>Hasil Hitung Fisik</th>
            </tr>
          </thead>
          <tbody>
            {ingredients.map((ingredient) => (
              <tr key={ingredient.id}>
                <td>
                  {ingredient.name} ({ingredient.unit})
                </td>
                <td>{ingredient.currentStock}</td>
                <td>
                  <input
                    type="number"
                    min="0"
                    step="any"
                    value={counts[ingredient.id] ?? ""}
                    onChange={(event) => setCount(ingredient.id, event.target.value)}
                  />
                </td>
              </tr>
            ))}
          </tbody>
        </table>

        <button type="submit" disabled={isSubmitting}>
          {isSubmitting ? "Menyimpan..." : "Simpan Opname"}
        </button>
      </form>

      <h2 style={{ marginTop: 32 }}>Riwayat Opname</h2>
      <table className="purchasing-table">
        <thead>
          <tr>
            <th>Tanggal</th>
            <th>Catatan</th>
            <th>Jumlah Bahan Berubah</th>
            <th></th>
          </tr>
        </thead>
        <tbody>
          {sessions.length === 0 ? (
            <tr>
              <td colSpan={4}>Belum ada sesi opname.</td>
            </tr>
          ) : (
            sessions.map((session) => (
              <Fragment key={session.id}>
                <tr>
                  <td>{session.date}</td>
                  <td>{session.notes || "-"}</td>
                  <td>{session.lines.length}</td>
                  <td>
                    <button
                      type="button"
                      onClick={() => setExpandedId(expandedId === session.id ? null : session.id)}
                    >
                      {expandedId === session.id ? "Tutup" : "Lihat"}
                    </button>
                  </td>
                </tr>
                {expandedId === session.id && (
                  <tr>
                    <td colSpan={4}>
                      {session.lines.length === 0 ? (
                        <p>Tidak ada selisih pada sesi ini.</p>
                      ) : (
                        <table className="purchasing-table">
                          <thead>
                            <tr>
                              <th>Bahan</th>
                              <th>Sebelum</th>
                              <th>Sesudah</th>
                              <th>Selisih</th>
                            </tr>
                          </thead>
                          <tbody>
                            {session.lines.map((line) => (
                              <tr key={line.ingredientId}>
                                <td>
                                  {line.ingredientName} ({line.unit})
                                </td>
                                <td>{line.quantityBefore}</td>
                                <td>{line.quantityAfter}</td>
                                <td className={line.changeQuantity < 0 ? "reports-row--warn" : undefined}>
                                  {line.changeQuantity > 0 ? "+" : ""}
                                  {line.changeQuantity}
                                </td>
                              </tr>
                            ))}
                          </tbody>
                        </table>
                      )}
                    </td>
                  </tr>
                )}
              </Fragment>
            ))
          )}
        </tbody>
      </table>
    </main>
  );
}
