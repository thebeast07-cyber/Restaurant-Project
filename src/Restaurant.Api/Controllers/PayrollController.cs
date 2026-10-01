using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Restaurant.Api.Auth;
using Restaurant.Api.Contracts;
using Restaurant.Domain.Common;
using Restaurant.Domain.Finance;
using Restaurant.Domain.HR;
using Restaurant.Infrastructure.Persistence;

namespace Restaurant.Api.Controllers;

/// <summary>
/// Monthly payroll. Running payroll for an Employee posts an
/// <see cref="OperatingExpense"/> (Category = Gaji) for NetPay, reusing the exact same
/// accrual pattern (Debit Beban Gaji(6000) / Credit Hutang Gaji(2000)) OperatingExpense
/// already uses — deliberately not a new ledger pattern, see docs/architecture/
/// 02-ui-roadmap.md item 2. Paying the resulting Payslip is just
/// POST /api/expenses/{operatingExpenseId}/payments — no separate payment endpoint
/// needed here.
/// </summary>
[ApiController]
[Route("api/payroll")]
[Authorize(Roles = "Owner,Manager")]
public class PayrollController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ICurrentTenantProvider _tenant;

    public PayrollController(AppDbContext db, ICurrentTenantProvider tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    [HttpGet("payslips")]
    public async Task<ActionResult<List<PayslipResponse>>> List(Guid? employeeId, int? year, int? month)
    {
        var query = _db.Payslips.AsQueryable();
        if (employeeId is not null) query = query.Where(p => p.EmployeeId == employeeId);
        if (year is not null) query = query.Where(p => p.PeriodYear == year);
        if (month is not null) query = query.Where(p => p.PeriodMonth == month);

        var payslips = await query.OrderByDescending(p => p.PeriodYear).ThenByDescending(p => p.PeriodMonth).ToListAsync();

        var employeeNames = await _db.Employees
            .Where(e => payslips.Select(p => p.EmployeeId).Contains(e.Id))
            .ToDictionaryAsync(e => e.Id, e => e.Name);
        var expenses = await _db.OperatingExpenses
            .Where(x => payslips.Select(p => p.OperatingExpenseId).Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x);

        return Ok(payslips.Select(p => ToResponse(p, employeeNames, expenses[p.OperatingExpenseId])).ToList());
    }

    /// <summary>
    /// Computes AttendanceDeduction (only Alpha days deduct, see Attendance.cs) and
    /// KasbonDeduction (processes outstanding Approved Kasbons oldest-first, each capped
    /// at its own per-installment share and at whatever GrossPay remains after earlier
    /// deductions — so NetPay can never go negative), then posts the result as an
    /// OperatingExpense. One Payslip per Employee per calendar month (unique
    /// constraint) — running it twice for the same period is rejected, not silently
    /// overwritten.
    /// </summary>
    [HttpPost("run")]
    public async Task<ActionResult<PayslipResponse>> Run(RunPayrollRequest request)
    {
        if (request.PeriodMonth is < 1 or > 12)
        {
            return BadRequest(new { message = "PeriodMonth must be between 1 and 12." });
        }

        var employee = await _db.Employees.SingleOrDefaultAsync(e => e.Id == request.EmployeeId);
        if (employee is null)
        {
            return BadRequest(new { message = "EmployeeId not found." });
        }

        var alreadyRun = await _db.Payslips.AnyAsync(p =>
            p.EmployeeId == request.EmployeeId && p.PeriodYear == request.PeriodYear && p.PeriodMonth == request.PeriodMonth);
        if (alreadyRun)
        {
            return BadRequest(new { message = "Payroll has already been run for this Employee and period." });
        }

        var workingDays = DateTime.DaysInMonth(request.PeriodYear, request.PeriodMonth);
        var periodStart = new DateOnly(request.PeriodYear, request.PeriodMonth, 1);
        var periodEnd = new DateOnly(request.PeriodYear, request.PeriodMonth, workingDays);

        var alphaDays = await _db.Attendances.CountAsync(a =>
            a.EmployeeId == request.EmployeeId && a.Date >= periodStart && a.Date <= periodEnd && a.Status == AttendanceStatus.Alpha);

        var dailyRate = employee.BaseSalary / workingDays;
        var attendanceDeduction = dailyRate * alphaDays;
        var grossPay = Math.Max(0, employee.BaseSalary - attendanceDeduction);

        var outstandingKasbons = await _db.Kasbons
            .Where(k => k.EmployeeId == request.EmployeeId && k.Status == KasbonStatus.Approved)
            .OrderBy(k => k.ApprovedAt)
            .ToListAsync();

        var remainingBudget = grossPay;
        var totalKasbonDeduction = 0m;
        foreach (var kasbon in outstandingKasbons)
        {
            if (remainingBudget <= 0) break;

            var remainingBalance = kasbon.Amount - kasbon.AmountRepaid;
            var scheduledShare = kasbon.Amount / kasbon.InstallmentCount;
            var target = Math.Min(scheduledShare, remainingBalance);
            var actual = Math.Min(target, remainingBudget);

            kasbon.AmountRepaid += actual;
            if (kasbon.AmountRepaid >= kasbon.Amount)
            {
                kasbon.Status = KasbonStatus.Settled;
            }

            totalKasbonDeduction += actual;
            remainingBudget -= actual;
        }

        var netPay = grossPay - totalKasbonDeduction;
        var userId = User.GetUserId();

        var expense = new OperatingExpense
        {
            TenantId = _tenant.TenantId!.Value,
            BranchId = _tenant.BranchId!.Value,
            Category = ExpenseCategory.Gaji,
            Description = $"Gaji {employee.Name} - {request.PeriodMonth:00}/{request.PeriodYear}",
            Amount = netPay,
            IncurredAt = periodEnd,
            RecordedByUserId = userId
        };

        var operatingExpenseAccount = await _db.Accounts.SingleAsync(a => a.Code == "6000");
        var accountsPayableAccount = await _db.Accounts.SingleAsync(a => a.Code == "2000");

        var journalEntry = new JournalEntry
        {
            TenantId = _tenant.TenantId!.Value,
            BranchId = _tenant.BranchId!.Value,
            ReferenceType = "OperatingExpense",
            ReferenceId = expense.Id
        };
        journalEntry.Lines.Add(new JournalLine { TenantId = _tenant.TenantId!.Value, JournalEntryId = journalEntry.Id, AccountId = operatingExpenseAccount.Id, Debit = netPay, Credit = 0 });
        journalEntry.Lines.Add(new JournalLine { TenantId = _tenant.TenantId!.Value, JournalEntryId = journalEntry.Id, AccountId = accountsPayableAccount.Id, Debit = 0, Credit = netPay });
        journalEntry.AssertBalanced();

        var payslip = new Payslip
        {
            TenantId = _tenant.TenantId!.Value,
            BranchId = _tenant.BranchId!.Value,
            EmployeeId = employee.Id,
            PeriodYear = request.PeriodYear,
            PeriodMonth = request.PeriodMonth,
            BaseSalary = employee.BaseSalary,
            WorkingDaysInPeriod = workingDays,
            AlphaDays = alphaDays,
            AttendanceDeduction = attendanceDeduction,
            GrossPay = grossPay,
            KasbonDeduction = totalKasbonDeduction,
            NetPay = netPay,
            OperatingExpenseId = expense.Id,
            GeneratedByUserId = userId
        };

        _db.OperatingExpenses.Add(expense);
        _db.JournalEntries.Add(journalEntry);
        _db.Payslips.Add(payslip);
        await _db.SaveChangesAsync();

        return Ok(ToResponse(payslip, new Dictionary<Guid, string> { [employee.Id] = employee.Name }, expense));
    }

    private static PayslipResponse ToResponse(Payslip p, IReadOnlyDictionary<Guid, string> employeeNames, OperatingExpense expense) =>
        new(p.Id, p.EmployeeId, employeeNames.GetValueOrDefault(p.EmployeeId, "?"), p.PeriodYear, p.PeriodMonth,
            p.BaseSalary, p.WorkingDaysInPeriod, p.AlphaDays, p.AttendanceDeduction, p.GrossPay, p.KasbonDeduction, p.NetPay,
            p.OperatingExpenseId, expense.AmountPaid, expense.Amount - expense.AmountPaid, expense.PaymentStatus);
}
