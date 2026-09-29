using Restaurant.Domain.Common;

namespace Restaurant.Domain.Catalog;

public class Category : Entity, IBranchScoped
{
    public required Guid TenantId { get; set; }
    public required Guid BranchId { get; set; }
    public required string Name { get; set; }
}
