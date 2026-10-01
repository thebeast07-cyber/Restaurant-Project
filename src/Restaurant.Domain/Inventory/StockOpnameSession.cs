using Restaurant.Domain.Common;

namespace Restaurant.Domain.Inventory;

/// <summary>
/// Groups multiple per-Ingredient Opname counts into one named counting event (e.g.
/// "Opname 1 Oktober 2026") so the history reads as a session with its own set of
/// discrepancies, instead of individual <see cref="StockMovement"/> rows scattered
/// among ordinary day-to-day adjustments. Deliberately additive: the underlying
/// mechanism (set an Ingredient's Stock to a physically counted value, log the delta
/// as Reason = Opname) already existed in IngredientsController.AdjustStock — this
/// only adds the grouping header via StockMovement.OpnameSessionId, nothing about
/// that mechanism changes for a non-batched single-ingredient count (OpnameSessionId
/// stays null there).
/// </summary>
public class StockOpnameSession : Entity, IBranchScoped
{
    public required Guid TenantId { get; set; }
    public required Guid BranchId { get; set; }
    public required DateOnly Date { get; set; }
    public string? Notes { get; set; }
    public required Guid CreatedByUserId { get; set; }
}
