import { apiFetch } from "./client";

export type AttendanceStatus = "Present" | "Alpha" | "Izin" | "Sakit" | "Cuti";
export type AttendanceSource = "SelfService" | "Manual";

export interface RosterEntry {
  id: string;
  name: string;
  position: string;
}

export interface Attendance {
  id: string;
  employeeId: string;
  employeeName: string;
  date: string;
  status: AttendanceStatus;
  clockInAt: string | null;
  clockOutAt: string | null;
  source: AttendanceSource;
  notes: string | null;
}

export function getRoster(): Promise<RosterEntry[]> {
  return apiFetch<RosterEntry[]>("/api/attendance/roster");
}

export function clockIn(employeeId: string, pin: string): Promise<Attendance> {
  return apiFetch<Attendance>("/api/attendance/clock-in", {
    method: "POST",
    body: JSON.stringify({ employeeId, pin }),
  });
}

export function clockOut(employeeId: string, pin: string): Promise<Attendance> {
  return apiFetch<Attendance>("/api/attendance/clock-out", {
    method: "POST",
    body: JSON.stringify({ employeeId, pin }),
  });
}

export function listAttendance(employeeId?: string, from?: string, to?: string): Promise<Attendance[]> {
  const params = new URLSearchParams();
  if (employeeId) params.set("employeeId", employeeId);
  if (from) params.set("from", from);
  if (to) params.set("to", to);
  const qs = params.toString();
  return apiFetch<Attendance[]>(`/api/attendance${qs ? `?${qs}` : ""}`);
}

export function recordAttendance(
  employeeId: string,
  date: string,
  status: AttendanceStatus,
  notes?: string,
): Promise<Attendance> {
  return apiFetch<Attendance>("/api/attendance", {
    method: "POST",
    body: JSON.stringify({ employeeId, date, status, notes: notes || null }),
  });
}
