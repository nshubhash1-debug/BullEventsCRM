"use client";

import * as React from "react";
import { AlertTriangle, HardHat, Plus, Search, Star, UsersRound } from "lucide-react";
import { toast } from "sonner";

import { PagePanel } from "@/components/shell/page-panel";
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
import { ApiError } from "@/lib/api";
import { formatMoney } from "@/lib/crm-api";
import {
  CREW_ENGAGEMENT_TYPES,
  CREW_ROLES,
  crewApi,
  humaniseLabel,
  type CrewMember,
  type CrewRoleCoverage,
} from "@/lib/resources-api";
import { cn } from "@/lib/utils";

function isoToday(offsetDays = 0) {
  const date = new Date();
  date.setDate(date.getDate() + offsetDays);
  return date.toISOString().slice(0, 10);
}

export default function CrewPage() {
  const [from, setFrom] = React.useState(() => isoToday(7));
  const [to, setTo] = React.useState(() => isoToday(9));
  const [role, setRole] = React.useState("");
  const [engagement, setEngagement] = React.useState("");
  const [availableOnly, setAvailableOnly] = React.useState(false);
  const [search, setSearch] = React.useState("");

  const [rows, setRows] = React.useState<CrewMember[]>([]);
  const [coverage, setCoverage] = React.useState<CrewRoleCoverage[]>([]);
  const [picked, setPicked] = React.useState<Set<number>>(new Set());
  const [nonce, setNonce] = React.useState(0);
  const [loadedKey, setLoadedKey] = React.useState<string | null>(null);

  const [booking, setBooking] = React.useState(false);
  const [event, setEvent] = React.useState({ eventName: "", clientName: "", venueName: "" });
  const [saving, setSaving] = React.useState(false);

  const [adding, setAdding] = React.useState(false);
  const [form, setForm] = React.useState({
    name: "",
    primaryRole: "Helper",
    engagementType: "Freelancer",
    phone: "",
    dayRate: "",
  });

  const queryKey = JSON.stringify({ from, to, role, engagement, availableOnly, nonce });
  const loading = loadedKey !== queryKey;

  React.useEffect(() => {
    let cancelled = false;

    Promise.all([
      crewApi.availability({
        from,
        to,
        role: role || undefined,
        engagementType: engagement || undefined,
        availableOnly,
      }),
      crewApi.coverage({ from, to }),
    ])
      .then(([people, cover]) => {
        if (cancelled) return;
        setRows(people);
        setCoverage(cover);
      })
      .catch(() => {
        if (cancelled) return;
        setRows([]);
        setCoverage([]);
      })
      .finally(() => {
        if (!cancelled) setLoadedKey(queryKey);
      });

    return () => {
      cancelled = true;
    };
  }, [from, to, role, engagement, availableOnly, nonce, queryKey]);

  const visible = React.useMemo(() => {
    const needle = search.trim().toLowerCase();
    if (!needle) return rows;
    return rows.filter(
      (r) =>
        r.name.toLowerCase().includes(needle) || r.code.toLowerCase().includes(needle)
    );
  }, [rows, search]);

  function toggle(id: number) {
    setPicked((current) => {
      const next = new Set(current);
      if (next.has(id)) next.delete(id);
      else next.add(id);
      return next;
    });
  }

  async function book() {
    if (picked.size === 0) return;
    if (!event.eventName.trim()) {
      toast.error("Name the event so the crew know what they are turning up to.");
      return;
    }

    setSaving(true);
    try {
      const created = await crewApi.requisition({
        fromDate: from,
        toDate: to,
        role: role || "Helper",
        crewMemberIds: [...picked],
        eventName: event.eventName.trim(),
        clientName: event.clientName.trim() || null,
        venueName: event.venueName.trim() || null,
      });
      toast.success(
        `${created.length} booked for ${event.eventName.trim()} — ${formatMoney(
          created.reduce((sum, a) => sum + a.totalCost, 0)
        )} in crew cost.`
      );
      setBooking(false);
      setPicked(new Set());
      setEvent({ eventName: "", clientName: "", venueName: "" });
      setNonce((n) => n + 1);
    } catch (error) {
      toast.error(error instanceof ApiError ? error.message : "Could not book that.");
    } finally {
      setSaving(false);
    }
  }

  async function addPerson() {
    if (!form.name.trim()) {
      toast.error("Give them a name.");
      return;
    }

    setSaving(true);
    try {
      await crewApi.create({
        name: form.name.trim(),
        primaryRole: form.primaryRole,
        engagementType: form.engagementType,
        phone: form.phone.trim() || null,
        dayRate: form.dayRate ? Number(form.dayRate) : null,
      });
      toast.success(`${form.name.trim()} added to the roster.`);
      setAdding(false);
      setForm((f) => ({ ...f, name: "", phone: "", dayRate: "" }));
      setNonce((n) => n + 1);
    } catch (error) {
      toast.error(error instanceof ApiError ? error.message : "Could not add that.");
    } finally {
      setSaving(false);
    }
  }

  const free = visible.filter((r) => r.isAvailable).length;
  const pickedCost = visible
    .filter((r) => picked.has(r.id))
    .reduce(
      (sum, r) =>
        sum +
        (r.dayRate ?? 0) *
          (Math.round(
            (new Date(to).getTime() - new Date(from).getTime()) / 86_400_000
          ) +
            1),
      0
    );

  return (
    <PagePanel
      icon={HardHat}
      title="Crew roster"
      hint="Everyone who can work an event — staff, freelancers and contractor gangs — and who is actually free on the dates."
      actions={
        <div className="flex gap-2">
          {picked.size > 0 ? (
            <Button size="sm" className="h-8" onClick={() => setBooking(true)}>
              <UsersRound className="mr-1 size-3.5" />
              Book {picked.size} · {formatMoney(pickedCost)}
            </Button>
          ) : null}
          <Button size="sm" variant="outline" className="h-8" onClick={() => setAdding(true)}>
            <Plus className="mr-1 size-3.5" />
            Add person
          </Button>
        </div>
      }
      toolbar={
        <div className="flex flex-wrap items-end gap-2">
          <div>
            <Label className="text-[11px] text-muted-foreground">From</Label>
            <Input
              type="date"
              className="mt-1 h-9 w-36"
              value={from}
              onChange={(e) => setFrom(e.target.value)}
            />
          </div>
          <div>
            <Label className="text-[11px] text-muted-foreground">To</Label>
            <Input
              type="date"
              className="mt-1 h-9 w-36"
              value={to}
              onChange={(e) => setTo(e.target.value)}
            />
          </div>

          <select
            className="h-9 rounded-md border bg-background px-2 text-sm"
            value={role}
            onChange={(e) => setRole(e.target.value)}
          >
            <option value="">All roles</option>
            {CREW_ROLES.map((r) => (
              <option key={r} value={r}>
                {humaniseLabel(r)}
              </option>
            ))}
          </select>

          <select
            className="h-9 rounded-md border bg-background px-2 text-sm"
            value={engagement}
            onChange={(e) => setEngagement(e.target.value)}
          >
            <option value="">All engagements</option>
            {CREW_ENGAGEMENT_TYPES.map((t) => (
              <option key={t} value={t}>
                {humaniseLabel(t)}
              </option>
            ))}
          </select>

          <Button
            size="sm"
            variant={availableOnly ? "secondary" : "outline"}
            className="h-9"
            onClick={() => setAvailableOnly((v) => !v)}
          >
            Free only
          </Button>

          <div className="relative">
            <Search className="pointer-events-none absolute left-2.5 top-1/2 size-3.5 -translate-y-1/2 text-muted-foreground" />
            <Input
              className="h-9 w-44 pl-8"
              placeholder="Filter by name"
              value={search}
              onChange={(e) => setSearch(e.target.value)}
            />
          </div>
        </div>
      }
      subToolbar={
        <span className="text-[12px] text-muted-foreground">
          {loading
            ? "Loading…"
            : `${visible.length} on the roster · ${free} free between ${from} and ${to}`}
        </span>
      }
    >
      <div className="min-h-0 flex-1 space-y-4 overflow-y-auto px-5 pb-5">
        {/* ---------------- coverage strip ---------------- */}
        {coverage.length > 0 ? (
          <div>
            <h2 className="mb-2 text-[12px] font-semibold uppercase tracking-wide text-muted-foreground">
              Who can be fielded over these dates
            </h2>
            <div className="flex flex-wrap gap-1.5">
              {coverage.map((c) => (
                <button
                  key={c.role}
                  type="button"
                  onClick={() => setRole(role === c.role ? "" : c.role)}
                  className={cn(
                    "rounded-md border px-2.5 py-1.5 text-left text-[12px] transition hover:border-primary/50",
                    role === c.role ? "border-primary bg-primary/5" : ""
                  )}
                >
                  <div className="font-medium">{humaniseLabel(c.role)}</div>
                  <div className="tabular-nums">
                    <span
                      className={cn(
                        "font-semibold",
                        c.available === 0
                          ? "text-rose-600"
                          : "text-emerald-600 dark:text-emerald-400"
                      )}
                    >
                      {c.available}
                    </span>
                    <span className="text-muted-foreground"> / {c.onRoster} free</span>
                  </div>
                </button>
              ))}
            </div>
          </div>
        ) : null}

        {/* ---------------- roster ---------------- */}
        <div className="overflow-x-auto">
          <table className="w-full min-w-[860px] text-[13px]">
            <thead>
              <tr className="border-b text-left text-[11px] uppercase tracking-wide text-muted-foreground">
                <th className="w-8 py-2" />
                <th className="py-2 pr-2 font-medium">Name</th>
                <th className="px-2 py-2 font-medium">Role</th>
                <th className="px-2 py-2 font-medium">Also covers</th>
                <th className="px-2 py-2 font-medium">Engagement</th>
                <th className="px-2 py-2 font-medium">Phone</th>
                <th className="px-2 py-2 text-right font-medium">Day rate</th>
                <th className="px-2 py-2 text-center font-medium">Rating</th>
                <th className="px-2 py-2 text-center font-medium">No-shows</th>
                <th className="px-2 py-2 text-center font-medium">On dates</th>
              </tr>
            </thead>

            <tbody>
              {visible.map((c) => (
                <tr
                  key={c.id}
                  className={cn(
                    "border-b last:border-0",
                    c.isAvailable ? "hover:bg-muted/40" : "opacity-60"
                  )}
                >
                  <td className="py-1.5">
                    <input
                      type="checkbox"
                      className="size-3.5 accent-primary"
                      disabled={!c.isAvailable}
                      checked={picked.has(c.id)}
                      onChange={() => toggle(c.id)}
                      aria-label={`Pick ${c.name}`}
                    />
                  </td>
                  <td className="py-1.5 pr-2">
                    <div className="font-medium">{c.name}</div>
                    <div className="font-mono text-[10.5px] text-muted-foreground">
                      {c.code}
                    </div>
                  </td>
                  <td className="px-2 py-1.5">{humaniseLabel(c.primaryRole)}</td>
                  <td className="px-2 py-1.5 text-[11.5px] text-muted-foreground">
                    {c.secondaryRoles.length > 0
                      ? c.secondaryRoles.map((r) => humaniseLabel(r)).join(", ")
                      : "—"}
                  </td>
                  <td className="px-2 py-1.5 text-muted-foreground">
                    {humaniseLabel(c.engagementType)}
                    {c.supplierVendorName ? (
                      <div className="text-[11px]">{c.supplierVendorName}</div>
                    ) : null}
                  </td>
                  <td className="px-2 py-1.5 tabular-nums text-muted-foreground">
                    {c.phone ?? "—"}
                  </td>
                  <td className="px-2 py-1.5 text-right tabular-nums">
                    {c.dayRate ? formatMoney(c.dayRate) : "—"}
                  </td>
                  <td className="px-2 py-1.5 text-center">
                    {c.rating ? (
                      <span className="inline-flex items-center gap-0.5 tabular-nums">
                        <Star className="size-3 fill-amber-400 text-amber-400" />
                        {c.rating}
                      </span>
                    ) : (
                      <span className="text-muted-foreground">—</span>
                    )}
                  </td>
                  <td className="px-2 py-1.5 text-center tabular-nums">
                    {c.noShowCount > 0 ? (
                      <span className="inline-flex items-center gap-0.5 text-rose-600">
                        <AlertTriangle className="size-3" />
                        {c.noShowCount}
                      </span>
                    ) : (
                      <span className="text-muted-foreground">—</span>
                    )}
                  </td>
                  <td className="px-2 py-1.5 text-center">
                    <span
                      className={cn(
                        "rounded px-1.5 py-0.5 text-[11px] font-medium",
                        c.isAvailable
                          ? "bg-emerald-50 text-emerald-700 dark:bg-emerald-950/40 dark:text-emerald-400"
                          : "bg-rose-50 text-rose-700 dark:bg-rose-950/40 dark:text-rose-400"
                      )}
                    >
                      {c.isAvailable ? "Free" : `Booked${c.clashingAssignments ? ` ×${c.clashingAssignments}` : ""}`}
                    </span>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>

        {!loading && visible.length === 0 ? (
          <p className="py-16 text-center text-[13px] text-muted-foreground">
            Nobody on the roster matches that.
          </p>
        ) : null}
      </div>

      {/* ---------------- requisition ---------------- */}
      <Dialog open={booking} onOpenChange={setBooking}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Book {picked.size} crew</DialogTitle>
            <DialogDescription>
              {from} to {to} as {humaniseLabel(role || "Helper").toLowerCase()} ·{" "}
              {formatMoney(pickedCost)} in day rates. Every one is re-checked before
              anything is written, so the whole booking fails rather than half of it
              landing.
            </DialogDescription>
          </DialogHeader>

          <div className="grid gap-3">
            <div>
              <Label className="text-[12px]">Event</Label>
              <Input
                className="mt-1 h-9"
                placeholder="Malhotra reception"
                value={event.eventName}
                onChange={(e) => setEvent((v) => ({ ...v, eventName: e.target.value }))}
              />
            </div>
            <div className="grid grid-cols-2 gap-3">
              <div>
                <Label className="text-[12px]">Client</Label>
                <Input
                  className="mt-1 h-9"
                  value={event.clientName}
                  onChange={(e) => setEvent((v) => ({ ...v, clientName: e.target.value }))}
                />
              </div>
              <div>
                <Label className="text-[12px]">Venue</Label>
                <Input
                  className="mt-1 h-9"
                  value={event.venueName}
                  onChange={(e) => setEvent((v) => ({ ...v, venueName: e.target.value }))}
                />
              </div>
            </div>
          </div>

          <DialogFooter>
            <Button variant="outline" onClick={() => setBooking(false)}>
              Cancel
            </Button>
            <Button disabled={saving} onClick={() => void book()}>
              {saving ? "Booking…" : `Book ${picked.size}`}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* ---------------- add to roster ---------------- */}
      <Dialog open={adding} onOpenChange={setAdding}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Add to the roster</DialogTitle>
            <DialogDescription>
              Freelancers and contractor hands live here rather than in HR — most of
              the people who work a wedding are not on the payroll.
            </DialogDescription>
          </DialogHeader>

          <div className="grid gap-3">
            <div>
              <Label className="text-[12px]">Name</Label>
              <Input
                className="mt-1 h-9"
                value={form.name}
                onChange={(e) => setForm((f) => ({ ...f, name: e.target.value }))}
              />
            </div>

            <div className="grid grid-cols-2 gap-3">
              <div>
                <Label className="text-[12px]">Role</Label>
                <select
                  className="mt-1 h-9 w-full rounded-md border bg-background px-2 text-sm"
                  value={form.primaryRole}
                  onChange={(e) => setForm((f) => ({ ...f, primaryRole: e.target.value }))}
                >
                  {CREW_ROLES.map((r) => (
                    <option key={r} value={r}>
                      {humaniseLabel(r)}
                    </option>
                  ))}
                </select>
              </div>
              <div>
                <Label className="text-[12px]">Engagement</Label>
                <select
                  className="mt-1 h-9 w-full rounded-md border bg-background px-2 text-sm"
                  value={form.engagementType}
                  onChange={(e) =>
                    setForm((f) => ({ ...f, engagementType: e.target.value }))
                  }
                >
                  {CREW_ENGAGEMENT_TYPES.map((t) => (
                    <option key={t} value={t}>
                      {humaniseLabel(t)}
                    </option>
                  ))}
                </select>
              </div>
            </div>

            <div className="grid grid-cols-2 gap-3">
              <div>
                <Label className="text-[12px]">Phone</Label>
                <Input
                  className="mt-1 h-9"
                  value={form.phone}
                  onChange={(e) => setForm((f) => ({ ...f, phone: e.target.value }))}
                />
              </div>
              <div>
                <Label className="text-[12px]">Day rate</Label>
                <Input
                  className="mt-1 h-9"
                  value={form.dayRate}
                  onChange={(e) => setForm((f) => ({ ...f, dayRate: e.target.value }))}
                />
              </div>
            </div>
          </div>

          <DialogFooter>
            <Button variant="outline" onClick={() => setAdding(false)}>
              Cancel
            </Button>
            <Button disabled={saving} onClick={() => void addPerson()}>
              {saving ? "Adding…" : "Add"}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </PagePanel>
  );
}
