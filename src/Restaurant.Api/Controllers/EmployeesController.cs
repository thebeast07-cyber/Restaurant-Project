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
/// HR staff master data. Restricted to Owner/Manager, same as every other
/// back-office write endpoint (Suppliers, Ingredients).
/// </summary>
[ApiController]
[Route("api/employees")]
[Authorize(Roles = "Owner,Manager")]
public class EmployeesController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ICurrentTenantProvider _tenant;

    public EmployeesController(AppDbContext db, ICurrentTenantProvider tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    [HttpGet]
    public async Task<ActionResult<List<EmployeeResponse>>> List()
    {
        var employees = await _db.Employees.OrderBy(e => e.Name).ToListAsync();
        return Ok(employees.Select(ToResponse).ToList());
    }

    [HttpPost]
    public async Task<ActionResult<EmployeeResponse>> Create(CreateEmployeeRequest request)
    {
        if (request.BaseSalary <= 0)
        {
            return BadRequest(new { message = "BaseSalary must be positive." });
        }

        if (request.UserId is not null && !await _db.Users.AnyAsync(u => u.Id == request.UserId))
        {
            return BadRequest(new { message = "UserId not found." });
        }

        var employee = new Employee
        {
            TenantId = _tenant.TenantId!.Value,
            BranchId = _tenant.BranchId!.Value,
            Name = request.Name,
            Position = request.Position,
            BaseSalary = request.BaseSalary,
            HireDate = request.HireDate,
            UserId = request.UserId,
            PinHash = request.Pin is null ? null : BCrypt.Net.BCrypt.HashPassword(request.Pin)
        };

        _db.Employees.Add(employee);
        await _db.SaveChangesAsync();

        return Ok(ToResponse(employee));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<EmployeeResponse>> Update(Guid id, UpdateEmployeeRequest request)
    {
        if (request.BaseSalary <= 0)
        {
            return BadRequest(new { message = "BaseSalary must be positive." });
        }

        var employee = await _db.Employees.SingleOrDefaultAsync(e => e.Id == id);
        if (employee is null)
        {
            return NotFound();
        }

        if (request.UserId is not null && !await _db.Users.AnyAsync(u => u.Id == request.UserId))
        {
            return BadRequest(new { message = "UserId not found." });
        }

        employee.Name = request.Name;
        employee.Position = request.Position;
        employee.BaseSalary = request.BaseSalary;
        employee.IsActive = request.IsActive;
        employee.UserId = request.UserId;

        await _db.SaveChangesAsync();
        return Ok(ToResponse(employee));
    }

    /// <summary>Separate from Update: a PIN reset is a distinct, sensitive action worth
    /// its own endpoint (and its own audit trail if that's ever added), same reasoning
    /// as Order.Void requiring the Manager PIN rather than being folded into a generic
    /// PATCH.</summary>
    [HttpPost("{id:guid}/pin")]
    public async Task<ActionResult<EmployeeResponse>> SetPin(Guid id, SetEmployeePinRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Pin))
        {
            return BadRequest(new { message = "Pin cannot be empty." });
        }

        var employee = await _db.Employees.SingleOrDefaultAsync(e => e.Id == id);
        if (employee is null)
        {
            return NotFound();
        }

        employee.PinHash = BCrypt.Net.BCrypt.HashPassword(request.Pin);
        await _db.SaveChangesAsync();
        return Ok(ToResponse(employee));
    }

    private static EmployeeResponse ToResponse(Employee e) =>
        new(e.Id, e.Name, e.Position, e.BaseSalary, e.HireDate, e.IsActive, e.UserId, e.PinHash is not null);
}
