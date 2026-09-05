"use client";

import * as React from "react";
import { BadgeCheck, FileCheck2, Loader2, RefreshCw, ShieldCheck } from "lucide-react";
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
import { ApiError } from "@/lib/api";
import { billingApi, TDS_TONE, type BillingSummary, type TdsRow } from "@/lib/billing-api";
import { inrExact, shortDate } from "@/lib/post-sales-api";
import { cn } from "@/lib/utils";

/**
 * The Form 16Bs buyers owe back.
 *
 * Under section 194-IA the buyer deducts 1% and pays it to the government, and
 * the developer credits them with the gross on trust. That trust is only
 * settled when the certificate arrives: until then the developer has given
 * credit for money it cannot prove was ever deposited, and cannot claim it
 * against its own liability.
 *
 * Most developers cannot produce this list at all, and find out the number at
 * assessment. So the awaited ones sort first and carry their age.
 */
export default function TdsPage() {
  const [rows, setRows] = React.useState<TdsRow[] | null>(null);
  const [summary, setSummary] = React.useState<BillingSummary | null>(null);
  const [status, setStatus] = React.useState("all");
  const [syncing, setSyncing] = React.useState(false);
  const [busy, setBusy] = React.useState<number | null>(null);
  const [recording, setRecording] = React.useState<TdsRow | null>(null);

  const load = React.useCallback(() => {
    billingApi
      .tds(status === "all" ? {} : { status })
      .then(setRows)
      .catch((error: unknown) => {
        toast.error("Could not load the TDS register", {
          description: error instanceof ApiError ? error.message : "Network error.",
        });
        setRows([]);
      });

    billingApi.summary().then(setSummary).catch(() => setSummary(null));
  }, [status]);

  React.useEffect(load, [load]);

  /**
   * Opens a tracking row for every receipt with TDS on it.
   *
   * A run rather than a requirement, because the deduction is entered on the
   * receipt at the counter and nobody is going to remember to also open a row
   * here. Safe to run twice — one row per receipt, enforced.
   */
  async function sync() {
    setSyncing(true);
    try {
      const { opened } = await billingApi.syncTds();

      toast.success(
        opened === 0
          ? "Every deduction is already tracked"
          : `${opened} deduction${opened === 1 ? "" : "s"} picked up`,
        {
          description:
            opened === 0
              ? "Nothing new since the last run."
              : "These are the certificates the buyers now owe you.",
        }
      );

      load();
    } catch (error) {
      toast.error("The sync could not run", {
        description: error instanceof ApiError ? error.message : "Network error.",
      });
    } finally {
      setSyncing(false);
    }
  }

  if (!rows) {
    return (
      <PagePanel icon={ShieldCheck} title="TDS certificates">
        <CrmLoadingState label="Loading the TDS register" />
      </PagePanel>
    );
  }

  return (
    <PagePanel
      icon={ShieldCheck}
      title="TDS certificates"
      hint="Section 194-IA: the buyer withholds 1% and you credit them the gross. Until the Form 16B arrives you have given credit for money you cannot prove was deposited."
      actions={
        <div className="flex items-center gap-2">
          <Select value={status} onValueChange={setStatus}>
            <SelectTrigger className="h-8 w-[150px] text-[12.5px]">
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="all">Every deduction</SelectItem>
              {["Awaited", "Received", "Verified", "Mismatched"].map((value) => (
                <SelectItem key={value} value={value}>
                  {value}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>

          <Button
            size="sm"
            variant="outline"
            className="h-8 gap-1.5 text-[12.5px]"
            disabled={syncing}
            onClick={sync}
          >
            {syncing ? (
              <Loader2 className="size-3.5 animate-spin" />
            ) : (
              <RefreshCw className="size-3.5" />
            )}
            Pick up new deductions
          </Button>
        </div>
      }
    >
      <div className="mx-5 mb-3 grid gap-px overflow-hidden rounded-lg border bg-border sm:grid-cols-2 lg:grid-cols-4">
        <Tile
          label="Certificates awaited"
          value={summary?.tdsAwaited ?? 0}
          hint={inrExact(summary?.tdsAwaitedValue ?? 0)}
          tone={(summary?.tdsAwaited ?? 0) > 0 ? "warning" : "success"}
        />
        <Tile
          label="Short-deposited"
          value={summary?.tdsMismatched ?? 0}
          hint={inrExact(summary?.tdsMismatchedValue ?? 0)}
          tone={(summary?.tdsMismatched ?? 0) > 0 ? "danger" : "neutral"}
        />
        <Tile
          label="Oldest awaited"
          value={
            rows.some((r) => r.status === "Awaited")
              ? `${Math.max(...rows.filter((r) => r.status === "Awaited").map((r) => r.awaitingDays))}d`
              : "—"
          }
          hint="Since the deduction"
        />
        <Tile
          label="Verified"
          value={rows.filter((r) => r.status === "Verified").length}
          hint="Agreed against Form 26AS"
          tone="success"
        />
      </div>

      <div className="min-h-0 flex-1 overflow-auto px-5 pb-4">
        {rows.length === 0 ? (
          <div className="grid place-items-center py-16 text-center">
            <ShieldCheck className="mb-2 size-8 text-muted-foreground/40" />
            <p className="text-[13.5px] font-medium">No deduction is being tracked</p>
            <p className="mt-1 max-w-md text-[12.5px] text-muted-foreground">
              Record a receipt with TDS on it, then run &ldquo;Pick up new deductions&rdquo; and it
              will appear here as a certificate you are owed.
            </p>
          </div>
        ) : (
          <table className="w-full min-w-[860px] text-[12.5px]">
            <thead className="sticky top-0 bg-card">
              <tr className="border-b text-left text-[11px] tracking-wide text-muted-foreground uppercase">
                <th className="py-2 pr-3 font-medium">Deductor</th>
                <th className="py-2 pr-3 font-medium">Against</th>
                <th className="py-2 pr-3 text-right font-medium">Gross credited</th>
                <th className="py-2 pr-3 text-right font-medium">Withheld</th>
                <th className="py-2 pr-3 font-medium">Quarter</th>
                <th className="py-2 pr-3 font-medium">Certificate</th>
                <th className="py-2 font-medium" />
              </tr>
            </thead>
            <tbody className="tabular-nums">
              {rows.map((row) => (
                <tr key={row.id} className="border-b align-top last:border-0 hover:bg-muted/40">
                  <td className="py-2.5 pr-3">
                    <Link
                      href={`/dashboard/post-sales/bookings/${row.bookingId}`}
                      className="font-medium text-primary hover:underline"
                    >
                      {row.deductorName ?? "—"}
                    </Link>
                    <p className="text-[11px] text-muted-foreground">
                      PAN {row.deductorPan ?? "not on file"}
                    </p>
                  </td>

                  <td className="py-2.5 pr-3">
                    <p>{row.receiptNumber}</p>
                    <p className="text-[11px] text-muted-foreground">
                      {shortDate(row.receivedOn)}
                    </p>
                  </td>

                  <td className="py-2.5 pr-3 text-right">{inrExact(row.amountPaid)}</td>

                  <td className="py-2.5 pr-3 text-right font-medium">{inrExact(row.tdsAmount)}</td>

                  <td className="py-2.5 pr-3">{row.quarter ?? "—"}</td>

                  <td className="max-w-[280px] py-2.5 pr-3">
                    <Pill tone={TDS_TONE[row.status] ?? "neutral"}>{row.status}</Pill>

                    {row.certificateNumber ? (
                      <p className="mt-0.5 text-[11px] text-muted-foreground">
                        {row.certificateNumber}
                        {row.certificateDate ? ` · ${shortDate(row.certificateDate)}` : ""}
                      </p>
                    ) : (
                      <p className={cn("mt-0.5 text-[11px]", TONE_TEXT.warning)}>
                        awaited {row.awaitingDays}d
                      </p>
                    )}

                    {row.notes ? (
                      <p className={cn("mt-0.5 text-[11px]", TONE_TEXT.danger)}>{row.notes}</p>
                    ) : null}
                  </td>

                  <td className="py-2.5 text-right whitespace-nowrap">
                    {busy === row.id ? (
                      <Loader2 className="ml-auto size-3.5 animate-spin text-muted-foreground" />
                    ) : row.status === "Verified" ? (
                      <BadgeCheck className={cn("ml-auto size-4", TONE_TEXT.success)} />
                    ) : (
                      <div className="flex justify-end gap-1">
                        <Button
                          size="sm"
                          variant={row.status === "Awaited" ? "default" : "outline"}
                          className="h-7 gap-1 px-2 text-[11px]"
                          onClick={() => setRecording(row)}
                        >
                          <FileCheck2 className="size-3" />
                          {row.status === "Awaited" ? "Record 16B" : "Re-enter"}
                        </Button>

                        {row.status !== "Awaited" ? (
                          <Button
                            size="sm"
                            variant="ghost"
                            className="h-7 px-2 text-[11px]"
                            onClick={async () => {
                              setBusy(row.id);
                              try {
                                await billingApi.verifyCertificate(row.id);
                                toast.success("Verified against Form 26AS");
                                load();
                              } catch (error) {
                                toast.error("That could not be verified", {
                                  description:
                                    error instanceof ApiError ? error.message : "Network error.",
                                });
                              } finally {
                                setBusy(null);
                              }
                            }}
                          >
                            Verify
                          </Button>
                        ) : null}
                      </div>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>

      <CertificateDialog
        row={recording}
        onClose={() => setRecording(null)}
        onDone={load}
      />
    </PagePanel>
  );
}

/* ------------------------------------------------------------------ *
 * Record a certificate
 * ------------------------------------------------------------------ */

function CertificateDialog({
  row,
  onClose,
  onDone,
}: {
  row: TdsRow | null;
  onClose: () => void;
  onDone: () => void;
}) {
  const [number, setNumber] = React.useState("");
  const [date, setDate] = React.useState("");
  const [challan, setChallan] = React.useState("");
  const [certified, setCertified] = React.useState("");
  const [saving, setSaving] = React.useState(false);
  const [seen, setSeen] = React.useState<TdsRow | null>(null);

  if (row !== seen) {
    setSeen(row);

    if (row) {
      setNumber(row.certificateNumber ?? "");
      setDate(row.certificateDate?.slice(0, 10) ?? "");
      setChallan(row.challanNumber ?? "");
      setCertified(row.tdsAmount.toString());
    }
  }

  const valid = number.trim() !== "" && date !== "";

  // Flagged before submitting, not after: an operator typing a figure that
  // disagrees with what was withheld should see that while they can still
  // check the paper in front of them.
  const short = row && Number(certified) > 0 ? row.tdsAmount - Number(certified) : 0;

  async function submit() {
    if (!row || !valid) return;

    setSaving(true);
    try {
      const updated = await billingApi.recordCertificate(row.id, {
        certificateNumber: number.trim(),
        certificateDate: date,
        challanNumber: challan.trim() || null,
        certifiedAmount: certified ? Number(certified) : null,
      });

      toast.success(
        updated.status === "Mismatched" ? "Recorded — and it is short" : "Certificate recorded",
        { description: updated.notes ?? "Verify it against Form 26AS when you reconcile." }
      );

      onClose();
      onDone();
    } catch (error) {
      toast.error("That could not be recorded", {
        description: error instanceof ApiError ? error.message : "Network error.",
      });
    } finally {
      setSaving(false);
    }
  }

  return (
    <Dialog open={row !== null} onOpenChange={(open) => (open ? null : onClose())}>
      <DialogContent className="sm:max-w-lg">
        <DialogHeader>
          <DialogTitle>Form 16B from {row?.deductorName ?? "the buyer"}</DialogTitle>
          <DialogDescription>
            {row ? inrExact(row.tdsAmount) : ""} was withheld against {row?.receiptNumber}. Record
            what the buyer actually produced.
          </DialogDescription>
        </DialogHeader>

        <div className="grid gap-3">
          <div className="grid grid-cols-2 gap-3">
            <div className="grid gap-1.5">
              <Label htmlFor="t-num">Certificate number</Label>
              <Input id="t-num" value={number} onChange={(e) => setNumber(e.target.value)} />
            </div>
            <div className="grid gap-1.5">
              <Label htmlFor="t-date">Certificate date</Label>
              <Input
                id="t-date"
                type="date"
                value={date}
                onChange={(e) => setDate(e.target.value)}
              />
            </div>
          </div>

          <div className="grid grid-cols-2 gap-3">
            <div className="grid gap-1.5">
              <Label htmlFor="t-challan">Form 26QB acknowledgement</Label>
              <Input
                id="t-challan"
                value={challan}
                onChange={(e) => setChallan(e.target.value)}
                placeholder="26QB-778812"
              />
            </div>
            <div className="grid gap-1.5">
              <Label htmlFor="t-amt">Amount on the certificate</Label>
              <Input
                id="t-amt"
                inputMode="decimal"
                value={certified}
                onChange={(e) => setCertified(e.target.value)}
              />
            </div>
          </div>

          {short > 1 ? (
            <p
              className={cn(
                "rounded-lg border border-red-500/40 bg-red-500/10 px-3 py-2 text-[12px]",
                TONE_TEXT.danger
              )}
            >
              This is short by {inrExact(short)}. The buyer withheld more than they deposited —
              that difference is yours to chase, and it will be flagged as a mismatch.
            </p>
          ) : null}
        </div>

        <DialogFooter>
          <Button variant="outline" onClick={onClose} disabled={saving}>
            Cancel
          </Button>
          <Button onClick={submit} disabled={saving || !valid} className="gap-1.5">
            {saving ? (
              <Loader2 className="size-4 animate-spin" />
            ) : (
              <FileCheck2 className="size-4" />
            )}
            Record it
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

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
