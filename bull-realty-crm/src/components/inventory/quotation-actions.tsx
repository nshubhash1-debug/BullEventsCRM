"use client";

import * as React from "react";
import {
  Ban,
  CheckCircle2,
  ClipboardCheck,
  Copy,
  Download,
  Link2,
  Loader2,
  MoreHorizontal,
  Pencil,
  Send,
  ShieldAlert,
  ShieldX,
  Trash2,
} from "lucide-react";
import { toast } from "sonner";

import { downloadQuotationPdf } from "@/components/inventory/quote-builder";
import { ShareLinkDialog } from "@/components/inventory/share-link-dialog";
import { SendQuotationDialog } from "@/components/inventory/send-quotation-dialog";
import { BookingDialog } from "@/components/post-sales/booking-dialog";
import { Button } from "@/components/ui/button";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import { ApiError } from "@/lib/api";
import { quoteBuilderApi } from "@/lib/inventory-api";

/**
 * What a quotation row can be told to do.
 *
 * The transitions are grouped by what they cost to get wrong. Downloading and
 * editing are free. Issuing and submitting for approval change who is looking
 * at the document. Accepting confirms the event dates on the calendar and moves the lead, and
 * declining and deleting hand it back — those four ask first, and the two that
 * need a reason collect one, because "why did we lose this" is the question the
 * sales desk asks a week later and cannot answer from a status alone.
 */
export interface QuotationRowLike {
  id: number;
  quoteNumber: string;
  status: string;
  approvalStatus?: string | null;
  customerName?: string | null;
  customerEmail?: string | null;
  customerPhone?: string | null;
}

type Prompt = {
  action: "decline" | "delete" | "submit";
  title: string;
  description: string;
  label: string;
  confirm: string;
  destructive: boolean;
  requiresReason: boolean;
};

const PROMPTS: Record<Prompt["action"], Omit<Prompt, "action">> = {
  submit: {
    title: "Send for approval",
    description:
      "A branch manager or above will see this in their queue. The quotation cannot be issued until they rule on it.",
    label: "What should the approver know?",
    confirm: "Send for approval",
    destructive: false,
    requiresReason: false,
  },
  decline: {
    title: "Decline this quotation",
    description:
      "The unit is handed back to the market and the lead's timeline records why.",
    label: "Why was it declined?",
    confirm: "Decline",
    destructive: true,
    requiresReason: true,
  },
  delete: {
    title: "Delete this quotation",
    description:
      "Any open approval is withdrawn and the unit is released. The document is hidden, not destroyed — it stays in the audit trail.",
    label: "",
    confirm: "Delete",
    destructive: true,
    requiresReason: false,
  },
};

export function QuotationActions({
  quotation,
  onEdit,
  onRevise,
  onChanged,
}: {
  quotation: QuotationRowLike;
  onEdit?: (quotation: QuotationRowLike) => void;
  /**
   * Raises a new version under the same reference. Offered alongside editing
   * because they answer different questions: edit when the price was wrong,
   * revise when the price changed and the customer has already seen the first.
   */
  onRevise?: (quotation: QuotationRowLike) => void;
  onChanged: () => void;
}) {
  const [busy, setBusy] = React.useState(false);
  const [prompt, setPrompt] = React.useState<Prompt["action"] | null>(null);
  const [reason, setReason] = React.useState("");
  const [sharing, setSharing] = React.useState(false);
  const [sending, setSending] = React.useState(false);
  const [booking, setBooking] = React.useState(false);

  const pending = quotation.approvalStatus === "Pending";
  const accepted = quotation.status === "Accepted";

  async function run(label: string, work: () => Promise<unknown>) {
    if (busy) return;
    setBusy(true);
    try {
      await work();
      toast.success(`${quotation.quoteNumber} — ${label}`);
      onChanged();
    } catch (error) {
      toast.error(`Could not ${label.toLowerCase()}`, {
        description: error instanceof ApiError ? error.message : "Network error.",
      });
    } finally {
      setBusy(false);
    }
  }

  async function confirmPrompt() {
    if (prompt === null) return;

    const spec = PROMPTS[prompt];
    const trimmed = reason.trim();

    if (spec.requiresReason && trimmed === "") {
      toast.error("A reason is required.");
      return;
    }

    const work =
      prompt === "decline"
        ? () => quoteBuilderApi.decline(quotation.id, trimmed)
        : prompt === "delete"
          ? () => quoteBuilderApi.remove(quotation.id)
          : () => quoteBuilderApi.submitForApproval(quotation.id, trimmed || null);

    const label =
      prompt === "decline" ? "declined" : prompt === "delete" ? "deleted" : "sent for approval";

    await run(label, work);

    setPrompt(null);
    setReason("");
  }

  const spec = prompt === null ? null : PROMPTS[prompt];

  return (
    <>
      <DropdownMenu>
        <DropdownMenuTrigger asChild>
          <Button
            variant="ghost"
            size="icon"
            aria-label={`Actions for ${quotation.quoteNumber}`}
            className="size-6 text-muted-foreground"
            onClick={(event) => event.stopPropagation()}
          >
            {busy ? (
              <Loader2 className="size-3.5 animate-spin" />
            ) : (
              <MoreHorizontal className="size-3.5" />
            )}
          </Button>
        </DropdownMenuTrigger>

        <DropdownMenuContent
          align="end"
          className="w-56"
          onClick={(event) => event.stopPropagation()}
        >
          <DropdownMenuItem
            onClick={() => downloadQuotationPdf(quotation.id, quotation.quoteNumber)}
          >
            <Download /> Download PDF
          </DropdownMenuItem>

          <DropdownMenuItem onClick={() => setSending(true)}>
            <Send /> Send Email / WhatsApp
          </DropdownMenuItem>

          <DropdownMenuItem onClick={() => setSharing(true)}>
            <Link2 /> Share link
          </DropdownMenuItem>

          {onEdit ? (
            <DropdownMenuItem
              disabled={accepted}
              onClick={() => onEdit(quotation)}
            >
              <Pencil /> Edit &amp; re-price
            </DropdownMenuItem>
          ) : null}

          {onRevise ? (
            <DropdownMenuItem onClick={() => onRevise(quotation)}>
              <Copy /> Create revision
            </DropdownMenuItem>
          ) : null}

          <DropdownMenuSeparator />
          <DropdownMenuLabel className="text-[11px] tracking-wide text-muted-foreground uppercase">
            Approval
          </DropdownMenuLabel>

          {pending ? (
            <DropdownMenuItem
              onClick={() =>
                run("approval withdrawn", () =>
                  quoteBuilderApi.withdrawApproval(quotation.id)
                )
              }
            >
              <ShieldX /> Withdraw request
            </DropdownMenuItem>
          ) : (
            <DropdownMenuItem disabled={accepted} onClick={() => setPrompt("submit")}>
              <ShieldAlert /> Send for approval
            </DropdownMenuItem>
          )}

          <DropdownMenuSeparator />
          <DropdownMenuLabel className="text-[11px] tracking-wide text-muted-foreground uppercase">
            Outcome
          </DropdownMenuLabel>

          <DropdownMenuItem
            disabled={pending || accepted}
            onClick={() => run("issued", () => quoteBuilderApi.issue(quotation.id))}
          >
            <Send /> Issue to customer
          </DropdownMenuItem>

          <DropdownMenuItem
            disabled={pending || accepted}
            onClick={() => run("accepted — unit booked", () => quoteBuilderApi.accept(quotation.id))}
          >
            <CheckCircle2 /> Accept &amp; book unit
          </DropdownMenuItem>

          {/* Only once the customer has said yes. Accepting confirms the dates and takes the space off
              the board; this opens the file that follows it for the next three
              years — schedule, demands, agreement, possession. */}
          <DropdownMenuItem disabled={!accepted} onClick={() => setBooking(true)}>
            <ClipboardCheck /> Open booking
          </DropdownMenuItem>

          <DropdownMenuItem disabled={accepted} onClick={() => setPrompt("decline")}>
            <Ban /> Decline
          </DropdownMenuItem>

          <DropdownMenuSeparator />
          <DropdownMenuItem
            variant="destructive"
            disabled={accepted}
            onClick={() => setPrompt("delete")}
          >
            <Trash2 /> Delete
          </DropdownMenuItem>
        </DropdownMenuContent>
      </DropdownMenu>

      <Dialog
        open={prompt !== null}
        onOpenChange={(next) => {
          if (!next) {
            setPrompt(null);
            setReason("");
          }
        }}
      >
        <DialogContent onClick={(event) => event.stopPropagation()}>
          <DialogHeader>
            <DialogTitle>{spec?.title}</DialogTitle>
            <DialogDescription>{spec?.description}</DialogDescription>
          </DialogHeader>

          {spec?.label ? (
            <div className="py-2">
              <Label htmlFor="quotation-reason" className="mb-1 block">
                {spec.label}
              </Label>
              <Textarea
                id="quotation-reason"
                rows={3}
                value={reason}
                onChange={(event) => setReason(event.target.value)}
              />
            </div>
          ) : null}

          <DialogFooter>
            <Button variant="outline" onClick={() => setPrompt(null)} disabled={busy}>
              Cancel
            </Button>
            <Button
              variant={spec?.destructive ? "destructive" : "default"}
              onClick={() => void confirmPrompt()}
              disabled={busy}
            >
              {busy ? <Loader2 className="size-4 animate-spin" /> : null}
              {spec?.confirm}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <ShareLinkDialog
        quotationId={quotation.id}
        quoteNumber={quotation.quoteNumber}
        open={sharing}
        onOpenChange={setSharing}
      />

      <SendQuotationDialog
        quotationId={quotation.id}
        quoteNumber={quotation.quoteNumber}
        customerName={quotation.customerName}
        open={sending}
        onOpenChange={setSending}
        onSent={onChanged}
      />

      <BookingDialog
        quotationId={quotation.id}
        quoteNumber={quotation.quoteNumber}
        customerName={quotation.customerName}
        customerPhone={quotation.customerPhone}
        customerEmail={quotation.customerEmail}
        open={booking}
        onOpenChange={setBooking}
        onBooked={onChanged}
      />
    </>
  );
}
