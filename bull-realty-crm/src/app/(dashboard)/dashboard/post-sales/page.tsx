"use client";

import * as React from "react";
import Link from "next/link";
import {
  AlertTriangle,
  ArrowRight,
  BadgeIndianRupee,
  Banknote,
  Building2,
  CalendarClock,
  LayoutDashboard,
  Landmark,
  ReceiptText,
  TrendingUp,
} from "lucide-react";
import { toast } from "sonner";

import { PagePanel } from "@/components/shell/page-panel";
import { CrmLoadingState } from "@/components/shell/crm-loader";
import {
  MetricStrip,
  Pill,
  TONE_FILL,
  TONE_TEXT,
  type Metric,
} from "@/components/crm/metrics";
import { Button } from "@/components/ui/button";
import { ApiError } from "@/lib/api";
import {
  inr,
  inrShort,
  postSalesApi,
  type BookingRow,
  type CollectionsSummary,
  type EscrowSummary,
} from "@/lib/post-sales-api";
import { bucketTone } from "@/lib/post-sales-tones";
import { cn } from "@/lib/utils";

/**
 * The post-sales overview.
 *
 * Written for the person who has to answer "where is the money" at nine in the
 * morning, so it is ordered the way that question decomposes: what is overdue,
 * what is due next, what has actually landed, and whether the escrow split is
 * still legal. Sale value gets one tile and no more — it is the number every
 * dashboard leads with and the only one nobody can act on.
 */
export default function PostSalesOverview() {
  const [summary, setSummary] = React.useState<CollectionsSummary | null>(null);
  const [escrow, setEscrow] = React.useState<EscrowSummary[]>([]);
  const [worst, setWorst] = React.useState<BookingRow[]>([]);

  React.useEffect(() => {
    postSalesApi
      .summary()
      .then(setSummary)
      .catch((error: unknown) => {
        toast.error("Could not load collections", {
          description: error instanceof ApiError ? error.message : "Network error.",
        });
      });

    postSalesApi.escrow().then(setEscrow).catch(() => setEscrow([]));

    postSalesApi
      .bookings({ overdueOnly: true })
      .then((rows) =>
        setWorst([...rows].sort((a, b) => b.overdueDays - a.overdueDays).slice(0, 8))
      )
      .catch(() => setWorst([]));
  }, []);

  if (!summary) {
    return (
      <PagePanel icon={LayoutDashboard} title="Post Sales">
        <CrmLoadingState label="Loading collections" detail="Ageing, receipts and escrow." />
      </PagePanel>
    );
  }

  const metrics: Metric[] = [
    {
      label: "Sale value",
      value: inrShort(summary.totalSold),
      hint: `${summary.liveBookings} live bookings`,
      icon: Building2,
    },
    {
      label: "Demanded",
      value: inrShort(summary.totalDemanded),
      hint: "Invoiced to customers so far",
      icon: ReceiptText,
      tone: "info",
    },
    {
      label: "Received",
      value: inrShort(summary.totalReceived),
      hint: "Cleared receipts, TDS included",
      icon: Banknote,
      tone: "success",
      progress: summary.collectionEfficiency,
    },
    {
      label: "Outstanding",
      value: inrShort(summary.totalOutstanding),
      hint: `${summary.overdueBookings} bookings overdue`,
      icon: AlertTriangle,
      tone: summary.totalOutstanding > 0 ? "danger" : "neutral",
    },
    {
      label: "Interest due",
      value: inrShort(summary.interestDue),
      hint: "Penal interest not yet waived",
      icon: TrendingUp,
      tone: summary.interestDue > 0 ? "warning" : "neutral",
    },
    {
      label: "Collection efficiency",
      // A ratio with nothing in its denominator is not 100% — it is unmeasured,
      // and printing it as a perfect score is the kind of number that looks
      // good on a screenshot and means nothing.
      value: summary.totalDemanded > 0
        ? `${Math.round(summary.collectionEfficiency * 100)}%`
        : "—",
      hint: summary.totalDemanded > 0
        ? "Received against demanded"
        : "Nothing demanded yet",
      icon: BadgeIndianRupee,
      tone:
        summary.totalDemanded === 0
          ? "neutral"
          : summary.collectionEfficiency >= 0.9
            ? "success"
            : summary.collectionEfficiency >= 0.75
              ? "warning"
              : "danger",
      progress: summary.totalDemanded > 0 ? summary.collectionEfficiency : undefined,
    },
  ];

  const agedTotal = summary.ageing.reduce((sum, bucket) => sum + bucket.amount, 0);
  const forecastPeak = Math.max(
    1,
    ...summary.forecast.map((point) => Math.max(point.scheduled, point.demanded))
  );

  return (
    <PagePanel
      icon={LayoutDashboard}
      title="Post Sales"
      hint="Everything owed, everything received, and what the paperwork is waiting on."
      actions={
        <>
          <Button asChild variant="outline" size="sm">
            <Link href="/dashboard/post-sales/bookings">Bookings</Link>
          </Button>
          <Button asChild size="sm">
            <Link href="/dashboard/post-sales/collections">Collections desk</Link>
          </Button>
        </>
      }
    >
      <div className="flex flex-col gap-4">
        <MetricStrip metrics={metrics} />

        {/* This month, stated plainly — the three numbers a review meeting opens with. */}
        <div className="grid gap-px overflow-hidden rounded-lg border bg-border sm:grid-cols-3">
          {[
            {
              label: "Falling due this month",
              value: summary.dueThisMonth,
              tone: "info" as const,
              icon: CalendarClock,
            },
            {
              label: "Received this month",
              value: summary.receivedThisMonth,
              tone: "success" as const,
              icon: Banknote,
            },
            {
              label: "Bounced this month",
              value: summary.bouncedThisMonth,
              tone: summary.bouncedThisMonth > 0 ? ("danger" as const) : ("neutral" as const),
              icon: AlertTriangle,
            },
          ].map((cell) => (
            <div key={cell.label} className="flex items-center gap-3 bg-card px-4 py-3">
              <cell.icon className={cn("size-4 shrink-0", TONE_TEXT[cell.tone])} />
              <div className="min-w-0">
                <p className="text-[11px] font-medium tracking-wide text-muted-foreground uppercase">
                  {cell.label}
                </p>
                <p className={cn("text-[17px] font-semibold tabular-nums", TONE_TEXT[cell.tone])}>
                  {inr(cell.value)}
                </p>
              </div>
            </div>
          ))}
        </div>

        <div className="grid gap-4 xl:grid-cols-2">
          {/* ---------------- ageing ---------------- */}
          <section className="overflow-hidden rounded-xl border bg-card shadow-xs">
            <header className="flex items-center justify-between gap-2 border-b px-4 py-2.5">
              <h2 className="flex items-center gap-2 text-[13.5px] font-semibold">
                <AlertTriangle className="size-4 text-primary" />
                Ageing of outstanding demands
              </h2>
              <span className="text-[12px] font-medium tabular-nums text-muted-foreground">
                {inr(agedTotal)}
              </span>
            </header>

            <div className="p-4">
              {agedTotal === 0 ? (
                <p className="py-6 text-center text-[12.5px] text-muted-foreground">
                  {summary.totalDemanded > 0
                    ? "Nothing outstanding. Every demand raised has been collected."
                    : "No demand has been raised yet."}
                </p>
              ) : (
                <>
                  <div className="flex h-2.5 w-full overflow-hidden rounded-full bg-muted">
                    {summary.ageing.map((bucket) =>
                      bucket.amount <= 0 ? null : (
                        <div
                          key={bucket.bucket}
                          className={cn(TONE_FILL[bucketTone(bucket.bucket)])}
                          style={{ width: `${(bucket.amount / agedTotal) * 100}%` }}
                          title={`${bucket.bucket}: ${inr(bucket.amount)}`}
                        />
                      )
                    )}
                  </div>

                  <table className="mt-3 w-full text-[12.5px]">
                    <thead>
                      <tr className="text-[10.5px] tracking-wide text-muted-foreground uppercase">
                        <th className="pb-1 text-left font-medium">Bucket</th>
                        <th className="pb-1 text-right font-medium">Demands</th>
                        <th className="pb-1 text-right font-medium">Amount</th>
                        <th className="pb-1 text-right font-medium">Share</th>
                      </tr>
                    </thead>
                    <tbody>
                      {summary.ageing.map((bucket) => (
                        <tr key={bucket.bucket} className="border-t">
                          <td className="py-1.5">
                            <Link
                              href={`/dashboard/post-sales/collections?bucket=${encodeURIComponent(bucket.bucket)}`}
                              className="inline-flex hover:underline"
                            >
                              <Pill tone={bucketTone(bucket.bucket)}>
                                {bucket.bucket === "Current" ? "Not yet due" : `${bucket.bucket} days`}
                              </Pill>
                            </Link>
                          </td>
                          <td className="py-1.5 text-right tabular-nums">{bucket.count}</td>
                          <td className="py-1.5 text-right font-medium tabular-nums">
                            {inr(bucket.amount)}
                          </td>
                          <td className="py-1.5 text-right tabular-nums text-muted-foreground">
                            {Math.round((bucket.amount / agedTotal) * 100)}%
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </>
              )}
            </div>
          </section>

          {/* ---------------- forecast ---------------- */}
          <section className="overflow-hidden rounded-xl border bg-card shadow-xs">
            <header className="flex items-center justify-between gap-2 border-b px-4 py-2.5">
              <h2 className="flex items-center gap-2 text-[13.5px] font-semibold">
                <CalendarClock className="size-4 text-primary" />
                Six-month collection forecast
              </h2>
              <span className="text-[11.5px] text-muted-foreground">
                Scheduled against demanded
              </span>
            </header>

            <div className="p-4">
              <div className="flex h-40 items-end gap-2">
                {summary.forecast.map((point) => (
                  <div key={point.month} className="flex min-w-0 flex-1 flex-col items-center gap-1">
                    <span className="text-[10.5px] font-medium tabular-nums text-muted-foreground">
                      {inrShort(point.scheduled)}
                    </span>

                    {/* Demanded sits inside scheduled rather than beside it: the
                        gap between the two bars is the work still to do. */}
                    <div
                      className="relative w-full max-w-10 rounded-t bg-primary/15"
                      style={{ height: `${(point.scheduled / forecastPeak) * 100}%`, minHeight: 3 }}
                    >
                      <div
                        className="absolute inset-x-0 bottom-0 rounded-t bg-primary"
                        style={{
                          height: `${point.scheduled > 0 ? (point.demanded / point.scheduled) * 100 : 0}%`,
                        }}
                        title={`Demanded ${inr(point.demanded)}`}
                      />
                    </div>

                    <span className="w-full truncate text-center text-[10.5px] text-muted-foreground">
                      {point.month}
                    </span>
                  </div>
                ))}
              </div>

              <p className="mt-3 flex items-center gap-3 text-[11.5px] text-muted-foreground">
                <span className="inline-flex items-center gap-1.5">
                  <span className="size-2 rounded-sm bg-primary" /> Demand raised
                </span>
                <span className="inline-flex items-center gap-1.5">
                  <span className="size-2 rounded-sm bg-primary/15" /> Scheduled by the plan
                </span>
              </p>
            </div>
          </section>
        </div>

        <div className="grid gap-4 xl:grid-cols-2">
          {/* ---------------- escrow ---------------- */}
          <section className="overflow-hidden rounded-xl border bg-card shadow-xs">
            <header className="flex items-center justify-between gap-2 border-b px-4 py-2.5">
              <h2 className="flex items-center gap-2 text-[13.5px] font-semibold">
                <Landmark className="size-4 text-primary" />
                RERA designated account
              </h2>
              <span className="text-[11.5px] text-muted-foreground">70% of every receipt</span>
            </header>

            {escrow.length === 0 ? (
              <p className="px-4 py-6 text-center text-[12.5px] text-muted-foreground">
                No collections recorded against a project yet.
              </p>
            ) : (
              <div className="overflow-x-auto">
                <table className="w-full text-[12.5px]">
                  <thead>
                    <tr className="border-b text-[10.5px] tracking-wide text-muted-foreground uppercase">
                      <th className="px-4 py-2 text-left font-medium">Project</th>
                      <th className="px-4 py-2 text-right font-medium">Collected</th>
                      <th className="px-4 py-2 text-right font-medium">Designated</th>
                      <th className="px-4 py-2 text-right font-medium">Free</th>
                      <th className="px-4 py-2 text-right font-medium">To transfer</th>
                    </tr>
                  </thead>
                  <tbody>
                    {escrow.map((row) => (
                      <tr key={row.projectName} className="border-b last:border-0">
                        <td className="px-4 py-2 font-medium">{row.projectName}</td>
                        <td className="px-4 py-2 text-right tabular-nums">{inr(row.collected)}</td>
                        <td className="px-4 py-2 text-right tabular-nums">{inr(row.designated)}</td>
                        <td className="px-4 py-2 text-right tabular-nums">{inr(row.free)}</td>
                        <td
                          className={cn(
                            "px-4 py-2 text-right font-medium tabular-nums",
                            row.pendingTransfer > 0 ? TONE_TEXT.warning : TONE_TEXT.success
                          )}
                        >
                          {inr(row.pendingTransfer)}
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </section>

          {/* ---------------- worst offenders ---------------- */}
          <section className="overflow-hidden rounded-xl border bg-card shadow-xs">
            <header className="flex items-center justify-between gap-2 border-b px-4 py-2.5">
              <h2 className="flex items-center gap-2 text-[13.5px] font-semibold">
                <AlertTriangle className="size-4 text-primary" />
                Longest overdue
              </h2>
              <Button asChild variant="ghost" size="sm" className="h-7 gap-1 text-[12px]">
                <Link href="/dashboard/post-sales/bookings?overdue=1">
                  All overdue <ArrowRight className="size-3.5" />
                </Link>
              </Button>
            </header>

            {worst.length === 0 ? (
              <p className="px-4 py-6 text-center text-[12.5px] text-muted-foreground">
                Nothing is past its due date.
              </p>
            ) : (
              <div className="overflow-x-auto">
                <table className="w-full text-[12.5px]">
                  <thead>
                    <tr className="border-b text-[10.5px] tracking-wide text-muted-foreground uppercase">
                      <th className="px-4 py-2 text-left font-medium">Customer</th>
                      <th className="px-4 py-2 text-left font-medium">Unit</th>
                      <th className="px-4 py-2 text-right font-medium">Outstanding</th>
                      <th className="px-4 py-2 text-right font-medium">Overdue</th>
                    </tr>
                  </thead>
                  <tbody>
                    {worst.map((booking) => (
                      <tr key={booking.id} className="border-b last:border-0 hover:bg-muted/40">
                        <td className="px-4 py-2">
                          <Link
                            href={`/dashboard/post-sales/bookings/${booking.id}`}
                            className="font-medium text-primary hover:underline"
                          >
                            {booking.customerName}
                          </Link>
                          <p className="text-[11px] text-muted-foreground">
                            {booking.bookingNumber}
                          </p>
                        </td>
                        <td className="px-4 py-2">
                          {booking.towerName ? `${booking.towerName} · ` : ""}
                          {booking.unitNumber}
                        </td>
                        <td className={cn("px-4 py-2 text-right font-medium tabular-nums", TONE_TEXT.danger)}>
                          {inr(booking.outstanding)}
                        </td>
                        <td className="px-4 py-2 text-right">
                          <Pill tone="danger">{booking.overdueDays}d</Pill>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </section>
        </div>
      </div>
    </PagePanel>
  );
}
