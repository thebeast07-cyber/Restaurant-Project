using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Restaurant.Api.Auth;
using Restaurant.Api.Contracts;
using Restaurant.Domain.Common;
using Restaurant.Domain.Sales;
using Restaurant.Infrastructure.Persistence;

namespace Restaurant.Api.Controllers;

[ApiController]
[Route("api/shifts")]
[Authorize]
public class ShiftsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ICurrentTenantProvider _tenant;

    public ShiftsController(AppDbContext db, ICurrentTenantProvider tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    [HttpGet("current")]
    public async Task<ActionResult<ShiftResponse>> GetCurrent()
    {
        var userId = User.GetUserId();
        var shift = await _db.Shifts
            .SingleOrDefaultAsync(s => s.UserId == userId && s.Status == ShiftStatus.Open);

        if (shift is null)
        {
            return NotFound(new { message = "No open shift for this user." });
        }

        return Ok(new ShiftResponse(shift.Id, shift.OpenedAt, shift.OpeningCash, shift.Status));
    }

    [HttpPost("open")]
    public async Task<ActionResult<ShiftResponse>> Open(OpenShiftRequest request)
    {
        var userId = User.GetUserId();

        var alreadyOpen = await _db.Shifts.AnyAsync(s => s.UserId == userId && s.Status == ShiftStatus.Open);
        if (alreadyOpen)
        {
            return Conflict(new { message = "This user already has an open shift." });
        }

        var shift = new Shift
        {
            TenantId = _tenant.TenantId!.Value,
            BranchId = _tenant.BranchId!.Value,
            UserId = userId,
            OpeningCash = request.OpeningCash
        };

        _db.Shifts.Add(shift);
        await _db.SaveChangesAsync();

        return Ok(new ShiftResponse(shift.Id, shift.OpenedAt, shift.OpeningCash, shift.Status));
    }
}
