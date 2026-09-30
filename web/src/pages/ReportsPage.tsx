import { useEffect, useState } from "react";
import { useNavigate } from "react-router-dom";
import {
  getSalesDailyReport,
  getStockLevelReport,
  getWasteReport,
  type SalesDailyReport,
  type StockLevelReport,
  type WasteReport,
} from "../api/reports";
import { useAuth } from "../context/AuthContext";
import { ApiError } from "../api/client";
import "./PurchasingShared.css";
import "./ReportsPage.css";

function todayIso(): string {
  return new Date().toISOString().slice(0, 10);
}

export function ReportsPage() {
  const navigate = useNavigate();
  const { user } = useAuth();
  const isManager = user?.role === "Manager";
  const isOwner = user?.role === "Owner";
  const now = new Date();

  const [error, setError] = useState<string | null>(null);

  const [salesDate, setSalesDate] = useState(todayIso());
  const [sales, setSales] = useState<SalesDailyReport | null>(null);

  const [stock, setStock] = useState<StockLevelReport | null>(null);

  const [wasteYear, setWasteYear] = useState(now.getFullYear());
  const [wasteMonth, setWasteMonth] = useState(now.getMonth() + 1);
  const [waste, setWaste] = useState<WasteReport | null>(null);

  const canView = isManager || isOwner;

  useEffect(() => {
    if (!canView) {
      return;
    }
    getSalesDailyReport(salesDate)
      .then(setSales)
      .catch((err) => setError(err instanceof ApiError ? err.message : "Tidak bisa terhubung ke server."));
  }, [canView, salesDate]);

  useEffect(() => {
    if (!canView) {
      return;
    }
    getStockLevelReport()
      .then(setStock)
      .catch((err) => setError(err instanceof ApiError ? err.message : "Tidak bisa terhubung ke server."));
  }, [canView]);

  useEffect(() => {
    if (!canView) {
      return;
    }
    getWasteReport(wasteYear, wasteMonth)
      .then(setWaste)
      .catch((err) => setError(err instanceof ApiError ? err.message : "Tidak bisa terhubung ke server."));
  }, [canView, wasteYear, wasteMonth]);

  if (!canView) {
    return (
      <main className="purchasing-page reports-page">
        <header className="purchasing-header">
          <button type="button" className="purchasing-back" onClick={() => navigate("/dashboard")}>
            ← Dashboard
          </button>
          <h1>Laporan</h1>
        </header>
        <p className="purchasing-error" role="alert">
          Halaman ini khusus untuk Manager dan Owner.
        </p>
      </main>
    );
  }

  return (
    <main className="purchasing-page reports-page">
      <header className="purchasing-header">
        <button type="button" className="purchasing-back" onClick={() => navigate("/dashboard")}>
          ← Dashboard
        </button>
        <h1>Laporan</h1>
      </header>

      {error && (
        <p className="purchasing-error" role="alert">
          {error}
        </p>
      )}

      <section className="reports-card">
        <div className="reports-card-header">
          <h2>Penjualan Harian</h2>
          <input type="date" value={salesDate} onChange={(event) => setSalesDate(event.target.value)} />
        </div>
        {sales && (
          <div className="reports-stat-grid">
            <div className="reports-stat">
              <span className="reports-stat-label">Order Selesai</span>
              <span className="reports-stat-value">{sales.completedOrderCount}</span>
            </div>
            <div className="reports-stat">
              <span className="reports-stat-label">Order Void</span>
              <span className="reports-stat-value">{sales.voidedOrderCount}</span>
            </div>
            <div className="reports-stat">
              <span className="reports-stat-label">Tunai</span>
              <span className="reports-stat-value">{formatRupiah(sales.cashTotal)}</span>
            </div>
            <div className="reports-stat">
              <span className="reports-stat-label">Non-Tunai</span>
              <span className="reports-stat-value">{formatRupiah(sales.nonCashTotal)}</span>
            </div>
            <div className="reports-stat reports-stat--total">
              <span className="reports-stat-label">Total Pendapatan</span>
              <span className="reports-stat-value">{formatRupiah(sales.totalRevenue)}</span>
            </div>
          </div>
        )}
      </section>

      <section className="reports-card">
        <div className="reports-card-header">
          <h2>Level Stok</h2>
          {stock && stock.belowMinimumCount > 0 && (
            <span className="purchasing-badge purchasing-badge--warn">{stock.belowMinimumCount} di bawah minimum</span>
          )}
        </div>
        {stock && (
          <table className="purchasing-table">
            <thead>
              <tr>
                <th>Nama</th>
                <th>Stok Saat Ini</th>
                <th>Stok Minimum</th>
              </tr>
            </thead>
            <tbody>
              {stock.items.map((item) => (
                <tr key={item.ingredientId} className={item.isBelowMinimum ? "reports-row--warn" : undefined}>
                  <td>{item.name}</td>
                  <td>
                    {item.currentStock} {item.unit}
                    {item.isBelowMinimum && <span className="purchasing-badge purchasing-badge--warn"> Rendah</span>}
                  </td>
                  <td>
                    {item.minimumStock} {item.unit}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </section>

      <section className="reports-card">
        <div className="reports-card-header">
          <h2>Waste Bulanan</h2>
          <div className="reports-month-picker">
            <select value={wasteMonth} onChange={(event) => setWasteMonth(Number(event.target.value))}>
              {MONTH_NAMES.map((name, index) => (
                <option key={name} value={index + 1}>
                  {name}
                </option>
              ))}
            </select>
            <input
              type="number"
              value={wasteYear}
              onChange={(event) => setWasteYear(Number(event.target.value))}
              className="reports-year-input"
            />
          </div>
        </div>
        {waste && (
          <>
            <table className="purchasing-table">
              <thead>
                <tr>
                  <th>Nama</th>
                  <th>Qty Terbuang</th>
                  <th>Nilai Kerugian</th>
                </tr>
              </thead>
              <tbody>
                {waste.items.length === 0 ? (
                  <tr>
                    <td colSpan={3}>Tidak ada waste bulan ini.</td>
                  </tr>
                ) : (
                  waste.items.map((item) => (
                    <tr key={item.ingredientId}>
                      <td>{item.name}</td>
                      <td>
                        {item.quantityWasted} {item.unit}
                      </td>
                      <td>{formatRupiah(item.wasteValue)}</td>
                    </tr>
                  ))
                )}
              </tbody>
            </table>
            <p className="reports-waste-total">Total nilai waste: {formatRupiah(waste.totalWasteValue)}</p>
          </>
        )}
      </section>
    </main>
  );
}

const MONTH_NAMES = [
  "Januari",
  "Februari",
  "Maret",
  "April",
  "Mei",
  "Juni",
  "Juli",
  "Agustus",
  "September",
  "Oktober",
  "November",
  "Desember",
];

function formatRupiah(amount: number): string {
  return new Intl.NumberFormat("id-ID", { style: "currency", currency: "IDR", maximumFractionDigits: 0 }).format(
    amount,
  );
}
