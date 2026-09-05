"use client";

import * as React from "react";
import {
  AlertTriangle,
  Plus,
  Truck,
} from "lucide-react";
import { toast } from "sonner";

import { GatePassSheet } from "@/components/props/gate-pass-sheet";
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
  PROP_ISSUE_STATUS_LABELS,
  PROP_ISSUE_STATUSES,
  propsApi,
  type PropIssue,
} from "@/lib/props-api";
import { cn } from "@/lib/utils";

function isoToday(offsetDays = 0) {
  const date = new Date();
  date.setDate(date.getDate() + offsetDays);
  return date.toISOString().slice(0, 10);
}

const STATUS_TONE: Record<string, string> = {
  Draft: "bg-muted text-muted-foreground",
  Reserved: "bg-sky-100 text-sky-800 dark:bg-sky-950/50 dark:text-sky-300",
  Dispatched: "bg-amber-100 text-amber-800 dark:bg-amber-950/50 dark:text-amber-300",
  PartiallyReturned: "bg-orange-100 text-orange-800 dark:bg-orange-950/50 dark:text-orange-300",
  Returned: "bg-emerald-100 text-emerald-800 dark:bg-emerald-950/50 dark:text-emerald-300",
  Closed: "bg-muted text-muted-foreground",
  Cancelled: "bg-muted text-muted-foreground line-through",
};

export default function GatePassesPage() {
  const [rows, setRows] = React.useState<PropIssue[]>([]);
  const [status, setStatus] = React.useState("");
  const [overdueOnly, setOverdueOnly] = React.useState(false);
  const [openId, setOpenId] = React.useState<number | null>(null);

  const [creating, setCreating] = React.useState(false);
  const [form, setForm] = React.useState({
    eventName: "",
    clientName: "",
    venueName: "",
    dispatchDate: isoToday(),
    eventDate: isoToday(2),
    expectedReturnDate: isoToday(4),
  });
  const [saving, setSaving] = React.useState(false);

  /**
   * Bumped whenever something on this page changes a pass, which re-runs the
   * fetch below. A nonce rather than a direct call so the effect stays the one
   * place that loads, and loading can be derived instead of set by hand.
   */
  const [nonce, setNonce] = React.useState(0);
  const [loadedKey, setLoadedKey] = React.useState<string | null>(null);

  const queryKey = JSON.stringify({ status, overdueOnly, nonce });
  const loading = loadedKey !== queryKey;

  const load = React.useCallback(() => setNonce((n) => n + 1), []);

  React.useEffect(() => {
    let cancelled = false;

    propsApi
      .issues({ status: status || undefined, overdueOnly, take: 200 })
      .then((result) => !cancelled && setRows(result))
      .catch(() => !cancelled && setRows([]))
      .finally(() => !cancelled && setLoadedKey(queryKey));

    return () => {
      cancelled = true;
    };
  }, [status, overdueOnly, queryKey]);

  async function create() {
    if (!form.eventName.trim()) {
      toast.error("Give the pass an event name so the godown knows what it is for.");
      return;
    }
    if (form.expectedReturnDate < form.dispatchDate) {
      toast.error("The return date cannot fall before the dispatch date.");
      return;
    }

    setSaving(true);
    try {
      const created = await propsApi.createIssue({
        eventName: form.eventName.trim(),
        clientName: form.clientName.trim() || null,
        venueName: form.venueName.trim() || null,
        dispatchDate: form.dispatchDate,
        eventDate: form.eventDate || null,
        expectedReturnDate: form.expectedReturnDate,
      });
      toast.success(`${created.code} created. Add the props it is taking.`);
      setCreating(false);
      setForm((f) => ({ ...f, eventName: "", clientName: "", venueName: "" }));
      load();
      setOpenId(created.id);
    } catch (error) {
      toast.error(error instanceof ApiError ? error.message : "Could not create that.");
    } finally {
      setSaving(false);
    }
  }

  const overdue = rows.filter((r) => r.isOverdue).length;
  const out = rows.filter((r) => r.status === "Dispatched" || r.status === "PartiallyReturned").length;

  return (
    <PagePanel
      icon={Truck}
      title="Gate passes"
      hint="What leaves the godown for an event, and what is counted back in. Reserving one holds the stock for its dates."
      actions={
        <Button size="sm" className="h-8" onClick={() => setCreating(true)}>
          <Plus className="mr-1 size-3.5" />
          New gate pass
        </Button>
      }
      toolbar={
        <div className="flex flex-wrap items-center gap-2">
          <select
            className="h-9 rounded-md border bg-background px-2 text-sm"
            value={status}
            onChange={(e) => setStatus(e.target.value)}
          >
            <option value="">All statuses</option>
            {PROP_ISSUE_STATUSES.map((s) => (
              <option key={s} value={s}>
                {PROP_ISSUE_STATUS_LABELS[s]}
              </option>
            ))}
          </select>

          <Button
            size="sm"
            variant={overdueOnly ? "secondary" : "outline"}
            className="h-9"
            onClick={() => setOverdueOnly((v) => !v)}
          >
            <AlertTriangle className="mr-1 size-3.5" />
            Overdue only
          </Button>
        </div>
      }
      subToolbar={
        <span className="text-[12px] text-muted-foreground">
          {loading
            ? "Loading…"
            : `${rows.length} passes · ${out} out on events · ${overdue} overdue`}
        </span>
      }
      flush
    >
      <div className="min-h-0 flex-1 overflow-auto">
        <table className="w-full min-w-[900px] text-[13px]">
          <thead className="sticky top-0 z-10 bg-card shadow-[0_1px_0_var(--border)]">
            <tr className="text-left text-[11px] uppercase tracking-wide text-muted-foreground">
              <th className="px-4 py-2 font-medium">Pass</th>
              <th className="px-2 py-2 font-medium">Event</th>
              <th className="px-2 py-2 font-medium">Venue</th>
              <th className="px-2 py-2 font-medium">Out</th>
              <th className="px-2 py-2 font-medium">Back by</th>
              <th className="px-2 py-2 text-right font-medium">Items</th>
              <th className="px-2 py-2 text-right font-medium">Pieces</th>
              <th className="px-2 py-2 text-right font-medium">Pending</th>
              <th className="px-2 py-2 text-right font-medium">Value</th>
              <th className="px-4 py-2 font-medium">Status</th>
            </tr>
          </thead>

          <tbody>
            {rows.map((row) => (
              <tr
                key={row.id}
                onClick={() => setOpenId(row.id)}
                className="cursor-pointer border-b last:border-0 hover:bg-muted/40"
              >
                <td className="px-4 py-2 font-mono text-[11.5px]">{row.code}</td>
                <td className="px-2 py-2">
                  <div className="font-medium">{row.eventName ?? "—"}</div>
                  {row.clientName ? (
                    <div className="text-[11px] text-muted-foreground">{row.clientName}</div>
                  ) : null}
                </td>
                <td className="px-2 py-2 text-muted-foreground">{row.venueName ?? "—"}</td>
                <td className="px-2 py-2 tabular-nums text-muted-foreground">
                  {row.dispatchDate.slice(0, 10)}
                </td>
                <td
                  className={cn(
                    "px-2 py-2 tabular-nums",
                    row.isOverdue
                      ? "font-semibold text-rose-600 dark:text-rose-400"
                      : "text-muted-foreground"
                  )}
                >
                  {row.expectedReturnDate.slice(0, 10)}
                  {row.isOverdue ? (
                    <AlertTriangle className="ml-1 inline size-3.5" />
                  ) : null}
                </td>
                <td className="px-2 py-2 text-right tabular-nums">{row.lineCount}</td>
                <td className="px-2 py-2 text-right tabular-nums">
                  {row.totalIssued || row.totalReserved}
                </td>
                <td
                  className={cn(
                    "px-2 py-2 text-right tabular-nums",
                    row.totalPending > 0 ? "font-semibold text-amber-600" : "text-muted-foreground"
                  )}
                >
                  {row.totalPending || "—"}
                </td>
                <td className="px-2 py-2 text-right tabular-nums text-muted-foreground">
                  {row.estimatedValue ? formatMoney(row.estimatedValue) : "—"}
                </td>
                <td className="px-4 py-2">
                  <span
                    className={cn(
                      "rounded px-1.5 py-0.5 text-[11px] font-medium",
                      STATUS_TONE[row.status] ?? "bg-muted"
                    )}
                  >
                    {PROP_ISSUE_STATUS_LABELS[row.status] ?? row.status}
                  </span>
                </td>
              </tr>
            ))}
          </tbody>
        </table>

        {!loading && rows.length === 0 ? (
          <p className="py-16 text-center text-[13px] text-muted-foreground">
            No gate passes yet. Create one when props are going out to an event.
          </p>
        ) : null}
      </div>

      <Dialog open={creating} onOpenChange={setCreating}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>New gate pass</DialogTitle>
            <DialogDescription>
              The dates are the whole window the stock is away — dispatch day to the
              day it is counted back in, not just the day of the function.
            </DialogDescription>
          </DialogHeader>

          <div className="grid gap-3">
            <div>
              <Label className="text-[12px]">Event</Label>
              <Input
                className="mt-1 h-9"
                placeholder="Sharma sangeet"
                value={form.eventName}
                onChange={(e) => setForm((f) => ({ ...f, eventName: e.target.value }))}
              />
            </div>

            <div className="grid grid-cols-2 gap-3">
              <div>
                <Label className="text-[12px]">Client</Label>
                <Input
                  className="mt-1 h-9"
                  value={form.clientName}
                  onChange={(e) => setForm((f) => ({ ...f, clientName: e.target.value }))}
                />
              </div>
              <div>
                <Label className="text-[12px]">Venue</Label>
                <Input
                  className="mt-1 h-9"
                  value={form.venueName}
                  onChange={(e) => setForm((f) => ({ ...f, venueName: e.target.value }))}
                />
              </div>
            </div>

            <div className="grid grid-cols-3 gap-3">
              <div>
                <Label className="text-[12px]">Dispatch</Label>
                <Input
                  type="date"
                  className="mt-1 h-9"
                  value={form.dispatchDate}
                  onChange={(e) => setForm((f) => ({ ...f, dispatchDate: e.target.value }))}
                />
              </div>
              <div>
                <Label className="text-[12px]">Event day</Label>
                <Input
                  type="date"
                  className="mt-1 h-9"
                  value={form.eventDate}
                  onChange={(e) => setForm((f) => ({ ...f, eventDate: e.target.value }))}
                />
              </div>
              <div>
                <Label className="text-[12px]">Back by</Label>
                <Input
                  type="date"
                  className="mt-1 h-9"
                  value={form.expectedReturnDate}
                  onChange={(e) =>
                    setForm((f) => ({ ...f, expectedReturnDate: e.target.value }))
                  }
                />
              </div>
            </div>
          </div>

          <DialogFooter>
            <Button variant="outline" onClick={() => setCreating(false)}>
              Cancel
            </Button>
            <Button disabled={saving} onClick={() => void create()}>
              {saving ? "Creating…" : "Create"}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* Keyed by the pass, so opening a different one is a fresh sheet rather
          than one that has to unpick the last pass's half-typed counts. */}
      <GatePassSheet
        key={openId ?? "none"}
        issueId={openId}
        open={openId !== null}
        onOpenChange={(open) => !open && setOpenId(null)}
        onChanged={load}
      />
    </PagePanel>
  );
}
