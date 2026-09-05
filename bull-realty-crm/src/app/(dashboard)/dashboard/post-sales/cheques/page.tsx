"use client";

import * as React from "react";
import {
  Banknote,
  Landmark,
  Loader2,
  Plus,
  TriangleAlert,
  Undo2,
  Wallet,
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
import { ApiError } from "@/lib/api";
import {
  billingApi,
  CHEQUE_HINT,
  CHEQUE_TONE,
  type BillingSummary,
  type Cheque,
} from "@/lib/billing-api";
import { inrExact, postSalesApi, shortDate, type BookingRow } from "@/lib/post-sales-api";
import { cn } from "@/lib/utils";

/**
 * The cheque drawer.
 *
 * This register exists because a post-dated cheque is money the developer has
 * been promised but cannot see: no receipt exists until it clears, so no ledger
 * built on receipts can answer "what is due for banking this week". A cheque
 * banked a week late is a week of cash flow lost, and a cheque nobody banked at
 * all is a collection everybody believed had happened.
 *
 * So the default sort is oldest bankable first, and the ones sitting past their
 * date are the loudest thing on the screen.
 */
export default function ChequesPage() {
  const [rows, setRows] = React.useState<Cheque[] | null>(null);
  const [summary, setSummary] = React.useState<BillingSummary | null>(null);
  const [status, setStatus] = React.useState("Held");
  const [busy, setBusy] = React.useState<number | null>(null);
  const [taking, setTaking] = React.useState(false);
  const [bouncing, setBouncing] = React.useState<Cheque | null>(null);

  const load = React.useCallback(() => {
    billingApi
      .cheques(status === "all" ? {} : { status })
      .then(setRows)
      .catch((error: unknown) => {
        toast.error("Could not load the register", {
          description: error instanceof ApiError ? error.message : "Network error.",
        });
        setRows([]);
      });

    billingApi.summary().then(setSummary).catch(() => setSummary(null));
  }, [status]);

  React.useEffect(load, [load]);

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

  if (!rows) {
    return (
      <PagePanel icon={Wallet} title="Cheque register">
        <CrmLoadingState label="Opening the drawer" />
      </PagePanel>
    );
  }

  const due = rows.filter((r) => r.dueForBanking);

  return (
    <PagePanel
      icon={Wallet}
      title="Cheque register"
      hint="Post-dated cheques held against bookings. No receipt exists until one clears, so this is the only place that answers what is due for banking."
      actions={
        <div className="flex items-center gap-2">
          <Select value={status} onValueChange={setStatus}>
            <SelectTrigger className="h-8 w-[185px] text-[12.5px]">
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="all">Every cheque</SelectItem>
              {["Held", "Deposited", "Cleared", "Bounced", "Returned"].map((value) => (
                <SelectItem key={value} value={value}>
                  {value} — {CHEQUE_HINT[value]}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>

          <Button size="sm" className="h-8 gap-1.5 text-[12.5px]" onClick={() => setTaking(true)}>
            <Plus className="size-3.5" />
            Take a cheque
          </Button>
        </div>
      }
    >
      <div className="mx-5 mb-3 grid gap-px overflow-hidden rounded-lg border bg-border sm:grid-cols-2 lg:grid-cols-4">
        <Tile
          label="In the drawer"
          value={summary?.chequesHeld ?? 0}
          hint={inrExact(summary?.chequesHeldValue ?? 0)}
        />
        <Tile
          label="Due for banking"
          value={summary?.chequesDueForBanking ?? 0}
          hint={inrExact(summary?.chequesDueValue ?? 0)}
          tone={(summary?.chequesDueForBanking ?? 0) > 0 ? "warning" : "success"}
        />
        <Tile
          label="Bounced"
          value={summary?.chequesBounced ?? 0}
          hint="Reversed out of the ledger"
          tone={(summary?.chequesBounced ?? 0) > 0 ? "danger" : "neutral"}
        />
        <Tile
          label="Longest unbanked"
          value={due.length ? `${Math.max(...due.map((d) => d.daysSinceBankable))}d` : "—"}
          hint="Days past the date on its face"
          tone={due.some((d) => d.daysSinceBankable > 7) ? "danger" : "neutral"}
        />
      </div>

      <div className="min-h-0 flex-1 overflow-auto px-5 pb-4">
        {rows.length === 0 ? (
          <div className="grid place-items-center py-16 text-center">
            <Wallet className="mb-2 size-8 text-muted-foreground/40" />
            <p className="text-[13.5px] font-medium">Nothing in the drawer</p>
            <p className="mt-1 max-w-md text-[12.5px] text-muted-foreground">
              Take a cheque against a booking and it will sit here until its date, then appear on
              the banking run.
            </p>
          </div>
        ) : (
          <table className="w-full min-w-[860px] text-[12.5px]">
            <thead className="sticky top-0 bg-card">
              <tr className="border-b text-left text-[11px] tracking-wide text-muted-foreground uppercase">
                <th className="py-2 pr-3 font-medium">Cheque</th>
                <th className="py-2 pr-3 font-medium">Customer</th>
                <th className="py-2 pr-3 text-right font-medium">Amount</th>
                <th className="py-2 pr-3 font-medium">Dated</th>
                <th className="py-2 pr-3 font-medium">Status</th>
                <th className="py-2 font-medium" />
              </tr>
            </thead>
            <tbody className="tabular-nums">
              {rows.map((cheque) => (
                <tr
                  key={cheque.id}
                  className={cn(
                    "border-b align-top last:border-0 hover:bg-muted/40",
                    cheque.dueForBanking && "bg-amber-500/5"
                  )}
                >
                  <td className="py-2.5 pr-3">
                    <p className="font-medium">{cheque.chequeNumber}</p>
                    <p className="text-[11px] text-muted-foreground">
                      {cheque.bankName}
                      {cheque.branchName ? ` · ${cheque.branchName}` : ""}
                    </p>
                  </td>

                  <td className="py-2.5 pr-3">
                    <Link
                      href={`/dashboard/post-sales/bookings/${cheque.bookingId}`}
                      className="font-medium text-primary hover:underline"
                    >
                      {cheque.customerName}
                    </Link>
                    <p className="text-[11px] text-muted-foreground">
                      {cheque.unitNumber}
                      {cheque.demandNumber ? ` · against ${cheque.demandNumber}` : ""}
                    </p>
                  </td>

                  <td className="py-2.5 pr-3 text-right font-medium">{inrExact(cheque.amount)}</td>

                  <td className="py-2.5 pr-3">
                    {shortDate(cheque.chequeDate)}
                    {cheque.dueForBanking ? (
                      <p className={cn("flex items-center gap-1 text-[11px]", TONE_TEXT.warning)}>
                        <TriangleAlert className="size-3" />
                        {cheque.daysSinceBankable === 0
                          ? "bankable today"
                          : `unbanked ${cheque.daysSinceBankable}d`}
                      </p>
                    ) : cheque.status === "Held" ? (
                      <p className="text-[11px] text-muted-foreground">
                        in {Math.abs(cheque.daysSinceBankable)}d
                      </p>
                    ) : null}
                  </td>

                  <td className="py-2.5 pr-3">
                    <Pill tone={CHEQUE_TONE[cheque.status] ?? "neutral"}>{cheque.status}</Pill>
                    {cheque.bounceReason ? (
                      <p className={cn("mt-0.5 text-[11px]", TONE_TEXT.danger)}>
                        {cheque.bounceReason}
                      </p>
                    ) : cheque.clearedOn ? (
                      <p className="mt-0.5 text-[11px] text-muted-foreground">
                        {shortDate(cheque.clearedOn)}
                      </p>
                    ) : null}
                  </td>

                  <td className="py-2.5 text-right whitespace-nowrap">
                    {busy === cheque.id ? (
                      <Loader2 className="ml-auto size-3.5 animate-spin text-muted-foreground" />
                    ) : (
                      <div className="flex justify-end gap-1">
                        {cheque.status === "Held" ? (
                          <>
                            <Button
                              size="sm"
                              variant={cheque.dueForBanking ? "default" : "outline"}
                              className="h-7 gap-1 px-2 text-[11px]"
                              onClick={() =>
                                act(
                                  cheque.id,
                                  () => billingApi.deposit(cheque.id),
                                  `${cheque.chequeNumber} sent to the bank`
                                )
                              }
                            >
                              <Landmark className="size-3" />
                              Bank it
                            </Button>
                            <Button
                              size="sm"
                              variant="ghost"
                              className="h-7 gap-1 px-2 text-[11px]"
                              onClick={() =>
                                act(
                                  cheque.id,
                                  () => billingApi.returnCheque(cheque.id, "Handed back"),
                                  "Handed back"
                                )
                              }
                            >
                              <Undo2 className="size-3" />
                            </Button>
                          </>
                        ) : cheque.status === "Deposited" ? (
                          <>
                            <Button
                              size="sm"
                              className="h-7 gap-1 px-2 text-[11px]"
                              onClick={() =>
                                act(
                                  cheque.id,
                                  () => billingApi.clearCheque(cheque.id),
                                  `${cheque.chequeNumber} cleared — receipt raised and allocated`
                                )
                              }
                            >
                              <Banknote className="size-3" />
                              Cleared
                            </Button>
                            <Button
                              size="sm"
                              variant="outline"
                              className="h-7 px-2 text-[11px]"
                              onClick={() => setBouncing(cheque)}
                            >
                              Bounced
                            </Button>
                          </>
                        ) : cheque.status === "Cleared" ? (
                          <Button
                            size="sm"
                            variant="ghost"
                            className="h-7 px-2 text-[11px]"
                            onClick={() => setBouncing(cheque)}
                          >
                            Returned unpaid
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

      <TakeDialog open={taking} onOpenChange={setTaking} onDone={load} />

      <BounceDialog
        cheque={bouncing}
        onClose={() => setBouncing(null)}
        onDone={load}
      />
    </PagePanel>
  );
}

/* ------------------------------------------------------------------ *
 * Take a cheque
 * ------------------------------------------------------------------ */

function TakeDialog({
  open,
  onOpenChange,
  onDone,
}: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onDone: () => void;
}) {
  const [bookings, setBookings] = React.useState<BookingRow[]>([]);
  const [bookingId, setBookingId] = React.useState("");
  const [chequeNumber, setChequeNumber] = React.useState("");
  const [bankName, setBankName] = React.useState("");
  const [branchName, setBranchName] = React.useState("");
  const [amount, setAmount] = React.useState("");
  const [chequeDate, setChequeDate] = React.useState("");
  const [saving, setSaving] = React.useState(false);
  const [wasOpen, setWasOpen] = React.useState(open);

  React.useEffect(() => {
    postSalesApi.bookings().then(setBookings).catch(() => setBookings([]));
  }, []);

  if (open !== wasOpen) {
    setWasOpen(open);

    if (open) {
      setBookingId("");
      setChequeNumber("");
      setBankName("");
      setBranchName("");
      setAmount("");
      setChequeDate("");
    }
  }

  const valid =
    bookingId !== "" &&
    chequeNumber.trim() !== "" &&
    bankName.trim() !== "" &&
    Number(amount) > 0 &&
    chequeDate !== "";

  async function submit() {
    if (!valid) return;

    setSaving(true);
    try {
      const cheque = await billingApi.takeCheque(Number(bookingId), {
        chequeNumber: chequeNumber.trim(),
        bankName: bankName.trim(),
        branchName: branchName.trim() || null,
        amount: Number(amount),
        chequeDate,
      });

      toast.success(`Cheque ${cheque.chequeNumber} taken`, {
        description: cheque.dueForBanking
          ? "It is already bankable — it is on the banking run now."
          : `Bankable from ${shortDate(cheque.chequeDate)}. No receipt until it clears.`,
      });

      onOpenChange(false);
      onDone();
    } catch (error) {
      toast.error("The cheque could not be taken", {
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
          <DialogTitle>Take a cheque</DialogTitle>
          <DialogDescription>
            Nothing is credited to the customer yet. The receipt is raised when the cheque clears —
            crediting it now would show them as paid while the paper sits in a folder.
          </DialogDescription>
        </DialogHeader>

        <div className="grid gap-3">
          <div className="grid gap-1.5">
            <Label>Booking</Label>
            <Select value={bookingId} onValueChange={setBookingId}>
              <SelectTrigger>
                <SelectValue placeholder="Choose a booking" />
              </SelectTrigger>
              <SelectContent>
                {bookings.map((b) => (
                  <SelectItem key={b.id} value={b.id.toString()}>
                    {b.customerName} · {b.unitNumber} · {b.bookingNumber}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>

          <div className="grid grid-cols-2 gap-3">
            <div className="grid gap-1.5">
              <Label htmlFor="q-num">Cheque number</Label>
              <Input
                id="q-num"
                value={chequeNumber}
                onChange={(e) => setChequeNumber(e.target.value)}
                placeholder="004512"
              />
            </div>
            <div className="grid gap-1.5">
              <Label htmlFor="q-amt">Amount</Label>
              <Input
                id="q-amt"
                inputMode="decimal"
                value={amount}
                onChange={(e) => setAmount(e.target.value)}
                placeholder="250000"
              />
            </div>
          </div>

          <div className="grid grid-cols-2 gap-3">
            <div className="grid gap-1.5">
              <Label htmlFor="q-bank">Bank</Label>
              <Input
                id="q-bank"
                value={bankName}
                onChange={(e) => setBankName(e.target.value)}
                placeholder="HDFC Bank"
              />
            </div>
            <div className="grid gap-1.5">
              <Label htmlFor="q-branch">Branch</Label>
              <Input
                id="q-branch"
                value={branchName}
                onChange={(e) => setBranchName(e.target.value)}
                placeholder="Andheri East"
              />
            </div>
          </div>

          <div className="grid gap-1.5">
            <Label htmlFor="q-date">Date on the cheque</Label>
            <Input
              id="q-date"
              type="date"
              value={chequeDate}
              onChange={(e) => setChequeDate(e.target.value)}
            />
            <p className="text-[11.5px] text-muted-foreground">
              It cannot be banked before this date, and the register will chase you once it passes.
            </p>
          </div>
        </div>

        <DialogFooter>
          <Button variant="outline" onClick={() => onOpenChange(false)} disabled={saving}>
            Cancel
          </Button>
          <Button onClick={submit} disabled={saving || !valid} className="gap-1.5">
            {saving ? <Loader2 className="size-4 animate-spin" /> : <Wallet className="size-4" />}
            Into the drawer
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

/* ------------------------------------------------------------------ *
 * Bounce
 * ------------------------------------------------------------------ */

function BounceDialog({
  cheque,
  onClose,
  onDone,
}: {
  cheque: Cheque | null;
  onClose: () => void;
  onDone: () => void;
}) {
  const [reason, setReason] = React.useState("Insufficient funds");
  const [saving, setSaving] = React.useState(false);

  async function submit() {
    if (!cheque) return;

    setSaving(true);
    try {
      await billingApi.bounceCheque(cheque.id, reason);

      toast.success(`${cheque.chequeNumber} marked bounced`, {
        description: cheque.receiptId
          ? "The receipt was reversed and the customer's outstanding is back where it was."
          : "Nothing had been credited, so the ledger is unchanged.",
      });

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
    <Dialog open={cheque !== null} onOpenChange={(open) => (open ? null : onClose())}>
      <DialogContent className="sm:max-w-md">
        <DialogHeader>
          <DialogTitle>Cheque {cheque?.chequeNumber} came back</DialogTitle>
          <DialogDescription>
            {cheque?.receiptId
              ? "This cheque was already credited, so the receipt will be reversed and the outstanding restored."
              : "Nothing was credited against this cheque, so only the register changes."}
          </DialogDescription>
        </DialogHeader>

        <div className="grid gap-1.5">
          <Label>Why the bank returned it</Label>
          <Select value={reason} onValueChange={setReason}>
            <SelectTrigger>
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              {[
                "Insufficient funds",
                "Signature mismatch",
                "Account closed",
                "Payment stopped by drawer",
                "Post-dated / stale cheque",
                "Amount in words and figures differ",
              ].map((value) => (
                <SelectItem key={value} value={value}>
                  {value}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
          <p className="text-[11.5px] text-muted-foreground">
            Kept on the record. The next person deciding whether to grant this customer a credit
            period needs to see it.
          </p>
        </div>

        <DialogFooter>
          <Button variant="outline" onClick={onClose} disabled={saving}>
            Cancel
          </Button>
          <Button variant="destructive" onClick={submit} disabled={saving} className="gap-1.5">
            {saving ? <Loader2 className="size-4 animate-spin" /> : null}
            Record the bounce
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
