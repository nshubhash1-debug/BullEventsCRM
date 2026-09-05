"use client";

import * as React from "react";
import {
  Ban,
  CircleAlert,
  FileStack,
  Loader2,
  Receipt,
  RotateCcw,
  Send,
  Zap,
} from "lucide-react";
import Link from "next/link";
import { toast } from "sonner";

import { Pill, TONE_TEXT, type Tone } from "@/components/crm/metrics";
import { CrmLoadingState } from "@/components/shell/crm-loader";
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
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { Textarea } from "@/components/ui/textarea";
import { ApiError } from "@/lib/api";
import {
  billingApi,
  INVOICE_TONE,
  type BillingReference,
  type BillingSummary,
  type CreditNote,
  type TaxInvoice,
} from "@/lib/billing-api";
import { inr, inrExact, shortDate } from "@/lib/post-sales-api";
import { cn } from "@/lib/utils";

/**
 * Tax invoices and credit notes.
 *
 * The demand and the invoice look like the same thing to everyone except the
 * law, so this screen keeps saying which is which. A demand is a request for
 * money under the agreement and can be revised; an invoice is a statutory
 * document with an unbroken number series that can only ever be reversed by a
 * credit note pointing back at it. The buttons enforce exactly that, and the
 * refusals say why rather than greying out.
 */
export default function BillingPage() {
  const [summary, setSummary] = React.useState<BillingSummary | null>(null);
  const [invoices, setInvoices] = React.useState<TaxInvoice[] | null>(null);
  const [credits, setCredits] = React.useState<CreditNote[]>([]);
  const [reference, setReference] = React.useState<BillingReference | null>(null);

  const [status, setStatus] = React.useState("all");
  const [tab, setTab] = React.useState<"invoices" | "credits">("invoices");
  const [busy, setBusy] = React.useState<number | null>(null);
  const [running, setRunning] = React.useState(false);
  const [crediting, setCrediting] = React.useState<TaxInvoice | null>(null);

  const load = React.useCallback(() => {
    billingApi.summary().then(setSummary).catch(() => setSummary(null));

    billingApi
      .invoices(status === "all" ? {} : { status })
      .then(setInvoices)
      .catch((error: unknown) => {
        toast.error("Could not load invoices", {
          description: error instanceof ApiError ? error.message : "Network error.",
        });
        setInvoices([]);
      });

    billingApi.creditNotes().then(setCredits).catch(() => setCredits([]));
  }, [status]);

  React.useEffect(load, [load]);

  React.useEffect(() => {
    billingApi.reference().then(setReference).catch(() => setReference(null));
  }, []);

  async function act(id: number, work: () => Promise<unknown>, done: string) {
    setBusy(id);
    try {
      await work();
      toast.success(done);
      load();
    } catch (error) {
      toast.error("That could not be done", {
        description: error instanceof ApiError ? error.message : "Network error.",
      });
    } finally {
      setBusy(null);
    }
  }

  /**
   * Invoices every demand that has none.
   *
   * Reports the skips as well as the successes: a run that quietly produced
   * nothing looks identical to a run that worked, and the operator has no way
   * to tell which they got.
   */
  async function runBulk() {
    setRunning(true);
    try {
      const result = await billingApi.bulk();

      if (result.count === 0 && result.skipped.length === 0) {
        toast.info("Every demand already has an invoice.");
      } else {
        toast.success(`${result.count} invoice${result.count === 1 ? "" : "s"} raised`, {
          description: result.skipped.length
            ? `${result.skipped.length} skipped — ${result.skipped[0]}`
            : "All as drafts. Issue them when you are ready to send.",
        });
      }

      load();
    } catch (error) {
      toast.error("The run could not finish", {
        description: error instanceof ApiError ? error.message : "Network error.",
      });
    } finally {
      setRunning(false);
    }
  }

  if (!invoices || !summary) {
    return (
      <PagePanel icon={Receipt} title="Tax invoices">
        <CrmLoadingState label="Loading invoices" />
      </PagePanel>
    );
  }

  return (
    <PagePanel
      icon={Receipt}
      title="Tax invoices"
      hint="A demand asks for money under the agreement. An invoice is the statutory document — its own series, its own tax breakup, and reversible only by a credit note."
      actions={
        <div className="flex items-center gap-2">
          <Select value={status} onValueChange={setStatus}>
            <SelectTrigger className="h-8 w-[150px] text-[12.5px]">
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="all">Every status</SelectItem>
              {(reference?.invoiceStatuses ?? []).map((value) => (
                <SelectItem key={value} value={value}>
                  {value}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>

          <Button size="sm" className="h-8 gap-1.5 text-[12.5px]" disabled={running} onClick={runBulk}>
            {running ? <Loader2 className="size-3.5 animate-spin" /> : <Zap className="size-3.5" />}
            Invoice every open demand
          </Button>
        </div>
      }
    >
      {summary.needsGstProfile ? (
        <div
          className={cn(
            "mx-5 mb-3 flex items-start gap-2 rounded-lg border border-amber-500/40 bg-amber-500/10 px-3 py-2.5 text-[12.5px]"
          )}
        >
          <CircleAlert className="mt-0.5 size-4 shrink-0 text-amber-600" />
          <p>
            No GST profile is set up, so no invoice can be raised. A tax invoice needs a GSTIN
            and a place of supply to be a valid document.{" "}
            <Link
              href="/dashboard/post-sales/gst-profiles"
              className="font-medium text-primary underline underline-offset-2"
            >
              Set one up
            </Link>
            .
          </p>
        </div>
      ) : null}

      {/* ---------------- the numbers ---------------- */}
      <div className="mx-5 mb-3 grid gap-px overflow-hidden rounded-lg border bg-border sm:grid-cols-2 lg:grid-cols-4">
        <Tile label="Drafts" value={summary.invoicesDraft} hint="Not yet issued" />
        <Tile label="Issued" value={summary.invoicesIssued} hint="Number is out in the world" />
        <Tile
          label="GST billed"
          value={inr(summary.taxBilled)}
          hint="CGST + SGST across every live invoice"
        />
        <Tile
          label="GST credited back"
          value={inr(summary.taxCredited)}
          hint="Reversed by credit note"
          tone={summary.taxCredited > 0 ? "warning" : "neutral"}
        />
      </div>

      {/* ---------------- tabs ---------------- */}
      <div className="mx-5 mb-2 flex gap-1 border-b">
        {(
          [
            ["invoices", `Invoices (${invoices.length})`],
            ["credits", `Credit notes (${credits.length})`],
          ] as const
        ).map(([value, label]) => (
          <button
            key={value}
            type="button"
            onClick={() => setTab(value)}
            className={cn(
              "-mb-px border-b-2 px-3 py-1.5 text-[12.5px] font-medium transition-colors",
              tab === value
                ? "border-primary text-primary"
                : "border-transparent text-muted-foreground hover:text-foreground"
            )}
          >
            {label}
          </button>
        ))}
      </div>

      <div className="min-h-0 flex-1 overflow-auto px-5 pb-4">
        {tab === "invoices" ? (
          invoices.length === 0 ? (
            <Empty
              icon={FileStack}
              title="No invoice has been raised yet"
              body="Raise one against a demand, or run the bulk action to invoice every open demand at once."
            />
          ) : (
            <table className="w-full min-w-[1080px] text-[12.5px]">
              <thead className="sticky top-0 bg-card">
                <tr className="border-b text-left text-[11px] tracking-wide text-muted-foreground uppercase">
                  <th className="py-2 pr-3 font-medium">Invoice</th>
                  <th className="py-2 pr-3 font-medium">Against</th>
                  <th className="py-2 pr-3 font-medium">Customer</th>
                  <th className="py-2 pr-3 text-right font-medium">Value</th>
                  <th className="py-2 pr-3 text-right font-medium">Taxable</th>
                  <th className="py-2 pr-3 text-right font-medium">CGST + SGST</th>
                  <th className="py-2 pr-3 text-right font-medium">Total</th>
                  <th className="py-2 pr-3 font-medium">Status</th>
                  <th className="py-2 font-medium" />
                </tr>
              </thead>
              <tbody className="tabular-nums">
                {invoices.map((invoice) => (
                  <tr key={invoice.id} className="border-b align-top last:border-0 hover:bg-muted/40">
                    <td className="py-2.5 pr-3">
                      <p className="font-medium">{invoice.invoiceNumber}</p>
                      <p className="text-[11px] text-muted-foreground">
                        {shortDate(invoice.invoiceDate)} · SAC {invoice.sacCode}
                      </p>
                    </td>

                    <td className="py-2.5 pr-3">
                      <Link
                        href={`/dashboard/post-sales/bookings/${invoice.bookingId}`}
                        className="font-medium text-primary hover:underline"
                      >
                        {invoice.demandNumber}
                      </Link>
                      <p className="text-[11px] text-muted-foreground">
                        {invoice.demandLabel} · {invoice.unitNumber}
                      </p>
                    </td>

                    <td className="py-2.5 pr-3">
                      <p>{invoice.customerName}</p>
                      <p className="text-[11px] text-muted-foreground">
                        {invoice.placeOfSupply ?? "—"}
                      </p>
                    </td>

                    <td className="py-2.5 pr-3 text-right">
                      {inrExact(invoice.grossValue)}
                      <p className="text-[11px] text-muted-foreground">
                        less land {inr(invoice.landAbatement)}
                      </p>
                    </td>

                    <td className="py-2.5 pr-3 text-right">
                      {inrExact(invoice.taxableValue)}
                      <p className="text-[11px] text-muted-foreground">
                        at {invoice.gstRate}%
                      </p>
                    </td>

                    <td className="py-2.5 pr-3 text-right">
                      {inrExact(invoice.totalTax)}
                      <p className="text-[11px] text-muted-foreground">
                        {inr(invoice.cgstAmount)} + {inr(invoice.sgstAmount)}
                      </p>
                    </td>

                    <td className="py-2.5 pr-3 text-right font-medium">
                      {inrExact(invoice.invoiceTotal)}
                      {invoice.credited > 0 ? (
                        <p className={cn("text-[11px]", TONE_TEXT.warning)}>
                          {inr(invoice.credited)} credited
                        </p>
                      ) : null}
                    </td>

                    <td className="py-2.5 pr-3">
                      <Pill tone={INVOICE_TONE[invoice.status] ?? "neutral"}>{invoice.status}</Pill>
                    </td>

                    <td className="py-2.5 text-right whitespace-nowrap">
                      {busy === invoice.id ? (
                        <Loader2 className="ml-auto size-3.5 animate-spin text-muted-foreground" />
                      ) : invoice.status === "Draft" ? (
                        <div className="flex justify-end gap-1">
                          <Button
                            size="sm"
                            className="h-7 gap-1 px-2 text-[11px]"
                            onClick={() =>
                              act(
                                invoice.id,
                                () => billingApi.issue(invoice.id),
                                `${invoice.invoiceNumber} issued`
                              )
                            }
                          >
                            <Send className="size-3" />
                            Issue
                          </Button>
                          <Button
                            size="sm"
                            variant="ghost"
                            className="h-7 gap-1 px-2 text-[11px]"
                            onClick={() =>
                              act(
                                invoice.id,
                                () => billingApi.cancelInvoice(invoice.id, "Cancelled before issue"),
                                "Draft cancelled"
                              )
                            }
                          >
                            <Ban className="size-3" />
                          </Button>
                        </div>
                      ) : invoice.status === "Issued" ? (
                        <Button
                          size="sm"
                          variant="outline"
                          className="h-7 gap-1 px-2 text-[11px]"
                          onClick={() => setCrediting(invoice)}
                        >
                          <RotateCcw className="size-3" />
                          Credit
                        </Button>
                      ) : null}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          )
        ) : credits.length === 0 ? (
          <Empty
            icon={RotateCcw}
            title="Nothing has been credited"
            body="A credit note reverses part or all of an issued invoice — an area reduction, a waiver, a cancellation. The invoice itself is never edited."
          />
        ) : (
          <table className="w-full min-w-[900px] text-[12.5px]">
            <thead className="sticky top-0 bg-card">
              <tr className="border-b text-left text-[11px] tracking-wide text-muted-foreground uppercase">
                <th className="py-2 pr-3 font-medium">Credit note</th>
                <th className="py-2 pr-3 font-medium">Reverses</th>
                <th className="py-2 pr-3 font-medium">Why</th>
                <th className="py-2 pr-3 text-right font-medium">Value</th>
                <th className="py-2 pr-3 text-right font-medium">Tax</th>
                <th className="py-2 text-right font-medium">Total</th>
              </tr>
            </thead>
            <tbody className="tabular-nums">
              {credits.map((note) => (
                <tr key={note.id} className="border-b align-top last:border-0 hover:bg-muted/40">
                  <td className="py-2.5 pr-3">
                    <p className="font-medium">{note.creditNoteNumber}</p>
                    <p className="text-[11px] text-muted-foreground">{shortDate(note.issuedOn)}</p>
                  </td>
                  <td className="py-2.5 pr-3">{note.invoiceNumber ?? "—"}</td>
                  <td className="max-w-[320px] py-2.5 pr-3">
                    <Pill tone="warning">{note.reasonLabel}</Pill>
                    {note.narrative ? (
                      <p className="mt-0.5 text-[11px] text-muted-foreground">{note.narrative}</p>
                    ) : null}
                  </td>
                  <td className="py-2.5 pr-3 text-right">{inrExact(note.grossValue)}</td>
                  <td className="py-2.5 pr-3 text-right">
                    {inrExact(note.cgstAmount + note.sgstAmount)}
                  </td>
                  <td className="py-2.5 text-right font-medium">{inrExact(note.creditTotal)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>

      <CreditDialog
        invoice={crediting}
        reasons={reference?.creditReasons ?? []}
        onClose={() => setCrediting(null)}
        onDone={load}
      />
    </PagePanel>
  );
}

/* ------------------------------------------------------------------ *
 * Credit dialog
 * ------------------------------------------------------------------ */

function CreditDialog({
  invoice,
  reasons,
  onClose,
  onDone,
}: {
  invoice: TaxInvoice | null;
  reasons: Array<{ value: string; label: string }>;
  onClose: () => void;
  onDone: () => void;
}) {
  const [amount, setAmount] = React.useState("");
  const [reason, setReason] = React.useState("AreaReduction");
  const [narrative, setNarrative] = React.useState("");
  const [saving, setSaving] = React.useState(false);
  const [seen, setSeen] = React.useState<TaxInvoice | null>(null);

  // Reset on open, in the render pass rather than an effect — an effect here
  // paints the previous invoice's amount for one frame.
  if (invoice !== seen) {
    setSeen(invoice);

    if (invoice) {
      setAmount("");
      setReason("AreaReduction");
      setNarrative("");
    }
  }

  const left = invoice ? invoice.grossValue - invoice.credited : 0;
  const parsed = Number(amount);
  const valid = parsed > 0 && parsed <= left + 0.001;

  async function submit() {
    if (!invoice || !valid) return;

    setSaving(true);
    try {
      const note = await billingApi.credit(invoice.id, {
        grossValue: parsed,
        reason,
        narrative: narrative || null,
      });

      toast.success(`${note.creditNoteNumber} raised`, {
        description: `${inrExact(note.creditTotal)} credited against ${invoice.invoiceNumber}.`,
      });

      onClose();
      onDone();
    } catch (error) {
      toast.error("The credit note could not be raised", {
        description: error instanceof ApiError ? error.message : "Network error.",
      });
    } finally {
      setSaving(false);
    }
  }

  return (
    <Dialog open={invoice !== null} onOpenChange={(open) => (open ? null : onClose())}>
      <DialogContent className="sm:max-w-lg">
        <DialogHeader>
          <DialogTitle>Credit {invoice?.invoiceNumber}</DialogTitle>
          <DialogDescription>
            The invoice itself is never edited — the number has been reported and the customer may
            already have claimed against it. This raises a new document that points back at it.
          </DialogDescription>
        </DialogHeader>

        <div className="grid gap-3">
          <div className="grid gap-1.5">
            <Label htmlFor="c-amount">Value to credit</Label>
            <Input
              id="c-amount"
              inputMode="decimal"
              value={amount}
              onChange={(e) => setAmount(e.target.value)}
              placeholder={left.toFixed(2)}
            />
            <p className="text-[11.5px] text-muted-foreground">
              At most {inrExact(left)} is left to credit on this invoice. Tax is reversed at the
              same {invoice?.gstRate}% the invoice charged, not today&apos;s rate.
            </p>
          </div>

          <div className="grid gap-1.5">
            <Label>Why</Label>
            <Select value={reason} onValueChange={setReason}>
              <SelectTrigger>
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                {reasons.map((r) => (
                  <SelectItem key={r.value} value={r.value}>
                    {r.label}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>

          <div className="grid gap-1.5">
            <Label htmlFor="c-note">Narrative</Label>
            <Textarea
              id="c-note"
              value={narrative}
              onChange={(e) => setNarrative(e.target.value)}
              placeholder="Carpet area re-measured at 612 sqft against 620 sold."
              className="min-h-[68px] text-[12.5px]"
            />
          </div>
        </div>

        <DialogFooter>
          <Button variant="outline" onClick={onClose} disabled={saving}>
            Cancel
          </Button>
          <Button onClick={submit} disabled={saving || !valid} className="gap-1.5">
            {saving ? (
              <Loader2 className="size-4 animate-spin" />
            ) : (
              <RotateCcw className="size-4" />
            )}
            Raise the credit note
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

/* ------------------------------------------------------------------ *
 * Small pieces
 * ------------------------------------------------------------------ */

function Tile({
  label,
  value,
  hint,
  tone = "neutral",
}: {
  label: string;
  value: React.ReactNode;
  hint?: string;
  tone?: Tone;
}) {
  return (
    <div className="bg-card p-2.5">
      <p className="text-[11px] tracking-wide text-muted-foreground uppercase">{label}</p>
      <p className={cn("text-[17px] font-semibold tabular-nums", TONE_TEXT[tone])}>{value}</p>
      {hint ? <p className="text-[11px] text-muted-foreground">{hint}</p> : null}
    </div>
  );
}

function Empty({
  icon: Icon,
  title,
  body,
}: {
  icon: React.ComponentType<{ className?: string }>;
  title: string;
  body: string;
}) {
  return (
    <div className="grid place-items-center py-16 text-center">
      <Icon className="mb-2 size-8 text-muted-foreground/40" />
      <p className="text-[13.5px] font-medium">{title}</p>
      <p className="mt-1 max-w-md text-[12.5px] text-muted-foreground">{body}</p>
    </div>
  );
}
