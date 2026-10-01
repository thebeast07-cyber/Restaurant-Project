using Restaurant.Domain.Common;

namespace Restaurant.Domain.Catalog;

/// <summary>
/// Maps a Product to the Ingredients (and quantities) it consumes. This is what
/// makes Inventory deduction meaningful for F&B — a menu item is not itself a
/// stock unit (docs/architecture/01-mvp-technical-design.md, section 1.3).
/// </summary>
public class RecipeItem : Entity, ITenantScoped
{
    public required Guid TenantId { get; set; }
    public required Guid ProductId { get; set; }

    /// <summary>Set when this line belongs to a specific ProductVariant's recipe
    /// rather than the bare Product's (see Product.RecipeItems vs
    /// ProductVariant.RecipeItems) — null is the "Product has no Variants" case,
    /// which is every RecipeItem that predates Variants.</summary>
    public Guid? ProductVariantId { get; set; }
    public required Guid IngredientId { get; set; }
    public required decimal Quantity { get; set; }
    public required string Unit { get; set; }
}
