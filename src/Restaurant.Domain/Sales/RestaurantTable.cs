using Restaurant.Domain.Common;

namespace Restaurant.Domain.Sales;

public enum TableStatus
{
    Available,
    Occupied
}

/// <summary>
/// Named RestaurantTable (not Table) to avoid clashing with SQL/EF terminology.
/// Full occupancy lifecycle (auto-flip on order, prevent double-booking) is a
/// fast-follow (docs/architecture/01-mvp-technical-design.md sprint Day 8) —
/// for now this just lets an Order reference a table number.
/// </summary>
public class RestaurantTable : Entity, IBranchScoped
{
    public required Guid TenantId { get; set; }
    public required Guid BranchId { get; set; }
    public required string Number { get; set; }
    public TableStatus Status { get; set; } = TableStatus.Available;
}
