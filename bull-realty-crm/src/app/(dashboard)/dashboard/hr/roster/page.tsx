"use client";

import * as React from "react";
import { CalendarRange, ChevronLeft, ChevronRight } from "lucide-react";
import { toast } from "sonner";

import { PagePanel } from "@/components/shell/page-panel";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import {
  Sheet,
  SheetContent,
  SheetDescription,
  SheetHeader,
  SheetTitle,
} from "@/components/ui/sheet";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { ApiError } from "@/lib/api";
import { hrApi } from "@/lib/hrms-api";
import {
  shiftApi,
  type HrAttendanceRequest,
  type HrRoster,
  type HrShiftRequest,
} from "@/lib/hr-leave-api";

function iso(date: Date) {
  return date.toISOString().slice(0, 10);
}

function pretty(value: string) {
  return new Date(value).toLocaleDateString("en-IN", {
    weekday: "short",
    day: "numeric",
    month: "short",
    year: "numeric",
  });
}

/** Times arrive as "09:30:00"; only the first five characters are worth showing. */
function clock(value: string | null) {
  return value ? value.slice(0, 5) : "—";
}

const STATE_TONE: Record<string, string> = {
  Working: "",
  Leave: "text-amber-700 dark:text-amber-400",
  Holiday: "text-blue-700 dark:text-blue-400",
  "Weekly off": "text-muted-foreground",
};

/**
 * Who is on what, for a day — and the requests that change it.
 *
 * The roster falls back to each person's default shift where no assignment
 * covers the date, because that is what actually happens: most people are on
 * their usual shift most days, and a roster showing only the exceptions is one
 * nobody can staff a site from.
 */
export default function HrRosterPage() {
  const [date, setDate] = React.useState(() => iso(new Date()));
  const [roster, setRoster] = React.useState<HrRoster | null>(null);
  const [shifts, setShifts] = React.useState<{ id: number; name: string; weeklyOff: string }[]>([]);
  const [shiftRequests, setShiftRequests] = React.useState<HrShiftRequest[]>([]);
  const [attendanceRequests, setAttendanceRequests] = React.useState<HrAttendanceRequest[]>([]);

  const [picked, setPicked] = React.useState<Set<number>>(new Set());
  const [rosterOpen, setRosterOpen] = React.useState(false);
  const [form, setForm] = React.useState({ shiftId: "", fromDate: "", toDate: "", notes: "" });
  const [busy, setBusy] = React.useState(false);

  const load = React.useCallback((on: string) => {
    shiftApi.roster(on).then(setRoster).catch(() => setRoster(null));
  }, [setRoster]);

  const loadRequests = React.useCallback(() => {
    shiftApi.requests().then(setShiftRequests).catch(() => setShiftRequests([]));
    shiftApi
      .attendanceRequests()
      .then(setAttendanceRequests)
      .catch(() => setAttendanceRequests([]));
  }, [setShiftRequests, setAttendanceRequests]);

  React.useEffect(() => {
    load(date);
  }, [date, load]);

  React.useEffect(() => {
    hrApi.shifts().then(setShifts).catch(() => setShifts([]));
    loadRequests();
  }, [loadRequests]);

  function shiftDays(delta: number) {
    const next = new Date(date);
    next.setDate(next.getDate() + delta);
    setDate(iso(next));
  }

  function toggle(employeeId: number) {
    setPicked((current) => {
      const next = new Set(current);
      if (next.has(employeeId)) next.delete(employeeId);
      else next.add(employeeId);
      return next;
    });
  }

  async function rosterPicked() {
    if (!form.shiftId || !form.fromDate) {
      toast.error("Pick a shift and a start date.");
      return;
    }
    setBusy(true);
    try {
      const result = await shiftApi.assignMany({
        employeeIds: [...picked],
        shiftId: Number(form.shiftId),
        fromDate: form.fromDate,
        toDate: form.toDate || null,
        leadId: null,
        notes: form.notes || null,
      });
      if (result.alreadyAssigned.length > 0) {
        toast.warning(
          `Rostered ${result.assigned}. Already on a shift for those dates: ` +
            result.alreadyAssigned.join(", ")
        );
      } else {
        toast.success(`Rostered ${result.assigned}.`);
      }
      setRosterOpen(false);
      setPicked(new Set());
      load(date);
    } catch (error) {
      toast.error(error instanceof ApiError ? error.message : "Could not roster them.");
    } finally {
      setBusy(false);
    }
  }

  async function decideShift(id: number, approve: boolean) {
    try {
      await shiftApi.decideRequest(id, approve);
      toast.success(approve ? "Shift assigned." : "Request rejected.");
      loadRequests();
      load(date);
    } catch (error) {
      toast.error(error instanceof ApiError ? error.message : "Could not decide that.");
    }
  }

  async function decideAttendance(id: number, approve: boolean) {
    try {
      await shiftApi.decideAttendanceRequest(id, approve);
      toast.success(approve ? "Attendance marked." : "Request rejected.");
      loadRequests();
    } catch (error) {
      toast.error(error instanceof ApiError ? error.message : "Could not decide that.");
    }
  }

  const pendingShift = shiftRequests.filter((r) => r.status === "PendingManager");
  const pendingAttendance = attendanceRequests.filter((r) => r.status === "PendingManager");

  return (
    <PagePanel
      icon={CalendarRange}
      title="Roster"
      hint="Who is on which shift, for a day. Pick people to put a crew on one shift for an event week."
      actions={
        <div className="flex items-center gap-1.5">
          <Button size="icon" variant="outline" className="size-8" onClick={() => shiftDays(-1)}>
            <ChevronLeft className="size-4" />
          </Button>
          <Input
            type="date"
            className="h-8 w-[150px]"
            value={date}
            onChange={(e) => setDate(e.target.value)}
          />
          <Button size="icon" variant="outline" className="size-8" onClick={() => shiftDays(1)}>
            <ChevronRight className="size-4" />
          </Button>
          <Button
            size="sm"
            className="h-8"
            disabled={picked.size === 0}
            onClick={() => {
              setForm({ shiftId: "", fromDate: date, toDate: "", notes: "" });
              setRosterOpen(true);
            }}
          >
            Roster {picked.size > 0 ? picked.size : ""}
          </Button>
        </div>
      }
    >
      <Tabs defaultValue="day">
        <TabsList>
          <TabsTrigger value="day">The day</TabsTrigger>
          <TabsTrigger value="shift-requests">
            Shift requests
            {pendingShift.length > 0 ? (
              <Badge variant="secondary" className="ml-1.5 h-4 px-1 text-[10px]">
                {pendingShift.length}
              </Badge>
            ) : null}
          </TabsTrigger>
          <TabsTrigger value="attendance-requests">
            Attendance requests
            {pendingAttendance.length > 0 ? (
              <Badge variant="secondary" className="ml-1.5 h-4 px-1 text-[10px]">
                {pendingAttendance.length}
              </Badge>
            ) : null}
          </TabsTrigger>
        </TabsList>

        <TabsContent value="day" className="pt-3">
          {roster ? (
            <div className="mb-2 flex flex-wrap items-center gap-x-5 gap-y-1 rounded-md bg-muted/50 px-3 py-2 text-[12px]">
              <span className="font-medium">{pretty(roster.onDate)}</span>
              <span>
                <span className="text-muted-foreground">Working </span>
                <span className="font-medium tabular-nums">{roster.working}</span>
              </span>
              <span>
                <span className="text-muted-foreground">On leave </span>
                <span className="font-medium tabular-nums">{roster.onLeave}</span>
              </span>
              <span>
                <span className="text-muted-foreground">On a rostered shift </span>
                <span className="font-medium tabular-nums">{roster.onAssignment}</span>
              </span>
              {roster.isHoliday ? (
                <Badge variant="secondary" className="font-normal">
                  Company holiday
                </Badge>
              ) : null}
            </div>
          ) : null}

          <div className="overflow-x-auto rounded border">
            <table className="w-full text-left text-[13px]">
              <thead className="bg-muted/50 text-[11px] uppercase text-muted-foreground">
                <tr>
                  <th className="w-8 p-2" />
                  <th className="p-2 font-medium">Employee</th>
                  <th className="p-2 font-medium">Department</th>
                  <th className="p-2 font-medium">Shift</th>
                  <th className="p-2 font-medium">Hours</th>
                  <th className="p-2 font-medium">State</th>
                </tr>
              </thead>
              <tbody>
                {roster?.rows.map((r) => (
                  <tr key={r.employeeId} className="border-t hover:bg-muted/40">
                    <td className="p-2">
                      <input
                        type="checkbox"
                        className="size-3.5 align-middle"
                        checked={picked.has(r.employeeId)}
                        onChange={() => toggle(r.employeeId)}
                        aria-label={`Pick ${r.employeeName}`}
                      />
                    </td>
                    <td className="p-2">
                      {r.employeeName}
                      <span className="ml-1.5 text-[11px] text-muted-foreground">
                        {r.employeeCode}
                      </span>
                    </td>
                    <td className="p-2 text-muted-foreground">{r.department}</td>
                    <td className="p-2">
                      {r.shiftName}
                      {r.onAssignment ? (
                        <Badge variant="secondary" className="ml-1.5 font-normal">
                          rostered
                        </Badge>
                      ) : null}
                    </td>
                    <td className="p-2 tabular-nums text-muted-foreground">
                      {clock(r.startTime)} – {clock(r.endTime)}
                    </td>
                    <td className={"p-2 " + (STATE_TONE[r.state] ?? "")}>{r.state}</td>
                  </tr>
                ))}
                {!roster || roster.rows.length === 0 ? (
                  <tr>
                    <td colSpan={6} className="p-6 text-center text-[12px] text-muted-foreground">
                      Nobody on the rolls for this day.
                    </td>
                  </tr>
                ) : null}
              </tbody>
            </table>
          </div>
        </TabsContent>

        <TabsContent value="shift-requests" className="pt-3">
          <div className="overflow-x-auto rounded border">
            <table className="w-full text-left text-[13px]">
              <thead className="bg-muted/50 text-[11px] uppercase text-muted-foreground">
                <tr>
                  <th className="p-2 font-medium">Employee</th>
                  <th className="p-2 font-medium">Shift</th>
                  <th className="p-2 font-medium">From</th>
                  <th className="p-2 font-medium">To</th>
                  <th className="p-2 font-medium">Reason</th>
                  <th className="p-2 font-medium">Status</th>
                  <th className="p-2" />
                </tr>
              </thead>
              <tbody>
                {shiftRequests.map((r) => (
                  <tr key={r.id} className="border-t">
                    <td className="p-2">{r.employeeName}</td>
                    <td className="p-2">{r.shiftName}</td>
                    <td className="p-2">{pretty(r.fromDate)}</td>
                    <td className="p-2">{r.toDate ? pretty(r.toDate) : "Open-ended"}</td>
                    <td className="p-2 text-muted-foreground">{r.reason ?? "—"}</td>
                    <td className="p-2">
                      <Badge variant="secondary" className="font-normal">
                        {r.status}
                      </Badge>
                    </td>
                    <td className="p-2 text-right">
                      {r.status === "PendingManager" ? (
                        <span className="flex justify-end gap-1">
                          <Button
                            size="sm"
                            variant="outline"
                            className="h-6 text-[11px]"
                            onClick={() => void decideShift(r.id, true)}
                          >
                            Approve
                          </Button>
                          <Button
                            size="sm"
                            variant="ghost"
                            className="h-6 text-[11px]"
                            onClick={() => void decideShift(r.id, false)}
                          >
                            Reject
                          </Button>
                        </span>
                      ) : null}
                    </td>
                  </tr>
                ))}
                {shiftRequests.length === 0 ? (
                  <tr>
                    <td colSpan={7} className="p-6 text-center text-[12px] text-muted-foreground">
                      No shift requests.
                    </td>
                  </tr>
                ) : null}
              </tbody>
            </table>
          </div>
        </TabsContent>

        <TabsContent value="attendance-requests" className="pt-3">
          <p className="mb-2 text-[12px] text-muted-foreground">
            Days the punch machine never saw — a week at a venue, a recce in another city, a
            load-out that ran past midnight. Without these, payroll reads the silence as absence.
          </p>
          <div className="overflow-x-auto rounded border">
            <table className="w-full text-left text-[13px]">
              <thead className="bg-muted/50 text-[11px] uppercase text-muted-foreground">
                <tr>
                  <th className="p-2 font-medium">Employee</th>
                  <th className="p-2 font-medium">From</th>
                  <th className="p-2 font-medium">To</th>
                  <th className="p-2 text-right font-medium">Days</th>
                  <th className="p-2 font-medium">Mark as</th>
                  <th className="p-2 font-medium">Reason</th>
                  <th className="p-2 font-medium">Status</th>
                  <th className="p-2" />
                </tr>
              </thead>
              <tbody>
                {attendanceRequests.map((r) => (
                  <tr key={r.id} className="border-t">
                    <td className="p-2">{r.employeeName}</td>
                    <td className="p-2">{pretty(r.fromDate)}</td>
                    <td className="p-2">{pretty(r.toDate)}</td>
                    <td className="p-2 text-right tabular-nums">{r.dayCount}</td>
                    <td className="p-2">{r.requestedStatus}</td>
                    <td className="p-2 text-muted-foreground">{r.reason ?? "—"}</td>
                    <td className="p-2">
                      <Badge variant="secondary" className="font-normal">
                        {r.status}
                      </Badge>
                    </td>
                    <td className="p-2 text-right">
                      {r.status === "PendingManager" ? (
                        <span className="flex justify-end gap-1">
                          <Button
                            size="sm"
                            variant="outline"
                            className="h-6 text-[11px]"
                            onClick={() => void decideAttendance(r.id, true)}
                          >
                            Approve
                          </Button>
                          <Button
                            size="sm"
                            variant="ghost"
                            className="h-6 text-[11px]"
                            onClick={() => void decideAttendance(r.id, false)}
                          >
                            Reject
                          </Button>
                        </span>
                      ) : null}
                    </td>
                  </tr>
                ))}
                {attendanceRequests.length === 0 ? (
                  <tr>
                    <td colSpan={8} className="p-6 text-center text-[12px] text-muted-foreground">
                      No attendance requests.
                    </td>
                  </tr>
                ) : null}
              </tbody>
            </table>
          </div>
        </TabsContent>
      </Tabs>

      {/* ---------------- roster the picked crew ---------------- */}

      <Sheet open={rosterOpen} onOpenChange={setRosterOpen}>
        <SheetContent className="w-full sm:max-w-md">
          <SheetHeader>
            <SheetTitle>Roster {picked.size} onto a shift</SheetTitle>
            <SheetDescription>
              Anybody already on a shift for those dates is reported back rather than
              overwritten.
            </SheetDescription>
          </SheetHeader>
          <div className="space-y-3 px-4 pb-8">
            <div className="space-y-1">
              <Label className="text-[12px]">Shift</Label>
              <select
                className="h-9 w-full rounded-md border bg-background px-2 text-[13px]"
                value={form.shiftId}
                onChange={(e) => setForm({ ...form, shiftId: e.target.value })}
              >
                <option value="">Pick a shift…</option>
                {shifts.map((s) => (
                  <option key={s.id} value={s.id}>
                    {s.name}
                  </option>
                ))}
              </select>
            </div>
            <div className="grid grid-cols-2 gap-2">
              <div className="space-y-1">
                <Label className="text-[12px]">From</Label>
                <Input
                  type="date"
                  className="h-9"
                  value={form.fromDate}
                  onChange={(e) => setForm({ ...form, fromDate: e.target.value })}
                />
              </div>
              <div className="space-y-1">
                <Label className="text-[12px]">To</Label>
                <Input
                  type="date"
                  className="h-9"
                  value={form.toDate}
                  onChange={(e) => setForm({ ...form, toDate: e.target.value })}
                />
                <p className="text-[11px] text-muted-foreground">Blank runs until ended.</p>
              </div>
            </div>
            <div className="space-y-1">
              <Label className="text-[12px]">Note</Label>
              <Input
                className="h-9"
                placeholder="Sharma wedding, Udaipur"
                value={form.notes}
                onChange={(e) => setForm({ ...form, notes: e.target.value })}
              />
            </div>
            <Button className="w-full" disabled={busy} onClick={() => void rosterPicked()}>
              {busy ? "Rostering…" : `Roster ${picked.size}`}
            </Button>
          </div>
        </SheetContent>
      </Sheet>
    </PagePanel>
  );
}
