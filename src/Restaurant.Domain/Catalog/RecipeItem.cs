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
    public required Guid IngredientId { get; set; }
    public required decimal Quantity { get; set; }
    public required string Unit { get; set; }
}
