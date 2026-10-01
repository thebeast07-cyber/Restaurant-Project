using Restaurant.Domain.Common;

namespace Restaurant.Domain.Promotion;

/// <summary>
/// A discount rule a cashier (POS) or customer (Self-Order) can attach to an Order
/// before Checkout by typing in Code — see OrdersController/SelfOrderController's
/// promo-code endpoints. Percentage-only for this first phase (no fixed-amount or
/// auto-applied rules yet, see docs/architecture/02-ui-roadmap.md). The three
/// constraint fields are independently optional — an admin leaves whichever don't
/// apply to a given code null, not a fixed "basic vs advanced" code type.
/// </summary>
public class PromoCode : Entity, IBranchScoped
{
    public required Guid TenantId { get; set; }
    public required Guid BranchId { get; set; }

    /// <summary>Stored upper-invariant so lookup is case-insensitive without a
    /// citext/collation dependency — see OrdersController.ApplyPromoCode.</summary>
    public required string Code { get; set; }
    public required decimal PercentageOff { get; set; }
    public decimal? MinimumPurchase { get; set; }
    public DateOnly? ExpiresAt { get; set; }
    public int? UsageLimit { get; set; }

    /// <summary>
    /// Incremented only at successful Checkout (not when an Order merely attaches the
    /// code) — an Order that attaches a code and is then Cancelled never consumed a
    /// slot. Denormalized for the same reason as Purchase.AmountPaid: the "would this
    /// exceed UsageLimit?" check in Checkout needs to be a single atomic conditional
    /// UPDATE, not a separate count-then-write that a concurrent checkout could race.
    /// </summary>
    public int UsageCount { get; set; }

    /// <summary>Admin's own on/off switch — distinct from ExpiresAt, which is a
    /// scheduled expiry rather than a manual toggle.</summary>
    public bool IsActive { get; set; } = true;
}
