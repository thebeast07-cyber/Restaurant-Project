import { useEffect, useState } from "react";
import { useNavigate } from "react-router-dom";
import {
  getSalesDailyReport,
  getSalesRangeReport,
  getStockLevelReport,
  getStockTrendReport,
  getWasteRangeReport,
  getWasteReport,
  type SalesDailyReport,
  type SalesRangeReport,
  type StockLevelReport,
  type StockTrendReport,
  type WasteRangeReport,
  type WasteReport,
} from "../api/reports";
import { ApiError } from "../api/client";
import { useAuth } from "../context/AuthContext";
import { ReportChart } from "../components/ReportChart";
import { exportToCsv, exportToExcel, exportToPdf } from "../lib/export";
import "./PurchasingShared.css";
import "./ReportsPage.css";

const SERIES_COLOR = "#7f77dd";
const SERIES_COLOR_PREV = "#b4b2a9";

function toIsoDate(date: Date): string {
  return date.toISOString().slice(0, 10);
}

function parseIsoDate(iso: string): Date {
  return new Date(`${iso}T00:00:00Z`);
}

function addDaysIso(iso: string, days: number): string {
  const date = parseIsoDate(iso);
  date.setUTCDate(date.getUTCDate() + days);
  return toIsoDate(date);
}

function daysInclusive(fromIso: string, toIso: string): number {
  return Math.round((parseIsoDate(toIso).getTime() - parseIsoDate(fromIso).getTime()) / 86_400_000) + 1;
}

function previousRange(fromIso: string, toIso: string): { from: string; to: string } {
  const length = daysInclusive(fromIso, toIso);
  const prevTo = addDaysIso(fromIso, -1);
  const prevFrom = addDaysIso(prevTo, -(length - 1));
  return { from: prevFrom, to: prevTo };
}

function todayIso(): string {
  return toIsoDate(new Date());
}

function formatRupiah(amount: number): string {
  return new Intl.NumberFormat("id-ID", { style: "currency", currency: "IDR", maximumFractionDigits: 0 }).format(
    amount,
  );
}

function formatDelta(current: number, previous: number): string {
  if (previous === 0) {
    return current === 0 ? "0%" : "+∞%";
  }
  const pct = ((current - previous) / Math.abs(previous)) * 100;
  const sign = pct >= 0 ? "+" : "";
  return `${sign}${pct.toFixed(1)}%`;
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

export function ReportsPage() {
  const navigate = useNavigate();
  const { user } = useAuth();
  const isManager = user?.role === "Manager";
  const isOwner = user?.role === "Owner";
  const canView = isManager || isOwner;

  const now = new Date();
  const [error, setError] = useState<string | null>(null);

  // Penjualan Harian (Phase 4, single day)
  const [salesDate, setSalesDate] = useState(todayIso());
  const [sales, setSales] = useState<SalesDailyReport | null>(null);

  // Level Stok (Phase 4, snapshot)
  const [stock, setStock] = useState<StockLevelReport | null>(null);

  // Waste Bulanan (Phase 4, single month)
  const [wasteYear, setWasteYear] = useState(now.getFullYear());
  const [wasteMonth, setWasteMonth] = useState(now.getMonth() + 1);
  const [waste, setWaste] = useState<WasteReport | null>(null);

  // Tren Penjualan (Phase 7)
  const [salesTrendFrom, setSalesTrendFrom] = useState(addDaysIso(todayIso(), -6));
  const [salesTrendTo, setSalesTrendTo] = useState(todayIso());
  const [salesTrendCompare, setSalesTrendCompare] = useState(false);
  const [salesTrend, setSalesTrend] = useState<SalesRangeReport | null>(null);
  const [salesTrendPrev, setSalesTrendPrev] = useState<SalesRangeReport | null>(null);

  // Tren Level Stok (Phase 7)
  const [stockTrendFrom, setStockTrendFrom] = useState(addDaysIso(todayIso(), -6));
  const [stockTrendTo, setStockTrendTo] = useState(todayIso());
  const [stockTrendCompare, setStockTrendCompare] = useState(false);
  const [stockTrend, setStockTrend] = useState<StockTrendReport | null>(null);
  const [stockTrendPrev, setStockTrendPrev] = useState<StockTrendReport | null>(null);

  // Tren Waste (Phase 7)
  const [wasteTrendFrom, setWasteTrendFrom] = useState(addDaysIso(todayIso(), -89));
  const [wasteTrendTo, setWasteTrendTo] = useState(todayIso());
  const [wasteTrendCompare, setWasteTrendCompare] = useState(false);
  const [wasteTrend, setWasteTrend] = useState<WasteRangeReport | null>(null);
  const [wasteTrendPrev, setWasteTrendPrev] = useState<WasteRangeReport | null>(null);

  function handleError(err: unknown) {
    setError(err instanceof ApiError ? err.message : "Tidak bisa terhubung ke server.");
  }

  useEffect(() => {
    if (!canView) return;
    getSalesDailyReport(salesDate).then(setSales).catch(handleError);
  }, [canView, salesDate]);

  useEffect(() => {
    if (!canView) return;
    getStockLevelReport().then(setStock).catch(handleError);
  }, [canView]);

  useEffect(() => {
    if (!canView) return;
    getWasteReport(wasteYear, wasteMonth).then(setWaste).catch(handleError);
  }, [canView, wasteYear, wasteMonth]);

  useEffect(() => {
    if (!canView) return;
    getSalesRangeReport(salesTrendFrom, salesTrendTo).then(setSalesTrend).catch(handleError);
    if (salesTrendCompare) {
      const prev = previousRange(salesTrendFrom, salesTrendTo);
      getSalesRangeReport(prev.from, prev.to).then(setSalesTrendPrev).catch(handleError);
    } else {
      setSalesTrendPrev(null);
    }
  }, [canView, salesTrendFrom, salesTrendTo, salesTrendCompare]);

  useEffect(() => {
    if (!canView) return;
    getStockTrendReport(stockTrendFrom, stockTrendTo).then(setStockTrend).catch(handleError);
    if (stockTrendCompare) {
      const prev = previousRange(stockTrendFrom, stockTrendTo);
      getStockTrendReport(prev.from, prev.to).then(setStockTrendPrev).catch(handleError);
    } else {
      setStockTrendPrev(null);
    }
  }, [canView, stockTrendFrom, stockTrendTo, stockTrendCompare]);

  useEffect(() => {
    if (!canView) return;
    getWasteRangeReport(wasteTrendFrom, wasteTrendTo).then(setWasteTrend).catch(handleError);
    if (wasteTrendCompare) {
      const prev = previousRange(wasteTrendFrom, wasteTrendTo);
      getWasteRangeReport(prev.from, prev.to).then(setWasteTrendPrev).catch(handleError);
    } else {
      setWasteTrendPrev(null);
    }
  }, [canView, wasteTrendFrom, wasteTrendTo, wasteTrendCompare]);

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

  const salesTrendTotal = salesTrend?.days.reduce((sum, d) => sum + d.totalRevenue, 0) ?? 0;
  const salesTrendPrevTotal = salesTrendPrev?.days.reduce((sum, d) => sum + d.totalRevenue, 0) ?? 0;

  const wasteTrendTotal = wasteTrend?.months.reduce((sum, m) => sum + m.totalWasteValue, 0) ?? 0;
  const wasteTrendPrevTotal = wasteTrendPrev?.months.reduce((sum, m) => sum + m.totalWasteValue, 0) ?? 0;

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

      {/* ---------- Penjualan Harian ---------- */}
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

      {/* ---------- Tren Penjualan ---------- */}
      <section className="reports-card">
        <div className="reports-trend-header">
          <h2>Tren Penjualan</h2>
          <div className="reports-trend-controls">
            <input type="date" value={salesTrendFrom} onChange={(event) => setSalesTrendFrom(event.target.value)} />
            <span>s/d</span>
            <input type="date" value={salesTrendTo} onChange={(event) => setSalesTrendTo(event.target.value)} />
            <label className="reports-compare-toggle">
              <input
                type="checkbox"
                checked={salesTrendCompare}
                onChange={(event) => setSalesTrendCompare(event.target.checked)}
              />
              Bandingkan periode sebelumnya
            </label>
          </div>
        </div>

        {salesTrend && (
          <>
            {salesTrendCompare && salesTrendPrev && (
              <p className="reports-delta">
                Total periode ini: <strong>{formatRupiah(salesTrendTotal)}</strong> vs periode sebelumnya{" "}
                {formatRupiah(salesTrendPrevTotal)} ({formatDelta(salesTrendTotal, salesTrendPrevTotal)})
              </p>
            )}
            <ReportChart
              type="line"
              ariaLabel="Grafik tren pendapatan harian"
              labels={salesTrend.days.map((d) => d.date.slice(5))}
              datasets={[
                { label: "Periode ini", data: salesTrend.days.map((d) => d.totalRevenue), color: SERIES_COLOR },
                ...(salesTrendCompare && salesTrendPrev
                  ? [
                      {
                        label: "Periode sebelumnya",
                        data: salesTrendPrev.days.map((d) => d.totalRevenue),
                        color: SERIES_COLOR_PREV,
                      },
                    ]
                  : []),
              ]}
            />
            <div className="reports-export-buttons">
              <button
                type="button"
                onClick={() =>
                  exportToExcel(
                    "penjualan-harian",
                    "Penjualan",
                    salesTrend.days.map((d) => ({
                      Tanggal: d.date,
                      "Order Selesai": d.completedOrderCount,
                      "Order Void": d.voidedOrderCount,
                      Tunai: d.cashTotal,
                      "Non-Tunai": d.nonCashTotal,
                      Total: d.totalRevenue,
                    })),
                  )
                }
              >
                Export Excel
              </button>
              <button
                type="button"
                onClick={() =>
                  exportToCsv(
                    "penjualan-harian",
                    salesTrend.days.map((d) => ({
                      Tanggal: d.date,
                      "Order Selesai": d.completedOrderCount,
                      "Order Void": d.voidedOrderCount,
                      Tunai: d.cashTotal,
                      "Non-Tunai": d.nonCashTotal,
                      Total: d.totalRevenue,
                    })),
                  )
                }
              >
                Export CSV
              </button>
              <button
                type="button"
                onClick={() =>
                  exportToPdf(
                    "penjualan-harian",
                    "Tren Penjualan",
                    ["Tanggal", "Selesai", "Void", "Tunai", "Non-Tunai", "Total"],
                    salesTrend.days.map((d) => [
                      d.date,
                      d.completedOrderCount,
                      d.voidedOrderCount,
                      formatRupiah(d.cashTotal),
                      formatRupiah(d.nonCashTotal),
                      formatRupiah(d.totalRevenue),
                    ]),
                  )
                }
              >
                Export PDF
              </button>
            </div>
          </>
        )}
      </section>

      {/* ---------- Level Stok ---------- */}
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

      {/* ---------- Tren Level Stok ---------- */}
      <section className="reports-card">
        <div className="reports-trend-header">
          <h2>Tren Level Stok</h2>
          <div className="reports-trend-controls">
            <input type="date" value={stockTrendFrom} onChange={(event) => setStockTrendFrom(event.target.value)} />
            <span>s/d</span>
            <input type="date" value={stockTrendTo} onChange={(event) => setStockTrendTo(event.target.value)} />
            <label className="reports-compare-toggle">
              <input
                type="checkbox"
                checked={stockTrendCompare}
                onChange={(event) => setStockTrendCompare(event.target.checked)}
              />
              Bandingkan periode sebelumnya
            </label>
          </div>
        </div>

        {stockTrend && (
          <>
            <p className="reports-chart-caption">Frekuensi di bawah stok minimum (hari)</p>
            <ReportChart
              type="bar"
              ariaLabel="Grafik jumlah hari tiap bahan berada di bawah stok minimum"
              labels={stockTrend.items.map((i) => i.name)}
              datasets={[
                { label: "Periode ini", data: stockTrend.items.map((i) => i.belowMinimumDays), color: SERIES_COLOR },
                ...(stockTrendCompare && stockTrendPrev
                  ? [
                      {
                        label: "Periode sebelumnya",
                        data: stockTrendPrev.items.map((i) => i.belowMinimumDays),
                        color: SERIES_COLOR_PREV,
                      },
                    ]
                  : []),
              ]}
            />

            <p className="reports-chart-caption">Kecepatan perubahan stok (net per hari, negatif = menipis)</p>
            <ReportChart
              type="bar"
              ariaLabel="Grafik kecepatan perubahan stok tiap bahan per hari"
              labels={stockTrend.items.map((i) => i.name)}
              datasets={[
                {
                  label: "Periode ini",
                  data: stockTrend.items.map((i) => Number(i.netChangePerDay.toFixed(1))),
                  color: SERIES_COLOR,
                },
                ...(stockTrendCompare && stockTrendPrev
                  ? [
                      {
                        label: "Periode sebelumnya",
                        data: stockTrendPrev.items.map((i) => Number(i.netChangePerDay.toFixed(1))),
                        color: SERIES_COLOR_PREV,
                      },
                    ]
                  : []),
              ]}
            />

            <div className="reports-export-buttons">
              <button
                type="button"
                onClick={() =>
                  exportToExcel(
                    "tren-stok",
                    "Stok",
                    stockTrend.items.map((i) => ({
                      Bahan: i.name,
                      Satuan: i.unit,
                      "Hari Di Bawah Minimum": i.belowMinimumDays,
                      "Perubahan/Hari": i.netChangePerDay.toFixed(2),
                    })),
                  )
                }
              >
                Export Excel
              </button>
              <button
                type="button"
                onClick={() =>
                  exportToCsv(
                    "tren-stok",
                    stockTrend.items.map((i) => ({
                      Bahan: i.name,
                      Satuan: i.unit,
                      "Hari Di Bawah Minimum": i.belowMinimumDays,
                      "Perubahan/Hari": i.netChangePerDay.toFixed(2),
                    })),
                  )
                }
              >
                Export CSV
              </button>
              <button
                type="button"
                onClick={() =>
                  exportToPdf(
                    "tren-stok",
                    "Tren Level Stok",
                    ["Bahan", "Satuan", "Hari Di Bawah Minimum", "Perubahan/Hari"],
                    stockTrend.items.map((i) => [i.name, i.unit, i.belowMinimumDays, i.netChangePerDay.toFixed(2)]),
                  )
                }
              >
                Export PDF
              </button>
            </div>
          </>
        )}
      </section>

      {/* ---------- Waste Bulanan ---------- */}
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

      {/* ---------- Tren Waste ---------- */}
      <section className="reports-card">
        <div className="reports-trend-header">
          <h2>Tren Waste</h2>
          <div className="reports-trend-controls">
            <input type="date" value={wasteTrendFrom} onChange={(event) => setWasteTrendFrom(event.target.value)} />
            <span>s/d</span>
            <input type="date" value={wasteTrendTo} onChange={(event) => setWasteTrendTo(event.target.value)} />
            <label className="reports-compare-toggle">
              <input
                type="checkbox"
                checked={wasteTrendCompare}
                onChange={(event) => setWasteTrendCompare(event.target.checked)}
              />
              Bandingkan periode sebelumnya
            </label>
          </div>
        </div>

        {wasteTrend && (
          <>
            {wasteTrendCompare && wasteTrendPrev && (
              <p className="reports-delta">
                Total periode ini: <strong>{formatRupiah(wasteTrendTotal)}</strong> vs periode sebelumnya{" "}
                {formatRupiah(wasteTrendPrevTotal)} ({formatDelta(wasteTrendTotal, wasteTrendPrevTotal)})
              </p>
            )}
            <ReportChart
              type="bar"
              ariaLabel="Grafik tren nilai waste per bulan"
              labels={wasteTrend.months.map((m) => `${MONTH_NAMES[m.month - 1].slice(0, 3)} ${m.year}`)}
              datasets={[
                { label: "Periode ini", data: wasteTrend.months.map((m) => m.totalWasteValue), color: SERIES_COLOR },
                ...(wasteTrendCompare && wasteTrendPrev
                  ? [
                      {
                        label: "Periode sebelumnya",
                        data: wasteTrendPrev.months.map((m) => m.totalWasteValue),
                        color: SERIES_COLOR_PREV,
                      },
                    ]
                  : []),
              ]}
            />
            <div className="reports-export-buttons">
              <button
                type="button"
                onClick={() =>
                  exportToExcel(
                    "tren-waste",
                    "Waste",
                    wasteTrend.months.map((m) => ({
                      Bulan: `${MONTH_NAMES[m.month - 1]} ${m.year}`,
                      "Nilai Waste": m.totalWasteValue,
                    })),
                  )
                }
              >
                Export Excel
              </button>
              <button
                type="button"
                onClick={() =>
                  exportToCsv(
                    "tren-waste",
                    wasteTrend.months.map((m) => ({
                      Bulan: `${MONTH_NAMES[m.month - 1]} ${m.year}`,
                      "Nilai Waste": m.totalWasteValue,
                    })),
                  )
                }
              >
                Export CSV
              </button>
              <button
                type="button"
                onClick={() =>
                  exportToPdf(
                    "tren-waste",
                    "Tren Waste",
                    ["Bulan", "Nilai Waste"],
                    wasteTrend.months.map((m) => [`${MONTH_NAMES[m.month - 1]} ${m.year}`, formatRupiah(m.totalWasteValue)]),
                  )
                }
              >
                Export PDF
              </button>
            </div>
          </>
        )}
      </section>
    </main>
  );
}
