import { useEffect, useState } from "react";
import { useNavigate } from "react-router-dom";
import { listTables, type RestaurantTable } from "../api/tables";
import { createOrder } from "../api/orders";
import { ApiError } from "../api/client";
import "./TablesPage.css";

export function TablesPage() {
  const navigate = useNavigate();

  const [tables, setTables] = useState<RestaurantTable[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [seatingTableId, setSeatingTableId] = useState<string | null>(null);

  useEffect(() => {
    refresh();
  }, []);

  function refresh() {
    setIsLoading(true);
    listTables()
      .then(setTables)
      .catch((err) => setError(err instanceof ApiError ? err.message : "Tidak bisa terhubung ke server."))
      .finally(() => setIsLoading(false));
  }

  async function handleTableClick(table: RestaurantTable) {
    if (table.status === "Occupied") {
      if (table.currentOrderId) {
        navigate(`/orders/${table.currentOrderId}`);
      }
      return;
    }

    setError(null);
    setSeatingTableId(table.id);
    try {
      const order = await createOrder(table.id);
      navigate(`/orders/${order.id}`);
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Tidak bisa terhubung ke server.");
      refresh();
    } finally {
      setSeatingTableId(null);
    }
  }

  return (
    <main className="tables-page">
      <header className="tables-header">
        <button type="button" className="tables-back" onClick={() => navigate("/dashboard")}>
          ← Dashboard
        </button>
        <h1>Meja</h1>
      </header>

      {error && (
        <p className="tables-error" role="alert">
          {error}
        </p>
      )}

      {isLoading ? (
        <p className="tables-meta">Memuat...</p>
      ) : (
        <div className="tables-grid">
          {tables.map((table) => (
            <button
              key={table.id}
              type="button"
              className={`table-tile table-tile--${table.status.toLowerCase()}`}
              disabled={seatingTableId === table.id}
              onClick={() => handleTableClick(table)}
            >
              <span className="table-tile-number">{table.number}</span>
              <span className="table-tile-status">
                {seatingTableId === table.id
                  ? "Memproses..."
                  : table.status === "Available"
                    ? "Kosong"
                    : "Terisi"}
              </span>
            </button>
          ))}
        </div>
      )}
    </main>
  );
}
