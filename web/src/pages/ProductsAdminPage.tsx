import { useEffect, useState, type FormEvent } from "react";
import { useNavigate } from "react-router-dom";
import { createProduct, listProducts, updateProduct, type Product, type Station } from "../api/products";
import { listCategories, type Category } from "../api/categories";
import { listIngredients, type Ingredient } from "../api/ingredients";
import { IngredientSelect } from "../components/IngredientSelect";
import { useAuth } from "../context/AuthContext";
import { ApiError } from "../api/client";
import "./PurchasingShared.css";

const CAN_MANAGE_ROLES = ["Owner", "Manager"];

interface RecipeLine {
  ingredientId: string;
  quantity: string;
  unit: string;
}

interface ProductDraft {
  name: string;
  categoryId: string;
  price: string;
  station: Station;
  isActive: boolean;
  recipeLines: RecipeLine[];
}

function emptyDraft(): ProductDraft {
  return { name: "", categoryId: "", price: "", station: "Kitchen", isActive: true, recipeLines: [] };
}

function draftFromProduct(product: Product): ProductDraft {
  return {
    name: product.name,
    categoryId: product.categoryId,
    price: String(product.price),
    station: product.station,
    isActive: product.isActive,
    recipeLines: product.recipeItems.map((item) => ({
      ingredientId: item.ingredientId,
      quantity: String(item.quantity),
      unit: item.unit,
    })),
  };
}

export function ProductsAdminPage() {
  const navigate = useNavigate();
  const { user } = useAuth();
  const canManage = !!user && CAN_MANAGE_ROLES.includes(user.role);

  const [products, setProducts] = useState<Product[]>([]);
  const [categories, setCategories] = useState<Category[]>([]);
  const [ingredients, setIngredients] = useState<Ingredient[]>([]);
  const [error, setError] = useState<string | null>(null);

  const [showCreate, setShowCreate] = useState(false);
  const [createDraft, setCreateDraft] = useState<ProductDraft>(emptyDraft());
  const [isSubmitting, setIsSubmitting] = useState(false);

  const [editingProduct, setEditingProduct] = useState<Product | null>(null);
  const [editDraft, setEditDraft] = useState<ProductDraft>(emptyDraft());
  const [isSaving, setIsSaving] = useState(false);

  useEffect(() => {
    refresh();
    listCategories().then(setCategories).catch(() => {});
    listIngredients().then(setIngredients).catch(() => {});
  }, []);

  function refresh() {
    listProducts(true)
      .then(setProducts)
      .catch((err) => setError(err instanceof ApiError ? err.message : "Tidak bisa terhubung ke server."));
  }

  function updateRecipeLine(
    draft: ProductDraft,
    setDraft: (d: ProductDraft) => void,
    index: number,
    patch: Partial<RecipeLine>,
  ) {
    setDraft({
      ...draft,
      recipeLines: draft.recipeLines.map((line, i) => (i === index ? { ...line, ...patch } : line)),
    });
  }

  function addRecipeLine(draft: ProductDraft, setDraft: (d: ProductDraft) => void) {
    setDraft({ ...draft, recipeLines: [...draft.recipeLines, { ingredientId: "", quantity: "", unit: "" }] });
  }

  function removeRecipeLine(draft: ProductDraft, setDraft: (d: ProductDraft) => void, index: number) {
    setDraft({ ...draft, recipeLines: draft.recipeLines.filter((_, i) => i !== index) });
  }

  async function handleCreate(event: FormEvent) {
    event.preventDefault();
    setError(null);
    setIsSubmitting(true);
    try {
      const items = createDraft.recipeLines
        .filter((line) => line.ingredientId && line.quantity)
        .map((line) => ({ ingredientId: line.ingredientId, quantity: Number(line.quantity), unit: line.unit }));
      await createProduct(createDraft.name, createDraft.categoryId, Number(createDraft.price), createDraft.station, items);
      setCreateDraft(emptyDraft());
      setShowCreate(false);
      refresh();
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Tidak bisa terhubung ke server.");
    } finally {
      setIsSubmitting(false);
    }
  }

  function openEdit(product: Product) {
    setEditingProduct(product);
    setEditDraft(draftFromProduct(product));
  }

  async function handleEditSave(event: FormEvent) {
    event.preventDefault();
    if (!editingProduct) {
      return;
    }
    setError(null);
    setIsSaving(true);
    try {
      const items = editDraft.recipeLines
        .filter((line) => line.ingredientId && line.quantity)
        .map((line) => ({ ingredientId: line.ingredientId, quantity: Number(line.quantity), unit: line.unit }));
      await updateProduct(
        editingProduct.id,
        editDraft.name,
        editDraft.categoryId,
        Number(editDraft.price),
        editDraft.station,
        editDraft.isActive,
        items,
      );
      setEditingProduct(null);
      refresh();
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Tidak bisa terhubung ke server.");
    } finally {
      setIsSaving(false);
    }
  }

  function categoryName(categoryId: string): string {
    return categories.find((c) => c.id === categoryId)?.name ?? "—";
  }

  function renderRecipeEditor(draft: ProductDraft, setDraft: (d: ProductDraft) => void) {
    return (
      <>
        <label>Resep</label>
        <div className="purchasing-line-items">
          {draft.recipeLines.map((line, index) => (
            <div className="purchasing-line-item" key={index}>
              <IngredientSelect
                ingredients={ingredients}
                value={line.ingredientId}
                required
                onChange={(ingredientId, unit) => updateRecipeLine(draft, setDraft, index, { ingredientId, unit })}
                onCreated={(ingredient) => setIngredients((prev) => [...prev, ingredient])}
              />
              <input
                type="number"
                min="0"
                step="any"
                placeholder="Jumlah"
                value={line.quantity}
                onChange={(event) => updateRecipeLine(draft, setDraft, index, { quantity: event.target.value })}
                required
              />
              <input
                placeholder="Satuan"
                value={line.unit}
                onChange={(event) => updateRecipeLine(draft, setDraft, index, { unit: event.target.value })}
                required
              />
              <button
                type="button"
                className="purchasing-line-item-remove"
                onClick={() => removeRecipeLine(draft, setDraft, index)}
              >
                Hapus
              </button>
            </div>
          ))}
        </div>
        <button type="button" className="purchasing-add-line" onClick={() => addRecipeLine(draft, setDraft)}>
          + Tambah Bahan
        </button>
      </>
    );
  }

  return (
    <main className="purchasing-page">
      <header className="purchasing-header">
        <button type="button" className="purchasing-back" onClick={() => navigate("/menu")}>
          ← Menu
        </button>
        <h1>Produk &amp; Resep</h1>
      </header>

      {error && (
        <p className="purchasing-error" role="alert">
          {error}
        </p>
      )}

      {canManage && !showCreate && (
        <button
          type="button"
          className="purchasing-add-line"
          style={{ marginTop: 16 }}
          onClick={() => setShowCreate(true)}
        >
          + Produk Baru
        </button>
      )}

      {canManage && showCreate && (
        <form className="purchasing-form" onSubmit={handleCreate} style={{ maxWidth: 520 }}>
          <h2>Produk Baru</h2>

          <label htmlFor="createName">Nama</label>
          <input
            id="createName"
            value={createDraft.name}
            onChange={(event) => setCreateDraft({ ...createDraft, name: event.target.value })}
            required
          />

          <label htmlFor="createCategory">Kategori</label>
          <select
            id="createCategory"
            value={createDraft.categoryId}
            onChange={(event) => setCreateDraft({ ...createDraft, categoryId: event.target.value })}
            required
          >
            <option value="">Pilih kategori</option>
            {categories.map((category) => (
              <option key={category.id} value={category.id}>
                {category.name}
              </option>
            ))}
          </select>

          <label htmlFor="createPrice">Harga</label>
          <input
            id="createPrice"
            type="number"
            min="0"
            step="any"
            value={createDraft.price}
            onChange={(event) => setCreateDraft({ ...createDraft, price: event.target.value })}
            required
          />

          <label htmlFor="createStation">Station</label>
          <select
            id="createStation"
            value={createDraft.station}
            onChange={(event) => setCreateDraft({ ...createDraft, station: event.target.value as Station })}
          >
            <option value="Kitchen">Dapur</option>
            <option value="Bar">Bar</option>
          </select>

          {renderRecipeEditor(createDraft, setCreateDraft)}

          <div className="purchasing-modal-actions">
            <button
              type="button"
              className="purchasing-modal-cancel"
              onClick={() => {
                setShowCreate(false);
                setCreateDraft(emptyDraft());
              }}
            >
              Batal
            </button>
            <button type="submit" disabled={isSubmitting}>
              {isSubmitting ? "Menyimpan..." : "Simpan Produk"}
            </button>
          </div>
        </form>
      )}

      <table className="purchasing-table">
        <thead>
          <tr>
            <th>Nama</th>
            <th>Kategori</th>
            <th>Harga</th>
            <th>Station</th>
            <th>Status</th>
            <th></th>
          </tr>
        </thead>
        <tbody>
          {products.map((product) => (
            <tr key={product.id}>
              <td>{product.name}</td>
              <td>{categoryName(product.categoryId)}</td>
              <td>{formatRupiah(product.price)}</td>
              <td>{product.station === "Kitchen" ? "Dapur" : "Bar"}</td>
              <td>
                <span className={`purchasing-badge ${product.isActive ? "purchasing-badge--positive" : ""}`}>
                  {product.isActive ? "Aktif" : "Nonaktif"}
                </span>
              </td>
              <td>
                {canManage && (
                  <div className="purchasing-row-actions">
                    <button type="button" onClick={() => openEdit(product)}>
                      Ubah
                    </button>
                  </div>
                )}
              </td>
            </tr>
          ))}
        </tbody>
      </table>

      {editingProduct && (
        <div className="purchasing-overlay">
          <form className="purchasing-modal" onSubmit={handleEditSave} style={{ maxWidth: 480 }}>
            <h2>Ubah — {editingProduct.name}</h2>

            <label htmlFor="editName">Nama</label>
            <input
              id="editName"
              value={editDraft.name}
              onChange={(event) => setEditDraft({ ...editDraft, name: event.target.value })}
              required
            />

            <label htmlFor="editCategory">Kategori</label>
            <select
              id="editCategory"
              value={editDraft.categoryId}
              onChange={(event) => setEditDraft({ ...editDraft, categoryId: event.target.value })}
              required
            >
              <option value="">Pilih kategori</option>
              {categories.map((category) => (
                <option key={category.id} value={category.id}>
                  {category.name}
                </option>
              ))}
            </select>

            <label htmlFor="editPrice">Harga</label>
            <input
              id="editPrice"
              type="number"
              min="0"
              step="any"
              value={editDraft.price}
              onChange={(event) => setEditDraft({ ...editDraft, price: event.target.value })}
              required
            />

            <label htmlFor="editStation">Station</label>
            <select
              id="editStation"
              value={editDraft.station}
              onChange={(event) => setEditDraft({ ...editDraft, station: event.target.value as Station })}
            >
              <option value="Kitchen">Dapur</option>
              <option value="Bar">Bar</option>
            </select>

            <label htmlFor="editActive">
              <input
                id="editActive"
                type="checkbox"
                checked={editDraft.isActive}
                onChange={(event) => setEditDraft({ ...editDraft, isActive: event.target.checked })}
                style={{ marginRight: 8 }}
              />
              Aktif (tampil di menu kasir)
            </label>

            {renderRecipeEditor(editDraft, setEditDraft)}

            <div className="purchasing-modal-actions">
              <button type="button" className="purchasing-modal-cancel" onClick={() => setEditingProduct(null)}>
                Batal
              </button>
              <button type="submit" disabled={isSaving}>
                {isSaving ? "Menyimpan..." : "Simpan Perubahan"}
              </button>
            </div>
          </form>
        </div>
      )}
    </main>
  );
}

function formatRupiah(amount: number): string {
  return new Intl.NumberFormat("id-ID", { style: "currency", currency: "IDR", maximumFractionDigits: 0 }).format(
    amount,
  );
}
