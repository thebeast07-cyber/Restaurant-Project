import { useState, type FormEvent } from "react";
import { createPortal } from "react-dom";
import { createIngredient, type Ingredient } from "../api/ingredients";
import { ApiError } from "../api/client";

const NEW_OPTION_VALUE = "__new__";

interface IngredientSelectProps {
  ingredients: Ingredient[];
  value: string;
  onChange: (ingredientId: string, unit: string) => void;
  onCreated: (ingredient: Ingredient) => void;
  required?: boolean;
}

/**
 * A normal ingredient <select>, plus a "+ Bahan Baru" option that opens an inline
 * create-ingredient modal instead of navigating away — the cashier/manager building
 * a Recipe or a Purchase line shouldn't have to lose their place in that form just
 * because the ingredient they need doesn't exist yet (previously the only way to add
 * one was via /purchasing/ingredients, a separate page).
 */
export function IngredientSelect({ ingredients, value, onChange, onCreated, required }: IngredientSelectProps) {
  const [showCreate, setShowCreate] = useState(false);
  const [name, setName] = useState("");
  const [unit, setUnit] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);

  function openCreate() {
    setName("");
    setUnit("");
    setError(null);
    setShowCreate(true);
  }

  async function handleCreate(event: FormEvent) {
    event.preventDefault();
    // React re-dispatches a portaled element's events along the *component* tree,
    // not the DOM tree — this form renders under document.body but is still a
    // React-tree descendant of whatever <form> hosts this <select> (Product edit,
    // Purchase Request, Purchase), so without stopping it here, submitting this
    // inner form also bubbles into and triggers the outer form's onSubmit.
    // Confirmed live: without this, saving a new Ingredient here also fired the
    // Product edit form's save and closed it, even though nothing about the
    // Product was touched.
    event.stopPropagation();
    setError(null);
    setIsSubmitting(true);
    try {
      const ingredient = await createIngredient(name, unit, 0);
      onCreated(ingredient);
      onChange(ingredient.id, ingredient.unit);
      setShowCreate(false);
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Tidak bisa terhubung ke server.");
    } finally {
      setIsSubmitting(false);
    }
  }

  return (
    <>
      <select
        value={value}
        required={required}
        onChange={(event) => {
          if (event.target.value === NEW_OPTION_VALUE) {
            openCreate();
            return;
          }
          const selectedUnit = ingredients.find((i) => i.id === event.target.value)?.unit ?? "";
          onChange(event.target.value, selectedUnit);
        }}
      >
        <option value="">Pilih bahan</option>
        {ingredients.map((ingredient) => (
          <option key={ingredient.id} value={ingredient.id}>
            {ingredient.name}
          </option>
        ))}
        <option value={NEW_OPTION_VALUE}>+ Bahan Baru</option>
      </select>

      {showCreate &&
        createPortal(
          // Rendered via portal, not inline: this <select> commonly sits inside a
          // parent <form> (Product edit, Purchase Request, Purchase) — HTML forbids
          // nesting <form> elements, and rendering this modal's <form> inline broke
          // the DOM (browser silently repairs invalid nested forms by reparenting,
          // which sent this modal's submit to the *outer* form's handler instead).
          // A portal to document.body keeps this <form> a sibling of the page's
          // other forms, not a descendant of any of them.
          <div className="purchasing-overlay">
            <form className="purchasing-modal" onSubmit={handleCreate}>
              <h2>Bahan Baru</h2>

              <label htmlFor="newIngredientName">Nama</label>
              <input
                id="newIngredientName"
                value={name}
                onChange={(event) => setName(event.target.value)}
                required
                autoFocus
              />

              <label htmlFor="newIngredientUnit">Satuan</label>
              <input
                id="newIngredientUnit"
                placeholder="gram, pcs, ml, ..."
                value={unit}
                onChange={(event) => setUnit(event.target.value)}
                required
              />

              {error && (
                <p className="purchasing-error" role="alert">
                  {error}
                </p>
              )}

              <div className="purchasing-modal-actions">
                <button type="button" className="purchasing-modal-cancel" onClick={() => setShowCreate(false)}>
                  Batal
                </button>
                <button type="submit" disabled={isSubmitting}>
                  {isSubmitting ? "Menyimpan..." : "Simpan & Pilih"}
                </button>
              </div>
            </form>
          </div>,
          document.body,
        )}
    </>
  );
}
