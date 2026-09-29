using Restaurant.Domain.Common;

namespace Restaurant.Domain.Catalog;

/// <summary>
/// A raw material tracked in Inventory (the "Gudang"). Menu items never hold stock
/// directly — they consume Ingredients through a Recipe.
/// </summary>
public class Ingredient : Entity, IBranchScoped
{
    public required Guid TenantId { get; set; }
    public required Guid BranchId { get; set; }
    public required string Name { get; set; }

    /// <summary>Unit of measure, e.g. "gram", "ml", "pcs".</summary>
    public required string Unit { get; set; }

    /// <summary>
    /// Reorder threshold — not in the original PRD, added by explicit agreement to
    /// give Purchasing a concrete signal for when a Manager should raise a
    /// PurchaseRequest. 0 means "no threshold set" (never flagged low), not "always
    /// out of stock" — there's no separate nullable/enabled flag because a real
    /// minimum of exactly 0 isn't a meaningful business case to distinguish from "not
    /// configured yet".
    /// </summary>
    public decimal MinimumStock { get; set; }
}
