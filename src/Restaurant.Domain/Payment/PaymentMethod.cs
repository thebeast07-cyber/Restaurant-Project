using Restaurant.Domain.Common;

namespace Restaurant.Domain.Payment;

public enum PaymentMethodCode
{
    Cash,
    Qris
}

/// <summary>
/// Generic, provider-agnostic payment method entity — adding EDC/e-wallet later is a
/// new row + adapter, not a redesign (docs/product/PRD.md §14, non-goal: no in-house
/// payment gateway).
/// </summary>
public class PaymentMethod : Entity, ITenantScoped
{
    public required Guid TenantId { get; set; }
    public required PaymentMethodCode Code { get; set; }
    public required string Name { get; set; }
}
