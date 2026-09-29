using Restaurant.Domain.Common;

namespace Restaurant.Domain.Organization;

public class Branch : Entity, ITenantScoped
{
    public required Guid TenantId { get; set; }
    public required string Name { get; set; }
    public string? Address { get; set; }
}
