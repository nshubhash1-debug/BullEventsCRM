"use client";

import * as React from "react";
import { ChevronLeft, ChevronRight, Plane, Timer } from "lucide-react";
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
import { ApiError, apiRequest } from "@/lib/api";
import { formatMoney } from "@/lib/crm-api";
import {
  workplaceApi,
  TRAVEL_MODES,
  type HrEventHours,
  type HrTimesheet,
  type HrTravel,
} from "@/lib/hr-workplace-api";

/** The Monday of the week a date falls in. */
function mondayOf(date: Date) {
  const copy = new Date(date);
  copy.setDate(copy.getDate() - ((copy.getDay() + 6) % 7));
  return copy;
}

function iso(date: Date) {
  return date.toISOString().slice(0, 10);
}

function shortDay(value: string) {
  return new Date(value).toLocaleDateString("en-IN", { weekday: "short", day: "numeric" });
}

function day(value: string | null) {
  if (!value) return "—";
  return new Date(value).toLocaleDateString("en-IN", {
    day: "numeric",
    month: "short",
    year: "numeric",
  });
}

const ACTIVITIES = [
  "Load-in", "Rehearsal", "Event day", "Load-out",
  "Workshop", "Recce", "Office", "Travel",
];

const TRAVEL_TONE: Record<string, string> = {
  PendingApproval: "bg-amber-500/10 text-amber-700 dark:text-amber-300",
  Approved: "bg-emerald-500/10 text-emerald-700 dark:text-emerald-300",
  Rejected: "bg-red-500/10 text-red-700 dark:text-red-300",
  Completed: "bg-blue-500/10 text-blue-700 dark:text-blue-300",
  Cancelled: "bg-zinc-500/10 text-zinc-700 dark:text-zinc-300",
};

interface DraftLine {
  onDate: string;
  leadId: string;
  activity: string;
  hours: string;
}

/**
 * Which job somebody's hours belong to, and where the company sent them.
 *
 * Attendance says the crew were at work. This says which event to charge it to,
 * and those are different questions — a wedding that took four crew three days
 * longer than quoted looks fine on attendance and terrible here.
 */
export default function HrTimesheetsPage() {
  const [week, setWeek] = React.useState(() => mondayOf(new Date()));
  const [sheets, setSheets] = React.useState<HrTimesheet[]>([]);
  const [byEvent, setByEvent] = React.useState<HrEventHours[]>([]);
  const [travel, setTravel] = React.useState<HrTravel[]>([]);
  const [employees, setEmployees] = React.useState<{ id: number; name: string }[]>([]);
  const [busy, setBusy] = React.useState(false);

  const [entryOpen, setEntryOpen] = React.useState(false);
  const [entryFor, setEntryFor] = React.useState("");
  const [lines, setLines] = React.useState<DraftLine[]>([]);

  const [tripOpen, setTripOpen] = React.useState(false);
  const [tripForm, setTripForm] = React.useState({
    employeeId: "",
    purpose: "",
    destination: "",
    fromDate: "",
    toDate: "",
    estimatedCost: "",
    advanceRequested: "",
  });

  const load = React.useCallback(() => {
    workplaceApi.timesheets().then(setSheets).catch(() => setSheets([]));
    workplaceApi.hoursByEvent().then(setByEvent).catch(() => setByEvent([]));
    workplaceApi.travel().then(setTravel).catch(() => setTravel([]));
  }, [setSheets, setByEvent, setTravel]);

  React.useEffect(() => {
    load();
    apiRequest<{ items: { id: number; name: string }[] }>("/api/hr/employees/query", {
      method: "POST",
      body: JSON.stringify({ page: 1, pageSize: 300 }),
      auth: true,
    })
      .then((page) => setEmployees(page.items))
      .catch(() => setEmployees([]));
  }, [load]);

  function shiftWeek(delta: number) {
    const next = new Date(week);
    next.setDate(next.getDate() + delta * 7);
    setWeek(mondayOf(next));
  }

  function openEntry() {
    setEntryFor("");
    setLines([
      { onDate: iso(week), leadId: "", activity: "Load-in", hours: "" },
    ]);
    setEntryOpen(true);
  }

  async function saveSheet(submit: boolean) {
    if (!entryFor) {
      toast.error("Pick whose week this is.");
      return;
    }

    const filled = lines.filter((l) => Number(l.hours) > 0);
    if (filled.length === 0) {
      toast.error("Put some hours in.");
      return;
    }

    setBusy(true);
    try {
      await workplaceApi.saveTimesheet({
        employeeId: Number(entryFor),
        weekStarting: iso(week),
        submit,
        lines: filled.map((l) => ({
          onDate: l.onDate,
          leadId: l.leadId ? Number(l.leadId) : null,
          activity: l.activity,
          hours: Number(l.hours),
          isBillable: Boolean(l.leadId),
          notes: null,
        })),
      });
      toast.success(submit ? "Submitted." : "Saved as a draft.");
      setEntryOpen(false);
      load();
    } catch (error) {
      toast.error(error instanceof ApiError ? error.message : "Could not save that.");
    } finally {
      setBusy(false);
    }
  }

  async function decide(sheet: HrTimesheet, approve: boolean) {
    const note = approve ? undefined : window.prompt("Why is it going back?")?.trim();
    if (!approve && !note) return;

    try {
      await workplaceApi.decideTimesheet(sheet.id, approve, note);
      toast.success(approve ? "Approved." : "Sent back.");
      load();
    } catch (error) {
      toast.error(error instanceof ApiError ? error.message : "Could not do that.");
    }
  }

  async function requestTrip() {
    setBusy(true);
    try {
      await workplaceApi.requestTravel({
        employeeId: Number(tripForm.employeeId),
        purpose: tripForm.purpose,
        leadId: null,
        fromDate: tripForm.fromDate,
        toDate: tripForm.toDate,
        destination: tripForm.destination || null,
        estimatedCost: Number(tripForm.estimatedCost || 0),
        advanceRequested: Number(tripForm.advanceRequested || 0),
        legs: null,
      });
      toast.success("Raised for approval.");
      setTripOpen(false);
      load();
    } catch (error) {
      toast.error(error instanceof ApiError ? error.message : "Could not raise that.");
    } finally {
      setBusy(false);
    }
  }

  async function decideTrip(trip: HrTravel, approve: boolean) {
    const note = approve
      ? undefined
      : window.prompt("Why? Somebody has planned around this.")?.trim();
    if (!approve && !note) return;

    try {
      await workplaceApi.decideTravel(trip.id, approve, undefined, note);
      toast.success(approve ? "Approved." : "Refused.");
      load();
    } catch (error) {
      toast.error(error instanceof ApiError ? error.message : "Could not do that.");
    }
  }

  const thisWeek = sheets.filter((s) => s.weekStarting.slice(0, 10) === iso(week));
  const pendingSheets = sheets.filter((s) => s.status === "Submitted");
  const pendingTrips = travel.filter((t) => t.status === "PendingApproval");

  return (
    <PagePanel
      icon={Timer}
      title="Timesheets and travel"
      hint="Which event the hours belong to, and where the company sent people. Attendance says they were at work; this says which job it cost."
      actions={
        <div className="flex items-center gap-1.5">
          <Button size="icon" variant="outline" className="size-8" onClick={() => shiftWeek(-1)}>
            <ChevronLeft className="size-4" />
          </Button>
          <span className="w-[150px] text-center text-[12px] tabular-nums">
            {day(iso(week))}
          </span>
          <Button size="icon" variant="outline" className="size-8" onClick={() => shiftWeek(1)}>
            <ChevronRight className="size-4" />
          </Button>
          <Button size="sm" variant="outline" className="h-8" onClick={() => setTripOpen(true)}>
            <Plane className="mr-1.5 size-3.5" />
            Trip
          </Button>
          <Button size="sm" className="h-8" onClick={openEntry}>
            Enter a week
          </Button>
        </div>
      }
    >
      <Tabs defaultValue="week">
        <TabsList>
          <TabsTrigger value="week">
            The week
            {pendingSheets.length > 0 ? (
              <Badge variant="secondary" className="ml-1.5 h-4 px-1 text-[10px]">
                {pendingSheets.length}
              </Badge>
            ) : null}
          </TabsTrigger>
          <TabsTrigger value="events">
            Hours by event
            <span className="ml-1.5 text-[11px] text-muted-foreground">{byEvent.length}</span>
          </TabsTrigger>
          <TabsTrigger value="travel">
            Travel
            {pendingTrips.length > 0 ? (
              <Badge variant="secondary" className="ml-1.5 h-4 px-1 text-[10px]">
                {pendingTrips.length}
              </Badge>
            ) : null}
          </TabsTrigger>
        </TabsList>

        <TabsContent value="week" className="pt-3">
          <div className="overflow-x-auto rounded border">
            <table className="w-full text-left text-[13px]">
              <thead className="bg-muted/50 text-[11px] uppercase text-muted-foreground">
                <tr>
                  <th className="p-2 font-medium">Employee</th>
                  <th className="p-2 font-medium">Week</th>
                  <th className="p-2 text-right font-medium">Hours</th>
                  <th className="p-2 text-right font-medium">Billable</th>
                  <th className="p-2 text-right font-medium">Share</th>
                  <th className="p-2 font-medium">Status</th>
                  <th className="p-2" />
                </tr>
              </thead>
              <tbody>
                {(thisWeek.length > 0 ? thisWeek : sheets).map((s) => (
                  <tr key={s.id} className="border-t">
                    <td className="p-2">{s.employeeName}</td>
                    <td className="p-2 text-muted-foreground">{day(s.weekStarting)}</td>
                    <td className="p-2 text-right tabular-nums">{s.totalHours}</td>
                    <td className="p-2 text-right tabular-nums">{s.billableHours}</td>
                    <td
                      className={
                        "p-2 text-right font-medium tabular-nums " +
                        (s.billablePercent < 60 ? "text-amber-700 dark:text-amber-400" : "")
                      }
                    >
                      {s.billablePercent}%
                    </td>
                    <td className="p-2">
                      <Badge variant="secondary" className="font-normal">
                        {s.status}
                      </Badge>
                    </td>
                    <td className="p-2 text-right">
                      {s.status === "Submitted" ? (
                        <span className="flex justify-end gap-1">
                          <Button
                            size="sm"
                            variant="outline"
                            className="h-6 text-[11px]"
                            onClick={() => void decide(s, true)}
                          >
                            Approve
                          </Button>
                          <Button
                            size="sm"
                            variant="ghost"
                            className="h-6 text-[11px]"
                            onClick={() => void decide(s, false)}
                          >
                            Send back
                          </Button>
                        </span>
                      ) : null}
                    </td>
                  </tr>
                ))}
                {sheets.length === 0 ? (
                  <tr>
                    <td colSpan={7} className="p-6 text-center text-[12px] text-muted-foreground">
                      No timesheets yet.
                    </td>
                  </tr>
                ) : null}
              </tbody>
            </table>
          </div>
        </TabsContent>

        <TabsContent value="events" className="pt-3">
          <p className="mb-2 text-[12px] text-muted-foreground">
            Approved hours only. This is the number that says whether an event made money.
          </p>
          <div className="grid gap-3 md:grid-cols-2 xl:grid-cols-3">
            {byEvent.map((e) => (
              <div key={e.leadId} className="rounded border p-3">
                <div className="flex items-baseline justify-between gap-2">
                  <h3 className="text-[13px] font-medium">Event #{e.leadId}</h3>
                  <span className="text-[11px] text-muted-foreground">{e.people} people</span>
                </div>
                <div className="mt-0.5 text-[12px]">
                  <span className="font-medium tabular-nums">{e.totalHours}</span> hours,{" "}
                  <span className="tabular-nums">{e.billableHours}</span> billable
                </div>
                <table className="mt-2 w-full text-[12px]">
                  <tbody>
                    {e.byActivity.map((a) => (
                      <tr key={a.activity} className="border-b last:border-0">
                        <td className="py-1 text-muted-foreground">{a.activity}</td>
                        <td className="py-1 text-right tabular-nums">{a.hours}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            ))}
            {byEvent.length === 0 ? (
              <p className="rounded border border-dashed p-6 text-center text-[12px] text-muted-foreground">
                Nothing approved yet.
              </p>
            ) : null}
          </div>
        </TabsContent>

        <TabsContent value="travel" className="pt-3">
          <div className="overflow-x-auto rounded border">
            <table className="w-full text-left text-[13px]">
              <thead className="bg-muted/50 text-[11px] uppercase text-muted-foreground">
                <tr>
                  <th className="p-2 font-medium">Who</th>
                  <th className="p-2 font-medium">Where</th>
                  <th className="p-2 font-medium">When</th>
                  <th className="p-2 text-right font-medium">Cost</th>
                  <th className="p-2 text-right font-medium">Advance</th>
                  <th className="p-2 font-medium">Status</th>
                  <th className="p-2" />
                </tr>
              </thead>
              <tbody>
                {travel.map((r) => (
                  <tr key={r.id} className="border-t">
                    <td className="p-2">{r.employeeName}</td>
                    <td className="p-2">
                      {r.destination ?? "—"}
                      <div className="text-[11px] text-muted-foreground">{r.purpose}</div>
                    </td>
                    <td className="p-2 text-muted-foreground">
                      {day(r.fromDate)} — {day(r.toDate)}
                      <div className="text-[11px]">{r.nights} nights</div>
                    </td>
                    <td className="p-2 text-right tabular-nums">
                      {formatMoney(r.estimatedCost)}
                    </td>
                    <td className="p-2 text-right tabular-nums">
                      {r.advancePaid > 0 ? formatMoney(r.advancePaid) : "—"}
                      {r.advanceRequested !== r.advancePaid && r.advanceRequested > 0 ? (
                        <div className="text-[11px] text-muted-foreground">
                          asked {formatMoney(r.advanceRequested)}
                        </div>
                      ) : null}
                    </td>
                    <td className="p-2">
                      <span
                        className={
                          "rounded px-1.5 py-0.5 text-[10.5px] font-medium " +
                          (TRAVEL_TONE[r.status] ?? TRAVEL_TONE.Cancelled)
                        }
                      >
                        {r.status}
                      </span>
                      {r.decisionNote ? (
                        <div className="mt-0.5 text-[11px] text-muted-foreground">
                          {r.decisionNote}
                        </div>
                      ) : null}
                    </td>
                    <td className="p-2 text-right">
                      {r.status === "PendingApproval" ? (
                        <span className="flex justify-end gap-1">
                          <Button
                            size="sm"
                            variant="outline"
                            className="h-6 text-[11px]"
                            onClick={() => void decideTrip(r, true)}
                          >
                            Approve
                          </Button>
                          <Button
                            size="sm"
                            variant="ghost"
                            className="h-6 text-[11px]"
                            onClick={() => void decideTrip(r, false)}
                          >
                            Refuse
                          </Button>
                        </span>
                      ) : null}
                    </td>
                  </tr>
                ))}
                {travel.length === 0 ? (
                  <tr>
                    <td colSpan={7} className="p-6 text-center text-[12px] text-muted-foreground">
                      Nobody is travelling.
                    </td>
                  </tr>
                ) : null}
              </tbody>
            </table>
          </div>
        </TabsContent>
      </Tabs>

      {/* ---------------- enter a week ---------------- */}

      <Sheet open={entryOpen} onOpenChange={setEntryOpen}>
        <SheetContent className="w-full overflow-y-auto sm:max-w-lg">
          <SheetHeader>
            <SheetTitle>Week of {day(iso(week))}</SheetTitle>
            <SheetDescription>
              A line with no event is overhead and cannot be billable, however it is entered.
            </SheetDescription>
          </SheetHeader>
          <div className="space-y-3 px-4 pb-8">
            <div className="space-y-1">
              <Label className="text-[12px]">Whose week</Label>
              <select
                className="h-9 w-full rounded-md border bg-background px-2 text-[13px]"
                value={entryFor}
                onChange={(e) => setEntryFor(e.target.value)}
              >
                <option value="">Pick someone…</option>
                {employees.map((e) => (
                  <option key={e.id} value={e.id}>
                    {e.name}
                  </option>
                ))}
              </select>
            </div>

            <div className="space-y-1.5">
              {lines.map((line, index) => (
                <div key={index} className="grid grid-cols-[90px_70px_1fr_60px] gap-1.5">
                  <select
                    className="h-9 rounded-md border bg-background px-1.5 text-[12px]"
                    value={line.onDate}
                    onChange={(e) => {
                      const next = [...lines];
                      next[index] = { ...line, onDate: e.target.value };
                      setLines(next);
                    }}
                  >
                    {Array.from({ length: 7 }, (_, i) => {
                      const date = new Date(week);
                      date.setDate(date.getDate() + i);
                      return (
                        <option key={i} value={iso(date)}>
                          {shortDay(iso(date))}
                        </option>
                      );
                    })}
                  </select>
                  <Input
                    className="h-9 text-[12px] tabular-nums"
                    placeholder="Event"
                    inputMode="numeric"
                    value={line.leadId}
                    onChange={(e) => {
                      const next = [...lines];
                      next[index] = { ...line, leadId: e.target.value };
                      setLines(next);
                    }}
                  />
                  <select
                    className="h-9 rounded-md border bg-background px-1.5 text-[12px]"
                    value={line.activity}
                    onChange={(e) => {
                      const next = [...lines];
                      next[index] = { ...line, activity: e.target.value };
                      setLines(next);
                    }}
                  >
                    {ACTIVITIES.map((a) => (
                      <option key={a} value={a}>
                        {a}
                      </option>
                    ))}
                  </select>
                  <Input
                    className="h-9 text-[12px] tabular-nums"
                    placeholder="hrs"
                    inputMode="decimal"
                    value={line.hours}
                    onChange={(e) => {
                      const next = [...lines];
                      next[index] = { ...line, hours: e.target.value };
                      setLines(next);
                    }}
                  />
                </div>
              ))}
            </div>

            <Button
              variant="outline"
              size="sm"
              className="w-full"
              onClick={() =>
                setLines([
                  ...lines,
                  { onDate: iso(week), leadId: "", activity: "Event day", hours: "" },
                ])
              }
            >
              Add a line
            </Button>

            <div className="rounded-md bg-muted/50 px-3 py-2 text-[12px]">
              <span className="text-muted-foreground">Total </span>
              <span className="font-medium tabular-nums">
                {lines.reduce((sum, l) => sum + (Number(l.hours) || 0), 0)}
              </span>
              <span className="ml-3 text-muted-foreground">Billable </span>
              <span className="font-medium tabular-nums">
                {lines
                  .filter((l) => l.leadId)
                  .reduce((sum, l) => sum + (Number(l.hours) || 0), 0)}
              </span>
            </div>

            <div className="flex gap-1.5">
              <Button
                variant="outline"
                className="flex-1"
                disabled={busy}
                onClick={() => void saveSheet(false)}
              >
                Save draft
              </Button>
              <Button className="flex-1" disabled={busy} onClick={() => void saveSheet(true)}>
                {busy ? "Saving…" : "Submit"}
              </Button>
            </div>
          </div>
        </SheetContent>
      </Sheet>

      {/* ---------------- a trip ---------------- */}

      <Sheet open={tripOpen} onOpenChange={setTripOpen}>
        <SheetContent className="w-full overflow-y-auto sm:max-w-md">
          <SheetHeader>
            <SheetTitle>Send somebody somewhere</SheetTitle>
            <SheetDescription>
              Nobody can be in two places at once — an overlapping trip is refused.
            </SheetDescription>
          </SheetHeader>
          <div className="space-y-3 px-4 pb-8">
            <div className="space-y-1">
              <Label className="text-[12px]">Who</Label>
              <select
                className="h-9 w-full rounded-md border bg-background px-2 text-[13px]"
                value={tripForm.employeeId}
                onChange={(e) => setTripForm({ ...tripForm, employeeId: e.target.value })}
              >
                <option value="">Pick someone…</option>
                {employees.map((e) => (
                  <option key={e.id} value={e.id}>
                    {e.name}
                  </option>
                ))}
              </select>
            </div>
            <div className="grid grid-cols-2 gap-2">
              <div className="space-y-1">
                <Label className="text-[12px]">Where</Label>
                <Input
                  className="h-9"
                  placeholder="Udaipur"
                  value={tripForm.destination}
                  onChange={(e) => setTripForm({ ...tripForm, destination: e.target.value })}
                />
              </div>
              <div className="space-y-1">
                <Label className="text-[12px]">What for</Label>
                <Input
                  className="h-9"
                  placeholder="Sharma wedding"
                  value={tripForm.purpose}
                  onChange={(e) => setTripForm({ ...tripForm, purpose: e.target.value })}
                />
              </div>
            </div>
            <div className="grid grid-cols-2 gap-2">
              <div className="space-y-1">
                <Label className="text-[12px]">From</Label>
                <Input
                  type="date"
                  className="h-9"
                  value={tripForm.fromDate}
                  onChange={(e) => setTripForm({ ...tripForm, fromDate: e.target.value })}
                />
              </div>
              <div className="space-y-1">
                <Label className="text-[12px]">To</Label>
                <Input
                  type="date"
                  className="h-9"
                  value={tripForm.toDate}
                  onChange={(e) => setTripForm({ ...tripForm, toDate: e.target.value })}
                />
              </div>
            </div>
            <div className="grid grid-cols-2 gap-2">
              <div className="space-y-1">
                <Label className="text-[12px]">Expected cost</Label>
                <Input
                  className="h-9 tabular-nums"
                  inputMode="decimal"
                  value={tripForm.estimatedCost}
                  onChange={(e) => setTripForm({ ...tripForm, estimatedCost: e.target.value })}
                />
              </div>
              <div className="space-y-1">
                <Label className="text-[12px]">Advance asked</Label>
                <Input
                  className="h-9 tabular-nums"
                  inputMode="decimal"
                  value={tripForm.advanceRequested}
                  onChange={(e) =>
                    setTripForm({ ...tripForm, advanceRequested: e.target.value })
                  }
                />
                <p className="text-[11px] text-muted-foreground">
                  Cannot exceed the expected cost.
                </p>
              </div>
            </div>
            <Button className="w-full" disabled={busy} onClick={() => void requestTrip()}>
              {busy ? "Raising…" : "Raise for approval"}
            </Button>
            <p className="text-[11px] leading-snug text-muted-foreground">
              Modes available on a leg: {TRAVEL_MODES.join(", ")}. Legs can be added through the
              API; this form raises the trip itself.
            </p>
          </div>
        </SheetContent>
      </Sheet>
    </PagePanel>
  );
}
