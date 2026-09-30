import { useEffect, useState, type FormEvent } from "react";
import { useNavigate } from "react-router-dom";
import { createCategory, listCategories, updateCategory, type Category } from "../api/categories";
import { useAuth } from "../context/AuthContext";
import { ApiError } from "../api/client";
import "./PurchasingShared.css";
import "./IngredientsPage.css";

const CAN_MANAGE_ROLES = ["Owner", "Manager"];

export function CategoriesPage() {
  const navigate = useNavigate();
  const { user } = useAuth();
  const canManage = !!user && CAN_MANAGE_ROLES.includes(user.role);

  const [categories, setCategories] = useState<Category[]>([]);
  const [name, setName] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);

  const [editingId, setEditingId] = useState<string | null>(null);
  const [editingName, setEditingName] = useState("");
  const [isSaving, setIsSaving] = useState(false);

  useEffect(() => {
    refresh();
  }, []);

  function refresh() {
    listCategories()
      .then(setCategories)
      .catch((err) => setError(err instanceof ApiError ? err.message : "Tidak bisa terhubung ke server."));
  }

  async function handleCreate(event: FormEvent) {
    event.preventDefault();
    setError(null);
    setIsSubmitting(true);
    try {
      await createCategory(name);
      setName("");
      refresh();
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Tidak bisa terhubung ke server.");
    } finally {
      setIsSubmitting(false);
    }
  }

  function startEdit(category: Category) {
    setEditingId(category.id);
    setEditingName(category.name);
  }

  async function saveEdit(id: string) {
    setIsSaving(true);
    setError(null);
    try {
      await updateCategory(id, editingName);
      setEditingId(null);
      refresh();
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Tidak bisa terhubung ke server.");
    } finally {
      setIsSaving(false);
    }
  }

  return (
    <main className="purchasing-page">
      <header className="purchasing-header">
        <button type="button" className="purchasing-back" onClick={() => navigate("/menu")}>
          ← Menu
        </button>
        <h1>Kategori</h1>
      </header>

      {error && (
        <p className="purchasing-error" role="alert">
          {error}
        </p>
      )}

      {canManage && (
        <form className="purchasing-form" onSubmit={handleCreate}>
          <h2>Kategori Baru</h2>
          <label htmlFor="categoryName">Nama</label>
          <input id="categoryName" value={name} onChange={(event) => setName(event.target.value)} required />
          <button type="submit" disabled={isSubmitting}>
            {isSubmitting ? "Menyimpan..." : "Simpan"}
          </button>
        </form>
      )}

      <table className="purchasing-table">
        <thead>
          <tr>
            <th>Nama</th>
            <th></th>
          </tr>
        </thead>
        <tbody>
          {categories.map((category) => (
            <tr key={category.id}>
              <td>
                {editingId === category.id ? (
                  <input
                    className="ingredients-min-input"
                    style={{ width: 220 }}
                    value={editingName}
                    onChange={(event) => setEditingName(event.target.value)}
                    autoFocus
                  />
                ) : (
                  category.name
                )}
              </td>
              <td>
                {canManage && (
                  <div className="purchasing-row-actions">
                    {editingId === category.id ? (
                      <>
                        <button type="button" disabled={isSaving} onClick={() => saveEdit(category.id)}>
                          Simpan
                        </button>
                        <button type="button" onClick={() => setEditingId(null)}>
                          Batal
                        </button>
                      </>
                    ) : (
                      <button type="button" onClick={() => startEdit(category)}>
                        Ubah Nama
                      </button>
                    )}
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
