using Restaurant.Domain.Common;

namespace Restaurant.Domain.Catalog;

/// <summary>
/// A sellable menu item. Never holds stock directly — physical stock deduction
/// happens on its linked Ingredients via RecipeItem when OrderPaid fires.
/// </summary>
public class Product : Entity, IBranchScoped
{
    public required Guid TenantId { get; set; }
    public required Guid BranchId { get; set; }
    public required Guid CategoryId { get; set; }
    public required string Name { get; set; }

    /// <summary>Sale price when this Product has no Variants (the common case).
    /// Ignored once Variants is non-empty — each Variant carries its own Price then,
    /// see ProductVariant.</summary>
    public required decimal Price { get; set; }
    public required Station Station { get; set; }
    public bool IsActive { get; set; } = true;
    public string? ImageUrl { get; set; }

    /// <summary>RecipeItems attached directly to this Product (no ProductVariantId) —
    /// only meaningful while Variants is empty. Once a Product has Variants, its
    /// recipe lives per-Variant instead (see ProductVariant.RecipeItems), since
    /// variants can consume different ingredient quantities (e.g. a "Jumbo" size
    /// using more rice).</summary>
    public List<RecipeItem> RecipeItems { get; set; } = [];

    public List<ProductVariant> Variants { get; set; } = [];
}

/// <summary>
/// A priced option under a Product (e.g. "Reguler"/"Jumbo" under "Nasi Goreng"). A
/// Product with zero Variants sells directly at Product.Price with Product's own
/// RecipeItems — Variants are opt-in, not forced on every Product.
/// </summary>
public class ProductVariant : Entity, ITenantScoped
{
    public required Guid TenantId { get; set; }
    public required Guid ProductId { get; set; }
    public required string Name { get; set; }
    public required decimal Price { get; set; }

    /// <summary>Same soft-delete reasoning as Product.IsActive: OrderItem.ProductVariantId
    /// is a hard FK, so a Variant that has ever been ordered can't be removed outright —
    /// deactivating hides it from the POS/Catalog without breaking that order's history.</summary>
    public bool IsActive { get; set; } = true;

    public List<RecipeItem> RecipeItems { get; set; } = [];
}
