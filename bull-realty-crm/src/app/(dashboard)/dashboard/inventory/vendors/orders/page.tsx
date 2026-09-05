"use client";

import * as React from "react";
import { FileSignature, Plus } from "lucide-react";
import { toast } from "sonner";

import { PurchaseOrderSheet } from "@/components/resources/purchase-order-sheet";
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
  PO_STATUSES,
  SERVICE_CATEGORIES,
  humaniseLabel,
  vendorsApi,
  type PurchaseOrder,
  type Vendor,
} from "@/lib/resources-api";
import { cn } from "@/lib/utils";

const STATUS_TONE: Record<string, string> = {
  Draft: "bg-muted text-muted-foreground",
  Sent: "bg-sky-100 text-sky-800 dark:bg-sky-950/50 dark:text-sky-300",
  Confirmed: "bg-emerald-100 text-emerald-800 dark:bg-emerald-950/50 dark:text-emerald-300",
  Delivered: "bg-violet-100 text-violet-800 dark:bg-violet-950/50 dark:text-violet-300",
  Closed: "bg-muted text-muted-foreground",
  Cancelled: "bg-muted text-muted-foreground line-through",
};

function isoToday(offsetDays = 0) {
  const date = new Date();
  date.setDate(date.getDate() + offsetDays);
  return date.toISOString().slice(0, 10);
}

export default function PurchaseOrdersPage() {
  const [rows, setRows] = React.useState<PurchaseOrder[]>([]);
  const [status, setStatus] = React.useState("");
  const [unpaidOnly, setUnpaidOnly] = React.useState(false);
  const [openId, setOpenId] = React.useState<number | null>(null);
  const [nonce, setNonce] = React.useState(0);
  const [loadedKey, setLoadedKey] = React.useState<string | null>(null);

  const [creating, setCreating] = React.useState(false);
  const [vendors, setVendors] = React.useState<Vendor[]>([]);
  const [saving, setSaving] = React.useState(false);
  const [form, setForm] = React.useState({
    vendorId: "",
    service: "Catering",
    eventName: "",
    clientName: "",
    guestCount: "",
    serviceDate: isoToday(7),
    serviceEndDate: isoToday(7),
  });

  const queryKey = JSON.stringify({ status, unpaidOnly, nonce });
  const loading = loadedKey !== queryKey;

  React.useEffect(() => {
    let cancelled = false;

    vendorsApi
      .orders({ status: status || undefined, unpaidOnly, take: 300 })
      .then((r) => !cancelled && setRows(r))
      .catch(() => !cancelled && setRows([]))
      .finally(() => !cancelled && setLoadedKey(queryKey));

    return () => {
      cancelled = true;
    };
  }, [status, unpaidOnly, nonce, queryKey]);

  React.useEffect(() => {
    vendorsApi
      .query({ page: 1, pageSize: 300, sort: [{ field: "name", descending: false }] })
      .then((r) => setVendors(r.items))
      .catch(() => setVendors([]));
  }, []);

  async function create() {
    if (!form.vendorId) {
      toast.error("Pick a vendor.");
      return;
    }

    setSaving(true);
    try {
      const created = await vendorsApi.createOrder({
        vendorId: Number(form.vendorId),
        service: form.service,
        serviceDate: form.serviceDate,
        serviceEndDate: form.serviceEndDate,
        eventName: form.eventName.trim() || null,
        clientName: form.clientName.trim() || null,
        guestCount: form.guestCount ? Number(form.guestCount) : null,
      });
      toast.success(`${created.code} created. Add what they are supplying.`);
      setCreating(false);
      setForm((f) => ({ ...f, eventName: "", clientName: "", guestCount: "" }));
      setNonce((n) => n + 1);
      setOpenId(created.id);
    } catch (error) {
      toast.error(error instanceof ApiError ? error.message : "Could not create that.");
    } finally {
      setSaving(false);
    }
  }

  const owed = rows
    .filter((o) => o.status !== "Cancelled")
    .reduce((sum, o) => sum + o.amountDue, 0);

  return (
    <PagePanel
      icon={FileSignature}
      title="Purchase orders"
      hint="What each supplier has been engaged to do, what it costs, and what is still owed. Confirming one holds their date."
      actions={
        <Button size="sm" className="h-8" onClick={() => setCreating(true)}>
          <Plus className="mr-1 size-3.5" />
          New order
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
            {PO_STATUSES.map((s) => (
              <option key={s} value={s}>
                {humaniseLabel(s)}
              </option>
            ))}
          </select>

          <Button
            size="sm"
            variant={unpaidOnly ? "secondary" : "outline"}
            className="h-9"
            onClick={() => setUnpaidOnly((v) => !v)}
          >
            Unpaid only
          </Button>
        </div>
      }
      subToolbar={
        <span className="text-[12px] text-muted-foreground">
          {loading ? "Loading…" : `${rows.length} orders · ${formatMoney(owed)} still owed`}
        </span>
      }
      flush
    >
      <div className="min-h-0 flex-1 overflow-auto">
        <table className="w-full min-w-[980px] text-[13px]">
          <thead className="sticky top-0 z-10 bg-card shadow-[0_1px_0_var(--border)]">
            <tr className="text-left text-[11px] uppercase tracking-wide text-muted-foreground">
              <th className="px-4 py-2 font-medium">Order</th>
              <th className="px-2 py-2 font-medium">Vendor</th>
              <th className="px-2 py-2 font-medium">Service</th>
              <th className="px-2 py-2 font-medium">Event</th>
              <th className="px-2 py-2 font-medium">Date</th>
              <th className="px-2 py-2 text-right font-medium">Cost</th>
              <th className="px-2 py-2 text-right font-medium">Sell</th>
              <th className="px-2 py-2 text-right font-medium">Margin</th>
              <th className="px-2 py-2 text-right font-medium">Due</th>
              <th className="px-4 py-2 font-medium">Status</th>
            </tr>
          </thead>

          <tbody>
            {rows.map((o) => (
              <tr
                key={o.id}
                onClick={() => setOpenId(o.id)}
                className="cursor-pointer border-b last:border-0 hover:bg-muted/40"
              >
                <td className="px-4 py-2 font-mono text-[11.5px]">{o.code}</td>
                <td className="px-2 py-2">
                  <div className="font-medium">{o.vendorName}</div>
                  {o.vendorPhone ? (
                    <div className="text-[11px] tabular-nums text-muted-foreground">
                      {o.vendorPhone}
                    </div>
                  ) : null}
                </td>
                <td className="px-2 py-2 text-muted-foreground">
                  {humaniseLabel(o.service)}
                </td>
                <td className="px-2 py-2">
                  <div>{o.eventName ?? "—"}</div>
                  {o.clientName ? (
                    <div className="text-[11px] text-muted-foreground">{o.clientName}</div>
                  ) : null}
                </td>
                <td className="px-2 py-2 tabular-nums text-muted-foreground">
                  {o.serviceDate.slice(0, 10)}
                </td>
                <td className="px-2 py-2 text-right tabular-nums">
                  {formatMoney(o.totalCost)}
                </td>
                <td className="px-2 py-2 text-right tabular-nums text-muted-foreground">
                  {o.totalSell ? formatMoney(o.totalSell) : "—"}
                </td>
                <td
                  className={cn(
                    "px-2 py-2 text-right tabular-nums",
                    o.margin < 0
                      ? "font-semibold text-rose-600"
                      : "text-emerald-600 dark:text-emerald-400"
                  )}
                >
                  {o.totalSell ? formatMoney(o.margin) : "—"}
                </td>
                <td
                  className={cn(
                    "px-2 py-2 text-right tabular-nums",
                    o.amountDue > 0 ? "font-semibold text-amber-600" : "text-muted-foreground"
                  )}
                >
                  {o.amountDue > 0 ? formatMoney(o.amountDue) : "—"}
                </td>
                <td className="px-4 py-2">
                  <span
                    className={cn(
                      "rounded px-1.5 py-0.5 text-[11px] font-medium",
                      STATUS_TONE[o.status] ?? "bg-muted"
                    )}
                  >
                    {humaniseLabel(o.status)}
                  </span>
                </td>
              </tr>
            ))}
          </tbody>
        </table>

        {!loading && rows.length === 0 ? (
          <p className="py-16 text-center text-[13px] text-muted-foreground">
            No purchase orders yet. Raise one when a supplier is engaged for an event.
          </p>
        ) : null}
      </div>

      <Dialog open={creating} onOpenChange={setCreating}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>New purchase order</DialogTitle>
            <DialogDescription>
              The dates are the whole window the supplier is committed for. Confirming
              the order is what holds them against it.
            </DialogDescription>
          </DialogHeader>

          <div className="grid gap-3">
            <div className="grid grid-cols-2 gap-3">
              <div>
                <Label className="text-[12px]">Vendor</Label>
                <select
                  className="mt-1 h-9 w-full rounded-md border bg-background px-2 text-sm"
                  value={form.vendorId}
                  onChange={(e) => setForm((f) => ({ ...f, vendorId: e.target.value }))}
                >
                  <option value="">Select…</option>
                  {vendors.map((v) => (
                    <option key={v.id} value={v.id}>
                      {v.name}
                    </option>
                  ))}
                </select>
              </div>
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
            </div>

            <div className="grid grid-cols-3 gap-3">
              <div className="col-span-2">
                <Label className="text-[12px]">Event</Label>
                <Input
                  className="mt-1 h-9"
                  placeholder="Malhotra reception"
                  value={form.eventName}
                  onChange={(e) => setForm((f) => ({ ...f, eventName: e.target.value }))}
                />
              </div>
              <div>
                <Label className="text-[12px]">Guests</Label>
                <Input
                  className="mt-1 h-9"
                  value={form.guestCount}
                  onChange={(e) => setForm((f) => ({ ...f, guestCount: e.target.value }))}
                />
              </div>
            </div>

            <div className="grid grid-cols-3 gap-3">
              <div>
                <Label className="text-[12px]">Client</Label>
                <Input
                  className="mt-1 h-9"
                  value={form.clientName}
                  onChange={(e) => setForm((f) => ({ ...f, clientName: e.target.value }))}
                />
              </div>
              <div>
                <Label className="text-[12px]">From</Label>
                <Input
                  type="date"
                  className="mt-1 h-9"
                  value={form.serviceDate}
                  onChange={(e) =>
                    setForm((f) => ({
                      ...f,
                      serviceDate: e.target.value,
                      serviceEndDate:
                        f.serviceEndDate < e.target.value ? e.target.value : f.serviceEndDate,
                    }))
                  }
                />
              </div>
              <div>
                <Label className="text-[12px]">To</Label>
                <Input
                  type="date"
                  className="mt-1 h-9"
                  value={form.serviceEndDate}
                  onChange={(e) => setForm((f) => ({ ...f, serviceEndDate: e.target.value }))}
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

      <PurchaseOrderSheet
        key={openId ?? "none"}
        orderId={openId}
        open={openId !== null}
        onOpenChange={(open) => !open && setOpenId(null)}
        onChanged={() => setNonce((n) => n + 1)}
      />
    </PagePanel>
  );
}
