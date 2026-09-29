using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Restaurant.Api.Auth;
using Restaurant.Api.Contracts;
using Restaurant.Domain.Common;
using Restaurant.Domain.Payment;
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

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // Lost the race against another concurrent "open shift" request for the
            // same user — the AnyAsync check above passed for both, but the partial
            // unique index on (UserId WHERE Status = Open) only lets one through.
            // Shift has no other unique constraint, so any DbUpdateException here can
            // only be this one.
            return Conflict(new { message = "This user already has an open shift." });
        }

        return Ok(new ShiftResponse(shift.Id, shift.OpenedAt, shift.OpeningCash, shift.Status));
    }

    /// <summary>
    /// The ShiftClosed event (Day 11): cash reconciliation against everything sold
    /// during this shift. Only the shift's own owner can close it — matches the
    /// permission matrix (discovery/04: Cashier "Execute" on Open/Close Shift,
    /// Manager/Owner only "Audit"/"View", not execute on someone else's shift).
    /// </summary>
    [HttpPost("{shiftId:guid}/close")]
    public async Task<ActionResult<ShiftCloseResponse>> Close(Guid shiftId, CloseShiftRequest request)
    {
        var userId = User.GetUserId();

        var shift = await _db.Shifts.SingleOrDefaultAsync(s => s.Id == shiftId);
        if (shift is null)
        {
            return NotFound();
        }

        if (shift.UserId != userId)
        {
            return Forbid();
        }

        if (shift.Status != ShiftStatus.Open)
        {
            return BadRequest(new { message = "Shift is already closed." });
        }

        // Only Completed orders count toward reconciliation — a Voided order's
        // Payment row is never edited/deleted (Void doesn't touch Payment at all,
        // see OrdersController.Void), so filtering on Order.Status here is what
        // actually excludes voided sales from the cash drawer, not the Payment table.
        var confirmedPayments = await _db.Payments
            .Where(p => p.Status == PaymentStatus.Confirmed)
            .Join(_db.Orders.Where(o => o.ShiftId == shiftId && o.Status == OrderStatus.Completed),
                p => p.OrderId, o => o.Id, (p, o) => p)
            .Join(_db.PaymentMethods, p => p.PaymentMethodId, m => m.Id, (p, m) => new { p.Amount, m.Code })
            .ToListAsync();

        var cashSalesTotal = confirmedPayments.Where(x => x.Code == PaymentMethodCode.Cash).Sum(x => x.Amount);
        var nonCashSalesTotal = confirmedPayments.Where(x => x.Code != PaymentMethodCode.Cash).Sum(x => x.Amount);
        var expectedCash = shift.OpeningCash + cashSalesTotal;
        var cashVariance = request.ClosingCash - expectedCash;

        var closedAt = DateTimeOffset.UtcNow;

        // Atomic claim, same guarded-transition pattern as every other status change
        // in this codebase — two concurrent close requests for the same shift must
        // not both succeed (would double-count as far as any caller reading the
        // response, even though the DB row itself only takes the last write).
        var claimed = await _db.Shifts
            .Where(s => s.Id == shiftId && s.Status == ShiftStatus.Open)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(s => s.Status, ShiftStatus.Closed)
                .SetProperty(s => s.ClosedAt, closedAt)
                .SetProperty(s => s.ClosingCash, request.ClosingCash));

        if (claimed == 0)
        {
            return BadRequest(new { message = "Shift was already closed by another request." });
        }

        return Ok(new ShiftCloseResponse(
            shift.Id,
            shift.OpenedAt,
            closedAt,
            shift.OpeningCash,
            cashSalesTotal,
            nonCashSalesTotal,
            expectedCash,
            request.ClosingCash,
            cashVariance,
            ShiftStatus.Closed));
    }
}
