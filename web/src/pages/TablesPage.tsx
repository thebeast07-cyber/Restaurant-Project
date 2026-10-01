import { useEffect, useState } from "react";
import { useNavigate } from "react-router-dom";
import QRCode from "qrcode";
import { listTables, type RestaurantTable } from "../api/tables";
import { createOrder } from "../api/orders";
import { ApiError } from "../api/client";
import "./TablesPage.css";
import "./PurchasingShared.css";

export function TablesPage() {
  const navigate = useNavigate();

  const [tables, setTables] = useState<RestaurantTable[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [seatingTableId, setSeatingTableId] = useState<string | null>(null);

  const [qrTable, setQrTable] = useState<RestaurantTable | null>(null);
  const [qrDataUrl, setQrDataUrl] = useState<string | null>(null);

  useEffect(() => {
    if (!qrTable) return;
    const url = `${window.location.origin}/s/${qrTable.id}`;
    QRCode.toDataURL(url, { width: 240, margin: 1 }).then(setQrDataUrl);
  }, [qrTable]);

  function closeQrModal() {
    setQrTable(null);
    setQrDataUrl(null);
  }

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
            <div key={table.id} className="table-tile-wrap">
              <button
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
              <button type="button" className="table-tile-qr-btn" onClick={() => setQrTable(table)}>
                QR Self-Order
              </button>
            </div>
          ))}
        </div>
      )}

      {qrTable && (
        <div className="purchasing-overlay">
          <div className="purchasing-modal">
            <h2>QR Self-Order — Meja {qrTable.number}</h2>
            <p className="purchasing-modal-meta">
              Tempel/cetak QR ini di meja. Pelanggan scan untuk pesan dari HP mereka sendiri.
            </p>
            {qrDataUrl && (
              <div style={{ textAlign: "center", margin: "16px 0" }}>
                <img src={qrDataUrl} alt={`QR self-order meja ${qrTable.number}`} width={240} height={240} />
              </div>
            )}
            <p className="purchasing-modal-meta" style={{ wordBreak: "break-all" }}>
              {window.location.origin}/s/{qrTable.id}
            </p>
            <div className="purchasing-modal-actions">
              <button type="button" className="purchasing-modal-cancel" onClick={closeQrModal}>
                Tutup
              </button>
              {qrDataUrl && (
                <a href={qrDataUrl} download={`qr-meja-${qrTable.number}.png`}>
                  <button type="button">Unduh QR</button>
                </a>
              )}
            </div>
          </div>
        </div>
      )}
    </main>
  );
}
