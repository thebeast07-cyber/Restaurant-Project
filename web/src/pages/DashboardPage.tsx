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

      <p className="dashboard-placeholder">
        Halaman ini adalah fondasi login (Hari 1). Fitur Shift, Order, dan lainnya
        menyusul di hari-hari berikutnya sesuai roadmap.
      </p>
    </main>
  );
}
