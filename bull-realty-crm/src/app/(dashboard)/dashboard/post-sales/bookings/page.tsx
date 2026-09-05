"use client";

import * as React from "react";
import Link from "next/link";
import { useSearchParams } from "next/navigation";
import {
  AlertTriangle,
  Building2,
  ClipboardCheck,
  FileSignature,
  KeyRound,
  Landmark,
  Search,
} from "lucide-react";
import { toast } from "sonner";

import { PagePanel } from "@/components/shell/page-panel";
import { CrmLoadingState } from "@/components/shell/crm-loader";
import { MetricStrip, Pill, TONE_TEXT, type Metric } from "@/components/crm/metrics";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { ApiError } from "@/lib/api";
import {
  BOOKING_STATUSES,
  inr,
  inrShort,
  postSalesApi,
  shortDate,
  type BookingRow,
} from "@/lib/post-sales-api";
import { bookingStatusTone, humanise } from "@/lib/post-sales-tones";
import { cn } from "@/lib/utils";

export default function BookingsPage() {
  return (
    <React.Suspense
      fallback={
        <PagePanel icon={ClipboardCheck} title="Bookings">
          <CrmLoadingState label="Loading bookings" />
        </PagePanel>
      }
    >
      <BookingsList />
    </React.Suspense>
  );
}

/**
 * The bookings list.
 *
 * Sorted by the server with the overdue first, and left that way: a collections
 * list whose default order is "newest" makes somebody re-sort it every single
 * morning before it is useful.
 */
function BookingsList() {
  const params = useSearchParams();

  const [rows, setRows] = React.useState<BookingRow[] | null>(null);
  const [status, setStatus] = React.useState("all");
  const [overdueOnly, setOverdueOnly] = React.useState(params.get("overdue") === "1");
  const [search, setSearch] = React.useState("");

  const load = React.useCallback(() => {
    setRows(null);

    postSalesApi
      .bookings({
        status: status === "all" ? undefined : status,
        overdueOnly: overdueOnly || undefined,
        search: search.trim() || undefined,
      })
      .then(setRows)
      .catch((error: unknown) => {
        toast.error("Could not load bookings", {
          description: error instanceof ApiError ? error.message : "Network error.",
        });
        setRows([]);
      });
  }, [status, overdueOnly, search]);

  // Debounced so typing in the search box does not fire a request per keystroke.
  React.useEffect(() => {
    const timer = setTimeout(load, search ? 300 : 0);
    return () => clearTimeout(timer);
  }, [load, search]);

  const metrics: Metric[] = React.useMemo(() => {
    const list = rows ?? [];
    const value = list.reduce((sum, r) => sum + r.grandTotal, 0);
    const received = list.reduce((sum, r) => sum + r.received, 0);
    const outstanding = list.reduce((sum, r) => sum + r.outstanding, 0);
    const overdue = list.filter((r) => r.overdueDays > 0).length;

    return [
      { label: "Bookings", value: list.length, icon: ClipboardCheck },
      { label: "Contract value", value: inrShort(value), icon: Building2 },
      {
        label: "Received",
        value: inrShort(received),
        tone: "success",
        progress: value > 0 ? received / value : 0,
        hint: value > 0 ? `${Math.round((received / value) * 100)}% of contract value` : undefined,
      },
      {
        label: "Outstanding",
        value: inrShort(outstanding),
        tone: outstanding > 0 ? "danger" : "neutral",
      },
      {
        label: "Overdue",
        value: overdue,
        tone: overdue > 0 ? "danger" : "success",
        icon: AlertTriangle,
        hint: overdue > 0 ? "Past a due date" : "All current",
      },
    ];
  }, [rows]);

  return (
    <PagePanel
      icon={ClipboardCheck}
      title="Bookings"
      hint="Every event contracted, from advance through the run-up to delivery."
      toolbar={
        <div className="flex flex-wrap items-center gap-2">
          <div className="relative min-w-0 flex-1 sm:max-w-xs">
            <Search className="absolute top-1/2 left-2.5 size-3.5 -translate-y-1/2 text-muted-foreground" />
            <Input
              value={search}
              onChange={(event) => setSearch(event.target.value)}
              placeholder="Client, space or contract number"
              className="h-8 pl-8 text-[12.5px]"
            />
          </div>

          <Select value={status} onValueChange={setStatus}>
            <SelectTrigger size="sm" className="w-[190px] text-[12.5px]">
              <SelectValue placeholder="Any status" />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="all">Any status</SelectItem>
              {BOOKING_STATUSES.map((value) => (
                <SelectItem key={value} value={value}>
                  {humanise(value)}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>

          <Button
            variant={overdueOnly ? "default" : "outline"}
            size="sm"
            className="h-8 gap-1.5 text-[12.5px]"
            onClick={() => setOverdueOnly((on) => !on)}
          >
            <AlertTriangle className="size-3.5" />
            Overdue only
          </Button>
        </div>
      }
    >
      <div className="flex min-h-0 flex-col gap-3">
        <MetricStrip metrics={metrics} loading={rows === null} />

        {rows === null ? (
          <CrmLoadingState label="Loading bookings" />
        ) : rows.length === 0 ? (
          <p className="py-12 text-center text-[13px] text-muted-foreground">
            No contract matches these filters.
          </p>
        ) : (
          <div className="min-h-0 flex-1 overflow-auto rounded-lg border">
            <table className="w-full min-w-[1080px] text-[12.5px]">
              <thead className="sticky top-0 z-10 bg-muted/70 backdrop-blur">
                <tr className="text-[10.5px] tracking-wide text-muted-foreground uppercase">
                  <th className="px-3 py-2 text-left font-medium">Booking</th>
                  <th className="px-3 py-2 text-left font-medium">Client</th>
                  <th className="px-3 py-2 text-left font-medium">Event</th>
                  <th className="px-3 py-2 text-left font-medium">Space</th>
                  <th className="px-3 py-2 text-right font-medium">Contract value</th>
                  <th className="px-3 py-2 text-right font-medium">Received</th>
                  <th className="px-3 py-2 text-right font-medium">Outstanding</th>
                  <th className="px-3 py-2 text-left font-medium">Collected</th>
                  <th className="px-3 py-2 text-left font-medium">Status</th>
                  <th className="px-3 py-2 text-center font-medium">Paperwork</th>
                </tr>
              </thead>

              <tbody>
                {rows.map((booking) => (
                  <tr key={booking.id} className="border-t hover:bg-muted/40">
                    <td className="px-3 py-2 align-top">
                      <Link
                        href={`/dashboard/post-sales/bookings/${booking.id}`}
                        className="font-medium text-primary hover:underline"
                      >
                        {booking.bookingNumber}
                      </Link>
                      <p className="text-[11px] text-muted-foreground">
                        {shortDate(booking.bookingDate)}
                      </p>
                    </td>

                    <td className="px-3 py-2 align-top">
                      <p className="font-medium">{booking.customerName}</p>
                      {booking.customerPhone ? (
                        <p className="text-[11px] text-muted-foreground tabular-nums">
                          {booking.customerPhone}
                        </p>
                      ) : null}
                    </td>

                    {/*
                      The event column sits before the space, because on an
                      events desk the date is what a contract is looked up by —
                      "the 14th of February wedding", never "hall B2".
                    */}
                    <td className="px-3 py-2 align-top">
                      {booking.eventDate ? (
                        <>
                          <p>
                            {new Date(booking.eventDate).toLocaleDateString(undefined, {
                              day: "numeric",
                              month: "short",
                              year: "numeric",
                            })}
                          </p>
                          <p
                            className={cn(
                              "text-[11px]",
                              booking.daysToEvent !== null &&
                                booking.daysToEvent >= 0 &&
                                booking.daysToEvent <= 21
                                ? "font-medium text-orange-600 dark:text-orange-400"
                                : "text-muted-foreground"
                            )}
                          >
                            {booking.eventType ? `${humanise(booking.eventType)} · ` : ""}
                            {booking.guestCount > 0
                              ? `${booking.guestCount.toLocaleString("en-IN")} guests`
                              : "—"}
                          </p>
                        </>
                      ) : (
                        <p className="text-muted-foreground">Date not fixed</p>
                      )}
                    </td>

                    <td className="px-3 py-2 align-top">
                      <p>
                        {booking.towerName ? `${booking.towerName} · ` : ""}
                        {booking.unitNumber}
                      </p>
                      <p className="text-[11px] text-muted-foreground">
                        {booking.projectName}
                        {booking.configuration ? ` · ${humanise(booking.configuration)}` : ""}
                      </p>
                    </td>

                    <td className="px-3 py-2 text-right align-top tabular-nums">
                      {inr(booking.grandTotal)}
                      <p className="text-[11px] text-muted-foreground">
                        AV {inrShort(booking.agreementValue)}
                      </p>
                    </td>

                    <td className={cn("px-3 py-2 text-right align-top tabular-nums", TONE_TEXT.success)}>
                      {inr(booking.received)}
                    </td>

                    <td className="px-3 py-2 text-right align-top tabular-nums">
                      <span className={booking.outstanding > 0 ? TONE_TEXT.danger : undefined}>
                        {inr(booking.outstanding)}
                      </span>
                      {booking.overdueDays > 0 ? (
                        <p className="mt-0.5">
                          <Pill tone="danger">{booking.overdueDays}d overdue</Pill>
                        </p>
                      ) : booking.interestDue > 0 ? (
                        <p className="mt-0.5">
                          <Pill tone="warning">{inrShort(booking.interestDue)} interest</Pill>
                        </p>
                      ) : null}
                    </td>

                    <td className="px-3 py-2 align-top">
                      <div className="flex items-center gap-2">
                        <div className="h-1.5 w-16 overflow-hidden rounded-full bg-muted">
                          <div
                            className={cn(
                              "h-full rounded-full",
                              booking.collectedFraction >= 0.99
                                ? "bg-emerald-500"
                                : booking.overdueDays > 0
                                  ? "bg-red-500"
                                  : "bg-primary"
                            )}
                            style={{
                              width: `${Math.min(100, booking.collectedFraction * 100)}%`,
                            }}
                          />
                        </div>
                        <span className="tabular-nums text-muted-foreground">
                          {Math.round(booking.collectedFraction * 100)}%
                        </span>
                      </div>
                    </td>

                    <td className="px-3 py-2 align-top">
                      <Pill tone={bookingStatusTone(booking.status)}>
                        {booking.statusLabel || humanise(booking.status)}
                      </Pill>
                    </td>

                    {/* Three icons rather than three columns: what a reviewer
                        wants here is "is anything missing", not the detail. */}
                    <td className="px-3 py-2 align-top">
                      <div className="flex items-center justify-center gap-2">
                        <FileSignature
                          className={cn(
                            "size-4",
                            booking.agreementStatus === "Registered"
                              ? TONE_TEXT.success
                              : booking.agreementStatus === "Pending"
                                ? "text-muted-foreground/40"
                                : TONE_TEXT.warning
                          )}
                          aria-label={`Agreement ${humanise(booking.agreementStatus)}`}
                        />
                        <Landmark
                          className={cn(
                            "size-4",
                            booking.hasLoan ? TONE_TEXT.info : "text-muted-foreground/40"
                          )}
                          aria-label={booking.hasLoan ? "Home loan" : "Self funded"}
                        />
                        <KeyRound
                          className={cn(
                            "size-4",
                            booking.possessionStatus === "HandedOver"
                              ? TONE_TEXT.success
                              : booking.possessionStatus === "Offered"
                                ? TONE_TEXT.warning
                                : "text-muted-foreground/40"
                          )}
                          aria-label={`Delivery ${humanise(booking.possessionStatus)}`}
                        />
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>
    </PagePanel>
  );
}
