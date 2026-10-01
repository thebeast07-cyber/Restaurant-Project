import { useEffect, useState, type FormEvent } from "react";
import { useNavigate } from "react-router-dom";
import { listPayslips, runPayroll, type Payslip } from "../api/payroll";
import { listEmployees, type Employee } from "../api/employees";
import { recordExpensePayment } from "../api/expenses";
import { useAuth } from "../context/AuthContext";
import { ApiError } from "../api/client";
import "./PurchasingShared.css";

function formatRupiah(amount: number): string {
  return new Intl.NumberFormat("id-ID", { style: "currency", currency: "IDR", maximumFractionDigits: 0 }).format(
    amount,
  );
}

function translatePaymentStatus(status: Payslip["paymentStatus"]): string {
  switch (status) {
    case "Unpaid":
      return "Belum Bayar";
    case "PartiallyPaid":
      return "Sebagian";
    case "Paid":
      return "Lunas";
  }
}

export function PayrollPage() {
  const navigate = useNavigate();
  const { user } = useAuth();
  const canView = !!user && (user.role === "Owner" || user.role === "Manager");

  const [employees, setEmployees] = useState<Employee[]>([]);
  const [payslips, setPayslips] = useState<Payslip[]>([]);
  const [error, setError] = useState<string | null>(null);

  const now = new Date();
  const [employeeId, setEmployeeId] = useState("");
  const [periodYear, setPeriodYear] = useState(now.getFullYear());
  const [periodMonth, setPeriodMonth] = useState(now.getMonth() + 1);
  const [isRunning, setIsRunning] = useState(false);

  const [payingId, setPayingId] = useState<string | null>(null);
  const [paymentAmount, setPaymentAmount] = useState("");
  const [paymentError, setPaymentError] = useState<string | null>(null);

  function handleError(err: unknown) {
    setError(err instanceof ApiError ? err.message : "Tidak bisa terhubung ke server.");
  }

  function refresh() {
    listPayslips().then(setPayslips).catch(handleError);
  }

  useEffect(() => {
    if (!canView) return;
    listEmployees().then((list) => {
      setEmployees(list);
      if (list.length > 0) setEmployeeId(list[0].id);
    });
    refresh();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [canView]);

  async function handleRun(event: FormEvent) {
    event.preventDefault();
    setError(null);
    setIsRunning(true);
    try {
      await runPayroll(employeeId, periodYear, periodMonth);
      refresh();
    } catch (err) {
      handleError(err);
    } finally {
      setIsRunning(false);
    }
  }

  function openPayment(payslipId: string) {
    setPayingId(payslipId);
    setPaymentAmount("");
    setPaymentError(null);
  }

  async function handlePaymentSubmit(event: FormEvent) {
    event.preventDefault();
    const payslip = payslips.find((p) => p.id === payingId);
    if (!payslip) return;
    setPaymentError(null);
    try {
      await recordExpensePayment(payslip.operatingExpenseId, Number(paymentAmount));
      setPayingId(null);
      refresh();
    } catch (err) {
      setPaymentError(err instanceof ApiError ? err.message : "Tidak bisa terhubung ke server.");
    }
  }

  if (!canView) {
    return (
      <main className="purchasing-page">
        <header className="purchasing-header">
          <button type="button" className="purchasing-back" onClick={() => navigate("/hr")}>
            ← HR
          </button>
          <h1>Payroll</h1>
        </header>
        <p className="purchasing-error" role="alert">
          Halaman ini khusus untuk Manager dan Owner.
        </p>
      </main>
    );
  }

  const payingPayslip = payslips.find((p) => p.id === payingId);

  return (
    <main className="purchasing-page">
      <header className="purchasing-header">
        <button type="button" className="purchasing-back" onClick={() => navigate("/hr")}>
          ← HR
        </button>
        <h1>Payroll</h1>
      </header>

      {error && (
        <p className="purchasing-error" role="alert">
          {error}
        </p>
      )}

      <form className="purchasing-form" onSubmit={handleRun} style={{ maxWidth: 480 }}>
        <h2>Jalankan Payroll</h2>

        <label htmlFor="payrollEmployee">Karyawan</label>
        <select id="payrollEmployee" value={employeeId} onChange={(event) => setEmployeeId(event.target.value)}>
          {employees.map((employee) => (
            <option key={employee.id} value={employee.id}>
              {employee.name}
            </option>
          ))}
        </select>

        <label htmlFor="payrollYear">Tahun</label>
        <input
          id="payrollYear"
          type="number"
          value={periodYear}
          onChange={(event) => setPeriodYear(Number(event.target.value))}
          required
        />

        <label htmlFor="payrollMonth">Bulan</label>
        <input
          id="payrollMonth"
          type="number"
          min={1}
          max={12}
          value={periodMonth}
          onChange={(event) => setPeriodMonth(Number(event.target.value))}
          required
        />

        <button type="submit" disabled={isRunning}>
          {isRunning ? "Memproses..." : "Jalankan Payroll"}
        </button>
      </form>

      <table className="purchasing-table">
        <thead>
          <tr>
            <th>Periode</th>
            <th>Karyawan</th>
            <th>Gaji Pokok</th>
            <th>Potongan Absensi</th>
            <th>Potongan Kasbon</th>
            <th>Gaji Bersih</th>
            <th>Status</th>
            <th></th>
          </tr>
        </thead>
        <tbody>
          {payslips.map((payslip) => (
            <tr key={payslip.id}>
              <td>
                {String(payslip.periodMonth).padStart(2, "0")}/{payslip.periodYear}
              </td>
              <td>{payslip.employeeName}</td>
              <td>{formatRupiah(payslip.baseSalary)}</td>
              <td>
                {formatRupiah(payslip.attendanceDeduction)}
                {payslip.alphaDays > 0 && <span className="purchasing-badge"> {payslip.alphaDays}x Alpha</span>}
              </td>
              <td>{formatRupiah(payslip.kasbonDeduction)}</td>
              <td>{formatRupiah(payslip.netPay)}</td>
              <td>
                <span
                  className={`purchasing-badge ${payslip.paymentStatus === "Paid" ? "purchasing-badge--positive" : ""}`}
                >
                  {translatePaymentStatus(payslip.paymentStatus)}
                </span>
              </td>
              <td>
                {payslip.paymentStatus !== "Paid" && (
                  <div className="purchasing-row-actions">
                    <button type="button" onClick={() => openPayment(payslip.id)}>
                      Bayar
                    </button>
                  </div>
                )}
              </td>
            </tr>
          ))}
        </tbody>
      </table>

      {payingPayslip && (
        <div className="purchasing-overlay">
          <form className="purchasing-modal" onSubmit={handlePaymentSubmit}>
            <h2>Bayar Gaji — {payingPayslip.employeeName}</h2>
            <p className="purchasing-modal-meta">Sisa: {formatRupiah(payingPayslip.remainingBalance)}</p>

            <label htmlFor="payrollPaymentAmount">Jumlah Bayar</label>
            <input
              id="payrollPaymentAmount"
              type="number"
              min="0"
              step="any"
              max={payingPayslip.remainingBalance}
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
              <button type="submit">Konfirmasi</button>
            </div>
          </form>
        </div>
      )}
    </main>
  );
}
