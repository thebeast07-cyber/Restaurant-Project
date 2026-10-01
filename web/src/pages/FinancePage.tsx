import { useEffect, useState, type FormEvent } from "react";
import { useNavigate } from "react-router-dom";
import {
  createExpense,
  listExpenses,
  recordExpensePayment,
  type ExpenseCategory,
  type OperatingExpense,
} from "../api/expenses";
import {
  getApAging,
  getBalanceSheet,
  getCashFlow,
  getProductMargin,
  getProfitLoss,
  type ApAgingReport,
  type BalanceSheetReport,
  type CashFlowReport,
  type ProductMarginReport,
  type ProfitLossReport,
} from "../api/finance";
import { useAuth } from "../context/AuthContext";
import { ApiError } from "../api/client";
import "./PurchasingShared.css";
import "./ReportsPage.css";

const EXPENSE_CATEGORIES: ExpenseCategory[] = ["Sewa", "Gaji", "Utilitas", "Marketing", "Lainnya"];

function todayIso(): string {
  return new Date().toISOString().slice(0, 10);
}

function firstOfMonthIso(): string {
  const now = new Date();
  return `${now.getFullYear()}-${String(now.getMonth() + 1).padStart(2, "0")}-01`;
}

function formatRupiah(amount: number): string {
  return new Intl.NumberFormat("id-ID", { style: "currency", currency: "IDR", maximumFractionDigits: 0 }).format(
    amount,
  );
}

function translatePaymentStatus(status: OperatingExpense["paymentStatus"]): string {
  switch (status) {
    case "Unpaid":
      return "Belum Bayar";
    case "PartiallyPaid":
      return "Sebagian";
    case "Paid":
      return "Lunas";
  }
}

export function FinancePage() {
  const navigate = useNavigate();
  const { user } = useAuth();
  const canView = !!user && (user.role === "Owner" || user.role === "Manager");

  const [error, setError] = useState<string | null>(null);

  // Biaya Operasional
  const [expenses, setExpenses] = useState<OperatingExpense[]>([]);
  const [category, setCategory] = useState<ExpenseCategory>("Lainnya");
  const [description, setDescription] = useState("");
  const [amount, setAmount] = useState("");
  const [incurredAt, setIncurredAt] = useState(todayIso());
  const [isSubmitting, setIsSubmitting] = useState(false);

  const [payingId, setPayingId] = useState<string | null>(null);
  const [paymentAmount, setPaymentAmount] = useState("");
  const [paymentError, setPaymentError] = useState<string | null>(null);
  const [isPaying, setIsPaying] = useState(false);

  // P&L
  const [plFrom, setPlFrom] = useState(firstOfMonthIso());
  const [plTo, setPlTo] = useState(todayIso());
  const [profitLoss, setProfitLoss] = useState<ProfitLossReport | null>(null);

  // Margin per Produk
  const [marginFrom, setMarginFrom] = useState(firstOfMonthIso());
  const [marginTo, setMarginTo] = useState(todayIso());
  const [productMargin, setProductMargin] = useState<ProductMarginReport | null>(null);

  // Arus Kas
  const [cfFrom, setCfFrom] = useState(firstOfMonthIso());
  const [cfTo, setCfTo] = useState(todayIso());
  const [cashFlow, setCashFlow] = useState<CashFlowReport | null>(null);

  // Hutang Jatuh Tempo (AP Aging)
  const [apAsOf, setApAsOf] = useState(todayIso());
  const [apAging, setApAging] = useState<ApAgingReport | null>(null);

  // Neraca
  const [bsAsOf, setBsAsOf] = useState(todayIso());
  const [balanceSheet, setBalanceSheet] = useState<BalanceSheetReport | null>(null);

  function handleError(err: unknown) {
    setError(err instanceof ApiError ? err.message : "Tidak bisa terhubung ke server.");
  }

  function refreshExpenses() {
    listExpenses().then(setExpenses).catch(handleError);
  }

  useEffect(() => {
    if (!canView) return;
    refreshExpenses();
  }, [canView]);

  useEffect(() => {
    if (!canView) return;
    getProfitLoss(plFrom, plTo).then(setProfitLoss).catch(handleError);
  }, [canView, plFrom, plTo]);

  useEffect(() => {
    if (!canView) return;
    getProductMargin(marginFrom, marginTo).then(setProductMargin).catch(handleError);
  }, [canView, marginFrom, marginTo]);

  useEffect(() => {
    if (!canView) return;
    getCashFlow(cfFrom, cfTo).then(setCashFlow).catch(handleError);
  }, [canView, cfFrom, cfTo]);

  useEffect(() => {
    if (!canView) return;
    getApAging(apAsOf).then(setApAging).catch(handleError);
  }, [canView, apAsOf]);

  useEffect(() => {
    if (!canView) return;
    getBalanceSheet(bsAsOf).then(setBalanceSheet).catch(handleError);
  }, [canView, bsAsOf]);

  async function handleExpenseSubmit(event: FormEvent) {
    event.preventDefault();
    setError(null);
    setIsSubmitting(true);
    try {
      await createExpense(category, description, Number(amount), incurredAt);
      setDescription("");
      setAmount("");
      setIncurredAt(todayIso());
      refreshExpenses();
    } catch (err) {
      handleError(err);
    } finally {
      setIsSubmitting(false);
    }
  }

  function openPayment(expenseId: string) {
    setPayingId(expenseId);
    setPaymentAmount("");
    setPaymentError(null);
  }

  async function handlePaymentSubmit(event: FormEvent) {
    event.preventDefault();
    if (!payingId) return;
    setPaymentError(null);
    setIsPaying(true);
    try {
      await recordExpensePayment(payingId, Number(paymentAmount));
      setPayingId(null);
      refreshExpenses();
    } catch (err) {
      setPaymentError(err instanceof ApiError ? err.message : "Tidak bisa terhubung ke server.");
    } finally {
      setIsPaying(false);
    }
  }

  if (!canView) {
    return (
      <main className="purchasing-page reports-page">
        <header className="purchasing-header">
          <button type="button" className="purchasing-back" onClick={() => navigate("/dashboard")}>
            ← Dashboard
          </button>
          <h1>Finance</h1>
        </header>
        <p className="purchasing-error" role="alert">
          Halaman ini khusus untuk Manager dan Owner.
        </p>
      </main>
    );
  }

  const payingExpense = expenses.find((e) => e.id === payingId);
  const sortedMargin = productMargin
    ? [...productMargin.items].sort((a, b) => b.grossProfit - a.grossProfit)
    : [];

  return (
    <main className="purchasing-page reports-page">
      <header className="purchasing-header">
        <button type="button" className="purchasing-back" onClick={() => navigate("/dashboard")}>
          ← Dashboard
        </button>
        <h1>Finance</h1>
      </header>

      {error && (
        <p className="purchasing-error" role="alert">
          {error}
        </p>
      )}

      {/* ---------- Biaya Operasional ---------- */}
      <form className="purchasing-form" onSubmit={handleExpenseSubmit} style={{ maxWidth: 480 }}>
        <h2>Catat Biaya Operasional</h2>

        <label htmlFor="expCategory">Kategori</label>
        <select id="expCategory" value={category} onChange={(event) => setCategory(event.target.value as ExpenseCategory)}>
          {EXPENSE_CATEGORIES.map((c) => (
            <option key={c} value={c}>
              {c}
            </option>
          ))}
        </select>

        <label htmlFor="expDescription">Deskripsi</label>
        <input
          id="expDescription"
          value={description}
          onChange={(event) => setDescription(event.target.value)}
          required
        />

        <label htmlFor="expAmount">Nominal</label>
        <input
          id="expAmount"
          type="number"
          min="0"
          step="any"
          value={amount}
          onChange={(event) => setAmount(event.target.value)}
          required
        />

        <label htmlFor="expIncurredAt">Tanggal Terjadi</label>
        <input
          id="expIncurredAt"
          type="date"
          value={incurredAt}
          onChange={(event) => setIncurredAt(event.target.value)}
          required
        />

        <button type="submit" disabled={isSubmitting}>
          {isSubmitting ? "Menyimpan..." : "Simpan"}
        </button>
      </form>

      <table className="purchasing-table">
        <thead>
          <tr>
            <th>Tanggal</th>
            <th>Kategori</th>
            <th>Deskripsi</th>
            <th>Nominal</th>
            <th>Sisa</th>
            <th>Status</th>
            <th></th>
          </tr>
        </thead>
        <tbody>
          {expenses.map((expense) => (
            <tr key={expense.id}>
              <td>{expense.incurredAt}</td>
              <td>{expense.category}</td>
              <td>{expense.description}</td>
              <td>{formatRupiah(expense.amount)}</td>
              <td>{formatRupiah(expense.remainingBalance)}</td>
              <td>
                <span
                  className={`purchasing-badge ${expense.paymentStatus === "Paid" ? "purchasing-badge--positive" : ""}`}
                >
                  {translatePaymentStatus(expense.paymentStatus)}
                </span>
              </td>
              <td>
                {expense.paymentStatus !== "Paid" && (
                  <div className="purchasing-row-actions">
                    <button type="button" onClick={() => openPayment(expense.id)}>
                      Bayar
                    </button>
                  </div>
                )}
              </td>
            </tr>
          ))}
        </tbody>
      </table>

      {/* ---------- Laba Rugi (P&L) ---------- */}
      <section className="reports-card">
        <div className="reports-trend-header">
          <h2>Laba Rugi</h2>
          <div className="reports-trend-controls">
            <input type="date" value={plFrom} onChange={(event) => setPlFrom(event.target.value)} />
            <span>s/d</span>
            <input type="date" value={plTo} onChange={(event) => setPlTo(event.target.value)} />
          </div>
        </div>

        {profitLoss && (
          <div className="reports-stat-grid">
            <div className="reports-stat">
              <span className="reports-stat-label">Pendapatan</span>
              <span className="reports-stat-value">{formatRupiah(profitLoss.revenue)}</span>
            </div>
            <div className="reports-stat">
              <span className="reports-stat-label">HPP (COGS)</span>
              <span className="reports-stat-value">{formatRupiah(profitLoss.cogs)}</span>
            </div>
            <div className="reports-stat reports-stat--total">
              <span className="reports-stat-label">Laba Kotor ({profitLoss.grossMarginPct.toFixed(1)}%)</span>
              <span className="reports-stat-value">{formatRupiah(profitLoss.grossProfit)}</span>
            </div>

            {profitLoss.operatingExpenses.map((line) => (
              <div className="reports-stat" key={line.category}>
                <span className="reports-stat-label">Biaya {line.category}</span>
                <span className="reports-stat-value">{formatRupiah(line.amount)}</span>
              </div>
            ))}
            <div className="reports-stat">
              <span className="reports-stat-label">Total Biaya Operasional</span>
              <span className="reports-stat-value">{formatRupiah(profitLoss.totalOperatingExpenses)}</span>
            </div>

            <div className="reports-stat reports-stat--total">
              <span className="reports-stat-label">Laba Bersih ({profitLoss.netMarginPct.toFixed(1)}%)</span>
              <span className="reports-stat-value">{formatRupiah(profitLoss.netProfit)}</span>
            </div>
          </div>
        )}
      </section>

      {/* ---------- Margin per Produk ---------- */}
      <section className="reports-card">
        <div className="reports-trend-header">
          <h2>Margin per Produk</h2>
          <div className="reports-trend-controls">
            <input type="date" value={marginFrom} onChange={(event) => setMarginFrom(event.target.value)} />
            <span>s/d</span>
            <input type="date" value={marginTo} onChange={(event) => setMarginTo(event.target.value)} />
          </div>
        </div>

        {productMargin && (
          <table className="purchasing-table">
            <thead>
              <tr>
                <th>Produk</th>
                <th>Qty Terjual</th>
                <th>Revenue</th>
                <th>HPP</th>
                <th>Laba Kotor</th>
                <th>Margin</th>
              </tr>
            </thead>
            <tbody>
              {sortedMargin.length === 0 ? (
                <tr>
                  <td colSpan={6}>Tidak ada penjualan di rentang ini.</td>
                </tr>
              ) : (
                sortedMargin.map((item) => (
                  <tr key={item.productId}>
                    <td>{item.productName}</td>
                    <td>{item.quantitySold}</td>
                    <td>{formatRupiah(item.revenue)}</td>
                    <td>
                      {formatRupiah(item.cogs)}
                      {item.cogsIsEstimated && (
                        <span className="purchasing-badge" title="Sebagian/semua dihitung pakai harga bahan saat ini, bukan snapshot saat transaksi">
                          {" "}
                          Estimasi
                        </span>
                      )}
                    </td>
                    <td className={item.grossProfit < 0 ? "reports-row--warn" : undefined}>
                      {formatRupiah(item.grossProfit)}
                    </td>
                    <td>{item.grossMarginPct.toFixed(1)}%</td>
                  </tr>
                ))
              )}
            </tbody>
          </table>
        )}
      </section>

      {/* ---------- Arus Kas (Cash Flow) ---------- */}
      <section className="reports-card">
        <div className="reports-trend-header">
          <h2>Arus Kas</h2>
          <div className="reports-trend-controls">
            <input type="date" value={cfFrom} onChange={(event) => setCfFrom(event.target.value)} />
            <span>s/d</span>
            <input type="date" value={cfTo} onChange={(event) => setCfTo(event.target.value)} />
          </div>
        </div>

        {cashFlow && (
          <>
            <div className="reports-stat-grid">
              <div className="reports-stat">
                <span className="reports-stat-label">Saldo Awal</span>
                <span className="reports-stat-value">{formatRupiah(cashFlow.openingCash)}</span>
              </div>
              <div className="reports-stat">
                <span className="reports-stat-label">Kas Masuk</span>
                <span className="reports-stat-value">{formatRupiah(cashFlow.cashIn)}</span>
              </div>
              <div className="reports-stat">
                <span className="reports-stat-label">Kas Keluar</span>
                <span className="reports-stat-value">{formatRupiah(cashFlow.cashOut)}</span>
              </div>
              <div className="reports-stat reports-stat--total">
                <span className="reports-stat-label">Saldo Akhir</span>
                <span className="reports-stat-value">{formatRupiah(cashFlow.closingCash)}</span>
              </div>
            </div>

            <table className="purchasing-table">
              <thead>
                <tr>
                  <th>Sumber</th>
                  <th>Kas Masuk</th>
                  <th>Kas Keluar</th>
                </tr>
              </thead>
              <tbody>
                {cashFlow.sources.length === 0 ? (
                  <tr>
                    <td colSpan={3}>Tidak ada transaksi kas di rentang ini.</td>
                  </tr>
                ) : (
                  cashFlow.sources.map((source) => (
                    <tr key={source.source}>
                      <td>{source.source}</td>
                      <td>{source.cashIn > 0 ? formatRupiah(source.cashIn) : "—"}</td>
                      <td>{source.cashOut > 0 ? formatRupiah(source.cashOut) : "—"}</td>
                    </tr>
                  ))
                )}
              </tbody>
            </table>
          </>
        )}
      </section>

      {/* ---------- Hutang Jatuh Tempo (AP Aging) ---------- */}
      <section className="reports-card">
        <div className="reports-trend-header">
          <h2>Hutang Jatuh Tempo</h2>
          <div className="reports-trend-controls">
            <span>per</span>
            <input type="date" value={apAsOf} onChange={(event) => setApAsOf(event.target.value)} />
          </div>
        </div>

        {apAging && (
          <>
            <div className="reports-stat-grid">
              <div className="reports-stat">
                <span className="reports-stat-label">0–30 hari</span>
                <span className="reports-stat-value">{formatRupiah(apAging.bucket0To30)}</span>
              </div>
              <div className="reports-stat">
                <span className="reports-stat-label">31–60 hari</span>
                <span className="reports-stat-value">{formatRupiah(apAging.bucket31To60)}</span>
              </div>
              <div className="reports-stat">
                <span className="reports-stat-label">61–90 hari</span>
                <span className="reports-stat-value">{formatRupiah(apAging.bucket61To90)}</span>
              </div>
              <div className="reports-stat reports-stat--total">
                <span className="reports-stat-label">&gt;90 hari</span>
                <span className="reports-stat-value">{formatRupiah(apAging.bucketOver90)}</span>
              </div>
            </div>

            <table className="purchasing-table">
              <thead>
                <tr>
                  <th>Jenis</th>
                  <th>Nama</th>
                  <th>Tanggal</th>
                  <th>Umur</th>
                  <th>Sisa Hutang</th>
                </tr>
              </thead>
              <tbody>
                {apAging.items.length === 0 ? (
                  <tr>
                    <td colSpan={5}>Tidak ada hutang terbuka.</td>
                  </tr>
                ) : (
                  apAging.items.map((item) => (
                    <tr key={item.referenceId}>
                      <td>{item.type}</td>
                      <td>{item.name}</td>
                      <td>{item.incurredDate}</td>
                      <td className={item.daysOutstanding > 90 ? "reports-row--warn" : undefined}>
                        {item.daysOutstanding} hari
                      </td>
                      <td>{formatRupiah(item.outstandingAmount)}</td>
                    </tr>
                  ))
                )}
              </tbody>
            </table>
          </>
        )}
      </section>

      {/* ---------- Neraca (Balance Sheet) ---------- */}
      <section className="reports-card">
        <div className="reports-trend-header">
          <h2>Neraca</h2>
          <div className="reports-trend-controls">
            <span>per</span>
            <input type="date" value={bsAsOf} onChange={(event) => setBsAsOf(event.target.value)} />
          </div>
        </div>

        {balanceSheet && (
          <div className="reports-stat-grid">
            <div className="reports-stat">
              <span className="reports-stat-label">Aset</span>
              <span className="reports-stat-value"></span>
            </div>
            {balanceSheet.assets.map((line) => (
              <div className="reports-stat" key={line.code}>
                <span className="reports-stat-label">{line.name}</span>
                <span className="reports-stat-value">{formatRupiah(line.balance)}</span>
              </div>
            ))}
            <div className="reports-stat reports-stat--total">
              <span className="reports-stat-label">Total Aset</span>
              <span className="reports-stat-value">{formatRupiah(balanceSheet.totalAssets)}</span>
            </div>

            <div className="reports-stat">
              <span className="reports-stat-label">Kewajiban</span>
              <span className="reports-stat-value"></span>
            </div>
            {balanceSheet.liabilities.map((line) => (
              <div className="reports-stat" key={line.code}>
                <span className="reports-stat-label">{line.name}</span>
                <span className="reports-stat-value">{formatRupiah(line.balance)}</span>
              </div>
            ))}
            <div className="reports-stat">
              <span className="reports-stat-label">Total Kewajiban</span>
              <span className="reports-stat-value">{formatRupiah(balanceSheet.totalLiabilities)}</span>
            </div>

            <div className="reports-stat">
              <span className="reports-stat-label">Modal (Laba Ditahan)</span>
              <span className="reports-stat-value">{formatRupiah(balanceSheet.retainedEarnings)}</span>
            </div>

            <div className="reports-stat reports-stat--total">
              <span className="reports-stat-label">Total Kewajiban + Modal</span>
              <span className="reports-stat-value">{formatRupiah(balanceSheet.totalLiabilitiesAndEquity)}</span>
            </div>

            {!balanceSheet.isBalanced && (
              <p className="purchasing-error" role="alert">
                Neraca tidak seimbang — Aset tidak sama dengan Kewajiban + Modal. Ini seharusnya tidak terjadi,
                mohon laporkan.
              </p>
            )}
          </div>
        )}
      </section>

      {payingExpense && (
        <div className="purchasing-overlay">
          <form className="purchasing-modal" onSubmit={handlePaymentSubmit}>
            <h2>Bayar — {payingExpense.description}</h2>
            <p className="purchasing-modal-meta">Sisa tagihan: {formatRupiah(payingExpense.remainingBalance)}</p>

            <label htmlFor="expPaymentAmount">Jumlah Bayar</label>
            <input
              id="expPaymentAmount"
              type="number"
              min="0"
              step="any"
              max={payingExpense.remainingBalance}
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
