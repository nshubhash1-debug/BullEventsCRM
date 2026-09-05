"use client";

import * as React from "react";
import { ChevronLeft, ChevronRight, Loader2, TriangleAlert } from "lucide-react";

import { Button } from "@/components/ui/button";
import {
  Tooltip,
  TooltipContent,
  TooltipTrigger,
} from "@/components/ui/tooltip";
import {
  schedulingApi,
  type Availability,
  type SlotState,
} from "@/lib/crm-api";
import { cn } from "@/lib/utils";

/** Local `YYYY-MM-DD` — the server treats the date as a calendar day, not an instant. */
function isoDay(date: Date) {
  const copy = new Date(date);
  copy.setMinutes(copy.getMinutes() - copy.getTimezoneOffset());
  return copy.toISOString().slice(0, 10);
}

function addDays(day: string, delta: number) {
  const date = new Date(`${day}T00:00:00`);
  date.setDate(date.getDate() + delta);
  return isoDay(date);
}

function humanDay(day: string) {
  return new Date(`${day}T00:00:00`).toLocaleDateString(undefined, {
    weekday: "long",
    day: "numeric",
    month: "short",
  });
}

const STATE_STYLE: Record<SlotState, string> = {
  Available:
    "border-emerald-500/40 bg-emerald-500/10 text-emerald-700 hover:bg-emerald-500/20 dark:text-emerald-400",
  Busy: "border-red-500/30 bg-red-500/10 text-red-700 dark:text-red-400 cursor-not-allowed",
  Past: "border-transparent bg-muted text-muted-foreground/50 cursor-not-allowed",
  OutsideHours:
    "border-transparent bg-muted/50 text-muted-foreground/40 cursor-not-allowed",
};

const STATE_LABEL: Record<SlotState, string> = {
  Available: "Available",
  Busy: "Busy",
  Past: "Already gone",
  OutsideHours: "Outside working hours",
};

export interface SlotSelection {
  /** ISO instant for the chosen slot start. */
  startsAt: string;
  label: string;
  /** True when the rep chose a slot that already has something in it. */
  overlaps: boolean;
}

interface SlotPickerProps {
  /** Whose calendar to read. Null means the signed-in user. */
  userId: number | null;
  /** How long the meeting runs — a slot is only free if the whole thing fits. */
  durationMinutes: number;
  value: SlotSelection | null;
  onChange: (selection: SlotSelection | null) => void;
  /** Days from today the picker opens on. */
  initialOffsetDays?: number;
}

/**
 * Day-at-a-glance slot grid.
 *
 * Availability is computed across site visits, field meetings and scheduled
 * follow-ups together, because they all take the same person's time. A busy
 * slot says what is in it rather than just refusing — a rep deciding whether to
 * double-book needs to know what they would be double-booking against.
 */
export function SlotPicker({
  userId,
  durationMinutes,
  value,
  onChange,
  initialOffsetDays = 1,
}: SlotPickerProps) {
  const [day, setDay] = React.useState(() =>
    addDays(isoDay(new Date()), initialOffsetDays)
  );

  // Keyed by the query that produced it, so "loading" is derived rather than a
  // second piece of state written inside the effect.
  const [loaded, setLoaded] = React.useState<{
    key: string;
    data: Availability;
  } | null>(null);

  const queryKey = `${day}|${userId ?? "me"}|${durationMinutes}`;

  React.useEffect(() => {
    let cancelled = false;

    schedulingApi
      .availability({ date: day, userId, durationMinutes })
      .then((data) => {
        if (!cancelled) setLoaded({ key: queryKey, data });
      })
      .catch(() => {
        if (!cancelled) setLoaded(null);
      });

    return () => {
      cancelled = true;
    };
  }, [day, userId, durationMinutes, queryKey]);

  const availability = loaded?.data ?? null;
  const loading = loaded === null || loaded.key !== queryKey;

  const today = isoDay(new Date());

  function move(delta: number) {
    setDay((current) => addDays(current, delta));
    onChange(null);
  }

  return (
    <div className="flex flex-col gap-2">
      {/* ---- day navigation ---- */}
      <div className="flex items-center gap-1.5">
        <Button
          type="button"
          variant="outline"
          size="icon"
          aria-label="Previous day"
          className="size-7"
          disabled={day <= today}
          onClick={() => move(-1)}
        >
          <ChevronLeft className="size-3.5" />
        </Button>

        <input
          type="date"
          value={day}
          min={today}
          onChange={(event) => {
            setDay(event.target.value);
            onChange(null);
          }}
          className="h-7 rounded-md border bg-background px-2 text-[12.5px] outline-none focus-visible:ring-[3px] focus-visible:ring-ring/50"
        />

        <Button
          type="button"
          variant="outline"
          size="icon"
          aria-label="Next day"
          className="size-7"
          onClick={() => move(1)}
        >
          <ChevronRight className="size-3.5" />
        </Button>

        <span className="ml-1 text-[12px] font-medium">{humanDay(day)}</span>

        <span className="ml-auto flex items-center gap-2 text-[11px] text-muted-foreground">
          {loading ? (
            <Loader2 className="size-3 animate-spin" />
          ) : availability ? (
            <>
              <span>{availability.userName}</span>
              <span>·</span>
              <span className="text-emerald-600 dark:text-emerald-400">
                {availability.availableCount} free
              </span>
              {availability.bookingCount > 0 ? (
                <>
                  <span>·</span>
                  <span>{availability.bookingCount} booked</span>
                </>
              ) : null}
            </>
          ) : null}
        </span>
      </div>

      {/* ---- slot grid ---- */}
      {loading ? (
        <div className="grid grid-cols-4 gap-1 sm:grid-cols-6">
          {Array.from({ length: 24 }, (_, index) => (
            <span key={index} className="h-7 animate-pulse rounded bg-muted" />
          ))}
        </div>
      ) : !availability ? (
        <p className="py-3 text-center text-[12.5px] text-muted-foreground">
          Could not load this day&apos;s availability.
        </p>
      ) : (
        <>
          <div className="grid grid-cols-4 gap-1 sm:grid-cols-6">
            {availability.slots.map((slot) => {
              const selected = value?.startsAt === slot.start;
              const bookable = slot.state === "Available" || slot.state === "Busy";

              const button = (
                <button
                  key={slot.start}
                  type="button"
                  disabled={!bookable}
                  onClick={() =>
                    onChange(
                      selected
                        ? null
                        : {
                            startsAt: slot.start,
                            label: slot.label,
                            overlaps: slot.state === "Busy",
                          }
                    )
                  }
                  className={cn(
                    "h-7 rounded border text-[11.5px] font-medium tabular-nums transition-colors",
                    STATE_STYLE[slot.state],
                    // A busy slot stays clickable so a deliberate double-book is
                    // possible — it just has to be deliberate.
                    slot.state === "Busy" && "cursor-pointer hover:bg-red-500/20",
                    selected && "ring-2 ring-primary ring-offset-1"
                  )}
                >
                  {slot.label}
                </button>
              );

              if (slot.bookings.length === 0) return button;

              return (
                <Tooltip key={slot.start}>
                  <TooltipTrigger asChild>{button}</TooltipTrigger>
                  <TooltipContent className="max-w-xs text-[11px]">
                    {slot.bookings.map((booking) => (
                      <span key={`${booking.kind}-${booking.id}`} className="block">
                        {booking.title}
                        {booking.subtitle ? ` · ${booking.subtitle}` : ""}
                      </span>
                    ))}
                  </TooltipContent>
                </Tooltip>
              );
            })}
          </div>

          {/* ---- legend ---- */}
          <div className="flex flex-wrap items-center gap-x-3 gap-y-1 text-[10.5px] text-muted-foreground">
            {(["Available", "Busy", "Past", "OutsideHours"] as SlotState[]).map(
              (state) => (
                <span key={state} className="inline-flex items-center gap-1">
                  <span
                    className={cn(
                      "size-2.5 rounded-sm border",
                      STATE_STYLE[state].split(" ").slice(0, 2).join(" ")
                    )}
                  />
                  {STATE_LABEL[state]}
                </span>
              )
            )}
            <span className="ml-auto">
              {availability.businessStart}–{availability.businessEnd} ·{" "}
              {durationMinutes} min meeting
            </span>
          </div>

          {value?.overlaps ? (
            <p className="flex items-start gap-1.5 rounded border border-amber-500/30 bg-amber-500/10 px-2 py-1.5 text-[11.5px] text-amber-700 dark:text-amber-400">
              <TriangleAlert className="mt-0.5 size-3.5 shrink-0" />
              <span>
                {value.label} already has something booked. Scheduling here will
                double-book {availability.userName}.
              </span>
            </p>
          ) : null}
        </>
      )}
    </div>
  );
}
