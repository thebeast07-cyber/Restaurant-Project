using Restaurant.Domain.Common;

namespace Restaurant.Domain.HR;

/// <summary>
/// Present = clocked in (self-service or manual). Alpha = unexcused absence, the only
/// status that deducts pay at Payroll time. Izin/Sakit/Cuti = excused absence, paid in
/// full — confirmed as the common small-restaurant policy during HR design (2026-10-01),
/// not guessed.
/// </summary>
public enum AttendanceStatus
{
    Present,
    Alpha,
    Izin,
    Sakit,
    Cuti
}

public enum AttendanceSource
{
    SelfService,
    Manual
}

/// <summary>
/// One record per Employee per calendar Date (see AppDbContext for the unique
/// constraint). Present records are created by the Employee's own PIN at the kiosk
/// (Source = SelfService); every other status has no clock-in by definition, so it's
/// always Manager/Owner-entered (Source = Manual) — same path a Manager also uses to
/// correct a forgotten self-service clock-in.
/// </summary>
public class Attendance : Entity, IBranchScoped
{
    public required Guid TenantId { get; set; }
    public required Guid BranchId { get; set; }
    public required Guid EmployeeId { get; set; }
    public required DateOnly Date { get; set; }
    public AttendanceStatus Status { get; set; } = AttendanceStatus.Present;
    public DateTimeOffset? ClockInAt { get; set; }
    public DateTimeOffset? ClockOutAt { get; set; }
    public required AttendanceSource Source { get; set; }
    public Guid? RecordedByUserId { get; set; }
    public string? Notes { get; set; }
}
