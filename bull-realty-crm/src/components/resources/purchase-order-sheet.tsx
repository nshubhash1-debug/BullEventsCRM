"use client";

import * as React from "react";
import { Banknote, Plus, Trash2 } from "lucide-react";
import { toast } from "sonner";

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
import { ApiError } from "@/lib/api";
import { formatMoney } from "@/lib/crm-api";
import {
  VENDOR_RATE_BASES,
  humaniseLabel,
  vendorsApi,
  type PurchaseOrder,
  type Vendor,
} from "@/lib/resources-api";
import { cn } from "@/lib/utils";

/** What an order may become next, given where it is now. */
const NEXT: Record<string, string[]> = {
  Draft: ["Sent", "Confirmed", "Cancelled"],
  Sent: ["Confirmed", "Cancelled"],
  Confirmed: ["Delivered", "Cancelled"],
  Delivered: ["Closed"],
  Closed: [],
  Cancelled: [],
};

function isoToday() {
  return new Date().toISOString().slice(0, 10);
}

export function PurchaseOrderSheet({
  orderId,
  open,
  onOpenChange,
  onChanged,
}: {
  orderId: number | null;
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onChanged?: () => void;
}) {
  const [order, setOrder] = React.useState<PurchaseOrder | null>(null);
  const [vendor, setVendor] = React.useState<Vendor | null>(null);
  const [busy, setBusy] = React.useState(false);

  const [line, setLine] = React.useState({
    description: "",
    quantity: "1",
    rate: "",
    sellRate: "",
    basis: "PerEvent",
    rateId: "",
  });

  const [payment, setPayment] = React.useState({
    amount: "",
    paidOn: isoToday(),
    kind: "Advance",
    mode: "NEFT",
  });

  const load = React.useCallback(() => {
    if (orderId === null) return;
    vendorsApi.order(orderId).then(setOrder).catch(() => setOrder(null));
  }, [orderId]);

  // Mounted under a key of the order id, so a different order is a fresh sheet.
  React.useEffect(() => load(), [load]);

  // The vendor's rate card, so a line can be dropped in rather than retyped.
  React.useEffect(() => {
    if (!order) return;
    vendorsApi.get(order.vendorId).then(setVendor).catch(() => setVendor(null));
  }, [order?.vendorId, order]);

  async function act(work: () => Promise<PurchaseOrder>, success: string) {
    setBusy(true);
    try {
      setOrder(await work());
      toast.success(success);
      onChanged?.();
      return true;
    } catch (error) {
      toast.error(error instanceof ApiError ? error.message : "That did not work.");
      return false;
    } finally {
      setBusy(false);
    }
  }

  if (!order) {
    return (
      <Sheet open={open} onOpenChange={onOpenChange}>
        <SheetContent className="w-full sm:max-w-2xl">
          <SheetHeader>
            <SheetTitle>Purchase order</SheetTitle>
            <SheetDescription>Loading…</SheetDescription>
          </SheetHeader>
        </SheetContent>
      </Sheet>
    );
  }

  const editable = order.status === "Draft" || order.status === "Sent";
  const nextStates = NEXT[order.status] ?? [];

  /** Fills the line form from a rate-card entry. */
  function pickRate(rateId: string) {
    const picked = vendor?.rates.find((r) => String(r.id) === rateId);
    setLine((l) => ({
      ...l,
      rateId,
      description: picked?.name ?? l.description,
      rate: picked ? String(picked.rate) : l.rate,
      sellRate: picked?.sellRate ? String(picked.sellRate) : l.sellRate,
      basis: picked?.basis ?? l.basis,
      // A per-plate line defaults to the guest count, which is what it will be.
      quantity:
        picked?.basis === "PerPlate" && order?.guestCount
          ? String(order.guestCount)
          : l.quantity,
    }));
  }

  return (
    <Sheet open={open} onOpenChange={onOpenChange}>
      <SheetContent className="flex w-full flex-col gap-0 overflow-y-auto p-0 sm:max-w-2xl">
        <SheetHeader className="border-b px-5 py-4">
          <SheetTitle className="flex items-center gap-2 text-[15px]">
            {order.vendorName}
            <span className="rounded bg-muted px-1.5 py-0.5 text-[11px] font-medium text-muted-foreground">
              {humaniseLabel(order.status)}
            </span>
          </SheetTitle>
          <SheetDescription className="text-xs">
            <span className="font-mono">{order.code}</span>
            {" · "}
            {humaniseLabel(order.service)}
            {order.eventName ? ` · ${order.eventName}` : ""}
            {" · "}
            {order.serviceDate.slice(0, 10)}
            {order.serviceEndDate !== order.serviceDate
              ? ` to ${order.serviceEndDate.slice(0, 10)}`
              : ""}
            {order.guestCount ? ` · ${order.guestCount} guests` : ""}
          </SheetDescription>
        </SheetHeader>

        {nextStates.length > 0 ? (
          <div className="flex flex-wrap gap-2 border-b px-5 py-3">
            {nextStates.map((next) => (
              <Button
                key={next}
                size="sm"
                variant={next === "Cancelled" ? "outline" : "default"}
                className="h-8"
                disabled={busy}
                onClick={() =>
                  void act(
                    () => vendorsApi.setOrderStatus(order.id, next),
                    next === "Confirmed"
                      ? "Confirmed — the supplier's date is held."
                      : `Marked ${humaniseLabel(next).toLowerCase()}.`
                  )
                }
              >
                {next === "Sent" ? "Mark sent" : humaniseLabel(next)}
              </Button>
            ))}
          </div>
        ) : null}

        {/* ---------------- lines ---------------- */}
        <div className="px-5 py-4">
          {order.lines.length === 0 ? (
            <p className="py-6 text-center text-[13px] text-muted-foreground">
              Nothing on this order yet.
            </p>
          ) : (
            <table className="w-full text-[12.5px]">
              <thead>
                <tr className="border-b text-left text-[10.5px] uppercase tracking-wide text-muted-foreground">
                  <th className="py-1.5 pr-2 font-medium">Item</th>
                  <th className="py-1.5 px-1 text-right font-medium">Qty</th>
                  <th className="py-1.5 px-1 text-right font-medium">Rate</th>
                  <th className="py-1.5 px-1 text-right font-medium">Cost</th>
                  <th className="py-1.5 px-1 text-right font-medium">Sell</th>
                  {editable ? <th className="w-8" /> : null}
                </tr>
              </thead>
              <tbody>
                {order.lines.map((l) => (
                  <tr key={l.id} className="border-b last:border-0">
                    <td className="py-1.5 pr-2">
                      <div className="font-medium">{l.description}</div>
                      <div className="text-[10.5px] text-muted-foreground">
                        {humaniseLabel(l.basis)}
                      </div>
                    </td>
                    <td className="px-1 py-1.5 text-right tabular-nums">{l.quantity}</td>
                    <td className="px-1 py-1.5 text-right tabular-nums">
                      {formatMoney(l.rate)}
                    </td>
                    <td className="px-1 py-1.5 text-right tabular-nums font-medium">
                      {formatMoney(l.lineCost)}
                    </td>
                    <td className="px-1 py-1.5 text-right tabular-nums text-muted-foreground">
                      {l.sellRate ? formatMoney(l.lineSell) : "—"}
                    </td>
                    {editable ? (
                      <td className="py-1.5">
                        <button
                          type="button"
                          className="text-muted-foreground hover:text-destructive"
                          onClick={() =>
                            void act(
                              () => vendorsApi.removeOrderLine(order.id, l.id),
                              "Line removed."
                            )
                          }
                        >
                          <Trash2 className="size-3.5" />
                        </button>
                      </td>
                    ) : null}
                  </tr>
                ))}
              </tbody>
            </table>
          )}

          {editable ? (
            <div className="mt-4 rounded-lg border p-3">
              <div className="mb-2 text-[13px] font-medium">Add a line</div>

              <div className="grid gap-2">
                {vendor && vendor.rates.length > 0 ? (
                  <div>
                    <Label className="text-[11px] text-muted-foreground">
                      From the rate card
                    </Label>
                    <select
                      className="mt-1 h-9 w-full rounded-md border bg-background px-2 text-sm"
                      value={line.rateId}
                      onChange={(e) => pickRate(e.target.value)}
                    >
                      <option value="">Type it in instead…</option>
                      {vendor.rates.map((r) => (
                        <option key={r.id} value={r.id}>
                          {r.name} — {formatMoney(r.rate)} {humaniseLabel(r.basis).toLowerCase()}
                        </option>
                      ))}
                    </select>
                  </div>
                ) : null}

                <Input
                  className="h-9"
                  placeholder="What they are supplying"
                  value={line.description}
                  onChange={(e) => setLine((l) => ({ ...l, description: e.target.value }))}
                />

                <div className="grid grid-cols-4 gap-2">
                  <div>
                    <Label className="text-[11px] text-muted-foreground">Qty</Label>
                    <Input
                      className="mt-1 h-9"
                      value={line.quantity}
                      onChange={(e) => setLine((l) => ({ ...l, quantity: e.target.value }))}
                    />
                  </div>
                  <div>
                    <Label className="text-[11px] text-muted-foreground">Cost</Label>
                    <Input
                      className="mt-1 h-9"
                      value={line.rate}
                      onChange={(e) => setLine((l) => ({ ...l, rate: e.target.value }))}
                    />
                  </div>
                  <div>
                    <Label className="text-[11px] text-muted-foreground">Sell</Label>
                    <Input
                      className="mt-1 h-9"
                      value={line.sellRate}
                      onChange={(e) => setLine((l) => ({ ...l, sellRate: e.target.value }))}
                    />
                  </div>
                  <div>
                    <Label className="text-[11px] text-muted-foreground">Basis</Label>
                    <select
                      className="mt-1 h-9 w-full rounded-md border bg-background px-1 text-[12px]"
                      value={line.basis}
                      onChange={(e) => setLine((l) => ({ ...l, basis: e.target.value }))}
                    >
                      {VENDOR_RATE_BASES.map((b) => (
                        <option key={b} value={b}>
                          {humaniseLabel(b)}
                        </option>
                      ))}
                    </select>
                  </div>
                </div>

                <Button
                  className="h-9"
                  disabled={busy}
                  onClick={() => {
                    if (!line.description.trim() || !line.rate) {
                      toast.error("A line needs a description and a cost.");
                      return;
                    }
                    void act(
                      () =>
                        vendorsApi.addOrderLine(order.id, {
                          description: line.description.trim(),
                          quantity: Number(line.quantity) || 1,
                          rate: Number(line.rate) || 0,
                          sellRate: line.sellRate ? Number(line.sellRate) : null,
                          basis: line.basis,
                          vendorRateId: line.rateId ? Number(line.rateId) : null,
                        }),
                      "Line added."
                    ).then((ok) => {
                      if (ok) {
                        setLine({
                          description: "",
                          quantity: "1",
                          rate: "",
                          sellRate: "",
                          basis: "PerEvent",
                          rateId: "",
                        });
                      }
                    });
                  }}
                >
                  <Plus className="mr-1 size-3.5" />
                  Add line
                </Button>
              </div>
            </div>
          ) : null}

          {/* ---------------- money ---------------- */}
          <div className="mt-4 rounded-lg border">
            <div className="grid grid-cols-3 divide-x text-center">
              <div className="p-3">
                <div className="text-[11px] uppercase tracking-wide text-muted-foreground">
                  We pay
                </div>
                <div className="mt-0.5 text-lg font-semibold tabular-nums">
                  {formatMoney(order.totalCost)}
                </div>
              </div>
              <div className="p-3">
                <div className="text-[11px] uppercase tracking-wide text-muted-foreground">
                  Client pays
                </div>
                <div className="mt-0.5 text-lg font-semibold tabular-nums">
                  {order.totalSell ? formatMoney(order.totalSell) : "—"}
                </div>
              </div>
              <div className="p-3">
                <div className="text-[11px] uppercase tracking-wide text-muted-foreground">
                  Margin
                </div>
                <div
                  className={cn(
                    "mt-0.5 text-lg font-semibold tabular-nums",
                    order.margin < 0
                      ? "text-rose-600"
                      : "text-emerald-600 dark:text-emerald-400"
                  )}
                >
                  {order.totalSell ? formatMoney(order.margin) : "—"}
                </div>
              </div>
            </div>

            <div className="flex items-center justify-between border-t px-3 py-2 text-[12.5px]">
              <span className="text-muted-foreground">
                Paid {formatMoney(order.amountPaid)}
              </span>
              <span
                className={cn(
                  "font-semibold tabular-nums",
                  order.amountDue > 0 ? "text-amber-600" : "text-emerald-600"
                )}
              >
                {order.amountDue > 0 ? `${formatMoney(order.amountDue)} due` : "Settled"}
              </span>
            </div>
          </div>

          {/* ---------------- payments ---------------- */}
          {order.payments.length > 0 ? (
            <ul className="mt-3 space-y-1">
              {order.payments.map((p) => (
                <li
                  key={p.id}
                  className="flex items-center justify-between rounded border px-3 py-1.5 text-[12px]"
                >
                  <span>
                    {p.kind} · {p.mode}
                    {p.reference ? ` · ${p.reference}` : ""}
                  </span>
                  <span className="tabular-nums">
                    {formatMoney(p.amount)}
                    <span className="ml-2 text-muted-foreground">
                      {p.paidOn.slice(0, 10)}
                    </span>
                  </span>
                </li>
              ))}
            </ul>
          ) : null}

          {order.status !== "Cancelled" && order.amountDue > 0 ? (
            <div className="mt-3 rounded-lg border p-3">
              <div className="mb-2 flex items-center gap-1.5 text-[13px] font-medium">
                <Banknote className="size-3.5 text-muted-foreground" />
                Record a payment
              </div>

              <div className="grid grid-cols-4 gap-2">
                <div>
                  <Label className="text-[11px] text-muted-foreground">Amount</Label>
                  <Input
                    className="mt-1 h-9"
                    value={payment.amount}
                    onChange={(e) => setPayment((p) => ({ ...p, amount: e.target.value }))}
                  />
                </div>
                <div>
                  <Label className="text-[11px] text-muted-foreground">Paid on</Label>
                  <Input
                    type="date"
                    className="mt-1 h-9"
                    value={payment.paidOn}
                    onChange={(e) => setPayment((p) => ({ ...p, paidOn: e.target.value }))}
                  />
                </div>
                <div>
                  <Label className="text-[11px] text-muted-foreground">Kind</Label>
                  <select
                    className="mt-1 h-9 w-full rounded-md border bg-background px-1 text-[12px]"
                    value={payment.kind}
                    onChange={(e) => setPayment((p) => ({ ...p, kind: e.target.value }))}
                  >
                    {["Advance", "Milestone", "Final", "Retention"].map((k) => (
                      <option key={k} value={k}>
                        {k}
                      </option>
                    ))}
                  </select>
                </div>
                <div>
                  <Label className="text-[11px] text-muted-foreground">Mode</Label>
                  <select
                    className="mt-1 h-9 w-full rounded-md border bg-background px-1 text-[12px]"
                    value={payment.mode}
                    onChange={(e) => setPayment((p) => ({ ...p, mode: e.target.value }))}
                  >
                    {["NEFT", "UPI", "Cheque", "Cash"].map((m) => (
                      <option key={m} value={m}>
                        {m}
                      </option>
                    ))}
                  </select>
                </div>
              </div>

              <Button
                className="mt-2 h-9 w-full"
                disabled={busy}
                onClick={() => {
                  const amount = Number(payment.amount);
                  if (!Number.isFinite(amount) || amount <= 0) {
                    toast.error("Enter what was paid.");
                    return;
                  }
                  void act(
                    () =>
                      vendorsApi.addPayment(order.id, {
                        amount,
                        paidOn: payment.paidOn,
                        kind: payment.kind,
                        mode: payment.mode,
                      }),
                    "Payment recorded."
                  ).then((ok) => {
                    if (ok) setPayment((p) => ({ ...p, amount: "" }));
                  });
                }}
              >
                Record payment
              </Button>
            </div>
          ) : null}
        </div>
      </SheetContent>
    </Sheet>
  );
}
