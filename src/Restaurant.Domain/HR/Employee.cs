using Restaurant.Domain.Common;

namespace Restaurant.Domain.HR;

/// <summary>
/// HR staff record, deliberately separate from <see cref="Restaurant.Domain.Identity.User"/>
/// (the POS login account): kitchen staff, servers, and cleaning staff need HR records
/// (salary, attendance, Kasbon) but most will never log into the POS. <see cref="UserId"/>
/// links the subset of staff who are also cashiers/managers — <see cref="Identity.User"/>
/// itself is untouched by this module. <see cref="PinHash"/> is this Employee's own PIN
/// for self-service attendance clock-in, distinct from User.PinHash (POS manager-action
/// authorization) since an Employee with no POS login still needs a way to clock in.
/// </summary>
public class Employee : Entity, IBranchScoped
{
    public required Guid TenantId { get; set; }
    public required Guid BranchId { get; set; }
    public Guid? UserId { get; set; }
    public required string Name { get; set; }
    public required string Position { get; set; }
    public required decimal BaseSalary { get; set; }
    public required DateOnly HireDate { get; set; }
    public bool IsActive { get; set; } = true;
    public string? PinHash { get; set; }
}
