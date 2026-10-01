import { useEffect, useState, type FormEvent } from "react";
import { useNavigate } from "react-router-dom";
import { createEmployee, listEmployees, setEmployeePin, updateEmployee, type Employee } from "../api/employees";
import { useAuth } from "../context/AuthContext";
import { ApiError } from "../api/client";
import "./PurchasingShared.css";

function formatRupiah(amount: number): string {
  return new Intl.NumberFormat("id-ID", { style: "currency", currency: "IDR", maximumFractionDigits: 0 }).format(
    amount,
  );
}

export function EmployeesPage() {
  const navigate = useNavigate();
  const { user } = useAuth();
  const canView = !!user && (user.role === "Owner" || user.role === "Manager");

  const [employees, setEmployees] = useState<Employee[]>([]);
  const [error, setError] = useState<string | null>(null);

  const [name, setName] = useState("");
  const [position, setPosition] = useState("");
  const [baseSalary, setBaseSalary] = useState("");
  const [hireDate, setHireDate] = useState(() => new Date().toISOString().slice(0, 10));
  const [pin, setPin] = useState("");
  const [isSubmitting, setIsSubmitting] = useState(false);

  const [pinTargetId, setPinTargetId] = useState<string | null>(null);
  const [newPin, setNewPin] = useState("");
  const [pinError, setPinError] = useState<string | null>(null);

  function handleError(err: unknown) {
    setError(err instanceof ApiError ? err.message : "Tidak bisa terhubung ke server.");
  }

  function refresh() {
    listEmployees().then(setEmployees).catch(handleError);
  }

  useEffect(() => {
    if (!canView) return;
    refresh();
  }, [canView]);

  async function handleSubmit(event: FormEvent) {
    event.preventDefault();
    setError(null);
    setIsSubmitting(true);
    try {
      await createEmployee(name, position, Number(baseSalary), hireDate, pin);
      setName("");
      setPosition("");
      setBaseSalary("");
      setPin("");
      refresh();
    } catch (err) {
      handleError(err);
    } finally {
      setIsSubmitting(false);
    }
  }

  async function toggleActive(employee: Employee) {
    try {
      await updateEmployee(employee.id, employee.name, employee.position, employee.baseSalary, !employee.isActive);
      refresh();
    } catch (err) {
      handleError(err);
    }
  }

  async function handlePinSubmit(event: FormEvent) {
    event.preventDefault();
    if (!pinTargetId) return;
    setPinError(null);
    try {
      await setEmployeePin(pinTargetId, newPin);
      setPinTargetId(null);
      setNewPin("");
      refresh();
    } catch (err) {
      setPinError(err instanceof ApiError ? err.message : "Tidak bisa terhubung ke server.");
    }
  }

  if (!canView) {
    return (
      <main className="purchasing-page">
        <header className="purchasing-header">
          <button type="button" className="purchasing-back" onClick={() => navigate("/hr")}>
            ← HR
          </button>
          <h1>Karyawan</h1>
        </header>
        <p className="purchasing-error" role="alert">
          Halaman ini khusus untuk Manager dan Owner.
        </p>
      </main>
    );
  }

  const pinTarget = employees.find((e) => e.id === pinTargetId);

  return (
    <main className="purchasing-page">
      <header className="purchasing-header">
        <button type="button" className="purchasing-back" onClick={() => navigate("/hr")}>
          ← HR
        </button>
        <h1>Karyawan</h1>
      </header>

      {error && (
        <p className="purchasing-error" role="alert">
          {error}
        </p>
      )}

      <form className="purchasing-form" onSubmit={handleSubmit} style={{ maxWidth: 480 }}>
        <h2>Tambah Karyawan</h2>

        <label htmlFor="empName">Nama</label>
        <input id="empName" value={name} onChange={(event) => setName(event.target.value)} required />

        <label htmlFor="empPosition">Jabatan</label>
        <input id="empPosition" value={position} onChange={(event) => setPosition(event.target.value)} required />

        <label htmlFor="empSalary">Gaji Pokok (per bulan)</label>
        <input
          id="empSalary"
          type="number"
          min="0"
          step="any"
          value={baseSalary}
          onChange={(event) => setBaseSalary(event.target.value)}
          required
        />

        <label htmlFor="empHireDate">Tanggal Masuk</label>
        <input
          id="empHireDate"
          type="date"
          value={hireDate}
          onChange={(event) => setHireDate(event.target.value)}
          required
        />

        <label htmlFor="empPin">PIN Absensi (4 digit)</label>
        <input id="empPin" value={pin} onChange={(event) => setPin(event.target.value)} maxLength={6} required />

        <button type="submit" disabled={isSubmitting}>
          {isSubmitting ? "Menyimpan..." : "Simpan"}
        </button>
      </form>

      <table className="purchasing-table">
        <thead>
          <tr>
            <th>Nama</th>
            <th>Jabatan</th>
            <th>Gaji Pokok</th>
            <th>Tanggal Masuk</th>
            <th>Status</th>
            <th></th>
          </tr>
        </thead>
        <tbody>
          {employees.map((employee) => (
            <tr key={employee.id}>
              <td>{employee.name}</td>
              <td>{employee.position}</td>
              <td>{formatRupiah(employee.baseSalary)}</td>
              <td>{employee.hireDate}</td>
              <td>
                <span className={`purchasing-badge ${employee.isActive ? "purchasing-badge--positive" : ""}`}>
                  {employee.isActive ? "Aktif" : "Nonaktif"}
                </span>
              </td>
              <td>
                <div className="purchasing-row-actions">
                  <button type="button" onClick={() => setPinTargetId(employee.id)}>
                    Reset PIN
                  </button>
                  <button type="button" onClick={() => toggleActive(employee)}>
                    {employee.isActive ? "Nonaktifkan" : "Aktifkan"}
                  </button>
                </div>
              </td>
            </tr>
          ))}
        </tbody>
      </table>

      {pinTarget && (
        <div className="purchasing-overlay">
          <form className="purchasing-modal" onSubmit={handlePinSubmit}>
            <h2>Reset PIN — {pinTarget.name}</h2>

            <label htmlFor="newPin">PIN Baru</label>
            <input id="newPin" value={newPin} onChange={(event) => setNewPin(event.target.value)} maxLength={6} required autoFocus />

            {pinError && (
              <p className="purchasing-error" role="alert">
                {pinError}
              </p>
            )}

            <div className="purchasing-modal-actions">
              <button type="button" className="purchasing-modal-cancel" onClick={() => setPinTargetId(null)}>
                Batal
              </button>
              <button type="submit">Simpan</button>
            </div>
          </form>
        </div>
      )}
    </main>
  );
}
