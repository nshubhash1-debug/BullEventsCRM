"use client";

import * as React from "react";
import Link from "next/link";
import { useSearchParams } from "next/navigation";
import {
  AlertTriangle,
  BadgeIndianRupee,
  HardHat,
  Loader2,
  ReceiptText,
  Send,
} from "lucide-react";
import { toast } from "sonner";

import { PagePanel } from "@/components/shell/page-panel";
import { CrmLoadingState } from "@/components/shell/crm-loader";
import { MetricStrip, Pill, TONE_TEXT, type Metric } from "@/components/crm/metrics";
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
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { ApiError } from "@/lib/api";
import {
  AGEING_BUCKETS,
  CONSTRUCTION_STAGES,
  inr,
  inrShort,
  postSalesApi,
  shortDate,
  type DemandRow,
  type ReceiptRow,
} from "@/lib/post-sales-api";
import {
  bucketTone,
  demandStatusTone,
  humanise,
  receiptStatusTone,
} from "@/lib/post-sales-tones";
import { cn } from "@/lib/utils";

export default function CollectionsPage() {
  return (
    <React.Suspense
      fallback={
        <PagePanel icon={BadgeIndianRupee} title="Collections">
          <CrmLoadingState label="Loading the collections desk" />
        </PagePanel>
      }
    >
      <CollectionsDesk />
    </React.Suspense>
  );
}

/**
 * The collections desk.
 *
 * Two lists, because collections is two jobs: chasing what is owed and
 * recording what arrives. They share one screen so the person doing the
 * chasing can see the payment land without navigating anywhere.
 */
function CollectionsDesk() {
  const params = useSearchParams();
  const bookingId = Number(params.get("bookingId")) || undefined;

  const [demands, setDemands] = React.useState<DemandRow[] | null>(null);
  const [receipts, setReceipts] = React.useState<ReceiptRow[] | null>(null);
  const [bucket, setBucket] = React.useState(params.get("bucket") ?? "all");
  const [status, setStatus] = React.useState("open");
  const [running, setRunning] = React.useState(false);

  // Clearing the list is a consequence of somebody changing a filter, so it
  // happens in the handler that changed it. The effect only fetches — putting
  // the reset here too would make every fetch a second render pass.
  const refilter = React.useCallback((apply: () => void) => {
    setDemands(null);
    apply();
  }, []);

  const load = React.useCallback(() => {
    postSalesApi
      .demands({
        bookingId,
        bucket: bucket === "all" ? undefined : bucket,
        status: status === "open" || status === "all" ? undefined : status,
      })
      .then((rows) =>
        // "Open" is not a status the server stores — it is the working set:
        // everything that still has money against it.
        setDemands(status === "open" ? rows.filter((row) => row.outstanding > 0) : rows)
      )
      .catch((error: unknown) => {
        toast.error("Could not load demands", {
          description: error instanceof ApiError ? error.message : "Network error.",
        });
        setDemands([]);
      });

    postSalesApi
      .receipts({ bookingId })
      .then(setReceipts)
      .catch(() => setReceipts([]));
  }, [bookingId, bucket, status]);

  React.useEffect(load, [load]);

  const metrics: Metric[] = React.useMemo(() => {
    const list = demands ?? [];
    const outstanding = list.reduce((sum, d) => sum + d.outstanding, 0);
    const interest = list.reduce((sum, d) => sum + d.interestDue, 0);
    const overdue = list.filter((d) => d.ageDays > 0);
    const overdueAmount = overdue.reduce((sum, d) => sum + d.outstanding, 0);

    return [
      { label: "Demands", value: list.length, icon: ReceiptText },
      { label: "Outstanding", value: inrShort(outstanding), tone: "danger" },
      {
        label: "Overdue",
        value: inrShort(overdueAmount),
        tone: overdue.length > 0 ? "danger" : "success",
        hint: `${overdue.length} past due`,
        icon: AlertTriangle,
      },
      {
        label: "Interest accrued",
        value: inrShort(interest),
        tone: interest > 0 ? "warning" : "neutral",
      },
      {
        label: "Oldest",
        value: overdue.length > 0 ? `${Math.max(...overdue.map((d) => d.ageDays))}d` : "—",
        tone: "neutral",
      },
    ];
  }, [demands]);

  return (
    <PagePanel
      icon={BadgeIndianRupee}
      title="Collections"
      hint="Demands raised against payment plans, and the money that answers them."
      actions={
        <Button size="sm" className="gap-1.5" onClick={() => setRunning(true)}>
          <HardHat className="size-3.5" />
          Stage demand run
        </Button>
      }
      toolbar={
        <div className="flex flex-wrap items-center gap-2">
          <Select value={status} onValueChange={(next) => refilter(() => setStatus(next))}>
            <SelectTrigger size="sm" className="w-[170px] text-[12.5px]">
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="open">Open demands</SelectItem>
              <SelectItem value="all">Every demand</SelectItem>
              <SelectItem value="Raised">Raised</SelectItem>
              <SelectItem value="PartlyPaid">Partly paid</SelectItem>
              <SelectItem value="Overdue">Overdue</SelectItem>
              <SelectItem value="Paid">Paid</SelectItem>
              <SelectItem value="Waived">Waived</SelectItem>
            </SelectContent>
          </Select>

          <div className="flex items-center gap-1">
            <Button
              variant={bucket === "all" ? "default" : "outline"}
              size="sm"
              className="h-8 text-[12px]"
              onClick={() => refilter(() => setBucket("all"))}
            >
              All ages
            </Button>
            {AGEING_BUCKETS.map((value) => (
              <Button
                key={value}
                variant={bucket === value ? "default" : "outline"}
                size="sm"
                className="h-8 text-[12px]"
                onClick={() => refilter(() => setBucket(value))}
              >
                {value === "Current" ? "Not due" : value}
              </Button>
            ))}
          </div>

          {bookingId ? (
            <Link
              href={`/dashboard/post-sales/bookings/${bookingId}`}
              className="text-[12px] text-primary hover:underline"
            >
              Filtered to one booking — open it
            </Link>
          ) : null}
        </div>
      }
    >
      <div className="flex min-h-0 flex-col gap-3">
        <MetricStrip metrics={metrics} loading={demands === null} />

        <Tabs defaultValue="demands" className="flex min-h-0 flex-1 flex-col">
          <TabsList className="w-full justify-start">
            <TabsTrigger value="demands">
              Demands
              {demands ? (
                <span className="ml-1.5 text-muted-foreground tabular-nums">
                  {demands.length}
                </span>
              ) : null}
            </TabsTrigger>
            <TabsTrigger value="receipts">
              Receipts
              {receipts ? (
                <span className="ml-1.5 text-muted-foreground tabular-nums">
                  {receipts.length}
                </span>
              ) : null}
            </TabsTrigger>
          </TabsList>

          <TabsContent value="demands" className="mt-3 min-h-0 flex-1">
            <DemandsTable rows={demands} onChanged={load} />
          </TabsContent>

          <TabsContent value="receipts" className="mt-3 min-h-0 flex-1">
            <ReceiptsTable rows={receipts} />
          </TabsContent>
        </Tabs>
      </div>

      <StageRunDialog open={running} onOpenChange={setRunning} onDone={load} />
    </PagePanel>
  );
}

/* ------------------------------------------------------------------ *
 * Demands
 * ------------------------------------------------------------------ */

function DemandsTable({
  rows,
  onChanged,
}: {
  rows: DemandRow[] | null;
  onChanged: () => void;
}) {
  const [busy, setBusy] = React.useState<number | null>(null);

  if (rows === null) return <CrmLoadingState label="Loading demands" />;

  if (rows.length === 0) {
    return (
      <p className="py-12 text-center text-[13px] text-muted-foreground">
        No demand matches these filters.
      </p>
    );
  }

  async function waiveInterest(demand: DemandRow) {
    const reason = window.prompt(
      `Waive ${inr(demand.interestDue)} of interest on ${demand.demandNumber}?\n\nReason, for the audit trail:`
    );
    if (!reason?.trim()) return;

    setBusy(demand.id);
    try {
      await postSalesApi.waiveInterest(demand.id, reason.trim());
      toast.success("Interest waived", { description: demand.demandNumber });
      onChanged();
    } catch (error) {
      toast.error("Could not waive the interest", {
        description: error instanceof ApiError ? error.message : "Network error.",
      });
    } finally {
      setBusy(null);
    }
  }

  return (
    <div className="min-h-0 overflow-auto rounded-lg border">
      <table className="w-full min-w-[1040px] text-[12.5px]">
        <thead className="sticky top-0 z-10 bg-muted/70 backdrop-blur">
          <tr className="text-[10.5px] tracking-wide text-muted-foreground uppercase">
            <th className="px-3 py-2 text-left font-medium">Demand</th>
            <th className="px-3 py-2 text-left font-medium">Customer</th>
            <th className="px-3 py-2 text-left font-medium">Unit</th>
            <th className="px-3 py-2 text-right font-medium">Raised</th>
            <th className="px-3 py-2 text-right font-medium">Received</th>
            <th className="px-3 py-2 text-right font-medium">Outstanding</th>
            <th className="px-3 py-2 text-right font-medium">Interest</th>
            <th className="px-3 py-2 text-left font-medium">Age</th>
            <th className="px-3 py-2 text-left font-medium">Status</th>
            <th className="px-3 py-2" />
          </tr>
        </thead>

        <tbody>
          {rows.map((demand) => (
            <tr key={demand.id} className="border-t align-top hover:bg-muted/40">
              <td className="px-3 py-2">
                <p className="font-medium">{demand.label}</p>
                <p className="text-[11px] text-muted-foreground">
                  {demand.demandNumber} · raised {shortDate(demand.raisedOn)}
                </p>
              </td>

              <td className="px-3 py-2">
                <Link
                  href={`/dashboard/post-sales/bookings/${demand.bookingId}`}
                  className="font-medium text-primary hover:underline"
                >
                  {demand.customerName}
                </Link>
                {demand.customerPhone ? (
                  <p className="text-[11px] tabular-nums text-muted-foreground">
                    {demand.customerPhone}
                  </p>
                ) : null}
              </td>

              <td className="px-3 py-2">
                <p>
                  {demand.towerName ? `${demand.towerName} · ` : ""}
                  {demand.unitNumber}
                </p>
                <p className="text-[11px] text-muted-foreground">{demand.projectName}</p>
              </td>

              <td className="px-3 py-2 text-right tabular-nums">{inr(demand.totalAmount)}</td>

              <td className={cn("px-3 py-2 text-right tabular-nums", demand.received > 0 && TONE_TEXT.success)}>
                {demand.received > 0 ? inr(demand.received) : "—"}
              </td>

              <td
                className={cn(
                  "px-3 py-2 text-right font-medium tabular-nums",
                  demand.outstanding > 0 ? TONE_TEXT.danger : TONE_TEXT.success
                )}
              >
                {inr(demand.outstanding)}
              </td>

              <td className="px-3 py-2 text-right tabular-nums">
                {demand.interestDue > 0 ? (
                  <>
                    <span className={TONE_TEXT.warning}>{inr(demand.interestDue)}</span>
                    <p className="text-[11px] text-muted-foreground">
                      {demand.interestRatePercent}% p.a.
                    </p>
                  </>
                ) : (
                  <span className="text-muted-foreground">—</span>
                )}
              </td>

              <td className="px-3 py-2 whitespace-nowrap">
                <Pill tone={bucketTone(demand.bucket)}>
                  {demand.bucket === "Current" ? "Not due" : `${demand.bucket} d`}
                </Pill>
                <p className="mt-0.5 text-[11px] text-muted-foreground">
                  due {shortDate(demand.dueDate)}
                </p>
              </td>

              <td className="px-3 py-2">
                <Pill tone={demandStatusTone(demand.status)}>{humanise(demand.status)}</Pill>
              </td>

              <td className="px-3 py-2 text-right whitespace-nowrap">
                {busy === demand.id ? (
                  <Loader2 className="ml-auto size-3.5 animate-spin text-muted-foreground" />
                ) : demand.interestDue > 0 ? (
                  <Button
                    size="sm"
                    variant="ghost"
                    className="h-6 px-2 text-[11px]"
                    onClick={() => waiveInterest(demand)}
                  >
                    Waive interest
                  </Button>
                ) : null}
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

/* ------------------------------------------------------------------ *
 * Receipts
 * ------------------------------------------------------------------ */

function ReceiptsTable({ rows }: { rows: ReceiptRow[] | null }) {
  if (rows === null) return <CrmLoadingState label="Loading receipts" />;

  if (rows.length === 0) {
    return (
      <p className="py-12 text-center text-[13px] text-muted-foreground">
        No receipt recorded yet.
      </p>
    );
  }

  return (
    <div className="min-h-0 overflow-auto rounded-lg border">
      <table className="w-full min-w-[900px] text-[12.5px]">
        <thead className="sticky top-0 z-10 bg-muted/70 backdrop-blur">
          <tr className="text-[10.5px] tracking-wide text-muted-foreground uppercase">
            <th className="px-3 py-2 text-left font-medium">Receipt</th>
            <th className="px-3 py-2 text-left font-medium">Customer</th>
            <th className="px-3 py-2 text-left font-medium">Mode</th>
            <th className="px-3 py-2 text-right font-medium">Paid</th>
            <th className="px-3 py-2 text-right font-medium">TDS</th>
            <th className="px-3 py-2 text-right font-medium">Credited</th>
            <th className="px-3 py-2 text-left font-medium">Status</th>
          </tr>
        </thead>

        <tbody>
          {rows.map((receipt) => (
            <tr key={receipt.id} className="border-t hover:bg-muted/40">
              <td className="px-3 py-2">
                <p className="font-medium">{receipt.receiptNumber}</p>
                <p className="text-[11px] text-muted-foreground">
                  {shortDate(receipt.receivedOn)}
                </p>
              </td>
              <td className="px-3 py-2">
                <Link
                  href={`/dashboard/post-sales/bookings/${receipt.bookingId}`}
                  className="font-medium text-primary hover:underline"
                >
                  {receipt.customerName}
                </Link>
                <p className="text-[11px] text-muted-foreground">
                  {receipt.bookingNumber} · {receipt.unitNumber}
                </p>
              </td>
              <td className="px-3 py-2">
                {receipt.mode}
                {receipt.instrument ? (
                  <p className="text-[11px] text-muted-foreground">{receipt.instrument}</p>
                ) : null}
              </td>
              <td className="px-3 py-2 text-right tabular-nums">{inr(receipt.amount)}</td>
              <td className="px-3 py-2 text-right tabular-nums text-muted-foreground">
                {receipt.tdsAmount > 0 ? inr(receipt.tdsAmount) : "—"}
              </td>
              <td className={cn("px-3 py-2 text-right font-medium tabular-nums", TONE_TEXT.success)}>
                {inr(receipt.creditedAmount)}
              </td>
              <td className="px-3 py-2">
                <Pill tone={receiptStatusTone(receipt.status)}>{receipt.status}</Pill>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

/* ------------------------------------------------------------------ *
 * The stage run
 * ------------------------------------------------------------------ */

/**
 * The slab-cast run.
 *
 * When a slab is cast, every construction-linked booking in that tower owes the
 * same instalment on the same day. Doing that one booking at a time is how a
 * developer loses a fortnight of cash flow, so this raises the whole set in one
 * pass and reports what it skipped and why.
 */
function StageRunDialog({
  open,
  onOpenChange,
  onDone,
}: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onDone: () => void;
}) {
  const [stage, setStage] = React.useState<string>("Slab");
  const [tower, setTower] = React.useState("");
  const [dueDate, setDueDate] = React.useState("");
  const [rate, setRate] = React.useState("12");
  const [saving, setSaving] = React.useState(false);

  const [wasOpen, setWasOpen] = React.useState(open);

  if (open !== wasOpen) {
    setWasOpen(open);

    if (open) {
      const due = new Date();
      due.setDate(due.getDate() + 21);

      setDueDate(due.toISOString().slice(0, 10));
      setTower("");
      setRate("12");
    }
  }

  async function run() {
    setSaving(true);
    try {
      const result = await postSalesApi.bulkRaise({
        constructionStage: stage,
        tower: tower.trim() || null,
        dueDate,
        interestRatePercent: Number(rate) || 0,
      });

      if (result.raised === 0) {
        toast.info("Nothing to raise", {
          description:
            result.reasons[0] ?? "No pending instalment sits on that stage.",
        });
      } else {
        toast.success(`${result.raised} demands raised`, {
          description: `${inr(result.totalAmount)} now due${
            result.skipped > 0 ? ` · ${result.skipped} skipped` : ""
          }.`,
        });
      }

      onOpenChange(false);
      onDone();
    } catch (error) {
      toast.error("The run failed", {
        description: error instanceof ApiError ? error.message : "Network error.",
      });
    } finally {
      setSaving(false);
    }
  }

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-md">
        <DialogHeader>
          <DialogTitle>Raise demands for a delivery stage</DialogTitle>
          <DialogDescription>
            Every pending instalment tied to this stage is raised at once, across all
            construction-linked bookings.
          </DialogDescription>
        </DialogHeader>

        <div className="grid gap-3">
          <div className="grid gap-1.5">
            <Label>Stage completed</Label>
            <Select value={stage} onValueChange={setStage}>
              <SelectTrigger>
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                {CONSTRUCTION_STAGES.map((value) => (
                  <SelectItem key={value} value={value}>
                    {value}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>

          <div className="grid gap-3 sm:grid-cols-3">
            <div className="grid gap-1.5">
              <Label htmlFor="tower">Tower</Label>
              <Input
                id="tower"
                value={tower}
                onChange={(event) => setTower(event.target.value)}
                placeholder="All"
              />
            </div>

            <div className="grid gap-1.5">
              <Label htmlFor="runDue">Due date</Label>
              <Input
                id="runDue"
                type="date"
                value={dueDate}
                onChange={(event) => setDueDate(event.target.value)}
              />
            </div>

            <div className="grid gap-1.5">
              <Label htmlFor="runRate">Interest %</Label>
              <Input
                id="runRate"
                inputMode="decimal"
                value={rate}
                onChange={(event) => setRate(event.target.value)}
                className="tabular-nums"
              />
            </div>
          </div>

          <p className="text-[12px] text-muted-foreground">
            Leave the tower blank to run across the whole project. Instalments already
            demanded, paid or waived are skipped.
          </p>
        </div>

        <DialogFooter>
          <Button variant="outline" onClick={() => onOpenChange(false)} disabled={saving}>
            Cancel
          </Button>
          <Button onClick={run} disabled={saving} className="gap-1.5">
            {saving ? <Loader2 className="size-4 animate-spin" /> : <Send className="size-4" />}
            Raise demands
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
