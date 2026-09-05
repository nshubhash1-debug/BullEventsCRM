"use client";

import * as React from "react";
import { AlertTriangle, Handshake, Plus, Search, Star } from "lucide-react";
import { toast } from "sonner";

import { VendorSheet } from "@/components/resources/vendor-sheet";
import { PagePanel } from "@/components/shell/page-panel";
import { Button } from "@/components/ui/button";
import {
  Dialog,
  DialogContent,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { ApiError } from "@/lib/api";
import { formatMoney } from "@/lib/crm-api";
import type { WireFilterNode } from "@/lib/query";
import {
  SERVICE_CATEGORIES,
  VENDOR_STATUSES,
  humaniseLabel,
  vendorsApi,
  type Vendor,
} from "@/lib/resources-api";
import { cn } from "@/lib/utils";

const STATUS_TONE: Record<string, string> = {
  Active: "bg-emerald-100 text-emerald-800 dark:bg-emerald-950/50 dark:text-emerald-300",
  OnWatch: "bg-amber-100 text-amber-800 dark:bg-amber-950/50 dark:text-amber-300",
  Pending: "bg-sky-100 text-sky-800 dark:bg-sky-950/50 dark:text-sky-300",
  Blacklisted: "bg-rose-100 text-rose-800 dark:bg-rose-950/50 dark:text-rose-300",
};

function isoToday(offsetDays = 0) {
  const date = new Date();
  date.setDate(date.getDate() + offsetDays);
  return date.toISOString().slice(0, 10);
}

export default function VendorsPage() {
  const [rows, setRows] = React.useState<Vendor[]>([]);
  const [total, setTotal] = React.useState(0);
  const [search, setSearch] = React.useState("");
  const [service, setService] = React.useState("");
  const [status, setStatus] = React.useState("");
  const [openId, setOpenId] = React.useState<number | null>(null);
  const [nonce, setNonce] = React.useState(0);

  /* Availability mode: pick dates and the list reports who can take the job. */
  const [checking, setChecking] = React.useState(false);
  const [from, setFrom] = React.useState(() => isoToday(7));
  const [to, setTo] = React.useState(() => isoToday(9));

  const [creating, setCreating] = React.useState(false);
  const [form, setForm] = React.useState({
    name: "",
    contactPerson: "",
    phone: "",
    city: "",
    service: "Catering",
    capacity: "1",
  });
  const [saving, setSaving] = React.useState(false);

  const [loadedKey, setLoadedKey] = React.useState<string | null>(null);
  const [debounced, setDebounced] = React.useState("");

  React.useEffect(() => {
    const timer = setTimeout(() => setDebounced(search), 300);
    return () => clearTimeout(timer);
  }, [search]);

  const queryKey = JSON.stringify({ debounced, service, status, checking, from, to, nonce });
  const loading = loadedKey !== queryKey;

  React.useEffect(() => {
    let cancelled = false;

    const request = checking
      ? vendorsApi.availability({
          from,
          to,
          service: service || undefined,
        })
      : (() => {
          const filters: WireFilterNode[] = [];
          if (status) filters.push({ field: "status", operator: "equals", value: status });
          if (service) {
            filters.push({ field: "services", operator: "contains", value: service });
          }
          return vendorsApi
            .query({
              search: debounced || undefined,
              page: 1,
              pageSize: 200,
              sort: [{ field: "name", descending: false }],
              filter: filters.length ? { conjunction: "and", children: filters } : null,
            })
            .then((r) => {
              if (!cancelled) setTotal(r.total);
              return r.items;
            });
        })();

    request
      .then((items) => {
        if (cancelled) return;
        const needle = debounced.trim().toLowerCase();
        const visible =
          checking && needle
            ? items.filter((v) => v.name.toLowerCase().includes(needle))
            : items;
        setRows(visible);
        if (checking) setTotal(visible.length);
      })
      .catch(() => {
        if (!cancelled) {
          setRows([]);
          setTotal(0);
        }
      })
      .finally(() => {
        if (!cancelled) setLoadedKey(queryKey);
      });

    return () => {
      cancelled = true;
    };
  }, [debounced, service, status, checking, from, to, nonce, queryKey]);

  async function create() {
    if (!form.name.trim()) {
      toast.error("Give the vendor a name.");
      return;
    }

    setSaving(true);
    try {
      const created = await vendorsApi.create({
        name: form.name.trim(),
        services: [form.service],
        contactPerson: form.contactPerson.trim() || null,
        phone: form.phone.trim() || null,
        city: form.city.trim() || null,
        concurrentEventCapacity: Number(form.capacity) || 1,
      });
      toast.success(`${created.name} added. Put their rates in next.`);
      setCreating(false);
      setForm((f) => ({ ...f, name: "", contactPerson: "", phone: "" }));
      setNonce((n) => n + 1);
      setOpenId(created.id);
    } catch (error) {
      toast.error(error instanceof ApiError ? error.message : "Could not add that.");
    } finally {
      setSaving(false);
    }
  }

  return (
    <PagePanel
      icon={Handshake}
      title="Vendors"
      hint="Caterers, DJs, florists, mandap decorators — who supplies what, at what rate, and who is free on a date."
      actions={
        <Button size="sm" className="h-8" onClick={() => setCreating(true)}>
          <Plus className="mr-1 size-3.5" />
          New vendor
        </Button>
      }
      toolbar={
        <div className="flex flex-wrap items-end gap-2">
          <div className="relative">
            <Search className="pointer-events-none absolute left-2.5 top-1/2 size-3.5 -translate-y-1/2 text-muted-foreground" />
            <Input
              className="h-9 w-56 pl-8"
              placeholder="Search vendors…"
              value={search}
              onChange={(e) => setSearch(e.target.value)}
            />
          </div>

          <select
            className="h-9 rounded-md border bg-background px-2 text-sm"
            value={service}
            onChange={(e) => setService(e.target.value)}
          >
            <option value="">All services</option>
            {SERVICE_CATEGORIES.map((s) => (
              <option key={s} value={s}>
                {humaniseLabel(s)}
              </option>
            ))}
          </select>

          {!checking ? (
            <select
              className="h-9 rounded-md border bg-background px-2 text-sm"
              value={status}
              onChange={(e) => setStatus(e.target.value)}
            >
              <option value="">All statuses</option>
              {VENDOR_STATUSES.map((s) => (
                <option key={s} value={s}>
                  {humaniseLabel(s)}
                </option>
              ))}
            </select>
          ) : (
            <>
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
            </>
          )}

          <Button
            size="sm"
            variant={checking ? "secondary" : "outline"}
            className="h-9"
            onClick={() => setChecking((v) => !v)}
          >
            Check dates
          </Button>
        </div>
      }
      subToolbar={
        <span className="text-[12px] text-muted-foreground">
          {loading
            ? "Loading…"
            : checking
              ? `${total} vendors · ${rows.filter((v) => v.isAvailable).length} free between ${from} and ${to}`
              : `${total} vendors`}
        </span>
      }
      flush
    >
      <div className="min-h-0 flex-1 overflow-auto">
        <table className="w-full min-w-[900px] text-[13px]">
          <thead className="sticky top-0 z-10 bg-card shadow-[0_1px_0_var(--border)]">
            <tr className="text-left text-[11px] uppercase tracking-wide text-muted-foreground">
              <th className="px-4 py-2 font-medium">Vendor</th>
              <th className="px-2 py-2 font-medium">Services</th>
              <th className="px-2 py-2 font-medium">Contact</th>
              <th className="px-2 py-2 font-medium">City</th>
              <th className="px-2 py-2 text-right font-medium">Rates</th>
              <th className="px-2 py-2 text-right font-medium">From</th>
              <th className="px-2 py-2 text-center font-medium">Rating</th>
              <th className="px-2 py-2 text-center font-medium">
                {checking ? "On dates" : "Jobs/date"}
              </th>
              <th className="px-4 py-2 font-medium">Status</th>
            </tr>
          </thead>

          <tbody>
            {rows.map((v) => (
              <tr
                key={v.id}
                onClick={() => setOpenId(v.id)}
                className="cursor-pointer border-b last:border-0 hover:bg-muted/40"
              >
                <td className="px-4 py-2">
                  <div className="font-medium">{v.name}</div>
                  <div className="font-mono text-[10.5px] text-muted-foreground">{v.code}</div>
                </td>
                <td className="px-2 py-2 text-muted-foreground">
                  {v.services.map((s) => humaniseLabel(s)).join(", ")}
                </td>
                <td className="px-2 py-2 text-muted-foreground">
                  {v.contactPerson ?? "—"}
                  {v.phone ? (
                    <div className="text-[11px] tabular-nums">{v.phone}</div>
                  ) : null}
                </td>
                <td className="px-2 py-2 text-muted-foreground">{v.city ?? "—"}</td>
                <td className="px-2 py-2 text-right tabular-nums text-muted-foreground">
                  {v.rateCount || "—"}
                </td>
                <td className="px-2 py-2 text-right tabular-nums text-muted-foreground">
                  {v.lowestRate ? formatMoney(v.lowestRate) : "—"}
                </td>
                <td className="px-2 py-2 text-center">
                  {v.rating ? (
                    <span className="inline-flex items-center gap-0.5 tabular-nums">
                      <Star className="size-3 fill-amber-400 text-amber-400" />
                      {v.rating}
                    </span>
                  ) : (
                    <span className="text-muted-foreground">—</span>
                  )}
                </td>
                <td className="px-2 py-2 text-center tabular-nums">
                  {checking ? (
                    <span
                      className={cn(
                        "rounded px-1.5 py-0.5 text-[11px] font-medium",
                        v.isAvailable
                          ? "bg-emerald-50 text-emerald-700 dark:bg-emerald-950/40 dark:text-emerald-400"
                          : "bg-rose-50 text-rose-700 dark:bg-rose-950/40 dark:text-rose-400"
                      )}
                    >
                      {v.committedOnDates} / {v.concurrentEventCapacity}
                    </span>
                  ) : (
                    <span className="text-muted-foreground">{v.concurrentEventCapacity}</span>
                  )}
                </td>
                <td className="px-4 py-2">
                  <span
                    className={cn(
                      "rounded px-1.5 py-0.5 text-[11px] font-medium",
                      STATUS_TONE[v.status] ?? "bg-muted"
                    )}
                  >
                    {humaniseLabel(v.status)}
                  </span>
                  {v.expiringDocuments > 0 ? (
                    <AlertTriangle
                      className="ml-1 inline size-3.5 text-amber-500"
                      aria-label={`${v.expiringDocuments} document(s) expiring`}
                    />
                  ) : null}
                </td>
              </tr>
            ))}
          </tbody>
        </table>

        {!loading && rows.length === 0 ? (
          <p className="py-16 text-center text-[13px] text-muted-foreground">
            No vendors match that.
          </p>
        ) : null}
      </div>

      <Dialog open={creating} onOpenChange={setCreating}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>New vendor</DialogTitle>
          </DialogHeader>

          <div className="grid gap-3">
            <div>
              <Label className="text-[12px]">Name</Label>
              <Input
                className="mt-1 h-9"
                placeholder="Annapurna Caterers"
                value={form.name}
                onChange={(e) => setForm((f) => ({ ...f, name: e.target.value }))}
              />
            </div>

            <div className="grid grid-cols-2 gap-3">
              <div>
                <Label className="text-[12px]">Service</Label>
                <select
                  className="mt-1 h-9 w-full rounded-md border bg-background px-2 text-sm"
                  value={form.service}
                  onChange={(e) => setForm((f) => ({ ...f, service: e.target.value }))}
                >
                  {SERVICE_CATEGORIES.map((s) => (
                    <option key={s} value={s}>
                      {humaniseLabel(s)}
                    </option>
                  ))}
                </select>
              </div>
              <div>
                <Label className="text-[12px]">Jobs they can run per date</Label>
                <Input
                  className="mt-1 h-9"
                  value={form.capacity}
                  onChange={(e) => setForm((f) => ({ ...f, capacity: e.target.value }))}
                />
              </div>
            </div>

            <div className="grid grid-cols-3 gap-3">
              <div>
                <Label className="text-[12px]">Contact</Label>
                <Input
                  className="mt-1 h-9"
                  value={form.contactPerson}
                  onChange={(e) => setForm((f) => ({ ...f, contactPerson: e.target.value }))}
                />
              </div>
              <div>
                <Label className="text-[12px]">Phone</Label>
                <Input
                  className="mt-1 h-9"
                  value={form.phone}
                  onChange={(e) => setForm((f) => ({ ...f, phone: e.target.value }))}
                />
              </div>
              <div>
                <Label className="text-[12px]">City</Label>
                <Input
                  className="mt-1 h-9"
                  value={form.city}
                  onChange={(e) => setForm((f) => ({ ...f, city: e.target.value }))}
                />
              </div>
            </div>
          </div>

          <DialogFooter>
            <Button variant="outline" onClick={() => setCreating(false)}>
              Cancel
            </Button>
            <Button disabled={saving} onClick={() => void create()}>
              {saving ? "Adding…" : "Add vendor"}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <VendorSheet
        key={openId ?? "none"}
        vendorId={openId}
        open={openId !== null}
        onOpenChange={(open) => !open && setOpenId(null)}
        onChanged={() => setNonce((n) => n + 1)}
      />
    </PagePanel>
  );
}
