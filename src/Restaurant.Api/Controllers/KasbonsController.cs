using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Restaurant.Api.Auth;
using Restaurant.Api.Contracts;
using Restaurant.Domain.Common;
using Restaurant.Domain.HR;
using Restaurant.Infrastructure.Persistence;

namespace Restaurant.Api.Controllers;

/// <summary>
/// Kasbon (salary advance) request/approval. Owner/Manager records the request on the
/// Employee's behalf (no separate Employee login exists to self-serve this) and
/// approves/rejects it — same person can do both given this business has no
/// maker-checker separation beyond Owner/Manager today. Approval doubles as the
/// disbursement moment; see Kasbon.cs for why that cash handoff isn't posted to the
/// Finance ledger. Repayment happens automatically at Payroll time (PayrollController).
/// </summary>
[ApiController]
[Route("api/kasbons")]
[Authorize(Roles = "Owner,Manager")]
public class KasbonsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ICurrentTenantProvider _tenant;

    public KasbonsController(AppDbContext db, ICurrentTenantProvider tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    [HttpGet]
    public async Task<ActionResult<List<KasbonResponse>>> List(Guid? employeeId, KasbonStatus? status)
    {
        var query = _db.Kasbons.AsQueryable();
        if (employeeId is not null) query = query.Where(k => k.EmployeeId == employeeId);
        if (status is not null) query = query.Where(k => k.Status == status);

        var kasbons = await query.OrderByDescending(k => k.CreatedAt).ToListAsync();
        var employeeNames = await _db.Employees
            .Where(e => kasbons.Select(k => k.EmployeeId).Contains(e.Id))
            .ToDictionaryAsync(e => e.Id, e => e.Name);

        return Ok(kasbons.Select(k => ToResponse(k, employeeNames)).ToList());
    }

    [HttpPost]
    public async Task<ActionResult<KasbonResponse>> Create(CreateKasbonRequest request)
    {
        if (request.Amount <= 0)
        {
            return BadRequest(new { message = "Amount must be positive." });
        }
        if (request.InstallmentCount < 1)
        {
            return BadRequest(new { message = "InstallmentCount must be at least 1." });
        }

        var employee = await _db.Employees.SingleOrDefaultAsync(e => e.Id == request.EmployeeId && e.IsActive);
        if (employee is null)
        {
            return BadRequest(new { message = "EmployeeId not found or inactive." });
        }

        var kasbon = new Kasbon
        {
            TenantId = _tenant.TenantId!.Value,
            BranchId = _tenant.BranchId!.Value,
            EmployeeId = request.EmployeeId,
            Amount = request.Amount,
            InstallmentCount = request.InstallmentCount,
            Notes = request.Notes
        };
        _db.Kasbons.Add(kasbon);
        await _db.SaveChangesAsync();

        return Ok(ToResponse(kasbon, new Dictionary<Guid, string> { [employee.Id] = employee.Name }));
    }

    [HttpPost("{id:guid}/approve")]
    public async Task<ActionResult<KasbonResponse>> Approve(Guid id)
    {
        var kasbon = await _db.Kasbons.SingleOrDefaultAsync(k => k.Id == id);
        if (kasbon is null)
        {
            return NotFound();
        }
        if (kasbon.Status != KasbonStatus.Pending)
        {
            return BadRequest(new { message = $"Cannot approve a Kasbon in status {kasbon.Status}; it must be Pending." });
        }

        kasbon.Status = KasbonStatus.Approved;
        kasbon.ApprovedByUserId = User.GetUserId();
        kasbon.ApprovedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();

        var employeeName = await _db.Employees.Where(e => e.Id == kasbon.EmployeeId).Select(e => e.Name).SingleAsync();
        return Ok(ToResponse(kasbon, new Dictionary<Guid, string> { [kasbon.EmployeeId] = employeeName }));
    }

    [HttpPost("{id:guid}/reject")]
    public async Task<ActionResult<KasbonResponse>> Reject(Guid id)
    {
        var kasbon = await _db.Kasbons.SingleOrDefaultAsync(k => k.Id == id);
        if (kasbon is null)
        {
            return NotFound();
        }
        if (kasbon.Status != KasbonStatus.Pending)
        {
            return BadRequest(new { message = $"Cannot reject a Kasbon in status {kasbon.Status}; it must be Pending." });
        }

        kasbon.Status = KasbonStatus.Rejected;
        await _db.SaveChangesAsync();

        var employeeName = await _db.Employees.Where(e => e.Id == kasbon.EmployeeId).Select(e => e.Name).SingleAsync();
        return Ok(ToResponse(kasbon, new Dictionary<Guid, string> { [kasbon.EmployeeId] = employeeName }));
    }

    private static KasbonResponse ToResponse(Kasbon k, IReadOnlyDictionary<Guid, string> employeeNames) =>
        new(k.Id, k.EmployeeId, employeeNames.GetValueOrDefault(k.EmployeeId, "?"), k.Amount, k.InstallmentCount,
            k.Status, k.AmountRepaid, k.Amount - k.AmountRepaid, k.Notes);
}
