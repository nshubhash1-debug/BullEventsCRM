"use client";

import * as React from "react";
import { Eye, FileText, Loader2, Plus, Printer, Send } from "lucide-react";
import { toast } from "sonner";

import { Pill } from "@/components/crm/metrics";
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
  documentsApi,
  TEMPLATE_LABELS,
  type GeneratedDocument,
} from "@/lib/lifecycle-api";
import { shortDate } from "@/lib/post-sales-api";

/** Letters that need a demand to render against. */
const NEEDS_DEMAND = new Set(["DemandLetter", "ReminderLetter"]);

const STATUS_TONE: Record<string, "neutral" | "info" | "success" | "danger"> = {
  Draft: "neutral",
  Issued: "info",
  Sent: "success",
  Cancelled: "danger",
};

/**
 * The letters written against one booking.
 *
 * Each one is stored as it went out, not re-rendered on demand. When the
 * template is edited next month the copy the customer is holding must still be
 * the copy this system can show — every dispute in this business turns on
 * exactly that, and "you never told me" is answered by producing the letter as
 * it was, not as it would be.
 */
export function LettersPanel({
  bookingId,
  demands,
}: {
  bookingId: number;
  demands: Array<{ id: number; label: string; demandNumber: string }>;
}) {
  const [letters, setLetters] = React.useState<GeneratedDocument[] | null>(null);
  const [writing, setWriting] = React.useState(false);
  const [reading, setReading] = React.useState<GeneratedDocument | null>(null);
  const [busy, setBusy] = React.useState<number | null>(null);

  const load = React.useCallback(() => {
    documentsApi
      .forBooking(bookingId)
      .then(setLetters)
      .catch(() => setLetters([]));
  }, [bookingId]);

  React.useEffect(load, [load]);

  async function act(id: number, work: () => Promise<unknown>, done: string) {
    setBusy(id);
    try {
      await work();
      toast.success(done);
      load();
    } catch (error) {
      toast.error("Could not update the letter", {
        description: error instanceof ApiError ? error.message : "Network error.",
      });
    } finally {
      setBusy(null);
    }
  }

  return (
    <RelatedList
      icon={FileText}
      title="Letters"
      count={letters?.length}
      hint="Stored as they went out"
      empty="No letter has been written for this booking yet."
      actions={
        <Button size="sm" className="h-7 gap-1 text-[12px]" onClick={() => setWriting(true)}>
          <Plus className="size-3.5" />
          Write one
        </Button>
      }
    >
      {letters === null ? (
        <p className="px-4 py-6 text-center text-[12.5px] text-muted-foreground">Loading…</p>
      ) : (
        <table className="w-full min-w-[720px] text-[12.5px]">
          <tbody>
            {letters.map((letter) => (
              <tr key={letter.id} className="border-b align-top last:border-0 hover:bg-muted/40">
                <td className="px-4 py-2.5">
                  <p className="font-medium">{letter.title}</p>
                  <p className="text-[11px] text-muted-foreground">
                    {letter.number} · {shortDate(letter.generatedAt)}
                  </p>
                </td>

                <td className="max-w-[300px] px-4 py-2.5 text-muted-foreground">
                  {letter.subject}
                </td>

                <td className="px-4 py-2.5">
                  <Pill tone={STATUS_TONE[letter.status] ?? "neutral"}>{letter.status}</Pill>
                  {letter.sentVia ? (
                    <p className="mt-0.5 text-[11px] text-muted-foreground">
                      via {letter.sentVia}
                    </p>
                  ) : null}
                </td>

                <td className="px-4 py-2.5 text-right whitespace-nowrap">
                  {busy === letter.id ? (
                    <Loader2 className="ml-auto size-3.5 animate-spin text-muted-foreground" />
                  ) : (
                    <div className="flex items-center justify-end gap-1">
                      <Button
                        size="sm"
                        variant="ghost"
                        className="h-7 gap-1 px-2 text-[11px]"
                        onClick={() => setReading(letter)}
                      >
                        <Eye className="size-3" />
                        Read
                      </Button>

                      {letter.status === "Draft" ? (
                        <Button
                          size="sm"
                          variant="outline"
                          className="h-7 px-2 text-[11px]"
                          onClick={() =>
                            act(letter.id, () => documentsApi.issue(letter.id), "Letter issued")
                          }
                        >
                          Issue
                        </Button>
                      ) : letter.status === "Issued" ? (
                        <Button
                          size="sm"
                          variant="outline"
                          className="h-7 gap-1 px-2 text-[11px]"
                          onClick={() =>
                            act(
                              letter.id,
                              () => documentsApi.markSent(letter.id, { via: "Email" }),
                              "Marked as sent"
                            )
                          }
                        >
                          <Send className="size-3" />
                          Mark sent
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

      <WriteDialog
        bookingId={bookingId}
        demands={demands}
        open={writing}
        onOpenChange={setWriting}
        onWritten={load}
      />

      <ReadDialog letter={reading} onClose={() => setReading(null)} />
    </RelatedList>
  );
}

function WriteDialog({
  bookingId,
  demands,
  open,
  onOpenChange,
  onWritten,
}: {
  bookingId: number;
  demands: Array<{ id: number; label: string; demandNumber: string }>;
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onWritten: () => void;
}) {
  const [kind, setKind] = React.useState("DemandLetter");
  const [demandId, setDemandId] = React.useState("");
  const [saving, setSaving] = React.useState(false);

  const [wasOpen, setWasOpen] = React.useState(open);

  if (open !== wasOpen) {
    setWasOpen(open);
    if (open) {
      setKind("DemandLetter");
      setDemandId(demands[0]?.id?.toString() ?? "");
    }
  }

  const needsDemand = NEEDS_DEMAND.has(kind);

  async function write() {
    if (needsDemand && !demandId) {
      toast.error("Choose which demand this letter is about.");
      return;
    }

    setSaving(true);
    try {
      const letter = await documentsApi.generate(bookingId, {
        kind,
        demandId: needsDemand ? Number(demandId) : null,
      });

      toast.success(`${letter.title} written`, { description: letter.number });
      onOpenChange(false);
      onWritten();
    } catch (error) {
      toast.error("Could not write the letter", {
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
          <DialogTitle>Write a letter</DialogTitle>
          <DialogDescription>
            Filled from this booking&apos;s own figures using the default template for the kind.
          </DialogDescription>
        </DialogHeader>

        <div className="grid gap-3">
          <div className="grid gap-1.5">
            <Label>Which letter</Label>
            <Select value={kind} onValueChange={setKind}>
              <SelectTrigger>
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                {Object.entries(TEMPLATE_LABELS).map(([value, label]) => (
                  <SelectItem key={value} value={value}>
                    {label}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>

          {needsDemand ? (
            <div className="grid gap-1.5">
              <Label>About which demand</Label>
              <Select value={demandId} onValueChange={setDemandId}>
                <SelectTrigger>
                  <SelectValue placeholder="Choose a demand" />
                </SelectTrigger>
                <SelectContent>
                  {demands.map((demand) => (
                    <SelectItem key={demand.id} value={demand.id.toString()}>
                      {demand.label} · {demand.demandNumber}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
              {demands.length === 0 ? (
                <p className="text-[11.5px] text-muted-foreground">
                  No demand has been raised yet. Raise one from the payment plan first.
                </p>
              ) : null}
            </div>
          ) : null}
        </div>

        <DialogFooter>
          <Button variant="outline" onClick={() => onOpenChange(false)} disabled={saving}>
            Cancel
          </Button>
          <Button onClick={write} disabled={saving} className="gap-1.5">
            {saving ? <Loader2 className="size-4 animate-spin" /> : <FileText className="size-4" />}
            Write it
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

/**
 * Shows the stored letter.
 *
 * Rendered into an iframe rather than into the page. The body carries its own
 * inline styles for email and print, and dropping that into the app's DOM would
 * let a letterhead's CSS leak into the CRM around it.
 */
function ReadDialog({
  letter,
  onClose,
}: {
  letter: GeneratedDocument | null;
  onClose: () => void;
}) {
  const [body, setBody] = React.useState<string | null>(null);
  const [seen, setSeen] = React.useState<GeneratedDocument | null>(null);

  if (letter !== seen) {
    setSeen(letter);
    setBody(null);

    if (letter) {
      documentsApi
        .read(letter.id)
        .then((doc) => setBody(doc.body))
        .catch(() => setBody("<p>This letter could not be loaded.</p>"));
    }
  }

  function print() {
    const frame = document.getElementById("letter-frame") as HTMLIFrameElement | null;
    frame?.contentWindow?.print();
  }

  return (
    <Dialog open={letter !== null} onOpenChange={(open) => (open ? null : onClose())}>
      <DialogContent className="sm:max-w-3xl">
        <DialogHeader>
          <DialogTitle>{letter?.title}</DialogTitle>
          <DialogDescription>
            {letter?.number} · written {letter ? shortDate(letter.generatedAt) : ""} · shown as it
            was produced, not as the template reads today
          </DialogDescription>
        </DialogHeader>

        <div className="h-[60vh] overflow-hidden rounded-lg border bg-white">
          {body === null ? (
            <div className="grid h-full place-items-center">
              <Loader2 className="size-5 animate-spin text-muted-foreground" />
            </div>
          ) : (
            <iframe
              id="letter-frame"
              title={letter?.title ?? "Letter"}
              srcDoc={body}
              className="size-full border-0"
              sandbox="allow-same-origin allow-modals"
            />
          )}
        </div>

        <DialogFooter>
          <Button variant="outline" onClick={onClose}>
            Close
          </Button>
          <Button onClick={print} disabled={body === null} className="gap-1.5">
            <Printer className="size-4" />
            Print
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
