import { useNavigate } from "react-router-dom";
import "./PurchasingShared.css";
import "./DashboardPage.css";

export function MenuPage() {
  const navigate = useNavigate();

  return (
    <main className="purchasing-page">
      <header className="purchasing-header">
        <button type="button" className="purchasing-back" onClick={() => navigate("/dashboard")}>
          ← Dashboard
        </button>
        <h1>Menu</h1>
      </header>

      <nav className="dashboard-nav" style={{ marginTop: 24 }}>
        <button type="button" onClick={() => navigate("/menu/categories")}>
          Kategori
        </button>
        <button type="button" onClick={() => navigate("/menu/products")}>
          Produk &amp; Resep
        </button>
      </nav>
    </main>
  );
}
