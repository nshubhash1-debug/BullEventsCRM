"use client";

import * as React from "react";
import Link from "next/link";
import {
  AlertTriangle,
  Building2,
  ChevronRight,
  FileCheck2,
  Mail,
  Phone,
  Search,
  UserRound,
  Users,
} from "lucide-react";
import { toast } from "sonner";

import { PagePanel } from "@/components/shell/page-panel";
import { CrmLoadingState } from "@/components/shell/crm-loader";
import { MetricStrip, Pill, TONE_TEXT, type Metric } from "@/components/crm/metrics";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { ApiError } from "@/lib/api";
import {
  inr,
  inrShort,
  postSalesApi,
  shortDate,
  type Customer,
} from "@/lib/post-sales-api";
import { bookingStatusTone, humanise } from "@/lib/post-sales-tones";
import { cn } from "@/lib/utils";

/**
 * The post-sales customer list.
 *
 * A customer is somebody who bought, so a row appears here only once their lead
 * has been closed as Booked — the gate is enforced on the server, not by a
 * filter on this screen. That boundary is the point: the collections desk works
 * accounts, and an open lead is not one.
 *
 * Keyed on the lead rather than on the booking, because a buyer who takes two
 * flats is one customer with two units — and because the lead is where the rest
 * of the CRM already knows them. Every row links back to it.
 */
export default function CustomersPage() {
  const [rows, setRows] = React.useState<Customer[] | null>(null);
  const [search, setSearch] = React.useState("");
  const [overdueOnly, setOverdueOnly] = React.useState(false);
  const [expanded, setExpanded] = React.useState<number | null>(null);

  /**
   * Live bookings the gate is holding back — ones whose lead is not closed as
   * Booked, or that have no lead at all. Counted rather than hidden: a customer
   * who is silently missing from this list is worse than one the screen admits
   * it cannot show, because nobody goes looking for a row they do not know is
   * absent.
   */
  const [held, setHeld] = React.useState(0);

  const load = React.useCallback(() => {
    postSalesApi
      .customers({
        search: search.trim() || undefined,
        overdueOnly: overdueOnly || undefined,
      })
      .then(setRows)
      .catch((error: unknown) => {
        toast.error("Could not load customers", {
          description: error instanceof ApiError ? error.message : "Network error.",
        });
        setRows([]);
      });
  }, [search, overdueOnly]);

  React.useEffect(() => {
    Promise.all([postSalesApi.customers(), postSalesApi.bookings()])
      .then(([customers, bookings]) => {
        const shown = customers.reduce((sum, c) => sum + c.units, 0);
        const live = bookings.filter((b) => b.status !== "Cancelled").length;
        setHeld(Math.max(0, live - shown));
      })
      .catch(() => setHeld(0));
  }, []);

  React.useEffect(() => {
    const timer = setTimeout(load, search ? 300 : 0);
    return () => clearTimeout(timer);
  }, [load, search]);

  const metrics: Metric[] = React.useMemo(() => {
    const list = rows ?? [];
    const portfolio = list.reduce((sum, r) => sum + r.portfolio, 0);
    const received = list.reduce((sum, r) => sum + r.received, 0);
    const repeat = list.filter((r) => r.units > 1).length;
    const overdue = list.filter((r) => r.overdueDays > 0).length;
    const kycPending = list.reduce((sum, r) => sum + r.kycPending, 0);

    return [
      { label: "Customers", value: list.length, icon: Users },
      {
        label: "Units owned",
        value: list.reduce((sum, r) => sum + r.units, 0),
        icon: Building2,
        hint: repeat > 0 ? `${repeat} bought more than once` : undefined,
      },
      { label: "Portfolio", value: inrShort(portfolio), tone: "primary" },
      {
        label: "Received",
        value: inrShort(received),
        tone: "success",
        progress: portfolio > 0 ? received / portfolio : 0,
      },
      {
        label: "Overdue",
        value: overdue,
        tone: overdue > 0 ? "danger" : "success",
        icon: AlertTriangle,
        hint: overdue > 0 ? "Customers past a due date" : "All current",
      },
      {
        label: "KYC pending",
        value: kycPending,
        tone: kycPending > 0 ? "warning" : "success",
        icon: FileCheck2,
        hint: "Applicants not yet verified",
      },
    ];
  }, [rows]);

  return (
    <PagePanel
      icon={UserRound}
      title="Customers"
      hint="Everyone who has bought. A lead appears here once it is closed as Booked."
      toolbar={
        <div className="flex flex-wrap items-center gap-2">
          <div className="relative min-w-0 flex-1 sm:max-w-xs">
            <Search className="absolute top-1/2 left-2.5 size-3.5 -translate-y-1/2 text-muted-foreground" />
            <Input
              value={search}
              onChange={(event) => setSearch(event.target.value)}
              placeholder="Name, phone, email or unit"
              className="h-8 pl-8 text-[12.5px]"
            />
          </div>

          <Button
            variant={overdueOnly ? "default" : "outline"}
            size="sm"
            className="h-8 gap-1.5 text-[12.5px]"
            onClick={() => {
              setRows(null);
              setOverdueOnly((on) => !on);
            }}
          >
            <AlertTriangle className="size-3.5" />
            Overdue only
          </Button>
        </div>
      }
    >
      <div className="flex min-h-0 flex-col gap-3">
        <MetricStrip metrics={metrics} loading={rows === null} />

        {held > 0 ? (
          <p className="flex items-start gap-2 rounded-md border border-amber-500/25 bg-amber-500/10 px-3 py-2 text-[12.5px] text-amber-700 dark:text-amber-400">
            <AlertTriangle className="mt-0.5 size-3.5 shrink-0" />
            <span>
              {held} booking{held === 1 ? "" : "s"} not shown here — the lead behind
              {held === 1 ? " it is" : " them are"} not closed as Booked, or there is no
              lead linked.{" "}
              <Link
                href="/dashboard/post-sales/bookings"
                className="font-medium underline underline-offset-2"
              >
                See all bookings
              </Link>
            </span>
          </p>
        ) : null}

        {rows === null ? (
          <CrmLoadingState label="Loading customers" />
        ) : rows.length === 0 ? (
          <div className="py-12 text-center">
            <p className="text-[13px] text-muted-foreground">
              {search || overdueOnly
                ? "No customer matches these filters."
                : "No customer yet."}
            </p>
            {!search && !overdueOnly ? (
              <p className="mx-auto mt-1 max-w-md text-[12.5px] text-muted-foreground">
                A lead becomes a customer here the moment it is closed as Booked and a
                booking is opened against it.
              </p>
            ) : null}
          </div>
        ) : (
          <div className="min-h-0 flex-1 overflow-auto rounded-lg border">
            <table className="w-full min-w-[1020px] text-[12.5px]">
              <thead className="sticky top-0 z-10 bg-muted/70 backdrop-blur">
                <tr className="text-[10.5px] tracking-wide text-muted-foreground uppercase">
                  <th className="w-8 px-2 py-2" />
                  <th className="px-3 py-2 text-left font-medium">Customer</th>
                  <th className="px-3 py-2 text-left font-medium">Contact</th>
                  <th className="px-3 py-2 text-center font-medium">Units</th>
                  <th className="px-3 py-2 text-right font-medium">Portfolio</th>
                  <th className="px-3 py-2 text-right font-medium">Received</th>
                  <th className="px-3 py-2 text-right font-medium">Outstanding</th>
                  <th className="px-3 py-2 text-left font-medium">Paperwork</th>
                  <th className="px-3 py-2 text-left font-medium">Since</th>
                  <th className="px-3 py-2 text-left font-medium">Lead</th>
                </tr>
              </thead>

              <tbody>
                {rows.map((customer) => {
                  const key = customer.leadId ?? -1;
                  const open = expanded === key;

                  return (
                    <React.Fragment key={key}>
                      <tr
                        className="cursor-pointer border-t hover:bg-muted/40"
                        onClick={() => setExpanded(open ? null : key)}
                      >
                        <td className="px-2 py-2 align-top">
                          <ChevronRight
                            className={cn(
                              "size-4 text-muted-foreground transition-transform",
                              open && "rotate-90"
                            )}
                          />
                        </td>

                        <td className="px-3 py-2 align-top">
                          <p className="font-medium">{customer.name}</p>
                          <p className="text-[11px] text-muted-foreground">
                            {customer.city ? `${customer.city} · ` : ""}
                            {customer.source ? humanise(customer.source) : "—"}
                            {customer.ownerName ? ` · ${customer.ownerName}` : ""}
                          </p>
                        </td>

                        <td className="px-3 py-2 align-top">
                          {customer.phone ? (
                            <p className="flex items-center gap-1.5 tabular-nums">
                              <Phone className="size-3 text-muted-foreground" />
                              {customer.phone}
                            </p>
                          ) : null}
                          {customer.email ? (
                            <p className="flex items-center gap-1.5 text-[11px] text-muted-foreground">
                              <Mail className="size-3" />
                              {customer.email}
                            </p>
                          ) : null}
                        </td>

                        <td className="px-3 py-2 text-center align-top">
                          <Pill tone={customer.units > 1 ? "violet" : "neutral"}>
                            {customer.units}
                          </Pill>
                          {customer.coApplicants > 0 ? (
                            <p className="mt-0.5 text-[10.5px] text-muted-foreground">
                              +{customer.coApplicants} on title
                            </p>
                          ) : null}
                        </td>

                        <td className="px-3 py-2 text-right align-top tabular-nums">
                          {inr(customer.portfolio)}
                        </td>

                        <td
                          className={cn(
                            "px-3 py-2 text-right align-top tabular-nums",
                            TONE_TEXT.success
                          )}
                        >
                          {inr(customer.received)}
                        </td>

                        <td className="px-3 py-2 text-right align-top tabular-nums">
                          <span
                            className={customer.outstanding > 0 ? TONE_TEXT.danger : undefined}
                          >
                            {inr(customer.outstanding)}
                          </span>
                          {customer.overdueDays > 0 ? (
                            <p className="mt-0.5">
                              <Pill tone="danger">{customer.overdueDays}d overdue</Pill>
                            </p>
                          ) : null}
                        </td>

                        {/* One cell for "is anything outstanding on paper", because
                            that is the whole question before registration day. */}
                        <td className="px-3 py-2 align-top">
                          {customer.kycPending === 0 && customer.documentsPending === 0 ? (
                            <Pill tone="success">Complete</Pill>
                          ) : (
                            <div className="flex flex-col gap-0.5">
                              {customer.kycPending > 0 ? (
                                <Pill tone="warning">{customer.kycPending} KYC</Pill>
                              ) : null}
                              {customer.documentsPending > 0 ? (
                                <Pill tone="warning">
                                  {customer.documentsPending} documents
                                </Pill>
                              ) : null}
                            </div>
                          )}
                        </td>

                        <td className="px-3 py-2 align-top whitespace-nowrap text-muted-foreground">
                          {shortDate(customer.customerSince)}
                          <p className="text-[10.5px]">{humanise(customer.lifecycleStage)}</p>
                        </td>

                        <td className="px-3 py-2 align-top">
                          {customer.leadId ? (
                            <Link
                              href={`/dashboard/leads/${customer.leadId}`}
                              className="text-primary hover:underline"
                              onClick={(event) => event.stopPropagation()}
                            >
                              Lead #{customer.leadId}
                            </Link>
                          ) : (
                            <span className="text-muted-foreground">—</span>
                          )}
                        </td>
                      </tr>

                      {open ? (
                        <tr className="border-t bg-muted/25">
                          <td />
                          <td colSpan={9} className="px-3 py-2.5">
                            <table className="w-full text-[12px]">
                              <thead>
                                <tr className="text-[10px] tracking-wide text-muted-foreground uppercase">
                                  <th className="pb-1 text-left font-medium">Booking</th>
                                  <th className="pb-1 text-left font-medium">Unit</th>
                                  <th className="pb-1 text-left font-medium">Status</th>
                                  <th className="pb-1 text-right font-medium">Value</th>
                                  <th className="pb-1 text-right font-medium">Received</th>
                                  <th className="pb-1 text-right font-medium">Outstanding</th>
                                </tr>
                              </thead>
                              <tbody>
                                {customer.bookings.map((unit) => (
                                  <tr key={unit.bookingId} className="border-t border-border/60">
                                    <td className="py-1.5">
                                      <Link
                                        href={`/dashboard/post-sales/bookings/${unit.bookingId}`}
                                        className="font-medium text-primary hover:underline"
                                      >
                                        {unit.bookingNumber}
                                      </Link>
                                      <span className="ml-2 text-[10.5px] text-muted-foreground">
                                        {shortDate(unit.bookingDate)}
                                      </span>
                                    </td>
                                    <td className="py-1.5">
                                      {unit.towerName ? `${unit.towerName} · ` : ""}
                                      {unit.unitNumber}
                                      <span className="ml-1.5 text-[10.5px] text-muted-foreground">
                                        {unit.projectName}
                                        {unit.configuration ? ` · ${unit.configuration}` : ""}
                                      </span>
                                    </td>
                                    <td className="py-1.5">
                                      <Pill tone={bookingStatusTone(unit.status)}>
                                        {humanise(unit.status)}
                                      </Pill>
                                    </td>
                                    <td className="py-1.5 text-right tabular-nums">
                                      {inr(unit.grandTotal)}
                                    </td>
                                    <td
                                      className={cn(
                                        "py-1.5 text-right tabular-nums",
                                        TONE_TEXT.success
                                      )}
                                    >
                                      {inr(unit.received)}
                                    </td>
                                    <td
                                      className={cn(
                                        "py-1.5 text-right tabular-nums",
                                        unit.outstanding > 0 && TONE_TEXT.danger
                                      )}
                                    >
                                      {inr(unit.outstanding)}
                                    </td>
                                  </tr>
                                ))}
                              </tbody>
                            </table>
                          </td>
                        </tr>
                      ) : null}
                    </React.Fragment>
                  );
                })}
              </tbody>
            </table>
          </div>
        )}
      </div>
    </PagePanel>
  );
}
