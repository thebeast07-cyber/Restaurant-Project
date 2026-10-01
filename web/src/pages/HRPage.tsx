import { useNavigate } from "react-router-dom";
import "./PurchasingShared.css";
import "./DashboardPage.css";

export function HRPage() {
  const navigate = useNavigate();

  return (
    <main className="purchasing-page">
      <header className="purchasing-header">
        <button type="button" className="purchasing-back" onClick={() => navigate("/dashboard")}>
          ← Dashboard
        </button>
        <h1>HR</h1>
      </header>

      <nav className="dashboard-nav" style={{ marginTop: 24 }}>
        <button type="button" onClick={() => navigate("/hr/attendance")}>
          Absensi
        </button>
        <button type="button" onClick={() => navigate("/hr/employees")}>
          Karyawan
        </button>
        <button type="button" onClick={() => navigate("/hr/kasbon")}>
          Kasbon
        </button>
        <button type="button" onClick={() => navigate("/hr/payroll")}>
          Payroll
        </button>
      </nav>
    </main>
  );
}
