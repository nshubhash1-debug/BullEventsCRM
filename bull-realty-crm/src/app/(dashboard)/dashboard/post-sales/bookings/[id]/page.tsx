"use client";

import * as React from "react";
import Link from "next/link";
import { useParams } from "next/navigation";
import {
  AlertTriangle,
  Banknote,
  BookOpen,
  ClipboardCheck,
  FileCheck2,
  FileSignature,
  Loader2,
  Plus,
  ReceiptText,
  Send,
  Users,
} from "lucide-react";
import { toast } from "sonner";

import { PagePanel } from "@/components/shell/page-panel";
import { CrmLoadingState } from "@/components/shell/crm-loader";
import { Pill, TONE_TEXT } from "@/components/crm/metrics";
import {
  FieldGrid,
  HighlightsPanel,
  RecordPath,
  RelatedList,
} from "@/components/post-sales/record";
import {
  AgreementPanel,
  LoanPanel,
  PossessionPanel,
} from "@/components/post-sales/lifecycle";
import { LettersPanel } from "@/components/post-sales/letters";
import { lifecycleApi, type Lifecycle } from "@/lib/lifecycle-api";
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
import { Textarea } from "@/components/ui/textarea";
import { ApiError } from "@/lib/api";
import {
  inr,
  inrExact,
  inrShort,
  PAYMENT_MODES,
  postSalesApi,
  shortDate,
  type BookingDetail,
  type BookingDocument,
  type DemandRow,
  type Ledger,
  type Milestone,
  type ReceiptRow,
} from "@/lib/post-sales-api";
import {
  bookingStatusTone,
  demandStatusTone,
  documentStatusTone,
  humanise,
  kycStatusTone,
  milestoneStatusTone,
  receiptStatusTone,
} from "@/lib/post-sales-tones";
import { cn } from "@/lib/utils";

/**
 * The stages a booking walks through, in order.
 *
 * Cancellation and transfer are deliberately absent: they are exits, not steps,
 * and the path renders them as a banner instead of pretending the record is
 * partway along a road it has left.
 */
const PATH_STAGES = [
  "Booked",
  "Allotted",
  "AgreementPending",
  "AgreementExecuted",
  "Registered",
  "PossessionOffered",
  "HandedOver",
] as const;

const PATH_LABELS: Record<string, string> = {
  Booked: "Booked",
  Allotted: "Allotted",
  AgreementPending: "Agreement due",
  AgreementExecuted: "Executed",
  Registered: "Registered",
  PossessionOffered: "Possession offered",
  HandedOver: "Handed over",
};

/**
 * The booking record page.
 *
 * Built to the Salesforce record anatomy — path, highlights, then tabs over
 * related lists — because that layout answers the two questions a post-sales
 * executive has on a call, in the order they ask them: where is this booking,
 * and what does this customer still owe.
 */
export default function BookingRecordPage() {
  const params = useParams<{ id: string }>();
  const id = Number(params.id);

  const [booking, setBooking] = React.useState<BookingDetail | null>(null);
  const [ledger, setLedger] = React.useState<Ledger | null>(null);
  const [demands, setDemands] = React.useState<DemandRow[]>([]);
  const [receipts, setReceipts] = React.useState<ReceiptRow[]>([]);
  const [documents, setDocuments] = React.useState<BookingDocument[]>([]);
  const [lifecycle, setLifecycle] = React.useState<Lifecycle | null>(null);
  const [missing, setMissing] = React.useState(false);

  const [receiving, setReceiving] = React.useState(false);
  const [raising, setRaising] = React.useState<Milestone | null>(null);

  const load = React.useCallback(() => {
    postSalesApi
      .booking(id)
      .then(setBooking)
      .catch((error: unknown) => {
        setMissing(true);
        toast.error("Could not load the booking", {
          description: error instanceof ApiError ? error.message : "Network error.",
        });
      });

    postSalesApi.ledger(id).then(setLedger).catch(() => setLedger(null));
    postSalesApi.demands({ bookingId: id }).then(setDemands).catch(() => setDemands([]));
    postSalesApi.receipts({ bookingId: id }).then(setReceipts).catch(() => setReceipts([]));
    postSalesApi.documents(id).then(setDocuments).catch(() => setDocuments([]));
    lifecycleApi.forBooking(id).then(setLifecycle).catch(() => setLifecycle(null));
  }, [id]);

  React.useEffect(load, [load]);

  if (missing) {
    return (
      <PagePanel icon={ClipboardCheck} title="Booking">
        <p className="py-12 text-center text-[13px] text-muted-foreground">
          This booking could not be opened.{" "}
          <Link href="/dashboard/post-sales/bookings" className="text-primary hover:underline">
            Back to bookings
          </Link>
        </p>
      </PagePanel>
    );
  }

  if (!booking) {
    return (
      <PagePanel icon={ClipboardCheck} title="Booking">
        <CrmLoadingState label="Loading booking" detail="Schedule, ledger and paperwork." />
      </PagePanel>
    );
  }

  const summary = booking.summary;
  const openDemands = demands.filter((d) => d.outstanding > 0);
  const pendingDocuments = documents.filter(
    (d) => d.isRequired && d.status !== "Verified" && d.status !== "NotApplicable"
  );

  return (
    <div className="flex min-h-0 flex-1 flex-col gap-4 overflow-auto">
      <HighlightsPanel
        icon={ClipboardCheck}
        objectLabel="Booking"
        breadcrumb={{ label: "Bookings", href: "/dashboard/post-sales/bookings" }}
        title={summary.customerName}
        subtitle={
          <>
            {summary.bookingNumber} · {summary.projectName}
            {summary.towerName ? ` · ${summary.towerName}` : ""} · Unit {summary.unitNumber}
            {summary.configuration ? ` · ${summary.configuration}` : ""} ·{" "}
            {summary.saleableArea.toLocaleString("en-IN")} sq.ft.
          </>
        }
        path={
          <RecordPath
            stages={PATH_STAGES}
            current={summary.status}
            labelOf={(stage) => PATH_LABELS[stage] ?? humanise(stage)}
          />
        }
        actions={
          <>
            <Button variant="outline" size="sm" asChild>
              <Link href={`/dashboard/post-sales/collections?bookingId=${summary.id}`}>
                Collections
              </Link>
            </Button>
            <Button size="sm" className="gap-1.5" onClick={() => setReceiving(true)}>
              <Banknote className="size-3.5" />
              Record receipt
            </Button>
          </>
        }
        fields={[
          {
            label: "Agreement value",
            value: inrShort(summary.agreementValue),
            hint: `Total ${inrShort(summary.grandTotal)}`,
          },
          {
            label: "Received",
            value: inrShort(summary.received),
            tone: "success",
            hint: `${Math.round(summary.collectedFraction * 100)}% collected`,
          },
          {
            label: "Outstanding",
            value: inrShort(summary.outstanding),
            tone: summary.outstanding > 0 ? "danger" : "success",
            hint: ledger ? `${inrShort(ledger.notYetDemanded)} not yet demanded` : undefined,
          },
          {
            label: "Interest due",
            value: inrShort(summary.interestDue),
            tone: summary.interestDue > 0 ? "warning" : "neutral",
          },
          {
            label: "Overdue",
            value: summary.overdueDays > 0 ? `${summary.overdueDays} days` : "Current",
            tone: summary.overdueDays > 0 ? "danger" : "success",
            hint: summary.ownerName ? `Owner ${summary.ownerName}` : undefined,
          },
        ]}
      />

      <Tabs defaultValue="related" className="min-h-0 flex-1">
        <TabsList className="w-full justify-start overflow-x-auto">
          <TabsTrigger value="related">Related</TabsTrigger>
          <TabsTrigger value="plan">
            Payment plan
            <span className="ml-1.5 text-muted-foreground tabular-nums">
              {booking.milestones.length}
            </span>
          </TabsTrigger>
          <TabsTrigger value="ledger">Ledger</TabsTrigger>
          <TabsTrigger value="receipts">
            Receipts
            <span className="ml-1.5 text-muted-foreground tabular-nums">{receipts.length}</span>
          </TabsTrigger>
          <TabsTrigger value="documents">
            Documents
            {pendingDocuments.length > 0 ? (
              <span className={cn("ml-1.5 tabular-nums", TONE_TEXT.warning)}>
                {pendingDocuments.length}
              </span>
            ) : null}
          </TabsTrigger>
          <TabsTrigger value="file">
            The file
            {lifecycle?.possession?.blockers?.length ? (
              <span className={cn("ml-1.5 tabular-nums", TONE_TEXT.warning)}>
                {lifecycle.possession.blockers.length}
              </span>
            ) : null}
          </TabsTrigger>
          <TabsTrigger value="letters">Letters</TabsTrigger>
          <TabsTrigger value="details">Details</TabsTrigger>
        </TabsList>

        {/* ---------------- Related ---------------- */}
        <TabsContent value="related" className="mt-3 grid gap-4 xl:grid-cols-2">
          <RelatedList
            icon={Users}
            title="Applicants"
            count={booking.applicants.length}
            empty="No applicant recorded."
          >
            <table className="w-full text-[12.5px]">
              <tbody>
                {booking.applicants.map((applicant) => (
                  <tr key={applicant.id} className="border-b last:border-0">
                    <td className="px-4 py-2.5">
                      <p className="font-medium">
                        {applicant.salutation ? `${applicant.salutation} ` : ""}
                        {applicant.name}
                      </p>
                      <p className="text-[11px] text-muted-foreground">
                        {humanise(applicant.role)}
                        {applicant.relation ? ` · ${applicant.relation}` : ""}
                        {applicant.phone ? ` · ${applicant.phone}` : ""}
                      </p>
                    </td>
                    <td className="px-4 py-2.5 text-right text-[11px] text-muted-foreground">
                      {applicant.pan ? <p className="tabular-nums">PAN {applicant.pan}</p> : null}
                      {applicant.aadhaarLast4 ? (
                        <p className="tabular-nums">Aadhaar ••••{applicant.aadhaarLast4}</p>
                      ) : null}
                    </td>
                    <td className="px-4 py-2.5 text-right">
                      <Pill tone={kycStatusTone(applicant.kycStatus)}>
                        {humanise(applicant.kycStatus)}
                      </Pill>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </RelatedList>

          <RelatedList
            icon={AlertTriangle}
            title="Open demands"
            count={openDemands.length}
            empty="Nothing outstanding against this booking."
          >
            <table className="w-full text-[12.5px]">
              <tbody>
                {openDemands.map((demand) => (
                  <tr key={demand.id} className="border-b last:border-0">
                    <td className="px-4 py-2.5">
                      <p className="font-medium">{demand.label}</p>
                      <p className="text-[11px] text-muted-foreground">
                        {demand.demandNumber} · due {shortDate(demand.dueDate)}
                      </p>
                    </td>
                    <td className="px-4 py-2.5 text-right tabular-nums">
                      <p className="font-medium">{inr(demand.outstanding)}</p>
                      {demand.interestDue > 0 ? (
                        <p className={cn("text-[11px]", TONE_TEXT.warning)}>
                          + {inr(demand.interestDue)} interest
                        </p>
                      ) : null}
                    </td>
                    <td className="px-4 py-2.5 text-right">
                      <Pill tone={demandStatusTone(demand.status)}>{demand.status}</Pill>
                      {demand.ageDays > 0 ? (
                        <p className={cn("mt-0.5 text-[11px] tabular-nums", TONE_TEXT.danger)}>
                          {demand.ageDays}d late
                        </p>
                      ) : null}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </RelatedList>

          <RelatedList
            icon={ReceiptText}
            title="Recent receipts"
            count={receipts.length}
            empty="No money received yet."
            actions={
              <Button
                variant="ghost"
                size="sm"
                className="h-7 gap-1 text-[12px]"
                onClick={() => setReceiving(true)}
              >
                <Plus className="size-3.5" /> New
              </Button>
            }
          >
            <table className="w-full text-[12.5px]">
              <tbody>
                {receipts.slice(0, 6).map((receipt) => (
                  <tr key={receipt.id} className="border-b last:border-0">
                    <td className="px-4 py-2.5">
                      <p className="font-medium">{receipt.receiptNumber}</p>
                      <p className="text-[11px] text-muted-foreground">
                        {shortDate(receipt.receivedOn)} · {receipt.mode}
                        {receipt.instrument ? ` · ${receipt.instrument}` : ""}
                      </p>
                    </td>
                    <td className="px-4 py-2.5 text-right tabular-nums">
                      <p className="font-medium">{inr(receipt.creditedAmount)}</p>
                      {receipt.tdsAmount > 0 ? (
                        <p className="text-[11px] text-muted-foreground">
                          incl. {inr(receipt.tdsAmount)} TDS
                        </p>
                      ) : null}
                    </td>
                    <td className="px-4 py-2.5 text-right">
                      <Pill tone={receiptStatusTone(receipt.status)}>{receipt.status}</Pill>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </RelatedList>

          <RelatedList
            icon={FileCheck2}
            title="Paperwork outstanding"
            count={pendingDocuments.length}
            hint={`${documents.length} on the checklist`}
            empty="Every required document is verified."
          >
            <table className="w-full text-[12.5px]">
              <tbody>
                {pendingDocuments.slice(0, 8).map((document) => (
                  <tr key={document.id} className="border-b last:border-0">
                    <td className="px-4 py-2.5">
                      <p className="font-medium">{document.name}</p>
                      <p className="text-[11px] text-muted-foreground">
                        {humanise(document.stage)}
                      </p>
                    </td>
                    <td className="px-4 py-2.5 text-right">
                      <Pill tone={documentStatusTone(document.status)}>
                        {humanise(document.status)}
                      </Pill>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </RelatedList>
        </TabsContent>

        {/* ---------------- Payment plan ---------------- */}
        <TabsContent value="plan" className="mt-3">
          <PlanTab
            booking={booking}
            onRaise={setRaising}
            onChanged={load}
          />
        </TabsContent>

        {/* ---------------- Ledger ---------------- */}
        <TabsContent value="ledger" className="mt-3">
          <LedgerTab ledger={ledger} />
        </TabsContent>

        {/* ---------------- Receipts ---------------- */}
        <TabsContent value="receipts" className="mt-3">
          <ReceiptsTab receipts={receipts} onChanged={load} />
        </TabsContent>

        {/* ---------------- Documents ---------------- */}
        <TabsContent value="documents" className="mt-3">
          <DocumentsTab documents={documents} onChanged={load} />
        </TabsContent>

        {/* ---------------- The file ---------------- */}
        <TabsContent value="file" className="mt-3 flex flex-col gap-4">
          <AgreementPanel
            bookingId={id}
            agreement={lifecycle?.agreement ?? null}
            onChanged={load}
          />
          <LoanPanel bookingId={id} loan={lifecycle?.loan ?? null} onChanged={load} />
          <PossessionPanel
            bookingId={id}
            possession={lifecycle?.possession ?? null}
            onChanged={load}
          />
        </TabsContent>

        {/* ---------------- Letters ---------------- */}
        <TabsContent value="letters" className="mt-3">
          <LettersPanel
            bookingId={id}
            demands={demands.map((d) => ({
              id: d.id,
              label: d.label,
              demandNumber: d.demandNumber,
            }))}
          />
        </TabsContent>

        {/* ---------------- Details ---------------- */}
        <TabsContent value="details" className="mt-3">
          <section className="rounded-xl border bg-card p-5 shadow-xs">
            <FieldGrid
              columns={3}
              fields={[
                { label: "Booking number", value: summary.bookingNumber },
                { label: "Booked on", value: shortDate(summary.bookingDate) },
                {
                  label: "Status",
                  value: (
                    <Pill tone={bookingStatusTone(summary.status)}>
                      {summary.statusLabel || humanise(summary.status)}
                    </Pill>
                  ),
                },
                { label: "Project", value: summary.projectName },
                { label: "Tower", value: summary.towerName ?? "—" },
                { label: "Unit", value: summary.unitNumber },
                { label: "Space type", value: humanise(summary.configuration) },
                {
                  label: "Saleable area",
                  value: `${summary.saleableArea.toLocaleString("en-IN")} sq.ft.`,
                },
                { label: "Payment plan", value: booking.paymentPlanName ?? "—" },
                {
                  label: "Agreement value",
                  value: (
                    <span className="tabular-nums">{inrExact(summary.agreementValue)}</span>
                  ),
                },
                {
                  label: "Grand total",
                  value: <span className="tabular-nums">{inrExact(summary.grandTotal)}</span>,
                },
                {
                  label: "Other charges",
                  value: (
                    <span className="tabular-nums">
                      {inrExact(summary.grandTotal - summary.agreementValue)}
                    </span>
                  ),
                },
                { label: "Owner", value: summary.ownerName ?? "—" },
                {
                  label: "Agreement",
                  value: humanise(summary.agreementStatus),
                },
                { label: "Delivery", value: humanise(summary.possessionStatus) },
                { label: "Home loan", value: summary.hasLoan ? "Yes" : "Self funded" },
                {
                  label: "Source quotation",
                  value: booking.quotationId ? (
                    <Link
                      href={`/dashboard/sales/quotations/${booking.quotationId}`}
                      className="text-primary hover:underline"
                    >
                      Quotation #{booking.quotationId}
                    </Link>
                  ) : (
                    "—"
                  ),
                },
                {
                  label: "Originating lead",
                  value: booking.leadId ? (
                    <Link
                      href={`/dashboard/leads/${booking.leadId}`}
                      className="text-primary hover:underline"
                    >
                      Lead #{booking.leadId}
                    </Link>
                  ) : (
                    "—"
                  ),
                },
              ]}
            />

            {booking.notes ? (
              <div className="mt-5">
                <p className="text-[11px] font-medium tracking-wide text-muted-foreground uppercase">
                  Notes
                </p>
                <p className="mt-1 text-[13px] whitespace-pre-wrap">{booking.notes}</p>
              </div>
            ) : null}
          </section>
        </TabsContent>
      </Tabs>

      <ReceiptDialog
        bookingId={id}
        open={receiving}
        outstanding={summary.outstanding}
        onOpenChange={setReceiving}
        onSaved={load}
      />

      <RaiseDialog
        bookingId={id}
        milestone={raising}
        onClose={() => setRaising(null)}
        onSaved={load}
      />
    </div>
  );
}

/* ------------------------------------------------------------------ *
 * Payment plan
 * ------------------------------------------------------------------ */

/**
 * The schedule, and the four things anyone ever does to it.
 *
 * The plan lives here rather than on the quotation because after booking it
 * stops being a proposal: rescheduling a line, waiving one or adding a charge
 * changes what the customer owes, and that is a post-sales decision with a
 * reason attached to it.
 */
function PlanTab({
  booking,
  onRaise,
  onChanged,
}: {
  booking: BookingDetail;
  onRaise: (milestone: Milestone) => void;
  onChanged: () => void;
}) {
  const [adding, setAdding] = React.useState(false);
  const [busy, setBusy] = React.useState<number | null>(null);

  const total = booking.milestones
    .filter((m) => m.status !== "Waived")
    .reduce((sum, m) => sum + m.totalAmount, 0);
  const received = booking.milestones.reduce((sum, m) => sum + m.received, 0);

  async function waive(milestone: Milestone) {
    const reason = window.prompt(
      `Waive "${milestone.label}" (${inr(milestone.totalAmount)})?\n\nReason, for the audit trail:`
    );
    if (!reason?.trim()) return;

    setBusy(milestone.id);
    try {
      await postSalesApi.waiveInstalment(booking.summary.id, milestone.id, reason.trim());
      toast.success("Instalment waived", { description: milestone.label });
      onChanged();
    } catch (error) {
      toast.error("Could not waive it", {
        description: error instanceof ApiError ? error.message : "Network error.",
      });
    } finally {
      setBusy(null);
    }
  }

  async function reschedule(milestone: Milestone) {
    const due = window.prompt(
      `New due date for "${milestone.label}" (YYYY-MM-DD):`,
      milestone.dueDate?.slice(0, 10) ?? ""
    );
    if (!due?.trim()) return;

    const reason = window.prompt("Reason, for the audit trail:");
    if (!reason?.trim()) return;

    setBusy(milestone.id);
    try {
      await postSalesApi.reschedule(
        booking.summary.id,
        milestone.id,
        due.trim(),
        reason.trim()
      );
      toast.success("Instalment rescheduled");
      onChanged();
    } catch (error) {
      toast.error("Could not reschedule it", {
        description: error instanceof ApiError ? error.message : "Network error.",
      });
    } finally {
      setBusy(null);
    }
  }

  return (
    <RelatedList
      icon={ClipboardCheck}
      title={booking.paymentPlanName ?? "Payment plan"}
      count={booking.milestones.length}
      hint={`${inr(received)} of ${inr(total)} collected`}
      actions={
        <Button
          variant="ghost"
          size="sm"
          className="h-7 gap-1 text-[12px]"
          onClick={() => setAdding(true)}
        >
          <Plus className="size-3.5" /> Add charge
        </Button>
      }
    >
      <table className="w-full min-w-[860px] text-[12.5px]">
        <thead>
          <tr className="border-b text-[10.5px] tracking-wide text-muted-foreground uppercase">
            <th className="px-4 py-2 text-left font-medium">#</th>
            <th className="px-4 py-2 text-left font-medium">Instalment</th>
            <th className="px-4 py-2 text-left font-medium">Stage</th>
            <th className="px-4 py-2 text-right font-medium">%</th>
            <th className="px-4 py-2 text-right font-medium">Basic</th>
            <th className="px-4 py-2 text-right font-medium">Tax</th>
            <th className="px-4 py-2 text-right font-medium">Total</th>
            <th className="px-4 py-2 text-right font-medium">Received</th>
            <th className="px-4 py-2 text-left font-medium">Status</th>
            <th className="px-4 py-2 text-right font-medium">Due</th>
            <th className="px-4 py-2" />
          </tr>
        </thead>

        <tbody>
          {booking.milestones.map((milestone) => (
            <tr
              key={milestone.id}
              className={cn(
                "border-b last:border-0",
                milestone.status === "Waived" && "text-muted-foreground line-through"
              )}
            >
              <td className="px-4 py-2 tabular-nums text-muted-foreground">
                {milestone.sortOrder}
              </td>
              <td className="px-4 py-2 font-medium">{milestone.label}</td>
              <td className="px-4 py-2 text-[11.5px] text-muted-foreground">
                {milestone.constructionStage ?? "—"}
              </td>
              {/* Stored as a fraction — 0.05 for 5% — so it is scaled here
                  rather than being printed as a fifth of a percent. */}
              <td className="px-4 py-2 text-right tabular-nums">
                {milestone.percent > 0 ? `${+(milestone.percent * 100).toFixed(2)}%` : "—"}
              </td>
              <td className="px-4 py-2 text-right tabular-nums">{inr(milestone.basicAmount)}</td>
              <td className="px-4 py-2 text-right tabular-nums text-muted-foreground">
                {inr(milestone.taxAmount)}
              </td>
              <td className="px-4 py-2 text-right font-medium tabular-nums">
                {inr(milestone.totalAmount)}
              </td>
              <td
                className={cn(
                  "px-4 py-2 text-right tabular-nums",
                  milestone.received > 0 && TONE_TEXT.success
                )}
              >
                {milestone.received > 0 ? inr(milestone.received) : "—"}
              </td>
              <td className="px-4 py-2">
                <Pill tone={milestoneStatusTone(milestone.status)}>{milestone.status}</Pill>
              </td>
              <td className="px-4 py-2 text-right text-[11.5px] whitespace-nowrap text-muted-foreground">
                {shortDate(milestone.dueDate)}
              </td>
              <td className="px-4 py-2 text-right whitespace-nowrap">
                {busy === milestone.id ? (
                  <Loader2 className="ml-auto size-3.5 animate-spin text-muted-foreground" />
                ) : milestone.status === "Pending" ? (
                  <div className="flex items-center justify-end gap-1">
                    <Button
                      size="sm"
                      variant="outline"
                      className="h-6 gap-1 px-2 text-[11px]"
                      onClick={() => onRaise(milestone)}
                    >
                      <Send className="size-3" /> Raise
                    </Button>
                    <Button
                      size="sm"
                      variant="ghost"
                      className="h-6 px-2 text-[11px]"
                      onClick={() => reschedule(milestone)}
                    >
                      Move
                    </Button>
                    <Button
                      size="sm"
                      variant="ghost"
                      className="h-6 px-2 text-[11px]"
                      onClick={() => waive(milestone)}
                    >
                      Waive
                    </Button>
                  </div>
                ) : null}
              </td>
            </tr>
          ))}
        </tbody>

        <tfoot>
          <tr className="border-t-2 bg-muted/40 font-semibold">
            <td className="px-4 py-2" colSpan={6}>
              Total payable
            </td>
            <td className="px-4 py-2 text-right tabular-nums">{inr(total)}</td>
            <td className={cn("px-4 py-2 text-right tabular-nums", TONE_TEXT.success)}>
              {inr(received)}
            </td>
            <td className="px-4 py-2" colSpan={3} />
          </tr>
        </tfoot>
      </table>

      <AddChargeDialog
        bookingId={booking.summary.id}
        open={adding}
        onOpenChange={setAdding}
        onSaved={onChanged}
      />
    </RelatedList>
  );
}

/* ------------------------------------------------------------------ *
 * Ledger
 * ------------------------------------------------------------------ */

/** The customer statement — the document that gets emailed when someone disputes a figure. */
function LedgerTab({ ledger }: { ledger: Ledger | null }) {
  if (!ledger) {
    return <CrmLoadingState label="Building the statement" />;
  }

  const debited = ledger.lines.reduce((sum, line) => sum + line.debit, 0);
  const credited = ledger.lines.reduce((sum, line) => sum + line.credit, 0);
  const closing = ledger.lines.at(-1)?.balance ?? 0;

  return (
    <RelatedList
      icon={BookOpen}
      title="Customer ledger"
      hint={`${ledger.lines.length} entries`}
      actions={
        <span className="text-[12px] font-medium tabular-nums">
          Balance {inrExact(closing)}
        </span>
      }
    >
      <table className="w-full min-w-[760px] text-[12.5px]">
        <thead>
          <tr className="border-b text-[10.5px] tracking-wide text-muted-foreground uppercase">
            <th className="px-4 py-2 text-left font-medium">Date</th>
            <th className="px-4 py-2 text-left font-medium">Particulars</th>
            <th className="px-4 py-2 text-left font-medium">Reference</th>
            <th className="px-4 py-2 text-right font-medium">Debit</th>
            <th className="px-4 py-2 text-right font-medium">Credit</th>
            <th className="px-4 py-2 text-right font-medium">Balance</th>
          </tr>
        </thead>

        <tbody>
          {ledger.lines.map((line, index) => (
            <tr key={`${line.reference}-${index}`} className="border-b last:border-0">
              <td className="px-4 py-2 whitespace-nowrap text-muted-foreground">
                {shortDate(line.on)}
              </td>
              <td className="px-4 py-2">{line.description}</td>
              <td className="px-4 py-2 text-[11.5px] text-muted-foreground">{line.reference}</td>
              <td className="px-4 py-2 text-right tabular-nums">
                {line.debit > 0 ? inrExact(line.debit) : "—"}
              </td>
              <td className={cn("px-4 py-2 text-right tabular-nums", line.credit > 0 && TONE_TEXT.success)}>
                {line.credit > 0 ? inrExact(line.credit) : "—"}
              </td>
              <td className="px-4 py-2 text-right font-medium tabular-nums">
                {inrExact(line.balance)}
              </td>
            </tr>
          ))}
        </tbody>

        {/* Column sums, not the booking's denormalised totals: those count
            principal only, and this statement also carries interest — a footer
            that disagrees with the column above it is what starts the dispute
            it was printed to settle. */}
        <tfoot>
          <tr className="border-t-2 bg-muted/40 font-semibold">
            <td className="px-4 py-2" colSpan={3}>
              Closing balance
            </td>
            <td className="px-4 py-2 text-right tabular-nums">{inrExact(debited)}</td>
            <td className="px-4 py-2 text-right tabular-nums">{inrExact(credited)}</td>
            <td
              className={cn(
                "px-4 py-2 text-right tabular-nums",
                closing > 0 ? TONE_TEXT.danger : TONE_TEXT.success
              )}
            >
              {inrExact(closing)}
            </td>
          </tr>
        </tfoot>
      </table>

      <div className="grid gap-px border-t bg-border sm:grid-cols-4">
        {[
          { label: "Not yet demanded", value: ledger.notYetDemanded },
          { label: "Interest charged", value: ledger.interestCharged },
          { label: "Interest waived", value: ledger.interestWaived },
          { label: "Overdue amount", value: ledger.overdueAmount },
        ].map((cell) => (
          <div key={cell.label} className="bg-card px-4 py-2.5">
            <p className="text-[10.5px] font-medium tracking-wide text-muted-foreground uppercase">
              {cell.label}
            </p>
            <p className="text-[13.5px] font-semibold tabular-nums">{inr(cell.value)}</p>
          </div>
        ))}
      </div>
    </RelatedList>
  );
}

/* ------------------------------------------------------------------ *
 * Receipts
 * ------------------------------------------------------------------ */

function ReceiptsTab({
  receipts,
  onChanged,
}: {
  receipts: ReceiptRow[];
  onChanged: () => void;
}) {
  const [busy, setBusy] = React.useState<number | null>(null);

  async function clear(receipt: ReceiptRow) {
    setBusy(receipt.id);
    try {
      await postSalesApi.clearReceipt(receipt.id);
      toast.success("Receipt cleared", { description: receipt.receiptNumber });
      onChanged();
    } catch (error) {
      toast.error("Could not clear it", {
        description: error instanceof ApiError ? error.message : "Network error.",
      });
    } finally {
      setBusy(null);
    }
  }

  async function bounce(receipt: ReceiptRow) {
    const reason = window.prompt(
      `Mark ${receipt.receiptNumber} as bounced?\n\nWhat the bank said:`
    );
    if (!reason?.trim()) return;

    setBusy(receipt.id);
    try {
      await postSalesApi.bounceReceipt(receipt.id, reason.trim());
      toast.success("Receipt bounced", {
        description: "The demands it answered are outstanding again.",
      });
      onChanged();
    } catch (error) {
      toast.error("Could not record the bounce", {
        description: error instanceof ApiError ? error.message : "Network error.",
      });
    } finally {
      setBusy(null);
    }
  }

  return (
    <RelatedList
      icon={ReceiptText}
      title="Receipts"
      count={receipts.length}
      empty="No money received against this booking yet."
    >
      <table className="w-full min-w-[900px] text-[12.5px]">
        <thead>
          <tr className="border-b text-[10.5px] tracking-wide text-muted-foreground uppercase">
            <th className="px-4 py-2 text-left font-medium">Receipt</th>
            <th className="px-4 py-2 text-left font-medium">Mode</th>
            <th className="px-4 py-2 text-right font-medium">Paid</th>
            <th className="px-4 py-2 text-right font-medium">TDS</th>
            <th className="px-4 py-2 text-right font-medium">Credited</th>
            <th className="px-4 py-2 text-left font-medium">Applied to</th>
            <th className="px-4 py-2 text-left font-medium">Status</th>
            <th className="px-4 py-2" />
          </tr>
        </thead>

        <tbody>
          {receipts.map((receipt) => (
            <tr key={receipt.id} className="border-b last:border-0 align-top">
              <td className="px-4 py-2.5">
                <p className="font-medium">{receipt.receiptNumber}</p>
                <p className="text-[11px] text-muted-foreground">
                  {shortDate(receipt.receivedOn)}
                </p>
              </td>
              <td className="px-4 py-2.5">
                <p>{receipt.mode}</p>
                {receipt.instrument ? (
                  <p className="text-[11px] text-muted-foreground">
                    {receipt.instrument}
                    {receipt.bankName ? ` · ${receipt.bankName}` : ""}
                  </p>
                ) : null}
              </td>
              <td className="px-4 py-2.5 text-right tabular-nums">{inr(receipt.amount)}</td>
              <td className="px-4 py-2.5 text-right tabular-nums text-muted-foreground">
                {receipt.tdsAmount > 0 ? inr(receipt.tdsAmount) : "—"}
              </td>
              <td className={cn("px-4 py-2.5 text-right font-medium tabular-nums", TONE_TEXT.success)}>
                {inr(receipt.creditedAmount)}
              </td>

              {/* The waterfall, shown rather than summarised: "why is 7,397 of my
                  payment gone" is the single most common collections dispute. */}
              <td className="px-4 py-2.5">
                {receipt.allocations.length === 0 ? (
                  <span className="text-muted-foreground">Unapplied</span>
                ) : (
                  <ul className="space-y-0.5">
                    {receipt.allocations.map((allocation) => (
                      <li key={allocation.demandId} className="text-[11.5px]">
                        <span className="text-muted-foreground">{allocation.label}</span>{" "}
                        <span className="tabular-nums">{inr(allocation.amount)}</span>
                        {allocation.towardsInterest > 0 ? (
                          <span className={cn("tabular-nums", TONE_TEXT.warning)}>
                            {" "}
                            ({inr(allocation.towardsInterest)} interest)
                          </span>
                        ) : null}
                      </li>
                    ))}
                  </ul>
                )}
                {receipt.unallocated > 0 ? (
                  <p className={cn("text-[11px] tabular-nums", TONE_TEXT.info)}>
                    {inr(receipt.unallocated)} on account
                  </p>
                ) : null}
              </td>

              <td className="px-4 py-2.5">
                <Pill tone={receiptStatusTone(receipt.status)}>{receipt.status}</Pill>
                {receipt.bounceReason ? (
                  <p className={cn("mt-0.5 text-[11px]", TONE_TEXT.danger)}>
                    {receipt.bounceReason}
                  </p>
                ) : null}
              </td>

              <td className="px-4 py-2.5 text-right whitespace-nowrap">
                {busy === receipt.id ? (
                  <Loader2 className="ml-auto size-3.5 animate-spin text-muted-foreground" />
                ) : receipt.status === "Pending" ? (
                  <div className="flex items-center justify-end gap-1">
                    <Button
                      size="sm"
                      variant="outline"
                      className="h-6 px-2 text-[11px]"
                      onClick={() => clear(receipt)}
                    >
                      Clear
                    </Button>
                    <Button
                      size="sm"
                      variant="ghost"
                      className="h-6 px-2 text-[11px]"
                      onClick={() => bounce(receipt)}
                    >
                      Bounce
                    </Button>
                  </div>
                ) : receipt.status === "Cleared" ? (
                  <Button
                    size="sm"
                    variant="ghost"
                    className="h-6 px-2 text-[11px]"
                    onClick={() => bounce(receipt)}
                  >
                    Bounce
                  </Button>
                ) : null}
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </RelatedList>
  );
}

/* ------------------------------------------------------------------ *
 * Documents
 * ------------------------------------------------------------------ */

const DOCUMENT_STAGE_ORDER = [
  "Booking",
  "KYC",
  "Agreement",
  "Loan",
  "Registration",
  "Possession",
];

/**
 * The checklist, grouped by the stage that needs it.
 *
 * Grouped rather than listed flat because the question is never "what documents
 * exist" — it is "can we go to registration on Friday", and that is answerable
 * only when the registration group is visible as a group.
 */
function DocumentsTab({
  documents,
  onChanged,
}: {
  documents: BookingDocument[];
  onChanged: () => void;
}) {
  const [busy, setBusy] = React.useState<number | null>(null);

  const groups = DOCUMENT_STAGE_ORDER.map((stage) => ({
    stage,
    items: documents.filter((d) => d.stage === stage),
  })).filter((group) => group.items.length > 0);

  async function setStatus(document: BookingDocument, status: string) {
    setBusy(document.id);
    try {
      await postSalesApi.saveDocument(document.id, {
        status,
        receivedOn:
          status === "Received" || status === "Verified"
            ? (document.receivedOn ?? new Date().toISOString())
            : document.receivedOn,
      });
      toast.success(`${document.name} — ${humanise(status).toLowerCase()}`);
      onChanged();
    } catch (error) {
      toast.error("Could not update the document", {
        description: error instanceof ApiError ? error.message : "Network error.",
      });
    } finally {
      setBusy(null);
    }
  }

  return (
    <div className="grid gap-4 xl:grid-cols-2">
      {groups.map((group) => {
        const done = group.items.filter(
          (d) => d.status === "Verified" || d.status === "NotApplicable"
        ).length;

        return (
          <RelatedList
            key={group.stage}
            icon={FileSignature}
            title={humanise(group.stage)}
            count={group.items.length}
            hint={`${done} of ${group.items.length} cleared`}
          >
            <table className="w-full text-[12.5px]">
              <tbody>
                {group.items.map((document) => (
                  <tr key={document.id} className="border-b last:border-0">
                    <td className="px-4 py-2">
                      <p className={cn("font-medium", !document.isRequired && "text-muted-foreground")}>
                        {document.name}
                        {document.isRequired ? null : (
                          <span className="ml-1.5 text-[11px] font-normal">(optional)</span>
                        )}
                      </p>
                      {document.receivedOn ? (
                        <p className="text-[11px] text-muted-foreground">
                          Received {shortDate(document.receivedOn)}
                        </p>
                      ) : null}
                    </td>
                    <td className="px-4 py-2 text-right whitespace-nowrap">
                      {busy === document.id ? (
                        <Loader2 className="ml-auto size-3.5 animate-spin text-muted-foreground" />
                      ) : (
                        <Select
                          value={document.status}
                          onValueChange={(value) => setStatus(document, value)}
                        >
                          <SelectTrigger size="sm" className="h-6 w-[132px] text-[11px]">
                            <SelectValue />
                          </SelectTrigger>
                          <SelectContent>
                            {["Pending", "Received", "Verified", "Rejected", "NotApplicable"].map(
                              (value) => (
                                <SelectItem key={value} value={value}>
                                  {humanise(value)}
                                </SelectItem>
                              )
                            )}
                          </SelectContent>
                        </Select>
                      )}
                    </td>
                    <td className="w-16 px-4 py-2 text-right">
                      <Pill tone={documentStatusTone(document.status)}>
                        {humanise(document.status)}
                      </Pill>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </RelatedList>
        );
      })}
    </div>
  );
}

/* ------------------------------------------------------------------ *
 * Dialogs
 * ------------------------------------------------------------------ */

/**
 * Taking money.
 *
 * The TDS field is separate from the amount and stated as such, because the
 * customer deducts it and pays it to the government — crediting them only with
 * what reached the bank leaves every 194-IA booking permanently 1% short.
 */
function ReceiptDialog({
  bookingId,
  open,
  outstanding,
  onOpenChange,
  onSaved,
}: {
  bookingId: number;
  open: boolean;
  outstanding: number;
  onOpenChange: (open: boolean) => void;
  onSaved: () => void;
}) {
  const [amount, setAmount] = React.useState("");
  const [tds, setTds] = React.useState("0");
  const [mode, setMode] = React.useState("NEFT");
  const [instrument, setInstrument] = React.useState("");
  const [bank, setBank] = React.useState("");
  const [receivedOn, setReceivedOn] = React.useState(
    () => new Date().toISOString().slice(0, 10)
  );
  const [notes, setNotes] = React.useState("");
  const [saving, setSaving] = React.useState(false);

  // Cleared by adjusting state during render rather than from an effect: the
  // fields belong to one visit to this dialog, and an effect would paint the
  // last entry for a frame before wiping it.
  const [wasOpen, setWasOpen] = React.useState(open);

  if (open !== wasOpen) {
    setWasOpen(open);

    if (open) {
      setAmount("");
      setTds("0");
      setMode("NEFT");
      setInstrument("");
      setBank("");
      setNotes("");
      setReceivedOn(new Date().toISOString().slice(0, 10));
    }
  }

  const paid = Number(amount) || 0;
  const withheld = Number(tds) || 0;
  const credited = paid + withheld;

  // A cheque is money in the tray, not money in the bank, so it lands as
  // pending and only counts once somebody clears it.
  const settlesLater = mode === "Cheque" || mode === "DD";

  async function save() {
    if (paid <= 0) {
      toast.error("Enter the amount received.");
      return;
    }

    setSaving(true);
    try {
      const receipt = await postSalesApi.receive({
        bookingId,
        amount: paid,
        tdsAmount: withheld,
        mode,
        instrument: instrument.trim() || null,
        bankName: bank.trim() || null,
        receivedOn,
        status: settlesLater ? "Pending" : "Cleared",
        notes: notes.trim() || null,
      });

      toast.success(`Receipt ${receipt.receiptNumber} recorded`, {
        description: settlesLater
          ? "Held as pending until the instrument clears."
          : `${inr(receipt.creditedAmount)} credited to the ledger.`,
      });

      onOpenChange(false);
      onSaved();
    } catch (error) {
      toast.error("Could not record the receipt", {
        description: error instanceof ApiError ? error.message : "Network error.",
      });
    } finally {
      setSaving(false);
    }
  }

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-lg">
        <DialogHeader>
          <DialogTitle>Record a receipt</DialogTitle>
          <DialogDescription>
            Applied to the oldest demand first, interest before principal.{" "}
            {outstanding > 0 ? `${inr(outstanding)} outstanding.` : "Nothing outstanding."}
          </DialogDescription>
        </DialogHeader>

        <div className="grid gap-3">
          <div className="grid gap-3 sm:grid-cols-2">
            <div className="grid gap-1.5">
              <Label htmlFor="amount">Amount received</Label>
              <Input
                id="amount"
                inputMode="decimal"
                value={amount}
                onChange={(event) => setAmount(event.target.value)}
                placeholder="0.00"
                className="tabular-nums"
              />
            </div>

            <div className="grid gap-1.5">
              <Label htmlFor="tds">TDS withheld (194-IA)</Label>
              <Input
                id="tds"
                inputMode="decimal"
                value={tds}
                onChange={(event) => setTds(event.target.value)}
                className="tabular-nums"
              />
            </div>
          </div>

          {credited > 0 ? (
            <p className="rounded-md bg-muted px-3 py-2 text-[12px]">
              Customer is credited with{" "}
              <span className="font-semibold tabular-nums">{inr(credited)}</span>
              {withheld > 0 ? " — what reached the bank plus the tax they deducted." : "."}
            </p>
          ) : null}

          <div className="grid gap-3 sm:grid-cols-2">
            <div className="grid gap-1.5">
              <Label>Mode</Label>
              <Select value={mode} onValueChange={setMode}>
                <SelectTrigger>
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {PAYMENT_MODES.map((value) => (
                    <SelectItem key={value} value={value}>
                      {humanise(value)}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>

            <div className="grid gap-1.5">
              <Label htmlFor="receivedOn">Received on</Label>
              <Input
                id="receivedOn"
                type="date"
                value={receivedOn}
                onChange={(event) => setReceivedOn(event.target.value)}
              />
            </div>
          </div>

          <div className="grid gap-3 sm:grid-cols-2">
            <div className="grid gap-1.5">
              <Label htmlFor="instrument">Reference / cheque no.</Label>
              <Input
                id="instrument"
                value={instrument}
                onChange={(event) => setInstrument(event.target.value)}
                placeholder="UTR or instrument number"
              />
            </div>

            <div className="grid gap-1.5">
              <Label htmlFor="bank">Bank</Label>
              <Input
                id="bank"
                value={bank}
                onChange={(event) => setBank(event.target.value)}
                placeholder="Drawn on"
              />
            </div>
          </div>

          <div className="grid gap-1.5">
            <Label htmlFor="notes">Notes</Label>
            <Textarea
              id="notes"
              value={notes}
              onChange={(event) => setNotes(event.target.value)}
              rows={2}
            />
          </div>

          {settlesLater ? (
            <p className="text-[12px] text-muted-foreground">
              Held as pending. It will not touch the ledger until it is cleared.
            </p>
          ) : null}
        </div>

        <DialogFooter>
          <Button variant="outline" onClick={() => onOpenChange(false)} disabled={saving}>
            Cancel
          </Button>
          <Button onClick={save} disabled={saving} className="gap-1.5">
            {saving ? <Loader2 className="size-4 animate-spin" /> : <Banknote className="size-4" />}
            Record receipt
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

/** Raising one instalment as a demand, with the penal rate it will carry. */
function RaiseDialog({
  bookingId,
  milestone,
  onClose,
  onSaved,
}: {
  bookingId: number;
  milestone: Milestone | null;
  onClose: () => void;
  onSaved: () => void;
}) {
  const [dueDate, setDueDate] = React.useState("");
  const [rate, setRate] = React.useState("12");
  const [saving, setSaving] = React.useState(false);

  const [seen, setSeen] = React.useState(milestone);

  if (milestone !== seen) {
    setSeen(milestone);

    if (milestone) {
      // Three weeks is the notice period most demand letters in this market
      // give, and it is the figure somebody overrides rather than types.
      const due = new Date();
      due.setDate(due.getDate() + 21);

      setDueDate((milestone.dueDate ?? due.toISOString()).slice(0, 10));
      setRate("12");
    }
  }

  async function save() {
    if (!milestone) return;

    setSaving(true);
    try {
      const demand = await postSalesApi.raiseDemand(bookingId, {
        milestoneId: milestone.id,
        dueDate,
        interestRatePercent: Number(rate) || 0,
      });

      toast.success(`Demand ${demand.demandNumber} raised`, {
        description: `${inr(demand.totalAmount)} due ${shortDate(demand.dueDate)}.`,
      });

      onClose();
      onSaved();
    } catch (error) {
      toast.error("Could not raise the demand", {
        description: error instanceof ApiError ? error.message : "Network error.",
      });
    } finally {
      setSaving(false);
    }
  }

  return (
    <Dialog open={milestone !== null} onOpenChange={(open) => (open ? null : onClose())}>
      <DialogContent className="sm:max-w-md">
        <DialogHeader>
          <DialogTitle>Raise a demand</DialogTitle>
          <DialogDescription>
            {milestone ? (
              <>
                {milestone.label} — {inr(milestone.totalAmount)}
              </>
            ) : null}
          </DialogDescription>
        </DialogHeader>

        <div className="grid gap-3 sm:grid-cols-2">
          <div className="grid gap-1.5">
            <Label htmlFor="due">Due date</Label>
            <Input
              id="due"
              type="date"
              value={dueDate}
              onChange={(event) => setDueDate(event.target.value)}
            />
          </div>

          <div className="grid gap-1.5">
            <Label htmlFor="rate">Penal interest (% p.a.)</Label>
            <Input
              id="rate"
              inputMode="decimal"
              value={rate}
              onChange={(event) => setRate(event.target.value)}
              className="tabular-nums"
            />
          </div>
        </div>

        <p className="text-[12px] text-muted-foreground">
          Interest runs day-wise on whatever is still unpaid, from the day after the due
          date.
        </p>

        <DialogFooter>
          <Button variant="outline" onClick={onClose} disabled={saving}>
            Cancel
          </Button>
          <Button onClick={save} disabled={saving} className="gap-1.5">
            {saving ? <Loader2 className="size-4 animate-spin" /> : <Send className="size-4" />}
            Raise demand
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

/** An ad-hoc charge added to the schedule after booking — a second car park, a club fee. */
function AddChargeDialog({
  bookingId,
  open,
  onOpenChange,
  onSaved,
}: {
  bookingId: number;
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onSaved: () => void;
}) {
  const [label, setLabel] = React.useState("");
  const [basic, setBasic] = React.useState("");
  const [tax, setTax] = React.useState("0");
  const [dueDate, setDueDate] = React.useState("");
  const [saving, setSaving] = React.useState(false);

  const [wasOpen, setWasOpen] = React.useState(open);

  if (open !== wasOpen) {
    setWasOpen(open);

    if (open) {
      setLabel("");
      setBasic("");
      setTax("0");
      setDueDate("");
    }
  }

  async function save() {
    if (!label.trim() || !(Number(basic) > 0)) {
      toast.error("A charge needs a name and an amount.");
      return;
    }

    setSaving(true);
    try {
      await postSalesApi.addInstalment(bookingId, {
        label: label.trim(),
        basicAmount: Number(basic),
        taxAmount: Number(tax) || 0,
        dueDate: dueDate || null,
      });

      toast.success("Charge added to the plan");
      onOpenChange(false);
      onSaved();
    } catch (error) {
      toast.error("Could not add the charge", {
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
          <DialogTitle>Add a charge</DialogTitle>
          <DialogDescription>
            Appended to the schedule and added to what this booking is worth.
          </DialogDescription>
        </DialogHeader>

        <div className="grid gap-3">
          <div className="grid gap-1.5">
            <Label htmlFor="label">Charge</Label>
            <Input
              id="label"
              value={label}
              onChange={(event) => setLabel(event.target.value)}
              placeholder="Second car park"
            />
          </div>

          <div className="grid gap-3 sm:grid-cols-3">
            <div className="grid gap-1.5">
              <Label htmlFor="basic">Basic</Label>
              <Input
                id="basic"
                inputMode="decimal"
                value={basic}
                onChange={(event) => setBasic(event.target.value)}
                className="tabular-nums"
              />
            </div>

            <div className="grid gap-1.5">
              <Label htmlFor="tax">Tax</Label>
              <Input
                id="tax"
                inputMode="decimal"
                value={tax}
                onChange={(event) => setTax(event.target.value)}
                className="tabular-nums"
              />
            </div>

            <div className="grid gap-1.5">
              <Label htmlFor="chargeDue">Due</Label>
              <Input
                id="chargeDue"
                type="date"
                value={dueDate}
                onChange={(event) => setDueDate(event.target.value)}
              />
            </div>
          </div>
        </div>

        <DialogFooter>
          <Button variant="outline" onClick={() => onOpenChange(false)} disabled={saving}>
            Cancel
          </Button>
          <Button onClick={save} disabled={saving} className="gap-1.5">
            {saving ? <Loader2 className="size-4 animate-spin" /> : <Plus className="size-4" />}
            Add charge
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
