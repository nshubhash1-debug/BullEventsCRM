"use client";

import * as React from "react";
import {
  Building2,
  Check,
  Clock,
  FileText,
  Loader2,
  Percent,
  ShieldCheck,
  X,
} from "lucide-react";
import { toast } from "sonner";

import { PdfButton } from "@/components/inventory/quote-builder";
import { Button } from "@/components/ui/button";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { Skeleton } from "@/components/ui/skeleton";
import { Textarea } from "@/components/ui/textarea";
import { ToggleGroup, ToggleGroupItem } from "@/components/ui/toggle-group";
import { CrmLoadingState } from "@/components/shell/crm-loader";
import { ApiError } from "@/lib/api";
import {
  approvalKindLabel,
  approvalsApi,
  formatRupees,
  type Approval,
  type ApprovalSummary,
} from "@/lib/inventory-api";
import { formatDateTime, timeAgo } from "@/lib/crm-api";
import { cn } from "@/lib/utils";

type Filter = "Pending" | "Approved" | "Rejected" | "All";

/**
 * The approval queue.
 *
 * Ordered oldest-first while pending, which is the opposite of every other list
 * in this app and deliberate: a request that has waited two days is the one
 * costing a deal, and newest-first would bury it. The ageing figure is on every
 * row for the same reason.
 */
export default function ApprovalsPage() {
  const [filter, setFilter] = React.useState<Filter>("Pending");
  const [items, setItems] = React.useState<Approval[] | null>(null);
  const [summary, setSummary] = React.useState<ApprovalSummary | null>(null);

  const [decision, setDecision] = React.useState<{
    approval: Approval;
    action: "approve" | "reject";
  } | null>(null);
  const [note, setNote] = React.useState("");
  const [saving, setSaving] = React.useState(false);

  // Same reason as the board: the effect fetches, and only the handler that
  // asks for a reload is allowed to blank the list synchronously.
  const [reloadToken, setReloadToken] = React.useState(0);

  React.useEffect(() => {
    let cancelled = false;

    approvalsApi
      .list({ status: filter })
      .then((rows) => !cancelled && setItems(rows))
      .catch(() => !cancelled && setItems([]));

    approvalsApi
      .summary()
      .then((next) => !cancelled && setSummary(next))
      .catch(() => !cancelled && setSummary(null));

    return () => {
      cancelled = true;
    };
  }, [filter, reloadToken]);

  const load = React.useCallback(() => {
    setItems(null);
    setReloadToken((token) => token + 1);
  }, []);

  async function decide() {
    if (!decision) return;

    setSaving(true);
    try {
      const call =
        decision.action === "approve" ? approvalsApi.approve : approvalsApi.reject;

      await call(decision.approval.id, note.trim() || undefined);

      toast.success(
        decision.action === "approve"
          ? `${decision.approval.entityLabel} approved`
          : `${decision.approval.entityLabel} rejected`,
        {
          description:
            decision.action === "approve"
              ? "The change it authorises has been applied."
              : "The record has been returned to its owner.",
        }
      );

      setDecision(null);
      setNote("");
      load();
    } catch (error) {
      toast.error("Could not record that decision", {
        description: error instanceof ApiError ? error.message : "Network error.",
      });
    } finally {
      setSaving(false);
    }
  }

  const canDecide = summary?.canDecide ?? false;

  return (
    <div className="flex min-h-0 flex-1 flex-col gap-3 p-4">
      <header className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <h1 className="flex items-center gap-2 text-lg font-semibold">
            <ShieldCheck className="size-4.5 text-primary" />
            Approvals
          </h1>
          <p className="text-[12.5px] text-muted-foreground">
            Discount exceptions, rate overrides, and space booking sign-off.
          </p>
        </div>

        <ToggleGroup
          type="single"
          value={filter}
          onValueChange={(value) => value && setFilter(value as Filter)}
          variant="outline"
          size="sm"
        >
          {(["Pending", "Approved", "Rejected", "All"] as Filter[]).map((option) => (
            <ToggleGroupItem
              key={option}
              value={option}
              className="px-3 text-[12px]"
              aria-label={option}
            >
              {option}
            </ToggleGroupItem>
          ))}
        </ToggleGroup>
      </header>

      {summary ? (
        <div className="grid grid-cols-2 gap-2 sm:grid-cols-4">
          <Stat label="Waiting" value={summary.pending} />
          <Stat
            label="Over a day old"
            value={summary.overdue}
            tone={summary.overdue > 0 ? "text-amber-600 dark:text-amber-400" : undefined}
          />
          <Stat label="Quotations" value={summary.pendingQuotations} />
          <Stat label="Spaces" value={summary.pendingUnits} />
        </div>
      ) : (
        <Skeleton className="h-16 w-full" />
      )}

      {!canDecide && summary ? (
        <p className="rounded border border-amber-500/40 bg-amber-500/10 px-3 py-2 text-[12px] text-amber-700 dark:text-amber-300">
          You can raise requests but not decide them — approvals are settled by a
          branch manager or above.
        </p>
      ) : null}

      <div className="min-h-0 flex-1 overflow-auto rounded-lg border bg-card">
        {items === null ? (
          <CrmLoadingState label="Loading approvals" />
        ) : items.length === 0 ? (
          <div className="flex h-56 items-center justify-center text-[13px] text-muted-foreground">
            {filter === "Pending"
              ? "Nothing is waiting. The desk is clear."
              : `No ${filter.toLowerCase()} requests.`}
          </div>
        ) : (
          <ul className="flex flex-col">
            {items.map((approval) => (
              <li
                key={approval.id}
                className="flex flex-wrap items-start gap-3 border-b p-3.5 last:border-b-0"
              >
                <span
                  className={cn(
                    "mt-0.5 flex size-8 shrink-0 items-center justify-center rounded-full",
                    approval.entityType === "Quotation"
                      ? "bg-sky-500/10 text-sky-600 dark:text-sky-400"
                      : "bg-violet-500/10 text-violet-600 dark:text-violet-400"
                  )}
                >
                  {approval.entityType === "Quotation" ? (
                    approval.kind === "Discount" ? (
                      <Percent className="size-4" />
                    ) : (
                      <FileText className="size-4" />
                    )
                  ) : (
                    <Building2 className="size-4" />
                  )}
                </span>

                <div className="min-w-0 flex-1">
                  <p className="flex flex-wrap items-center gap-2 text-[13px] font-medium">
                    {approval.summary}
                    <StatusBadge status={approval.status} />
                  </p>

                  <p className="text-[12px] text-muted-foreground">
                    <span className="font-medium text-foreground/80">
                      {approvalKindLabel(approval.kind)}
                    </span>
                    {" · "}
                    {approval.entityLabel} · raised by {approval.requestedByName} ·{" "}
                    {timeAgo(approval.requestedAt)}
                    {approval.amount ? ` · ${formatRupees(approval.amount)}` : ""}
                  </p>

                  {approval.reason ? (
                    <p className="mt-1 rounded bg-muted/50 px-2 py-1 text-[12px]">
                      &ldquo;{approval.reason}&rdquo;
                    </p>
                  ) : null}

                  {approval.decidedAt ? (
                    <p className="mt-1 text-[11.5px] text-muted-foreground">
                      {approval.status} by {approval.decidedByName} on{" "}
                      {formatDateTime(approval.decidedAt)}
                      {approval.decisionNote ? ` — ${approval.decisionNote}` : ""}
                    </p>
                  ) : (
                    <p className="mt-1 flex items-center gap-1 text-[11.5px] text-muted-foreground">
                      <Clock className="size-3" />
                      Waiting {approval.ageHours < 1
                        ? "under an hour"
                        : `${Math.round(approval.ageHours)} hours`}
                    </p>
                  )}
                </div>

                <div className="flex shrink-0 items-center gap-2">
                  {approval.entityType === "Quotation" ? (
                    <PdfButton id={approval.entityId} quoteNumber={approval.entityLabel} />
                  ) : null}

                  {approval.status === "Pending" && canDecide ? (
                    <>
                      <Button
                        variant="outline"
                        size="sm"
                        onClick={() => {
                          setNote("");
                          setDecision({ approval, action: "reject" });
                        }}
                      >
                        <X className="size-3.5" /> Reject
                      </Button>
                      <Button
                        size="sm"
                        onClick={() => {
                          setNote("");
                          setDecision({ approval, action: "approve" });
                        }}
                      >
                        <Check className="size-3.5" /> Approve
                      </Button>
                    </>
                  ) : null}
                </div>
              </li>
            ))}
          </ul>
        )}
      </div>

      <Dialog open={decision !== null} onOpenChange={(open) => !open && setDecision(null)}>
        <DialogContent className="sm:max-w-md">
          <DialogHeader>
            <DialogTitle>
              {decision?.action === "approve" ? "Approve" : "Reject"}{" "}
              {decision?.approval.entityLabel}
            </DialogTitle>
            <DialogDescription>
              {decision?.approval.summary}
              {decision?.action === "approve"
                ? " — approving applies the change immediately."
                : " — rejecting returns the record to its owner."}
            </DialogDescription>
          </DialogHeader>

          <Textarea
            rows={3}
            value={note}
            placeholder={
              decision?.action === "approve"
                ? "Optional note for the record"
                : "Why is this being declined?"
            }
            onChange={(event) => setNote(event.target.value)}
          />

          <DialogFooter>
            <Button variant="outline" onClick={() => setDecision(null)} disabled={saving}>
              Cancel
            </Button>
            <Button
              variant={decision?.action === "reject" ? "destructive" : "default"}
              onClick={decide}
              disabled={saving}
            >
              {saving ? <Loader2 className="size-4 animate-spin" /> : null}
              {decision?.action === "approve" ? "Approve" : "Reject"}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}

function Stat({
  label,
  value,
  tone,
}: {
  label: string;
  value: number;
  tone?: string;
}) {
  return (
    <div className="rounded-lg border bg-card px-3 py-2">
      <p className="text-[10.5px] tracking-wide text-muted-foreground uppercase">
        {label}
      </p>
      <p className={cn("text-lg font-semibold tabular-nums", tone)}>{value}</p>
    </div>
  );
}

function StatusBadge({ status }: { status: Approval["status"] }) {
  const tone =
    status === "Approved"
      ? "bg-emerald-500/10 text-emerald-700 border-emerald-500/30 dark:text-emerald-300"
      : status === "Rejected"
        ? "bg-rose-500/10 text-rose-700 border-rose-500/30 dark:text-rose-300"
        : status === "Cancelled"
          ? "bg-muted text-muted-foreground border-border"
          : "bg-amber-500/10 text-amber-700 border-amber-500/30 dark:text-amber-300";

  return (
    <span className={cn("rounded-full border px-2 py-0.5 text-[10.5px]", tone)}>
      {status}
    </span>
  );
}
