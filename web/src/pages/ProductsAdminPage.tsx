import { useEffect, useState, type FormEvent } from "react";
import { useNavigate } from "react-router-dom";
import {
  createProduct,
  listProducts,
  updateProduct,
  uploadProductImage,
  type Product,
  type ProductVariantInput,
  type Station,
} from "../api/products";
import { listCategories, type Category } from "../api/categories";
import { listIngredients, type Ingredient } from "../api/ingredients";
import { IngredientSelect } from "../components/IngredientSelect";
import { useAuth } from "../context/AuthContext";
import { ApiError } from "../api/client";
import "./PurchasingShared.css";

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL as string;
const CAN_MANAGE_ROLES = ["Owner", "Manager"];

interface RecipeLine {
  ingredientId: string;
  quantity: string;
  unit: string;
}

interface VariantDraft {
  id?: string;
  name: string;
  price: string;
  recipeLines: RecipeLine[];
}

interface ProductDraft {
  name: string;
  categoryId: string;
  price: string;
  station: Station;
  isActive: boolean;
  recipeLines: RecipeLine[];
  variants: VariantDraft[];
}

function emptyDraft(): ProductDraft {
  return { name: "", categoryId: "", price: "", station: "Kitchen", isActive: true, recipeLines: [], variants: [] };
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
    variants: product.variants.map((variant) => ({
      id: variant.id,
      name: variant.name,
      price: String(variant.price),
      recipeLines: variant.recipeItems.map((item) => ({
        ingredientId: item.ingredientId,
        quantity: String(item.quantity),
        unit: item.unit,
      })),
    })),
  };
}

function toRecipeItemInputs(lines: RecipeLine[]) {
  return lines
    .filter((line) => line.ingredientId && line.quantity)
    .map((line) => ({ ingredientId: line.ingredientId, quantity: Number(line.quantity), unit: line.unit }));
}

function toVariantInputs(variants: VariantDraft[]): ProductVariantInput[] {
  return variants
    .filter((variant) => variant.name && variant.price)
    .map((variant) => ({
      id: variant.id,
      name: variant.name,
      price: Number(variant.price),
      recipeItems: toRecipeItemInputs(variant.recipeLines),
    }));
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
  const [isUploadingImage, setIsUploadingImage] = useState(false);

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
    lines: RecipeLine[],
    setLines: (lines: RecipeLine[]) => void,
    index: number,
    patch: Partial<RecipeLine>,
  ) {
    setLines(lines.map((line, i) => (i === index ? { ...line, ...patch } : line)));
  }

  function addRecipeLine(lines: RecipeLine[], setLines: (lines: RecipeLine[]) => void) {
    setLines([...lines, { ingredientId: "", quantity: "", unit: "" }]);
  }

  function removeRecipeLine(lines: RecipeLine[], setLines: (lines: RecipeLine[]) => void, index: number) {
    setLines(lines.filter((_, i) => i !== index));
  }

  async function handleCreate(event: FormEvent) {
    event.preventDefault();
    setError(null);
    setIsSubmitting(true);
    try {
      const items = toRecipeItemInputs(createDraft.recipeLines);
      const variants = toVariantInputs(createDraft.variants);
      await createProduct(
        createDraft.name, createDraft.categoryId, Number(createDraft.price), createDraft.station, items, variants,
      );
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
      const items = toRecipeItemInputs(editDraft.recipeLines);
      const variants = toVariantInputs(editDraft.variants);
      await updateProduct(
        editingProduct.id,
        editDraft.name,
        editDraft.categoryId,
        Number(editDraft.price),
        editDraft.station,
        editDraft.isActive,
        items,
        variants,
      );
      setEditingProduct(null);
      refresh();
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Tidak bisa terhubung ke server.");
    } finally {
      setIsSaving(false);
    }
  }

  async function handleImageUpload(event: FormEvent<HTMLInputElement>) {
    const file = event.currentTarget.files?.[0];
    if (!file || !editingProduct) {
      return;
    }
    setError(null);
    setIsUploadingImage(true);
    try {
      const updated = await uploadProductImage(editingProduct.id, file);
      setEditingProduct(updated);
      refresh();
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Tidak bisa terhubung ke server.");
    } finally {
      setIsUploadingImage(false);
      event.currentTarget.value = "";
    }
  }

  function addVariant(draft: ProductDraft, setDraft: (d: ProductDraft) => void) {
    setDraft({ ...draft, variants: [...draft.variants, { name: "", price: "", recipeLines: [] }] });
  }

  function removeVariant(draft: ProductDraft, setDraft: (d: ProductDraft) => void, index: number) {
    setDraft({ ...draft, variants: draft.variants.filter((_, i) => i !== index) });
  }

  function updateVariant(
    draft: ProductDraft,
    setDraft: (d: ProductDraft) => void,
    index: number,
    patch: Partial<VariantDraft>,
  ) {
    setDraft({
      ...draft,
      variants: draft.variants.map((variant, i) => (i === index ? { ...variant, ...patch } : variant)),
    });
  }

  function categoryName(categoryId: string): string {
    return categories.find((c) => c.id === categoryId)?.name ?? "—";
  }

  function renderRecipeEditor(lines: RecipeLine[], setLines: (lines: RecipeLine[]) => void) {
    return (
      <>
        <label>Resep</label>
        <div className="purchasing-line-items">
          {lines.map((line, index) => (
            <div className="purchasing-line-item" key={index}>
              <IngredientSelect
                ingredients={ingredients}
                value={line.ingredientId}
                required
                onChange={(ingredientId, unit) => updateRecipeLine(lines, setLines, index, { ingredientId, unit })}
                onCreated={(ingredient) => setIngredients((prev) => [...prev, ingredient])}
              />
              <input
                type="number"
                min="0"
                step="any"
                placeholder="Jumlah"
                value={line.quantity}
                onChange={(event) => updateRecipeLine(lines, setLines, index, { quantity: event.target.value })}
                required
              />
              <input
                placeholder="Satuan"
                value={line.unit}
                onChange={(event) => updateRecipeLine(lines, setLines, index, { unit: event.target.value })}
                required
              />
              <button
                type="button"
                className="purchasing-line-item-remove"
                onClick={() => removeRecipeLine(lines, setLines, index)}
              >
                Hapus
              </button>
            </div>
          ))}
        </div>
        <button type="button" className="purchasing-add-line" onClick={() => addRecipeLine(lines, setLines)}>
          + Tambah Bahan
        </button>
      </>
    );
  }

  function renderVariantEditor(draft: ProductDraft, setDraft: (d: ProductDraft) => void) {
    return (
      <>
        <label>Varian</label>
        {draft.variants.length === 0 && (
          <p className="purchasing-modal-meta">
            Belum ada varian — produk ini dijual langsung dengan satu harga di atas.
          </p>
        )}
        {draft.variants.map((variant, index) => (
          <div key={variant.id ?? `new-${index}`} className="purchasing-form" style={{ marginBottom: 12 }}>
            <div className="purchasing-line-item">
              <input
                placeholder="Nama varian (mis. Jumbo)"
                value={variant.name}
                onChange={(event) => updateVariant(draft, setDraft, index, { name: event.target.value })}
                required
              />
              <input
                type="number"
                min="0"
                step="any"
                placeholder="Harga"
                value={variant.price}
                onChange={(event) => updateVariant(draft, setDraft, index, { price: event.target.value })}
                required
              />
              <button
                type="button"
                className="purchasing-line-item-remove"
                onClick={() => removeVariant(draft, setDraft, index)}
              >
                Hapus Varian
              </button>
            </div>
            {renderRecipeEditor(
              variant.recipeLines,
              (lines) => updateVariant(draft, setDraft, index, { recipeLines: lines }),
            )}
          </div>
        ))}
        <button type="button" className="purchasing-add-line" onClick={() => addVariant(draft, setDraft)}>
          + Tambah Varian
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

          <label htmlFor="createPrice">Harga {createDraft.variants.length > 0 && "(diabaikan — varian punya harga sendiri)"}</label>
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

          {createDraft.variants.length === 0 &&
            renderRecipeEditor(createDraft.recipeLines, (lines) => setCreateDraft({ ...createDraft, recipeLines: lines }))}

          {renderVariantEditor(createDraft, setCreateDraft)}

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
            <th></th>
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
              <td>
                {product.imageUrl ? (
                  <img
                    src={`${API_BASE_URL}${product.imageUrl}`}
                    alt={product.name}
                    style={{ width: 40, height: 40, objectFit: "cover", borderRadius: 4 }}
                  />
                ) : (
                  <span style={{ opacity: 0.4 }}>—</span>
                )}
              </td>
              <td>{product.name}</td>
              <td>{categoryName(product.categoryId)}</td>
              <td>
                {product.variants.length > 0
                  ? `${product.variants.length} varian`
                  : formatRupiah(product.price)}
              </td>
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

            <label htmlFor="editPrice">Harga {editDraft.variants.length > 0 && "(diabaikan — varian punya harga sendiri)"}</label>
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

            <label htmlFor="editImage">Foto Produk</label>
            {editingProduct.imageUrl && (
              <img
                src={`${API_BASE_URL}${editingProduct.imageUrl}`}
                alt={editingProduct.name}
                style={{ width: 96, height: 96, objectFit: "cover", borderRadius: 6, marginBottom: 8 }}
              />
            )}
            <input id="editImage" type="file" accept="image/jpeg,image/png,image/webp" onChange={handleImageUpload} disabled={isUploadingImage} />

            {editDraft.variants.length === 0 &&
              renderRecipeEditor(editDraft.recipeLines, (lines) => setEditDraft({ ...editDraft, recipeLines: lines }))}

            {renderVariantEditor(editDraft, setEditDraft)}

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
