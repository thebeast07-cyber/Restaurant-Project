using Restaurant.Domain.Common;

namespace Restaurant.Domain.HR;

/// <summary>
/// One Payslip per Employee per calendar month (see AppDbContext for the unique
/// constraint preventing a double-run). Fields below are a snapshot of the calculation
/// at run time — BaseSalary in particular is copied from Employee.BaseSalary so a later
/// salary change never silently rewrites a past payslip.
///
/// Posts to Finance as an <see cref="Restaurant.Domain.Finance.OperatingExpense"/>
/// (Category = Gaji) for <see cref="NetPay"/> — not GrossPay — because the Kasbon
/// disbursement itself was never booked as a receivable (see Kasbon.cs): the company
/// never formally recorded that cash leaving, so it doesn't formally record recovering
/// it either. Net effect: a correct, balanced ledger with zero new accounts, at the cost
/// of the Kasbon balance itself living only in the HR module, not Finance's chart of
/// accounts. <see cref="OperatingExpenseId"/> links to that row — paying the salary
/// reuses the existing POST /api/expenses/{id}/payments endpoint unchanged, same accrual
/// pattern (Hutang Gaji then a separate "Bayar Gaji") as every other OperatingExpense.
/// </summary>
public class Payslip : Entity, IBranchScoped
{
    public required Guid TenantId { get; set; }
    public required Guid BranchId { get; set; }
    public required Guid EmployeeId { get; set; }
    public required int PeriodYear { get; set; }
    public required int PeriodMonth { get; set; }
    public required decimal BaseSalary { get; set; }
    public required int WorkingDaysInPeriod { get; set; }
    public required int AlphaDays { get; set; }
    public required decimal AttendanceDeduction { get; set; }
    public required decimal GrossPay { get; set; }
    public required decimal KasbonDeduction { get; set; }
    public required decimal NetPay { get; set; }
    public required Guid OperatingExpenseId { get; set; }
    public required Guid GeneratedByUserId { get; set; }
}
