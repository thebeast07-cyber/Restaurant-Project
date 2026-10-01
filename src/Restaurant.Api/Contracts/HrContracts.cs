using Restaurant.Domain.Finance;
using Restaurant.Domain.HR;

namespace Restaurant.Api.Contracts;

public record CreateEmployeeRequest(string Name, string Position, decimal BaseSalary, DateOnly HireDate, Guid? UserId, string? Pin);

public record UpdateEmployeeRequest(string Name, string Position, decimal BaseSalary, bool IsActive, Guid? UserId);

public record SetEmployeePinRequest(string Pin);

public record EmployeeResponse(
    Guid Id, string Name, string Position, decimal BaseSalary, DateOnly HireDate, bool IsActive, Guid? UserId, bool HasPin);

public record ClockInRequest(Guid EmployeeId, string Pin);

public record ClockOutRequest(Guid EmployeeId, string Pin);

public record RecordAttendanceRequest(Guid EmployeeId, DateOnly Date, AttendanceStatus Status, string? Notes);

public record AttendanceResponse(
    Guid Id, Guid EmployeeId, string EmployeeName, DateOnly Date, AttendanceStatus Status,
    DateTimeOffset? ClockInAt, DateTimeOffset? ClockOutAt, AttendanceSource Source, string? Notes);

public record CreateKasbonRequest(Guid EmployeeId, decimal Amount, int InstallmentCount, string? Notes);

public record KasbonResponse(
    Guid Id, Guid EmployeeId, string EmployeeName, decimal Amount, int InstallmentCount,
    KasbonStatus Status, decimal AmountRepaid, decimal RemainingBalance, string? Notes);

public record RunPayrollRequest(Guid EmployeeId, int PeriodYear, int PeriodMonth);

public record PayslipResponse(
    Guid Id, Guid EmployeeId, string EmployeeName, int PeriodYear, int PeriodMonth,
    decimal BaseSalary, int WorkingDaysInPeriod, int AlphaDays, decimal AttendanceDeduction,
    decimal GrossPay, decimal KasbonDeduction, decimal NetPay,
    Guid OperatingExpenseId, decimal AmountPaid, decimal RemainingBalance, ExpensePaymentStatus PaymentStatus);
