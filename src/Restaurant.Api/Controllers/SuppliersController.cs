using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Restaurant.Api.Contracts;
using Restaurant.Domain.Common;
using Restaurant.Domain.Purchasing;
using Restaurant.Infrastructure.Persistence;

namespace Restaurant.Api.Controllers;

[ApiController]
[Route("api/suppliers")]
[Authorize]
public class SuppliersController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ICurrentTenantProvider _tenant;

    public SuppliersController(AppDbContext db, ICurrentTenantProvider tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    [HttpGet]
    public async Task<ActionResult<List<SupplierResponse>>> List()
    {
        var suppliers = await _db.Suppliers
            .OrderBy(s => s.Name)
            .Select(s => new SupplierResponse(s.Id, s.Name, s.ContactInfo, s.IsActive))
            .ToListAsync();

        return Ok(suppliers);
    }

    [HttpPost]
    [Authorize(Roles = "Owner,Manager")]
    public async Task<ActionResult<SupplierResponse>> Create(CreateSupplierRequest request)
    {
        var supplier = new Supplier
        {
            TenantId = _tenant.TenantId!.Value,
            BranchId = _tenant.BranchId!.Value,
            Name = request.Name,
            ContactInfo = request.ContactInfo
        };

        _db.Suppliers.Add(supplier);
        await _db.SaveChangesAsync();

        return Ok(new SupplierResponse(supplier.Id, supplier.Name, supplier.ContactInfo, supplier.IsActive));
    }
}
