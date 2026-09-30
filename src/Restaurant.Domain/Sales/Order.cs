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
    public required int Quantity { get; set; }
    public required decimal UnitPrice { get; set; }
    public required decimal Subtotal { get; set; }

    /// <summary>Denormalized from Product at add-time so ticket routing survives later menu edits.</summary>
    public required Station Station { get; set; }
}
