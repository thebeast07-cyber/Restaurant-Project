using Restaurant.Domain.Common;

namespace Restaurant.Domain.Organization;

public class Tenant : Entity
{
    public required string Name { get; set; }
}
