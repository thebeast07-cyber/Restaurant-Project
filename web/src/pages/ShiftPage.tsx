import { useEffect, useState, type FormEvent } from "react";
import { useNavigate } from "react-router-dom";
import { closeShift, getCurrentShift, openShift, type Shift, type ShiftCloseResult } from "../api/shifts";
import { ApiError } from "../api/client";
import "./ShiftPage.css";

export function ShiftPage() {
  const navigate = useNavigate();

  const [shift, setShift] = useState<Shift | null | undefined>(undefined);
  const [openingCash, setOpeningCash] = useState("");
  const [closingCash, setClosingCash] = useState("");
  const [closeResult, setCloseResult] = useState<ShiftCloseResult | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);

  useEffect(() => {
    getCurrentShift()
      .then(setShift)
      .catch(() => setShift(null));
  }, []);

  async function handleOpen(event: FormEvent) {
    event.preventDefault();
    setError(null);
    setIsSubmitting(true);
    try {
      const opened = await openShift(Number(openingCash));
      setShift(opened);
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Tidak bisa terhubung ke server.");
    } finally {
      setIsSubmitting(false);
    }
  }

  async function handleClose(event: FormEvent) {
    event.preventDefault();
    if (!shift) {
      return;
    }
    setError(null);
    setIsSubmitting(true);
    try {
      const result = await closeShift(shift.id, Number(closingCash));
      setCloseResult(result);
      setShift(null);
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Tidak bisa terhubung ke server.");
    } finally {
      setIsSubmitting(false);
    }
  }

  if (shift === undefined) {
    return (
      <main className="shift-page">
        <p>Memuat...</p>
      </main>
    );
  }

  if (closeResult) {
    return (
      <main className="shift-page">
        <div className="shift-card">
          <h1>Shift Ditutup</h1>
          <dl className="shift-summary">
            <dt>Kas Awal</dt>
            <dd>{formatRupiah(closeResult.openingCash)}</dd>
            <dt>Penjualan Tunai</dt>
            <dd>{formatRupiah(closeResult.cashSalesTotal)}</dd>
            <dt>Penjualan Non-Tunai</dt>
            <dd>{formatRupiah(closeResult.nonCashSalesTotal)}</dd>
            <dt>Kas Seharusnya</dt>
            <dd>{formatRupiah(closeResult.expectedCash)}</dd>
            <dt>Kas Dihitung</dt>
            <dd>{formatRupiah(closeResult.closingCash)}</dd>
            <dt>Selisih</dt>
            <dd className={closeResult.cashVariance === 0 ? "" : "shift-variance"}>
              {formatRupiah(closeResult.cashVariance)}
            </dd>
          </dl>
          <button type="button" onClick={() => navigate("/dashboard")}>
            Kembali ke Dashboard
          </button>
        </div>
      </main>
    );
  }

  if (shift) {
    return (
      <main className="shift-page">
        <form className="shift-card" onSubmit={handleClose}>
          <h1>Tutup Shift</h1>
          <p className="shift-meta">
            Dibuka {new Date(shift.openedAt).toLocaleString("id-ID")} · Kas awal {formatRupiah(shift.openingCash)}
          </p>

          <label htmlFor="closingCash">Kas Akhir (hasil hitung fisik)</label>
          <input
            id="closingCash"
            type="number"
            min="0"
            step="1"
            value={closingCash}
            onChange={(event) => setClosingCash(event.target.value)}
            required
          />

          {error && (
            <p className="shift-error" role="alert">
              {error}
            </p>
          )}

          <div className="shift-actions">
            <button type="button" className="shift-secondary" onClick={() => navigate("/tables")}>
              Batal
            </button>
            <button type="submit" disabled={isSubmitting}>
              {isSubmitting ? "Memproses..." : "Tutup Shift"}
            </button>
          </div>
        </form>
      </main>
    );
  }

  return (
    <main className="shift-page">
      <form className="shift-card" onSubmit={handleOpen}>
        <h1>Buka Shift</h1>
        <p className="shift-meta">Masukkan kas awal untuk memulai shift.</p>

        <label htmlFor="openingCash">Kas Awal</label>
        <input
          id="openingCash"
          type="number"
          min="0"
          step="1"
          value={openingCash}
          onChange={(event) => setOpeningCash(event.target.value)}
          required
          autoFocus
        />

        {error && (
          <p className="shift-error" role="alert">
            {error}
          </p>
        )}

        <button type="submit" disabled={isSubmitting}>
          {isSubmitting ? "Memproses..." : "Buka Shift"}
        </button>
      </form>
    </main>
  );
}

function formatRupiah(amount: number): string {
  return new Intl.NumberFormat("id-ID", { style: "currency", currency: "IDR", maximumFractionDigits: 0 }).format(
    amount,
  );
}
