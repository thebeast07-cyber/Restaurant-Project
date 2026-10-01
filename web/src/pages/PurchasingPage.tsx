import { useNavigate } from "react-router-dom";
import "./PurchasingShared.css";
import "./DashboardPage.css";

export function PurchasingPage() {
  const navigate = useNavigate();

  return (
    <main className="purchasing-page">
      <header className="purchasing-header">
        <button type="button" className="purchasing-back" onClick={() => navigate("/dashboard")}>
          ← Dashboard
        </button>
        <h1>Purchasing</h1>
      </header>

      <nav className="dashboard-nav" style={{ marginTop: 24 }}>
        <button type="button" onClick={() => navigate("/purchasing/suppliers")}>
          Supplier
        </button>
        <button type="button" onClick={() => navigate("/purchasing/ingredients")}>
          Bahan Baku &amp; Stok
        </button>
        <button type="button" onClick={() => navigate("/purchasing/requests")}>
          Permintaan Pembelian
        </button>
        <button type="button" onClick={() => navigate("/purchasing/purchases")}>
          Pembelian &amp; Pembayaran
        </button>
        <button type="button" onClick={() => navigate("/purchasing/stock-opname")}>
          Stock Opname
        </button>
      </nav>
    </main>
  );
}
