import { useEffect, useState, type FormEvent } from "react";
import { useNavigate } from "react-router-dom";
import { createSupplier, listSuppliers, type Supplier } from "../api/suppliers";
import { useAuth } from "../context/AuthContext";
import { ApiError } from "../api/client";
import "./PurchasingShared.css";

const CAN_MANAGE_ROLES = ["Owner", "Manager"];

export function SuppliersPage() {
  const navigate = useNavigate();
  const { user } = useAuth();
  const canManage = !!user && CAN_MANAGE_ROLES.includes(user.role);

  const [suppliers, setSuppliers] = useState<Supplier[]>([]);
  const [name, setName] = useState("");
  const [contactInfo, setContactInfo] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);

  useEffect(() => {
    refresh();
  }, []);

  function refresh() {
    listSuppliers()
      .then(setSuppliers)
      .catch((err) => setError(err instanceof ApiError ? err.message : "Tidak bisa terhubung ke server."));
  }

  async function handleSubmit(event: FormEvent) {
    event.preventDefault();
    setError(null);
    setIsSubmitting(true);
    try {
      await createSupplier(name, contactInfo || null);
      setName("");
      setContactInfo("");
      refresh();
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Tidak bisa terhubung ke server.");
    } finally {
      setIsSubmitting(false);
    }
  }

  return (
    <main className="purchasing-page">
      <header className="purchasing-header">
        <button type="button" className="purchasing-back" onClick={() => navigate("/purchasing")}>
          ← Purchasing
        </button>
        <h1>Supplier</h1>
      </header>

      {error && (
        <p className="purchasing-error" role="alert">
          {error}
        </p>
      )}

      {canManage && (
        <form className="purchasing-form" onSubmit={handleSubmit}>
          <h2>Supplier Baru</h2>
          <label htmlFor="name">Nama</label>
          <input id="name" value={name} onChange={(event) => setName(event.target.value)} required />

          <label htmlFor="contactInfo">Kontak (opsional)</label>
          <input id="contactInfo" value={contactInfo} onChange={(event) => setContactInfo(event.target.value)} />

          <button type="submit" disabled={isSubmitting}>
            {isSubmitting ? "Menyimpan..." : "Simpan"}
          </button>
        </form>
      )}

      <table className="purchasing-table">
        <thead>
          <tr>
            <th>Nama</th>
            <th>Kontak</th>
            <th>Status</th>
          </tr>
        </thead>
        <tbody>
          {suppliers.map((supplier) => (
            <tr key={supplier.id}>
              <td>{supplier.name}</td>
              <td>{supplier.contactInfo ?? "—"}</td>
              <td>{supplier.isActive ? "Aktif" : "Nonaktif"}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </main>
  );
}
