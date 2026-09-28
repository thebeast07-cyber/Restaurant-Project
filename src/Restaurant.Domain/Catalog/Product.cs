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
    public required decimal Price { get; set; }
    public required Station Station { get; set; }
    public bool IsActive { get; set; } = true;

    public List<RecipeItem> RecipeItems { get; set; } = [];
}
