using Restaurant.Domain.Common;

namespace Restaurant.Domain.Audit;

public enum AuditAction
{
    Void,
    StockAdjustment
}

/// <summary>
/// Append-only record for sensitive operations (docs/product/PRD.md §12: void,
/// refund, stock adjustment must answer who/when/what/before-after). Never updated
/// or deleted after creation.
/// </summary>
public class AuditLog : Entity, IBranchScoped
{
    public required Guid TenantId { get; set; }
    public required Guid BranchId { get; set; }
    public required Guid UserId { get; set; }
    public required AuditAction Action { get; set; }
    public required string EntityType { get; set; }
    public required Guid EntityId { get; set; }
    public string? BeforeValue { get; set; }
    public string? AfterValue { get; set; }
    public string? Reason { get; set; }
}
