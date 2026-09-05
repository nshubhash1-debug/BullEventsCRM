"use client";

import * as React from "react";
import {
  Banknote,
  CircleCheck,
  CircleDashed,
  FileSignature,
  KeyRound,
  Landmark,
  Loader2,
  TriangleAlert,
} from "lucide-react";
import { toast } from "sonner";

import { Pill, TONE_TEXT } from "@/components/crm/metrics";
import { RelatedList } from "@/components/post-sales/record";
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
import {
  AGREEMENT_LABELS,
  AGREEMENT_LADDER,
  lifecycleApi,
  type Agreement,
  type HomeLoan,
  type Possession,
} from "@/lib/lifecycle-api";
import { inr, shortDate } from "@/lib/post-sales-api";
import { cn } from "@/lib/utils";

/* ------------------------------------------------------------------ *
 * Agreement and registration
 * ------------------------------------------------------------------ */

/**
 * The paperwork ladder.
 *
 * Drawn as a ladder rather than a form because that is what it is: each rung
 * only becomes available once the one below it is recorded, and the server
 * refuses anything else. Showing the whole ladder with the reached rungs filled
 * in answers "where is this agreement" without anybody reading five dates.
 */
export function AgreementPanel({
  bookingId,
  agreement,
  onChanged,
}: {
  bookingId: number;
  agreement: Agreement | null;
  onChanged: () => void;
}) {
  const [busy, setBusy] = React.useState(false);
  const [asking, setAsking] = React.useState<string | null>(null);
  const [stampDuty, setStampDuty] = React.useState("");
  const [registrationNumber, setRegistrationNumber] = React.useState("");
  const [office, setOffice] = React.useState("");
  const [registrationFee, setRegistrationFee] = React.useState("");

  const status = agreement?.status ?? "NotStarted";
  const reached = AGREEMENT_LADDER.indexOf(status as (typeof AGREEMENT_LADDER)[number]);
  const next = agreement?.nextStatus ?? (status === "NotStarted" ? "Drafted" : null);

  const dates: Record<string, string | null | undefined> = {
    Drafted: agreement?.draftSharedOn,
    WithCustomer: agreement?.allotmentLetterOn,
    Franked: agreement?.frankedOn,
    Executed: agreement?.executedOn,
    Registered: agreement?.registeredOn,
  };

  async function advance(toStatus: string, extra: Record<string, unknown> = {}) {
    setBusy(true);
    try {
      await lifecycleApi.advanceAgreement(bookingId, { toStatus, ...extra });
      toast.success(`Agreement ${AGREEMENT_LABELS[toStatus].toLowerCase()}`);
      setAsking(null);
      onChanged();
    } catch (error) {
      toast.error("Could not advance the agreement", {
        description: error instanceof ApiError ? error.message : "Network error.",
      });
    } finally {
      setBusy(false);
    }
  }

  function step(toStatus: string) {
    // Franking and registration carry figures the record needs, so those two
    // ask. The rest are a date and a click.
    if (toStatus === "Franked" || toStatus === "Registered") {
      setStampDuty("");
      setRegistrationNumber("");
      setOffice("");
      setRegistrationFee("");
      setAsking(toStatus);
      return;
    }

    advance(toStatus);
  }

  return (
    <RelatedList
      icon={FileSignature}
      title="Agreement & registration"
      hint={agreement?.statusLabel ?? "Not started"}
      actions={
        next ? (
          <Button size="sm" className="h-7 gap-1 text-[12px]" disabled={busy} onClick={() => step(next)}>
            {busy ? <Loader2 className="size-3.5 animate-spin" /> : null}
            Record {AGREEMENT_LABELS[next].toLowerCase()}
          </Button>
        ) : (
          <Pill tone="success">Registered</Pill>
        )
      }
    >
      <div className="px-4 py-3">
        <ol className="flex flex-col gap-0">
          {AGREEMENT_LADDER.slice(1).map((rung, index) => {
            const done = reached > index;
            const current = reached === index + 1;

            return (
              <li key={rung} className="flex gap-3">
                <div className="flex flex-col items-center">
                  {done ? (
                    <CircleCheck className={cn("size-4 shrink-0", TONE_TEXT.success)} />
                  ) : (
                    <CircleDashed className="size-4 shrink-0 text-muted-foreground/40" />
                  )}
                  {index < AGREEMENT_LADDER.length - 2 ? (
                    <span
                      className={cn(
                        "w-px flex-1",
                        done ? "bg-emerald-500/40" : "bg-border"
                      )}
                    />
                  ) : null}
                </div>

                <div className={cn("pb-3", !done && "opacity-55")}>
                  <p className="text-[13px] font-medium">
                    {AGREEMENT_LABELS[rung]}
                    {current ? <Pill tone="primary" className="ml-1.5">now</Pill> : null}
                  </p>
                  <p className="text-[11.5px] text-muted-foreground">
                    {dates[rung] ? shortDate(dates[rung]!) : "—"}
                    {rung === "Franked" && agreement?.stampDuty
                      ? ` · stamp duty ${inr(agreement.stampDuty)}`
                      : ""}
                    {rung === "Registered" && agreement?.registrationNumber
                      ? ` · ${agreement.registrationNumber}${
                          agreement.subRegistrarOffice ? ` · ${agreement.subRegistrarOffice}` : ""
                        }`
                      : ""}
                  </p>
                </div>
              </li>
            );
          })}
        </ol>

        {agreement ? (
          <div className="mt-1 grid gap-px border-t bg-border sm:grid-cols-3">
            {[
              { label: "Consideration", value: inr(agreement.considerationValue) },
              { label: "Stamp duty", value: inr(agreement.stampDuty) },
              { label: "Registration fee", value: inr(agreement.registrationFee) },
            ].map((cell) => (
              <div key={cell.label} className="bg-card px-3 py-2">
                <p className="text-[10.5px] tracking-wide text-muted-foreground uppercase">
                  {cell.label}
                </p>
                <p className="text-[13px] font-semibold tabular-nums">{cell.value}</p>
              </div>
            ))}
          </div>
        ) : null}
      </div>

      <Dialog open={asking !== null} onOpenChange={(open) => (open ? null : setAsking(null))}>
        <DialogContent className="sm:max-w-md">
          <DialogHeader>
            <DialogTitle>
              {asking === "Franked" ? "Record franking" : "Record registration"}
            </DialogTitle>
            <DialogDescription>
              {asking === "Franked"
                ? "The stamp duty actually paid, which the record has to carry."
                : "The number the sub-registrar issued. Without it this is not a registration."}
            </DialogDescription>
          </DialogHeader>

          {asking === "Franked" ? (
            <div className="grid gap-1.5">
              <Label htmlFor="duty">Stamp duty paid</Label>
              <Input
                id="duty"
                inputMode="decimal"
                value={stampDuty}
                onChange={(e) => setStampDuty(e.target.value)}
                className="tabular-nums"
              />
            </div>
          ) : (
            <div className="grid gap-3">
              <div className="grid gap-1.5">
                <Label htmlFor="regno">Registration number</Label>
                <Input
                  id="regno"
                  value={registrationNumber}
                  onChange={(e) => setRegistrationNumber(e.target.value)}
                  placeholder="VNS-1-2026-4471"
                />
              </div>
              <div className="grid gap-3 sm:grid-cols-2">
                <div className="grid gap-1.5">
                  <Label htmlFor="sro">Sub-registrar office</Label>
                  <Input id="sro" value={office} onChange={(e) => setOffice(e.target.value)} />
                </div>
                <div className="grid gap-1.5">
                  <Label htmlFor="regfee">Registration fee</Label>
                  <Input
                    id="regfee"
                    inputMode="decimal"
                    value={registrationFee}
                    onChange={(e) => setRegistrationFee(e.target.value)}
                    className="tabular-nums"
                  />
                </div>
              </div>
            </div>
          )}

          <DialogFooter>
            <Button variant="outline" onClick={() => setAsking(null)} disabled={busy}>
              Cancel
            </Button>
            <Button
              disabled={busy}
              onClick={() =>
                advance(
                  asking!,
                  asking === "Franked"
                    ? { stampDuty: Number(stampDuty) || 0 }
                    : {
                        registrationNumber,
                        subRegistrarOffice: office || null,
                        registrationFee: Number(registrationFee) || 0,
                      }
                )
              }
            >
              {busy ? <Loader2 className="size-4 animate-spin" /> : null}
              Record it
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </RelatedList>
  );
}

/* ------------------------------------------------------------------ *
 * Home loan
 * ------------------------------------------------------------------ */

/**
 * The bank paying part of the price.
 *
 * Worth its own panel because once a loan is sanctioned and the tripartite
 * signed, the collection risk moves: the remaining instalments arrive from a
 * bank on construction progress rather than from a person on a due date. A
 * collections desk that cannot see which bookings are bank-funded chases the
 * wrong customers.
 */
export function LoanPanel({
  bookingId,
  loan,
  onChanged,
}: {
  bookingId: number;
  loan: HomeLoan | null;
  onChanged: () => void;
}) {
  const [busy, setBusy] = React.useState(false);
  const [editing, setEditing] = React.useState(false);
  const [sanctioning, setSanctioning] = React.useState(false);
  const [disbursing, setDisbursing] = React.useState(false);

  async function run(work: () => Promise<unknown>, done: string) {
    setBusy(true);
    try {
      await work();
      toast.success(done);
      setEditing(false);
      setSanctioning(false);
      setDisbursing(false);
      onChanged();
    } catch (error) {
      toast.error("Could not update the loan", {
        description: error instanceof ApiError ? error.message : "Network error.",
      });
    } finally {
      setBusy(false);
    }
  }

  return (
    <RelatedList
      icon={Landmark}
      title="Home loan"
      hint={loan ? `${loan.bankName} · ${loan.statusLabel}` : "Self funded"}
      actions={
        <>
          {loan?.sanctionLapsed ? <Pill tone="danger">sanction lapsed</Pill> : null}
          <Button
            size="sm"
            variant="ghost"
            className="h-7 px-2 text-[11px]"
            onClick={() => setEditing(true)}
          >
            {loan ? "Edit" : "Record a loan"}
          </Button>
          {loan && !loan.sanctionedOn ? (
            <Button
              size="sm"
              variant="outline"
              className="h-7 px-2 text-[11px]"
              onClick={() => setSanctioning(true)}
            >
              Sanction
            </Button>
          ) : null}
          {loan?.sanctionedOn && !loan.tripartiteSignedOn ? (
            <Button
              size="sm"
              variant="outline"
              className="h-7 px-2 text-[11px]"
              disabled={busy}
              onClick={() => run(() => lifecycleApi.tripartite(bookingId, null), "Tripartite recorded")}
            >
              Tripartite signed
            </Button>
          ) : null}
          {loan?.tripartiteSignedOn && loan.undisbursedAmount > 0 ? (
            <Button
              size="sm"
              className="h-7 gap-1 px-2 text-[11px]"
              onClick={() => setDisbursing(true)}
            >
              <Banknote className="size-3" />
              Disbursement
            </Button>
          ) : null}
        </>
      }
    >
      {!loan ? (
        <p className="px-4 py-6 text-center text-[12.5px] text-muted-foreground">
          This booking is self funded. Record a loan when the customer applies for one.
        </p>
      ) : (
        <div className="grid gap-px bg-border sm:grid-cols-4">
          {[
            { label: "Sanctioned", value: inr(loan.sanctionedAmount), hint: shortDate(loan.sanctionedOn) },
            { label: "Disbursed", value: inr(loan.disbursedAmount), tone: "success" as const },
            { label: "Still to come", value: inr(loan.undisbursedAmount), tone: "info" as const },
            {
              label: "Tripartite",
              value: loan.tripartiteSignedOn ? shortDate(loan.tripartiteSignedOn) : "Not signed",
              tone: loan.tripartiteSignedOn ? undefined : ("warning" as const),
            },
          ].map((cell) => (
            <div key={cell.label} className="bg-card px-4 py-2.5">
              <p className="text-[10.5px] tracking-wide text-muted-foreground uppercase">
                {cell.label}
              </p>
              <p
                className={cn(
                  "text-[14px] font-semibold tabular-nums",
                  cell.tone ? TONE_TEXT[cell.tone] : undefined
                )}
              >
                {cell.value}
              </p>
              {cell.hint ? (
                <p className="text-[11px] text-muted-foreground">{cell.hint}</p>
              ) : null}
            </div>
          ))}
        </div>
      )}

      <LoanDialog
        bookingId={bookingId}
        loan={loan}
        open={editing}
        onOpenChange={setEditing}
        onSaved={onChanged}
      />

      <AmountDialog
        open={sanctioning}
        title="Record the sanction"
        description="From here the remaining instalments arrive from the bank on construction progress."
        label="Sanctioned amount"
        extraLabel="Valid until"
        extraType="date"
        busy={busy}
        onOpenChange={setSanctioning}
        onSubmit={(amount, extra) =>
          run(
            () =>
              lifecycleApi.sanction(bookingId, {
                sanctionedAmount: amount,
                validUntil: extra || null,
              }),
            "Sanction recorded"
          )
        }
      />

      <AmountDialog
        open={disbursing}
        title="Record a disbursement"
        description="This lands in the ledger as a receipt at the same moment, so the account cannot show a bank that has paid and a customer who still owes."
        label="Amount released"
        extraLabel="Bank reference"
        busy={busy}
        onOpenChange={setDisbursing}
        onSubmit={(amount, extra) =>
          run(
            () => lifecycleApi.disburse(bookingId, { amount, reference: extra || null }),
            "Disbursement recorded"
          )
        }
      />
    </RelatedList>
  );
}

function LoanDialog({
  bookingId,
  loan,
  open,
  onOpenChange,
  onSaved,
}: {
  bookingId: number;
  loan: HomeLoan | null;
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onSaved: () => void;
}) {
  const [bank, setBank] = React.useState("");
  const [branch, setBranch] = React.useState("");
  const [application, setApplication] = React.useState("");
  const [requested, setRequested] = React.useState("");
  const [saving, setSaving] = React.useState(false);

  const [wasOpen, setWasOpen] = React.useState(open);

  if (open !== wasOpen) {
    setWasOpen(open);

    if (open) {
      setBank(loan?.bankName ?? "");
      setBranch(loan?.branchName ?? "");
      setApplication(loan?.applicationNumber ?? "");
      setRequested(loan?.requestedAmount?.toString() ?? "");
    }
  }

  async function save() {
    setSaving(true);
    try {
      await lifecycleApi.saveLoan(bookingId, {
        bankName: bank,
        branchName: branch || null,
        applicationNumber: application || null,
        requestedAmount: Number(requested) || 0,
      });

      toast.success("Loan recorded");
      onOpenChange(false);
      onSaved();
    } catch (error) {
      toast.error("Could not save the loan", {
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
          <DialogTitle>{loan ? "Edit home loan" : "Record a home loan"}</DialogTitle>
          <DialogDescription>
            Which bank is funding this, and how much the customer asked for.
          </DialogDescription>
        </DialogHeader>

        <div className="grid gap-3">
          <div className="grid gap-3 sm:grid-cols-2">
            <div className="grid gap-1.5">
              <Label htmlFor="bank">Bank</Label>
              <Input id="bank" value={bank} onChange={(e) => setBank(e.target.value)} />
            </div>
            <div className="grid gap-1.5">
              <Label htmlFor="branch">Branch</Label>
              <Input id="branch" value={branch} onChange={(e) => setBranch(e.target.value)} />
            </div>
          </div>

          <div className="grid gap-3 sm:grid-cols-2">
            <div className="grid gap-1.5">
              <Label htmlFor="appno">Application number</Label>
              <Input
                id="appno"
                value={application}
                onChange={(e) => setApplication(e.target.value)}
              />
            </div>
            <div className="grid gap-1.5">
              <Label htmlFor="requested">Amount requested</Label>
              <Input
                id="requested"
                inputMode="decimal"
                value={requested}
                onChange={(e) => setRequested(e.target.value)}
                className="tabular-nums"
              />
            </div>
          </div>
        </div>

        <DialogFooter>
          <Button onClick={save} disabled={saving} className="gap-1.5">
            {saving ? <Loader2 className="size-4 animate-spin" /> : <Landmark className="size-4" />}
            Save
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

/** A small dialog for the "one number, maybe one more field" cases. */
function AmountDialog({
  open,
  title,
  description,
  label,
  extraLabel,
  extraType = "text",
  busy,
  onOpenChange,
  onSubmit,
}: {
  open: boolean;
  title: string;
  description: string;
  label: string;
  extraLabel?: string;
  extraType?: string;
  busy: boolean;
  onOpenChange: (open: boolean) => void;
  onSubmit: (amount: number, extra: string) => void;
}) {
  const [amount, setAmount] = React.useState("");
  const [extra, setExtra] = React.useState("");

  const [wasOpen, setWasOpen] = React.useState(open);

  if (open !== wasOpen) {
    setWasOpen(open);
    if (open) {
      setAmount("");
      setExtra("");
    }
  }

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-sm">
        <DialogHeader>
          <DialogTitle>{title}</DialogTitle>
          <DialogDescription>{description}</DialogDescription>
        </DialogHeader>

        <div className="grid gap-3">
          <div className="grid gap-1.5">
            <Label htmlFor="amt">{label}</Label>
            <Input
              id="amt"
              inputMode="decimal"
              value={amount}
              onChange={(e) => setAmount(e.target.value)}
              className="tabular-nums"
            />
          </div>

          {extraLabel ? (
            <div className="grid gap-1.5">
              <Label htmlFor="extra">{extraLabel}</Label>
              <Input
                id="extra"
                type={extraType}
                value={extra}
                onChange={(e) => setExtra(e.target.value)}
              />
            </div>
          ) : null}
        </div>

        <DialogFooter>
          <Button disabled={busy} onClick={() => onSubmit(Number(amount) || 0, extra)}>
            {busy ? <Loader2 className="size-4 animate-spin" /> : null}
            Record it
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

/* ------------------------------------------------------------------ *
 * Possession and handover
 * ------------------------------------------------------------------ */

/**
 * Getting to the keys.
 *
 * The blockers are the panel. Handover is the point after which a developer's
 * leverage is gone — a unit handed over with dues outstanding is money that
 * gets collected by asking nicely, if at all — so the screen leads with exactly
 * what is still in the way rather than with a button that fails.
 */
export function PossessionPanel({
  bookingId,
  possession,
  onChanged,
}: {
  bookingId: number;
  possession: Possession | null;
  onChanged: () => void;
}) {
  const [busy, setBusy] = React.useState(false);
  const [offering, setOffering] = React.useState(false);
  const [inspecting, setInspecting] = React.useState(false);

  async function run(work: () => Promise<unknown>, done: string) {
    setBusy(true);
    try {
      await work();
      toast.success(done);
      setOffering(false);
      setInspecting(false);
      onChanged();
    } catch (error) {
      toast.error("Could not update possession", {
        description: error instanceof ApiError ? error.message : "Network error.",
      });
    } finally {
      setBusy(false);
    }
  }

  const handedOver = possession?.status === "HandedOver";

  return (
    <RelatedList
      icon={KeyRound}
      title="Possession & handover"
      hint={possession?.statusLabel ?? "Not due"}
      actions={
        handedOver ? (
          <Pill tone="success">Handed over {shortDate(possession!.handedOverOn)}</Pill>
        ) : (
          <>
            {!possession?.offeredOn ? (
              <Button size="sm" className="h-7 px-2 text-[11px]" onClick={() => setOffering(true)}>
                Offer possession
              </Button>
            ) : null}
            {possession?.offeredOn && !possession.inspectedOn ? (
              <Button
                size="sm"
                variant="outline"
                className="h-7 px-2 text-[11px]"
                onClick={() => setInspecting(true)}
              >
                Record inspection
              </Button>
            ) : null}
            {possession && possession.snagsOpen > 0 ? (
              <Button
                size="sm"
                variant="outline"
                className="h-7 px-2 text-[11px]"
                disabled={busy}
                onClick={() => run(() => lifecycleApi.closeSnags(bookingId, 1), "Snag closed")}
              >
                Close a snag
              </Button>
            ) : null}
            {possession?.readyToHandOver ? (
              <Button
                size="sm"
                className="h-7 gap-1 px-2 text-[11px]"
                disabled={busy}
                onClick={() =>
                  run(
                    () => lifecycleApi.handOver(bookingId, { documentsHandedOver: true }),
                    "Handed over"
                  )
                }
              >
                <KeyRound className="size-3" />
                Hand over
              </Button>
            ) : null}
          </>
        )
      }
    >
      {!possession?.offeredOn ? (
        <p className="px-4 py-6 text-center text-[12.5px] text-muted-foreground">
          Possession has not been offered yet.
        </p>
      ) : (
        <>
          <div className="grid gap-px bg-border sm:grid-cols-4">
            {[
              { label: "Offered", value: shortDate(possession.offeredOn) },
              {
                label: "Snags",
                value: `${possession.snagsClosed} / ${possession.snagsRaised}`,
                tone: possession.snagsOpen > 0 ? ("warning" as const) : ("success" as const),
              },
              {
                label: "Maintenance",
                value: inr(possession.maintenanceAmount),
                tone: possession.maintenanceCollected
                  ? ("success" as const)
                  : ("warning" as const),
                hint: possession.maintenanceCollected ? "collected" : "not collected",
              },
              {
                label: "Corpus",
                value: inr(possession.corpusDeposit),
                tone: possession.corpusCollected ? ("success" as const) : ("warning" as const),
                hint: possession.corpusCollected ? "collected" : "not collected",
              },
            ].map((cell) => (
              <div key={cell.label} className="bg-card px-4 py-2.5">
                <p className="text-[10.5px] tracking-wide text-muted-foreground uppercase">
                  {cell.label}
                </p>
                <p
                  className={cn(
                    "text-[14px] font-semibold tabular-nums",
                    cell.tone ? TONE_TEXT[cell.tone] : undefined
                  )}
                >
                  {cell.value}
                </p>
                {cell.hint ? (
                  <p className="text-[11px] text-muted-foreground">{cell.hint}</p>
                ) : null}
              </div>
            ))}
          </div>

          {!handedOver ? (
            <div className="border-t px-4 py-3">
              {possession.blockers.length === 0 ? (
                <p className={cn("flex items-center gap-2 text-[12.5px]", TONE_TEXT.success)}>
                  <CircleCheck className="size-4" />
                  Everything is clear. The keys can be handed over.
                </p>
              ) : (
                <>
                  <p className="mb-1.5 flex items-center gap-2 text-[12px] font-medium">
                    <TriangleAlert className={cn("size-3.5", TONE_TEXT.warning)} />
                    Still in the way
                  </p>
                  <ul className="space-y-0.5">
                    {possession.blockers.map((blocker) => (
                      <li key={blocker} className="text-[12.5px] text-muted-foreground">
                        · {blocker}
                      </li>
                    ))}
                  </ul>

                  <div className="mt-2.5 flex flex-wrap gap-1.5">
                    {!possession.maintenanceCollected && possession.maintenanceAmount > 0 ? (
                      <Button
                        size="sm"
                        variant="outline"
                        className="h-7 px-2 text-[11px]"
                        disabled={busy}
                        onClick={() =>
                          run(
                            () =>
                              lifecycleApi.collect(bookingId, { maintenance: true, corpus: false }),
                            "Maintenance advance collected"
                          )
                        }
                      >
                        Maintenance collected
                      </Button>
                    ) : null}
                    {!possession.corpusCollected && possession.corpusDeposit > 0 ? (
                      <Button
                        size="sm"
                        variant="outline"
                        className="h-7 px-2 text-[11px]"
                        disabled={busy}
                        onClick={() =>
                          run(
                            () =>
                              lifecycleApi.collect(bookingId, { maintenance: false, corpus: true }),
                            "Corpus deposit collected"
                          )
                        }
                      >
                        Corpus collected
                      </Button>
                    ) : null}
                    {possession.snagsOpen > 0 ? (
                      <Button
                        size="sm"
                        variant="outline"
                        className="h-7 px-2 text-[11px]"
                        disabled={busy}
                        onClick={() =>
                          run(
                            () => lifecycleApi.closeSnags(bookingId, possession.snagsOpen),
                            "All snags closed"
                          )
                        }
                      >
                        Close all {possession.snagsOpen} snags
                      </Button>
                    ) : null}
                  </div>
                </>
              )}
            </div>
          ) : null}
        </>
      )}

      <AmountDialog
        open={offering}
        title="Offer possession"
        description="Maintenance charges become payable from this date, whether or not the keys are taken."
        label="Maintenance advance"
        extraLabel="Corpus deposit"
        busy={busy}
        onOpenChange={setOffering}
        onSubmit={(amount, extra) =>
          run(
            () =>
              lifecycleApi.offerPossession(bookingId, {
                maintenanceAmount: amount,
                maintenanceMonths: 12,
                corpusDeposit: Number(extra) || 0,
              }),
            "Possession offered"
          )
        }
      />

      <AmountDialog
        open={inspecting}
        title="Record the joint inspection"
        description="How many defects the customer raised when they walked the unit."
        label="Snags raised"
        busy={busy}
        onOpenChange={setInspecting}
        onSubmit={(amount) =>
          run(
            () => lifecycleApi.inspect(bookingId, { snagsRaised: Math.round(amount) }),
            "Inspection recorded"
          )
        }
      />
    </RelatedList>
  );
}
