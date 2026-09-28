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
}
