import { useEffect, useState, type FormEvent } from "react";
import { useNavigate } from "react-router-dom";
import {
  clockIn,
  clockOut,
  getRoster,
  listAttendance,
  recordAttendance,
  type Attendance,
  type AttendanceStatus,
  type RosterEntry,
} from "../api/attendance";
import { useAuth } from "../context/AuthContext";
import { ApiError } from "../api/client";
import "./PurchasingShared.css";

const STATUS_LABEL: Record<AttendanceStatus, string> = {
  Present: "Hadir",
  Alpha: "Alpha",
  Izin: "Izin",
  Sakit: "Sakit",
  Cuti: "Cuti",
};

function todayIso(): string {
  return new Date().toISOString().slice(0, 10);
}

export function AttendancePage() {
  const navigate = useNavigate();
  const { user } = useAuth();
  const canManage = !!user && (user.role === "Owner" || user.role === "Manager");

  const [roster, setRoster] = useState<RosterEntry[]>([]);
  const [selectedEmployeeId, setSelectedEmployeeId] = useState("");
  const [pin, setPin] = useState("");
  const [kioskMessage, setKioskMessage] = useState<string | null>(null);
  const [kioskError, setKioskError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);

  const [records, setRecords] = useState<Attendance[]>([]);
  const [error, setError] = useState<string | null>(null);

  const [manualEmployeeId, setManualEmployeeId] = useState("");
  const [manualDate, setManualDate] = useState(todayIso());
  const [manualStatus, setManualStatus] = useState<AttendanceStatus>("Alpha");
  const [manualNotes, setManualNotes] = useState("");

  function handleError(err: unknown) {
    setError(err instanceof ApiError ? err.message : "Tidak bisa terhubung ke server.");
  }

  useEffect(() => {
    getRoster().then((entries) => {
      setRoster(entries);
      if (entries.length > 0) {
        setSelectedEmployeeId(entries[0].id);
        setManualEmployeeId(entries[0].id);
      }
    });
  }, []);

  function refreshRecords() {
    if (!canManage) return;
    listAttendance(undefined, undefined, undefined).then(setRecords).catch(handleError);
  }

  useEffect(() => {
    refreshRecords();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [canManage]);

  async function handleClockIn(event: FormEvent) {
    event.preventDefault();
    setKioskError(null);
    setKioskMessage(null);
    setIsSubmitting(true);
    try {
      await clockIn(selectedEmployeeId, pin);
      setKioskMessage("Berhasil clock-in.");
      setPin("");
      refreshRecords();
    } catch (err) {
      setKioskError(err instanceof ApiError ? err.message : "Tidak bisa terhubung ke server.");
    } finally {
      setIsSubmitting(false);
    }
  }

  async function handleClockOut(event: FormEvent) {
    event.preventDefault();
    setKioskError(null);
    setKioskMessage(null);
    setIsSubmitting(true);
    try {
      await clockOut(selectedEmployeeId, pin);
      setKioskMessage("Berhasil clock-out.");
      setPin("");
      refreshRecords();
    } catch (err) {
      setKioskError(err instanceof ApiError ? err.message : "Tidak bisa terhubung ke server.");
    } finally {
      setIsSubmitting(false);
    }
  }

  async function handleManualSubmit(event: FormEvent) {
    event.preventDefault();
    setError(null);
    try {
      await recordAttendance(manualEmployeeId, manualDate, manualStatus, manualNotes);
      setManualNotes("");
      refreshRecords();
    } catch (err) {
      handleError(err);
    }
  }

  return (
    <main className="purchasing-page">
      <header className="purchasing-header">
        <button type="button" className="purchasing-back" onClick={() => navigate("/hr")}>
          ← HR
        </button>
        <h1>Absensi</h1>
      </header>

      {/* ---------- Kiosk: self-service clock in/out ---------- */}
      <form className="purchasing-form" style={{ maxWidth: 480 }}>
        <h2>Clock In / Clock Out</h2>

        <label htmlFor="attEmployee">Nama</label>
        <select id="attEmployee" value={selectedEmployeeId} onChange={(event) => setSelectedEmployeeId(event.target.value)}>
          {roster.map((entry) => (
            <option key={entry.id} value={entry.id}>
              {entry.name} — {entry.position}
            </option>
          ))}
        </select>

        <label htmlFor="attPin">PIN</label>
        <input
          id="attPin"
          type="password"
          value={pin}
          onChange={(event) => setPin(event.target.value)}
          maxLength={6}
          required
        />

        {kioskError && (
          <p className="purchasing-error" role="alert">
            {kioskError}
          </p>
        )}
        {kioskMessage && <p className="purchasing-badge purchasing-badge--positive">{kioskMessage}</p>}

        <div className="purchasing-row-actions">
          <button type="button" disabled={isSubmitting || !selectedEmployeeId} onClick={handleClockIn}>
            Clock In
          </button>
          <button type="button" disabled={isSubmitting || !selectedEmployeeId} onClick={handleClockOut}>
            Clock Out
          </button>
        </div>
      </form>

      {canManage && (
        <>
          {error && (
            <p className="purchasing-error" role="alert">
              {error}
            </p>
          )}

          {/* ---------- Manager: manual entry/correction ---------- */}
          <form className="purchasing-form" onSubmit={handleManualSubmit} style={{ maxWidth: 480 }}>
            <h2>Koreksi / Catat Manual</h2>

            <label htmlFor="manualEmployee">Karyawan</label>
            <select id="manualEmployee" value={manualEmployeeId} onChange={(event) => setManualEmployeeId(event.target.value)}>
              {roster.map((entry) => (
                <option key={entry.id} value={entry.id}>
                  {entry.name}
                </option>
              ))}
            </select>

            <label htmlFor="manualDate">Tanggal</label>
            <input id="manualDate" type="date" value={manualDate} onChange={(event) => setManualDate(event.target.value)} required />

            <label htmlFor="manualStatus">Status</label>
            <select
              id="manualStatus"
              value={manualStatus}
              onChange={(event) => setManualStatus(event.target.value as AttendanceStatus)}
            >
              {(Object.keys(STATUS_LABEL) as AttendanceStatus[]).map((status) => (
                <option key={status} value={status}>
                  {STATUS_LABEL[status]}
                </option>
              ))}
            </select>

            <label htmlFor="manualNotes">Catatan</label>
            <input id="manualNotes" value={manualNotes} onChange={(event) => setManualNotes(event.target.value)} />

            <button type="submit">Simpan</button>
          </form>

          <table className="purchasing-table">
            <thead>
              <tr>
                <th>Tanggal</th>
                <th>Karyawan</th>
                <th>Status</th>
                <th>Clock In</th>
                <th>Clock Out</th>
                <th>Sumber</th>
              </tr>
            </thead>
            <tbody>
              {records.map((record) => (
                <tr key={record.id}>
                  <td>{record.date}</td>
                  <td>{record.employeeName}</td>
                  <td>
                    <span
                      className={`purchasing-badge ${record.status === "Present" ? "purchasing-badge--positive" : ""}`}
                    >
                      {STATUS_LABEL[record.status]}
                    </span>
                  </td>
                  <td>{record.clockInAt ? new Date(record.clockInAt).toLocaleTimeString("id-ID") : "-"}</td>
                  <td>{record.clockOutAt ? new Date(record.clockOutAt).toLocaleTimeString("id-ID") : "-"}</td>
                  <td>{record.source === "SelfService" ? "Mandiri" : "Manual"}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </>
      )}
    </main>
  );
}
