import { useEffect, useState, type FormEvent } from "react";
import { useNavigate } from "react-router-dom";
import { approveKasbon, createKasbon, listKasbons, rejectKasbon, type Kasbon } from "../api/kasbon";
import { listEmployees, type Employee } from "../api/employees";
import { useAuth } from "../context/AuthContext";
import { ApiError } from "../api/client";
import "./PurchasingShared.css";

function formatRupiah(amount: number): string {
  return new Intl.NumberFormat("id-ID", { style: "currency", currency: "IDR", maximumFractionDigits: 0 }).format(
    amount,
  );
}

export function KasbonPage() {
  const navigate = useNavigate();
  const { user } = useAuth();
  const canView = !!user && (user.role === "Owner" || user.role === "Manager");

  const [employees, setEmployees] = useState<Employee[]>([]);
  const [kasbons, setKasbons] = useState<Kasbon[]>([]);
  const [error, setError] = useState<string | null>(null);

  const [employeeId, setEmployeeId] = useState("");
  const [amount, setAmount] = useState("");
  const [installmentCount, setInstallmentCount] = useState("1");
  const [notes, setNotes] = useState("");
  const [isSubmitting, setIsSubmitting] = useState(false);

  function handleError(err: unknown) {
    setError(err instanceof ApiError ? err.message : "Tidak bisa terhubung ke server.");
  }

  function refresh() {
    listKasbons().then(setKasbons).catch(handleError);
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

  async function handleSubmit(event: FormEvent) {
    event.preventDefault();
    setError(null);
    setIsSubmitting(true);
    try {
      await createKasbon(employeeId, Number(amount), Number(installmentCount), notes);
      setAmount("");
      setInstallmentCount("1");
      setNotes("");
      refresh();
    } catch (err) {
      handleError(err);
    } finally {
      setIsSubmitting(false);
    }
  }

  async function handleApprove(id: string) {
    try {
      await approveKasbon(id);
      refresh();
    } catch (err) {
      handleError(err);
    }
  }

  async function handleReject(id: string) {
    try {
      await rejectKasbon(id);
      refresh();
    } catch (err) {
      handleError(err);
    }
  }

  if (!canView) {
    return (
      <main className="purchasing-page">
        <header className="purchasing-header">
          <button type="button" className="purchasing-back" onClick={() => navigate("/hr")}>
            ← HR
          </button>
          <h1>Kasbon</h1>
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
        <button type="button" className="purchasing-back" onClick={() => navigate("/hr")}>
          ← HR
        </button>
        <h1>Kasbon</h1>
      </header>

      {error && (
        <p className="purchasing-error" role="alert">
          {error}
        </p>
      )}

      <form className="purchasing-form" onSubmit={handleSubmit} style={{ maxWidth: 480 }}>
        <h2>Ajukan Kasbon</h2>

        <label htmlFor="kasbonEmployee">Karyawan</label>
        <select id="kasbonEmployee" value={employeeId} onChange={(event) => setEmployeeId(event.target.value)}>
          {employees.map((employee) => (
            <option key={employee.id} value={employee.id}>
              {employee.name}
            </option>
          ))}
        </select>

        <label htmlFor="kasbonAmount">Jumlah</label>
        <input
          id="kasbonAmount"
          type="number"
          min="0"
          step="any"
          value={amount}
          onChange={(event) => setAmount(event.target.value)}
          required
        />

        <label htmlFor="kasbonInstallments">Jumlah Cicilan (1 = langsung lunas di payroll berikutnya)</label>
        <input
          id="kasbonInstallments"
          type="number"
          min="1"
          step="1"
          value={installmentCount}
          onChange={(event) => setInstallmentCount(event.target.value)}
          required
        />

        <label htmlFor="kasbonNotes">Catatan</label>
        <input id="kasbonNotes" value={notes} onChange={(event) => setNotes(event.target.value)} />

        <button type="submit" disabled={isSubmitting}>
          {isSubmitting ? "Menyimpan..." : "Ajukan"}
        </button>
      </form>

      <table className="purchasing-table">
        <thead>
          <tr>
            <th>Karyawan</th>
            <th>Jumlah</th>
            <th>Cicilan</th>
            <th>Sisa</th>
            <th>Status</th>
            <th></th>
          </tr>
        </thead>
        <tbody>
          {kasbons.map((kasbon) => (
            <tr key={kasbon.id}>
              <td>{kasbon.employeeName}</td>
              <td>{formatRupiah(kasbon.amount)}</td>
              <td>{kasbon.installmentCount}x</td>
              <td>{formatRupiah(kasbon.remainingBalance)}</td>
              <td>
                <span
                  className={`purchasing-badge ${kasbon.status === "Settled" ? "purchasing-badge--positive" : ""}`}
                >
                  {kasbon.status}
                </span>
              </td>
              <td>
                {kasbon.status === "Pending" && (
                  <div className="purchasing-row-actions">
                    <button type="button" onClick={() => handleApprove(kasbon.id)}>
                      Setujui
                    </button>
                    <button type="button" onClick={() => handleReject(kasbon.id)}>
                      Tolak
                    </button>
                  </div>
                )}
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </main>
  );
}
