import { useNavigate } from "react-router-dom";
import { useAuth } from "../context/AuthContext";
import "./DashboardPage.css";

export function DashboardPage() {
  const { user, logout } = useAuth();
  const navigate = useNavigate();

  function handleLogout() {
    logout();
    navigate("/login");
  }

  return (
    <main className="dashboard-page">
      <header className="dashboard-header">
        <div>
          <strong>{user?.name}</strong>
          <span className="dashboard-role">{user?.role}</span>
        </div>
        <button type="button" onClick={handleLogout}>
          Keluar
        </button>
      </header>

      <nav className="dashboard-nav">
        <button type="button" onClick={() => navigate("/tables")}>
          Meja &amp; Order
        </button>
        <button type="button" onClick={() => navigate("/shift")}>
          Shift
        </button>
        <button type="button" onClick={() => navigate("/hr/attendance")}>
          Absensi
        </button>
        {(user?.role === "Owner" || user?.role === "Manager") && (
          <>
            <button type="button" onClick={() => navigate("/purchasing")}>
              Purchasing
            </button>
            <button type="button" onClick={() => navigate("/menu")}>
              Menu
            </button>
            <button type="button" onClick={() => navigate("/reports")}>
              Laporan
            </button>
            <button type="button" onClick={() => navigate("/finance")}>
              Finance
            </button>
            <button type="button" onClick={() => navigate("/hr")}>
              HR
            </button>
            <button type="button" onClick={() => navigate("/promo-codes")}>
              Kode Promo
            </button>
          </>
        )}
      </nav>
    </main>
  );
}
