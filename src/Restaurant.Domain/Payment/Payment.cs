using Restaurant.Domain.Common;

namespace Restaurant.Domain.Payment;

public enum PaymentStatus
{
    Pending,
    Confirmed
}

/// <summary>
/// Cash is Confirmed immediately (cashier physically counted it). QRIS in the MVP is
/// "static QR + manual confirm" (no gateway integration yet), so it also ends up
/// Confirmed by explicit cashier action, not a webhook.
/// </summary>
public class Payment : Entity, ITenantScoped
{
    public required Guid TenantId { get; set; }
    public required Guid OrderId { get; set; }
    public required Guid PaymentMethodId { get; set; }
    public required decimal Amount { get; set; }
    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;
    public Guid? ConfirmedByUserId { get; set; }
    public DateTimeOffset? ConfirmedAt { get; set; }
}
