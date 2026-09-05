"use client";

import * as React from "react";
import {
  Ban,
  CalendarClock,
  CheckCircle2,
  CircleDashed,
  CirclePlay,
  UserX,
} from "lucide-react";

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
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import { StatusDot, type Tone } from "@/components/crm/metrics";
import { humanise } from "@/lib/crm-api";
import { followUpStatusTone, visitStatusTone } from "@/lib/crm-tones";
import { cn } from "@/lib/utils";

/**
 * The one control that moves a scheduled record between states.
 *
 * Site visits, OBM meetings, follow-ups and tasks all share it, so the same
 * five words mean the same thing on every list and on the lead record — and a
 * new state is added in one file rather than four.
 */

export type StatusFamily = "visit" | "followUp";

/** What the caller sends on. Empty fields are simply omitted by the callers. */
export interface StatusChange {
  status: string;
  /** A visit's new slot, or a follow-up's new due date. */
  at?: string;
  /** Free text — lands on the lead's timeline, so it is worth filling in. */
  note?: string;
}

interface Option {
  status: string;
  label: string;
  icon: typeof CheckCircle2;
  /** Opens the dialog instead of firing straight away. */
  prompt?: "reschedule" | "note";
  hint?: string;
}

const VISIT_OPTIONS: Option[] = [
  { status: "Scheduled", label: "Scheduled", icon: CalendarClock },
  { status: "Confirmed", label: "Confirmed", icon: CirclePlay, hint: "Visitor has agreed the slot" },
  { status: "Rescheduled", label: "Reschedule…", icon: CalendarClock, prompt: "reschedule" },
  { status: "Completed", label: "Complete", icon: CheckCircle2 },
  { status: "NoShow", label: "No show", icon: UserX, prompt: "note" },
  { status: "Cancelled", label: "Cancel…", icon: Ban, prompt: "note" },
];

const FOLLOW_UP_OPTIONS: Option[] = [
  { status: "Open", label: "Open", icon: CircleDashed },
  { status: "InProgress", label: "In progress", icon: CirclePlay },
  { status: "Open", label: "Reschedule…", icon: CalendarClock, prompt: "reschedule" },
  { status: "Completed", label: "Complete", icon: CheckCircle2 },
  { status: "Cancelled", label: "Cancel…", icon: Ban, prompt: "note" },
];

/**
 * `datetime-local` wants `YYYY-MM-DDTHH:mm` in local time, which is exactly
 * what `toISOString` does not give — it converts to UTC and the value lands an
 * offset away from what the user picked.
 */
function toLocalInput(iso: string | null | undefined) {
  const date = iso ? new Date(iso) : new Date();
  const offset = date.getTimezoneOffset() * 60_000;
  return new Date(date.getTime() - offset).toISOString().slice(0, 16);
}

export function StatusMenu({
  family,
  status,
  scheduledAt,
  isOverdue = false,
  disabled = false,
  align = "end",
  onChange,
}: {
  family: StatusFamily;
  status: string;
  /** Seeds the reschedule dialog with the slot the record already holds. */
  scheduledAt?: string | null;
  isOverdue?: boolean;
  disabled?: boolean;
  align?: "start" | "end";
  onChange: (change: StatusChange) => Promise<void> | void;
}) {
  const [pending, setPending] = React.useState<Option | null>(null);
  const [at, setAt] = React.useState("");
  const [note, setNote] = React.useState("");
  const [saving, setSaving] = React.useState(false);

  const options = family === "visit" ? VISIT_OPTIONS : FOLLOW_UP_OPTIONS;
  const tone: Tone =
    family === "visit" ? visitStatusTone(status) : followUpStatusTone(status, isOverdue);

  function start(option: Option) {
    if (!option.prompt) {
      void onChange({ status: option.status });
      return;
    }

    setAt(toLocalInput(scheduledAt));
    setNote("");
    setPending(option);
  }

  async function confirm() {
    if (!pending) return;

    setSaving(true);
    try {
      await onChange({
        status: pending.status,
        // `datetime-local` has no zone; treating it as local and letting the
        // Date constructor apply the offset is what makes the stored instant
        // match the slot the user actually picked.
        at: pending.prompt === "reschedule" ? new Date(at).toISOString() : undefined,
        note: note.trim() || undefined,
      });
      setPending(null);
    } finally {
      setSaving(false);
    }
  }

  const isReschedule = pending?.prompt === "reschedule";

  return (
    <>
      <DropdownMenu>
        <DropdownMenuTrigger asChild disabled={disabled}>
          <Button
            variant="ghost"
            size="sm"
            className="h-6 max-w-full gap-1 px-1.5 text-[11.5px]"
            onClick={(event) => event.stopPropagation()}
          >
            <StatusDot label={humanise(status)} tone={tone} />
          </Button>
        </DropdownMenuTrigger>

        <DropdownMenuContent
          align={align}
          className="w-52"
          onClick={(event) => event.stopPropagation()}
        >
          <DropdownMenuLabel className="text-[11px] tracking-wide text-muted-foreground uppercase">
            Move to
          </DropdownMenuLabel>
          <DropdownMenuSeparator />

          {options.map((option) => {
            const Icon = option.icon;
            // The record's own state is shown, not offered — except the
            // reschedule row, which is a date change rather than a move.
            const current = option.status === status && !option.prompt;

            return (
              <DropdownMenuItem
                key={option.label}
                disabled={current}
                onSelect={() => start(option)}
                className={cn("text-[12.5px]", current && "opacity-50")}
              >
                <Icon className="size-3.5" />
                <span className="flex-1">{option.label}</span>
                {current ? <span className="text-[10.5px]">current</span> : null}
              </DropdownMenuItem>
            );
          })}
        </DropdownMenuContent>
      </DropdownMenu>

      <Dialog open={pending !== null} onOpenChange={(open) => !open && setPending(null)}>
        <DialogContent className="sm:max-w-md" onClick={(event) => event.stopPropagation()}>
          <DialogHeader>
            <DialogTitle>{isReschedule ? "Reschedule" : pending?.label.replace("…", "")}</DialogTitle>
            <DialogDescription>
              {isReschedule
                ? family === "visit"
                  ? "Pick the new slot. Any check-in against the old one is cleared."
                  : "Pick the new due date. The item stays in the queue."
                : "Say what happened. This is what the lead's timeline will show."}
            </DialogDescription>
          </DialogHeader>

          <div className="flex flex-col gap-3">
            {isReschedule ? (
              <div className="flex flex-col gap-1.5">
                <Label htmlFor="status-menu-at" className="text-[12px]">
                  {family === "visit" ? "New date and time" : "New due date"}
                </Label>
                <Input
                  id="status-menu-at"
                  type="datetime-local"
                  value={at}
                  onChange={(event) => setAt(event.target.value)}
                />
              </div>
            ) : null}

            <div className="flex flex-col gap-1.5">
              <Label htmlFor="status-menu-note" className="text-[12px]">
                {isReschedule ? "Reason (optional)" : "Reason"}
              </Label>
              <Textarea
                id="status-menu-note"
                rows={3}
                value={note}
                placeholder={
                  isReschedule
                    ? "Client asked to move it…"
                    : "Client went with another project…"
                }
                onChange={(event) => setNote(event.target.value)}
              />
            </div>
          </div>

          <DialogFooter>
            <Button variant="outline" onClick={() => setPending(null)} disabled={saving}>
              Cancel
            </Button>
            <Button onClick={confirm} disabled={saving || (isReschedule && !at)}>
              {saving ? "Saving…" : isReschedule ? "Reschedule" : "Confirm"}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </>
  );
}
