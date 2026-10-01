using Restaurant.Domain.Catalog;
using Restaurant.Domain.Common;

namespace Restaurant.Domain.Sales;

public enum OrderStatus
{
    Draft,
    Open,
    PendingPayment,
    Paid,
    Completed,
    Voided,

    /// <summary>
    /// Abandoned before Checkout — e.g. a cashier tapped a table just to look, or
    /// backed out of an empty cart. Only reachable from Draft/Open (never Completed;
    /// that's what Void is for), and only ever the very first write against an Order
    /// that Payment/Stock/Journal never touched, so cancelling has nothing to
    /// reverse — unlike Void, which undoes a settled sale.
    /// </summary>
    Cancelled
}

public class Order : Entity, IBranchScoped
{
    public required Guid TenantId { get; set; }
    public required Guid BranchId { get; set; }
    public Guid? TableId { get; set; }
    public required Guid ShiftId { get; set; }

    /// <summary>Set for a Self-Order (captured at the start of that flow) — null for
    /// an order a staff member entered without asking for the customer's contact.
    /// Stays whoever started the table's tab; later submissions to the same Order
    /// don't overwrite it.</summary>
    public Guid? CustomerId { get; set; }
    public OrderStatus Status { get; set; } = OrderStatus.Draft;
    public decimal TotalAmount { get; set; }

    public List<OrderItem> Items { get; set; } = [];

    public void RecalculateTotal()
    {
        TotalAmount = Items.Sum(i => i.Subtotal);
    }
}

public class OrderItem : Entity, ITenantScoped
{
    public required Guid TenantId { get; set; }
    public required Guid OrderId { get; set; }
    public required Guid ProductId { get; set; }

    /// <summary>Which ProductVariant was ordered — null for a Product sold with no
    /// variants (today's default). Required when Product.Variants is non-empty, see
    /// OrdersController.AddItem. Drives which RecipeItem set Checkout resolves for
    /// this line (RecipeItem.ProductVariantId must match), since a variant can have
    /// its own recipe distinct from its sibling variants.</summary>
    public Guid? ProductVariantId { get; set; }
    public required int Quantity { get; set; }
    public required decimal UnitPrice { get; set; }
    public required decimal Subtotal { get; set; }

    /// <summary>Denormalized from Product at add-time so ticket routing survives later menu edits.</summary>
    public required Station Station { get; set; }

    /// <summary>Free-text instruction for this line (e.g. "tanpa es", "pedas level 2") — printed on the station ticket.</summary>
    public string? Notes { get; set; }

    /// <summary>
    /// This line's share of Recipe-derived ingredient cost, snapshotted at Checkout —
    /// null for every OrderItem that predates this field (the per-product margin
    /// report falls back to today's Ingredient.AverageCost for those, see
    /// ReportsController.ProductMargin). Exists because Checkout's JournalEntry only
    /// ever posted one aggregate COGS line for the whole Order — accurate for the
    /// ledger, but with no way to attribute cost back to an individual product line
    /// for a per-product margin report. Mirrors the same snapshot-at-the-moment
    /// reasoning as StockMovement.UnitCostAtTime.
    /// </summary>
    public decimal? EstimatedCogs { get; set; }
}
