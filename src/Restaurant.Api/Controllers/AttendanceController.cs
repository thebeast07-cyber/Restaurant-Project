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
/// Clock-in/out is deliberately open to any authenticated role (not just Owner/Manager):
/// it's meant to run at the kiosk/cashier device, self-service, by whichever Employee is
/// coming or going — authorization for *which* Employee is the Employee's own PIN, not
/// the logged-in POS role. Manual entry/correction and the review list stay
/// Owner/Manager-only, same as every other back-office write.
/// </summary>
[ApiController]
[Route("api/attendance")]
[Authorize]
public class AttendanceController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ICurrentTenantProvider _tenant;

    public AttendanceController(AppDbContext db, ICurrentTenantProvider tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    /// <summary>Minimal roster for the clock-in kiosk screen — no salary/PIN exposed.</summary>
    [HttpGet("roster")]
    public async Task<ActionResult<List<object>>> Roster()
    {
        var roster = await _db.Employees
            .Where(e => e.IsActive)
            .OrderBy(e => e.Name)
            .Select(e => new { e.Id, e.Name, e.Position })
            .ToListAsync();
        return Ok(roster);
    }

    [HttpGet]
    [Authorize(Roles = "Owner,Manager")]
    public async Task<ActionResult<List<AttendanceResponse>>> List(Guid? employeeId, DateOnly? from, DateOnly? to)
    {
        var query = _db.Attendances.AsQueryable();
        if (employeeId is not null) query = query.Where(a => a.EmployeeId == employeeId);
        if (from is not null) query = query.Where(a => a.Date >= from);
        if (to is not null) query = query.Where(a => a.Date <= to);

        var records = await query.OrderByDescending(a => a.Date).ToListAsync();
        var employeeNames = await _db.Employees
            .Where(e => records.Select(r => r.EmployeeId).Contains(e.Id))
            .ToDictionaryAsync(e => e.Id, e => e.Name);

        return Ok(records.Select(a => ToResponse(a, employeeNames)).ToList());
    }

    [HttpPost("clock-in")]
    public async Task<ActionResult<AttendanceResponse>> ClockIn(ClockInRequest request)
    {
        var employee = await VerifyPinAsync(request.EmployeeId, request.Pin);
        if (employee is null)
        {
            return Unauthorized(new { message = "Invalid PIN." });
        }

        var today = DateOnly.FromDateTime(DateTimeOffset.UtcNow.Date);
        var existing = await _db.Attendances.SingleOrDefaultAsync(a => a.EmployeeId == employee.Id && a.Date == today);
        if (existing is not null)
        {
            return BadRequest(new { message = "Already clocked in today." });
        }

        var attendance = new Attendance
        {
            TenantId = _tenant.TenantId!.Value,
            BranchId = _tenant.BranchId!.Value,
            EmployeeId = employee.Id,
            Date = today,
            Status = AttendanceStatus.Present,
            ClockInAt = DateTimeOffset.UtcNow,
            Source = AttendanceSource.SelfService
        };
        _db.Attendances.Add(attendance);
        await _db.SaveChangesAsync();

        return Ok(ToResponse(attendance, new Dictionary<Guid, string> { [employee.Id] = employee.Name }));
    }

    [HttpPost("clock-out")]
    public async Task<ActionResult<AttendanceResponse>> ClockOut(ClockOutRequest request)
    {
        var employee = await VerifyPinAsync(request.EmployeeId, request.Pin);
        if (employee is null)
        {
            return Unauthorized(new { message = "Invalid PIN." });
        }

        var today = DateOnly.FromDateTime(DateTimeOffset.UtcNow.Date);
        var attendance = await _db.Attendances.SingleOrDefaultAsync(a => a.EmployeeId == employee.Id && a.Date == today);
        if (attendance is null || attendance.ClockInAt is null)
        {
            return BadRequest(new { message = "No clock-in record found for today." });
        }
        if (attendance.ClockOutAt is not null)
        {
            return BadRequest(new { message = "Already clocked out today." });
        }

        attendance.ClockOutAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();

        return Ok(ToResponse(attendance, new Dictionary<Guid, string> { [employee.Id] = employee.Name }));
    }

    /// <summary>Manager/Owner create-or-correct for a single Employee+Date — covers both
    /// a forgotten self-service clock-in and marking Alpha/Izin/Sakit/Cuti, which have no
    /// clock-in by definition.</summary>
    [HttpPost]
    [Authorize(Roles = "Owner,Manager")]
    public async Task<ActionResult<AttendanceResponse>> RecordManual(RecordAttendanceRequest request)
    {
        var employee = await _db.Employees.SingleOrDefaultAsync(e => e.Id == request.EmployeeId);
        if (employee is null)
        {
            return BadRequest(new { message = "EmployeeId not found." });
        }

        var userId = User.GetUserId();
        var attendance = await _db.Attendances
            .SingleOrDefaultAsync(a => a.EmployeeId == request.EmployeeId && a.Date == request.Date);

        if (attendance is null)
        {
            attendance = new Attendance
            {
                TenantId = _tenant.TenantId!.Value,
                BranchId = _tenant.BranchId!.Value,
                EmployeeId = request.EmployeeId,
                Date = request.Date,
                Source = AttendanceSource.Manual
            };
            _db.Attendances.Add(attendance);
        }

        attendance.Status = request.Status;
        attendance.Notes = request.Notes;
        attendance.RecordedByUserId = userId;
        attendance.Source = AttendanceSource.Manual;

        await _db.SaveChangesAsync();
        return Ok(ToResponse(attendance, new Dictionary<Guid, string> { [employee.Id] = employee.Name }));
    }

    private async Task<Employee?> VerifyPinAsync(Guid employeeId, string pin)
    {
        var employee = await _db.Employees.SingleOrDefaultAsync(e => e.Id == employeeId && e.IsActive);
        if (employee?.PinHash is null || !BCrypt.Net.BCrypt.Verify(pin, employee.PinHash))
        {
            return null;
        }
        return employee;
    }

    private static AttendanceResponse ToResponse(Attendance a, IReadOnlyDictionary<Guid, string> employeeNames) =>
        new(a.Id, a.EmployeeId, employeeNames.GetValueOrDefault(a.EmployeeId, "?"), a.Date, a.Status,
            a.ClockInAt, a.ClockOutAt, a.Source, a.Notes);
}
