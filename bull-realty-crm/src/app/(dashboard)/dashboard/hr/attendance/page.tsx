"use client";

import * as React from "react";
import { CalendarClock } from "lucide-react";
import { toast } from "sonner";

import { PagePanel } from "@/components/shell/page-panel";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { ApiError } from "@/lib/api";
import { humanise } from "@/lib/crm-api";
import { hrApi, hrEmployeesApi, hrx, type HrEmployee } from "@/lib/hrms-api";

export default function HrAttendancePage() {
  const [rows, setRows] = React.useState<
    { id: number; employeeName: string; workDate: string; inTime: string | null; outTime: string | null; status: string; isLate: boolean; employeeId: number }[]
  >([]);
  const [corrections, setCorrections] = React.useState<
    { id: number; employeeName: string; workDate: string; reason: string; status: string }[]
  >([]);
  const [people, setPeople] = React.useState<HrEmployee[]>([]);
  const [employeeId, setEmployeeId] = React.useState("");
  const [date, setDate] = React.useState(() => new Date().toISOString().slice(0, 10));
  const [status, setStatus] = React.useState("Present");

  const load = React.useCallback(() => {
    hrApi.attendance().then(setRows).catch(() => setRows([]));
    hrApi.corrections().then(setCorrections).catch(() => setCorrections([]));
  }, []);

  React.useEffect(() => {
    load();
    hrEmployeesApi
      .query({ page: 1, pageSize: 200, sort: [], filter: null })
      .then((r) => {
        setPeople(r.items);
        if (r.items[0]) setEmployeeId(String(r.items[0].id));
      })
      .catch(() => setPeople([]));
  }, [load]);

  async function mark() {
    if (!employeeId) return;
    try {
      await hrApi.markAttendance({
        employeeId: Number(employeeId),
        workDate: date,
        status,
        inTime: status === "Present" || status === "Late" ? "09:35:00" : null,
        outTime: status === "Present" || status === "Late" ? "18:30:00" : null,
      });
      toast.success("Attendance saved");
      load();
    } catch (error) {
      toast.error(error instanceof ApiError ? error.message : "Could not mark.");
    }
  }

  return (
    <PagePanel
      icon={CalendarClock}
      title="Attendance"
      hint="Daily marks, geo punches (Frappe check-in) and regularisation. Muster feeds payroll."
    >
      <div className="mb-4 flex flex-wrap items-end gap-2">
        <select
          className="h-9 rounded-md border bg-background px-2 text-sm"
          value={employeeId}
          onChange={(e) => setEmployeeId(e.target.value)}
        >
          {people.map((p) => (
            <option key={p.id} value={p.id}>
              {p.employeeCode} · {p.name}
            </option>
          ))}
        </select>
        <Input type="date" className="h-9 w-40" value={date} onChange={(e) => setDate(e.target.value)} />
        <select
          className="h-9 rounded-md border bg-background px-2 text-sm"
          value={status}
          onChange={(e) => setStatus(e.target.value)}
        >
          {["Present", "Absent", "Late", "WorkFromHome", "OnDuty", "Holiday", "WeeklyOff", "Leave"].map(
            (s) => (
              <option key={s} value={s}>
                {humanise(s)}
              </option>
            )
          )}
        </select>
        <Button size="sm" className="h-9" onClick={() => void mark()}>
          Mark
        </Button>
        <Button
          size="sm"
          variant="outline"
          className="h-9"
          onClick={() =>
            void (async () => {
              let latitude: number | undefined;
              let longitude: number | undefined;
              if (navigator.geolocation) {
                try {
                  const pos = await new Promise<GeolocationPosition>((resolve, reject) =>
                    navigator.geolocation.getCurrentPosition(resolve, reject, { timeout: 8000 })
                  );
                  latitude = pos.coords.latitude;
                  longitude = pos.coords.longitude;
                } catch {
                  /* punch still records without GPS */
                }
              }
              try {
                await hrx.punch({
                  employeeId: employeeId ? Number(employeeId) : undefined,
                  kind: "In",
                  latitude,
                  longitude,
                  device: "Web",
                });
                toast.success(
                  latitude != null
                    ? `Checked in at ${latitude.toFixed(4)}, ${longitude!.toFixed(4)}`
                    : "Checked in (no GPS)"
                );
                load();
              } catch (error) {
                toast.error(error instanceof ApiError ? error.message : "Punch failed");
              }
            })()
          }
        >
          Geo check-in
        </Button>
        <Button
          size="sm"
          variant="outline"
          className="h-9"
          onClick={() =>
            void hrx
              .punch({ employeeId: employeeId ? Number(employeeId) : undefined, kind: "Out", device: "Web" })
              .then(() => {
                toast.success("Checked out");
                load();
              })
          }
        >
          Check-out
        </Button>
      </div>
      <div className="overflow-x-auto rounded border">
        <table className="w-full text-left text-[13px]">
          <thead className="bg-muted/50 text-[11px] uppercase text-muted-foreground">
            <tr>
              <th className="p-2">Date</th>
              <th className="p-2">Employee</th>
              <th className="p-2">In</th>
              <th className="p-2">Out</th>
              <th className="p-2">Status</th>
            </tr>
          </thead>
          <tbody>
            {rows.map((r) => (
              <tr key={r.id} className="border-t">
                <td className="p-2 tabular-nums">{String(r.workDate).slice(0, 10)}</td>
                <td className="p-2">{r.employeeName}</td>
                <td className="p-2">{r.inTime ?? "—"}</td>
                <td className="p-2">{r.outTime ?? "—"}</td>
                <td className="p-2">
                  {humanise(r.status)}
                  {r.isLate ? " · late" : ""}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      <h2 className="mt-6 text-[13px] font-semibold">Correction requests</h2>
      <ul className="mt-2 space-y-2">
        {corrections.map((c) => (
          <li key={c.id} className="flex flex-wrap items-center justify-between gap-2 rounded border p-2 text-[13px]">
            <span>
              {c.employeeName} · {String(c.workDate).slice(0, 10)} · {c.reason} · {humanise(c.status)}
            </span>
            {c.status.includes("Pending") ? (
              <span className="flex gap-1">
                <Button
                  size="sm"
                  variant="outline"
                  className="h-7"
                  onClick={() => void hrApi.decideCorrection(c.id, true).then(load)}
                >
                  Approve
                </Button>
                <Button
                  size="sm"
                  variant="outline"
                  className="h-7"
                  onClick={() => void hrApi.decideCorrection(c.id, false).then(load)}
                >
                  Reject
                </Button>
              </span>
            ) : null}
          </li>
        ))}
      </ul>
    </PagePanel>
  );
}
