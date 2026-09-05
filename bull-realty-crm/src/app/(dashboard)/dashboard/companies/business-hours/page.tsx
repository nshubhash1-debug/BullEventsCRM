"use client";

import * as React from "react";
import { CalendarOff, Clock, Loader2, Plus, Trash2 } from "lucide-react";
import { toast } from "sonner";

import { PagePanel } from "@/components/shell/page-panel";
import { CrmLoadingState } from "@/components/shell/crm-loader";
import { Pill } from "@/components/crm/metrics";
import { RuleCard } from "@/components/admin/rule-list";
import { Button } from "@/components/ui/button";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Switch } from "@/components/ui/switch";
import { ApiError } from "@/lib/api";
import {
  automationApi,
  clock,
  DAY_NAMES,
  toMinutes,
  type BusinessHours,
  type DayWindow,
  type SaveBusinessHours,
} from "@/lib/automation-api";
import { cn } from "@/lib/utils";

const CLOSED = -1;

const DEFAULT_WEEK: DayWindow[] = [
  { open: CLOSED, close: CLOSED },
  { open: 600, close: 1140 },
  { open: 600, close: 1140 },
  { open: 600, close: 1140 },
  { open: 600, close: 1140 },
  { open: 600, close: 1140 },
  { open: 600, close: 1140 },
];

/**
 * Business hours and holidays.
 *
 * This is the foundation the SLA clock stands on. "Respond within four hours"
 * means four <em>working</em> hours, and without a definition of working hours
 * that target expires over a weekend and produces an alert nobody believes.
 *
 * Holidays can recur, so Republic Day is one row rather than one row a year —
 * which is how a holiday list quietly stops working the following January.
 */
export default function BusinessHoursPage() {
  const [rows, setRows] = React.useState<BusinessHours[] | null>(null);
  const [editing, setEditing] = React.useState<BusinessHours | null>(null);
  const [creating, setCreating] = React.useState(false);
  // Only the setter is read: every mutation on this screen reloads the whole
  // list afterwards, so there is no per-row spinner to drive — the value would
  // be written and never looked at.
  const [, setBusy] = React.useState<number | null>(null);
  const [addingTo, setAddingTo] = React.useState<BusinessHours | null>(null);

  const load = React.useCallback(() => {
    automationApi
      .businessHours()
      .then(setRows)
      .catch((error: unknown) => {
        toast.error("Could not load business hours", {
          description: error instanceof ApiError ? error.message : "Network error.",
        });
        setRows([]);
      });
  }, []);

  React.useEffect(load, [load]);

  async function act(id: number, work: () => Promise<unknown>, done: string) {
    setBusy(id);
    try {
      await work();
      toast.success(done);
      load();
    } catch (error) {
      toast.error("Could not save", {
        description: error instanceof ApiError ? error.message : "Network error.",
      });
    } finally {
      setBusy(null);
    }
  }

  if (!rows) {
    return (
      <PagePanel icon={Clock} title="Business hours & holidays">
        <CrmLoadingState label="Loading business hours" />
      </PagePanel>
    );
  }

  return (
    <PagePanel
      icon={Clock}
      title="Business hours & holidays"
      hint="The working week the SLA clock counts against. Weekends and holidays do not count."
      actions={
        <Button size="sm" className="gap-1.5" onClick={() => setCreating(true)}>
          <Plus className="size-3.5" />
          New set
        </Button>
      }
    >
      <div className="flex flex-col gap-4">
        {rows.length === 0 ? (
          <div className="py-14 text-center">
            <p className="text-[13px] text-muted-foreground">No business hours set.</p>
            <p className="mx-auto mt-1 max-w-md text-[12.5px] text-muted-foreground">
              Until there are, an SLA target counts wall-clock time — so a lead that arrives on
              Friday evening is late by Saturday morning.
            </p>
            <Button size="sm" variant="outline" className="mt-3" onClick={() => setCreating(true)}>
              Set the working week
            </Button>
          </div>
        ) : (
          rows.map((row) => (
            <RuleCard
              key={row.id}
              icon={Clock}
              title={row.name}
              hint={row.timeZoneId}
              actions={
                <>
                  {row.isDefault ? <Pill tone="primary">Default</Pill> : null}
                  <Pill tone={row.openNow ? "success" : "neutral"}>
                    {row.openNow ? "Open now" : "Closed now"}
                  </Pill>
                  <Button
                    size="sm"
                    variant="ghost"
                    className="h-7 px-2 text-[11px]"
                    onClick={() => setEditing(row)}
                  >
                    Edit
                  </Button>
                  {!row.isDefault ? (
                    <Button
                      size="sm"
                      variant="ghost"
                      className="h-7 px-1.5 text-muted-foreground"
                      onClick={() => {
                        if (window.confirm(`Delete "${row.name}"?`)) {
                          act(
                            row.id,
                            () => automationApi.deleteBusinessHours(row.id),
                            "Hours deleted"
                          );
                        }
                      }}
                    >
                      <Trash2 className="size-3.5" />
                    </Button>
                  ) : null}
                </>
              }
            >
              <div className="grid gap-px bg-border sm:grid-cols-7">
                {row.days.map((day, index) => (
                  <div key={index} className="bg-card px-3 py-2.5 text-center">
                    <p className="text-[10.5px] font-medium tracking-wide text-muted-foreground uppercase">
                      {DAY_NAMES[index]}
                    </p>
                    {day.open < 0 || day.close <= day.open ? (
                      <p className="mt-0.5 text-[12px] text-muted-foreground">Closed</p>
                    ) : (
                      <p className="mt-0.5 text-[12px] tabular-nums">
                        {clock(day.open)}–{clock(day.close)}
                      </p>
                    )}
                  </div>
                ))}
              </div>

              <div className="border-t px-4 py-2.5">
                <div className="flex flex-wrap items-center gap-2">
                  <span className="flex items-center gap-1.5 text-[11px] font-medium tracking-wide text-muted-foreground uppercase">
                    <CalendarOff className="size-3.5" />
                    Holidays
                  </span>

                  {row.holidays.length === 0 ? (
                    <span className="text-[12px] text-muted-foreground">None</span>
                  ) : (
                    row.holidays.map((holiday) => (
                      <span
                        key={holiday.id}
                        className="group inline-flex items-center gap-1 rounded border bg-muted/40 px-1.5 py-0.5 text-[11.5px]"
                      >
                        {holiday.name}
                        <span className="text-muted-foreground tabular-nums">
                          {new Date(holiday.date).toLocaleDateString("en-IN", {
                            day: "numeric",
                            month: "short",
                            ...(holiday.isRecurring ? {} : { year: "2-digit" }),
                          })}
                        </span>
                        {holiday.isRecurring ? (
                          <span className="text-muted-foreground/70">·yearly</span>
                        ) : null}
                        <button
                          type="button"
                          className="text-muted-foreground/50 hover:text-red-600"
                          onClick={() =>
                            act(
                              row.id,
                              () => automationApi.deleteHoliday(holiday.id),
                              "Holiday removed"
                            )
                          }
                        >
                          ×
                        </button>
                      </span>
                    ))
                  )}

                  <Button
                    size="sm"
                    variant="ghost"
                    className="h-6 gap-1 px-2 text-[11px]"
                    onClick={() => setAddingTo(row)}
                  >
                    <Plus className="size-3" />
                    Add
                  </Button>
                </div>
              </div>
            </RuleCard>
          ))
        )}
      </div>

      <Dialog
        open={creating || editing !== null}
        onOpenChange={(open) => {
          if (!open) {
            setCreating(false);
            setEditing(null);
          }
        }}
      >
        <HoursForm
          hours={editing}
          onDone={() => {
            setCreating(false);
            setEditing(null);
            load();
          }}
        />
      </Dialog>

      <Dialog open={addingTo !== null} onOpenChange={(open) => (open ? null : setAddingTo(null))}>
        <HolidayForm
          hours={addingTo}
          onDone={() => {
            setAddingTo(null);
            load();
          }}
        />
      </Dialog>
    </PagePanel>
  );
}

function HoursForm({ hours, onDone }: { hours: BusinessHours | null; onDone: () => void }) {
  const [name, setName] = React.useState(hours?.name ?? "Standard hours");
  const [zone, setZone] = React.useState(hours?.timeZoneId ?? "Asia/Kolkata");
  const [days, setDays] = React.useState<DayWindow[]>(hours?.days ?? DEFAULT_WEEK);
  const [isDefault, setIsDefault] = React.useState(hours?.isDefault ?? false);
  const [saving, setSaving] = React.useState(false);

  function setDay(index: number, patch: Partial<DayWindow>) {
    setDays((current) => current.map((d, i) => (i === index ? { ...d, ...patch } : d)));
  }

  async function save() {
    setSaving(true);
    try {
      const body: SaveBusinessHours = {
        name,
        timeZoneId: zone,
        days,
        isDefault,
        isActive: hours?.isActive ?? true,
      };

      if (hours) await automationApi.updateBusinessHours(hours.id, body);
      else await automationApi.createBusinessHours(body);

      toast.success(hours ? "Hours updated" : "Hours created");
      onDone();
    } catch (error) {
      toast.error("Could not save the hours", {
        description: error instanceof ApiError ? error.message : "Network error.",
      });
    } finally {
      setSaving(false);
    }
  }

  return (
    <DialogContent className="sm:max-w-lg">
      <DialogHeader>
        <DialogTitle>{hours ? "Edit business hours" : "New business hours"}</DialogTitle>
        <DialogDescription>
          Escalation targets are counted only inside these windows.
        </DialogDescription>
      </DialogHeader>

      <div className="grid gap-3">
        <div className="grid gap-3 sm:grid-cols-2">
          <div className="grid gap-1.5">
            <Label htmlFor="bh-name">Name</Label>
            <Input id="bh-name" value={name} onChange={(e) => setName(e.target.value)} />
          </div>
          <div className="grid gap-1.5">
            <Label htmlFor="bh-zone">Time zone</Label>
            <Input
              id="bh-zone"
              value={zone}
              onChange={(e) => setZone(e.target.value)}
              placeholder="Asia/Kolkata"
              className="font-mono text-[12px]"
            />
          </div>
        </div>

        <div className="grid gap-1.5">
          <Label>The working week</Label>
          <div className="flex flex-col gap-1.5">
            {days.map((day, index) => {
              const closed = day.open < 0 || day.close <= day.open;

              return (
                <div key={index} className="flex items-center gap-2">
                  <span className="w-10 text-[12px] font-medium">{DAY_NAMES[index]}</span>

                  <Switch
                    checked={!closed}
                    onCheckedChange={(on) =>
                      setDay(index, on ? { open: 600, close: 1140 } : { open: CLOSED, close: CLOSED })
                    }
                  />

                  <Input
                    type="time"
                    className={cn("h-8 w-28", closed && "opacity-40")}
                    disabled={closed}
                    value={closed ? "" : clock(day.open)}
                    onChange={(e) => setDay(index, { open: toMinutes(e.target.value) })}
                  />
                  <span className="text-muted-foreground">–</span>
                  <Input
                    type="time"
                    className={cn("h-8 w-28", closed && "opacity-40")}
                    disabled={closed}
                    value={closed ? "" : clock(day.close)}
                    onChange={(e) => setDay(index, { close: toMinutes(e.target.value) })}
                  />

                  {closed ? (
                    <span className="text-[11.5px] text-muted-foreground">Closed</span>
                  ) : null}
                </div>
              );
            })}
          </div>
        </div>

        <label className="flex items-center gap-2.5 text-[12.5px]">
          <Switch checked={isDefault} onCheckedChange={setIsDefault} />
          <span>
            Use these when a rule does not name its own
            <span className="block text-[11.5px] text-muted-foreground">
              Exactly one set is the default; setting this clears it elsewhere.
            </span>
          </span>
        </label>
      </div>

      <DialogFooter>
        <Button onClick={save} disabled={saving} className="gap-1.5">
          {saving ? <Loader2 className="size-4 animate-spin" /> : <Clock className="size-4" />}
          {hours ? "Save hours" : "Create hours"}
        </Button>
      </DialogFooter>
    </DialogContent>
  );
}

function HolidayForm({ hours, onDone }: { hours: BusinessHours | null; onDone: () => void }) {
  const [name, setName] = React.useState("");
  const [date, setDate] = React.useState("");
  const [recurring, setRecurring] = React.useState(true);
  const [saving, setSaving] = React.useState(false);

  const [seen, setSeen] = React.useState<BusinessHours | null>(null);

  if (hours !== seen) {
    setSeen(hours);
    if (hours) {
      setName("");
      setDate("");
      setRecurring(true);
    }
  }

  async function save() {
    if (!hours || !name.trim() || !date) {
      toast.error("A holiday needs a name and a date.");
      return;
    }

    setSaving(true);
    try {
      await automationApi.addHoliday(hours.id, {
        name: name.trim(),
        date,
        isRecurring: recurring,
      });

      toast.success("Holiday added");
      onDone();
    } catch (error) {
      toast.error("Could not add the holiday", {
        description: error instanceof ApiError ? error.message : "Network error.",
      });
    } finally {
      setSaving(false);
    }
  }

  return (
    <DialogContent className="sm:max-w-sm">
      <DialogHeader>
        <DialogTitle>Add a holiday</DialogTitle>
        <DialogDescription>The clock stops on these days.</DialogDescription>
      </DialogHeader>

      <div className="grid gap-3">
        <div className="grid gap-1.5">
          <Label htmlFor="h-name">Name</Label>
          <Input
            id="h-name"
            value={name}
            onChange={(e) => setName(e.target.value)}
            placeholder="Diwali"
          />
        </div>

        <div className="grid gap-1.5">
          <Label htmlFor="h-date">Date</Label>
          <Input id="h-date" type="date" value={date} onChange={(e) => setDate(e.target.value)} />
        </div>

        <label className="flex items-center gap-2.5 text-[12.5px]">
          <Switch checked={recurring} onCheckedChange={setRecurring} />
          <span>
            Same date every year
            <span className="block text-[11.5px] text-muted-foreground">
              Right for Republic Day. Wrong for Diwali, which moves.
            </span>
          </span>
        </label>
      </div>

      <DialogFooter>
        <Button onClick={save} disabled={saving} className="gap-1.5">
          {saving ? <Loader2 className="size-4 animate-spin" /> : <CalendarOff className="size-4" />}
          Add holiday
        </Button>
      </DialogFooter>
    </DialogContent>
  );
}
