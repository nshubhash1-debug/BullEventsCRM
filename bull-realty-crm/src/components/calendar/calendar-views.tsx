"use client";

import * as React from "react";
import { AlertTriangle, Clock, MapPin, User } from "lucide-react";

import { Badge } from "@/components/ui/badge";
import {
  eventColor,
  humanise,
  isClosed,
  SOURCE_META,
  type CalendarEvent,
} from "@/lib/calendar/api";
import {
  WEEKDAY_LABELS,
  addDays,
  formatTime,
  formatTimeRange,
  isSameDay,
  isSameMonth,
  isToday,
  isWeekend,
  minutesFromMidnight,
  monthGrid,
  relativeDay,
  startOfDay,
  weekGrid,
} from "@/lib/calendar/dates";
import { cn } from "@/lib/utils";

/* ------------------------------------------------------------------ *
 * Shared
 * ------------------------------------------------------------------ */

export interface ViewProps {
  anchor: Date;
  events: CalendarEvent[];
  onSelect: (event: CalendarEvent) => void;
  onPickDay?: (day: Date) => void;
}

/** Events that fall on one day, in start order. */
function eventsOn(events: CalendarEvent[], day: Date) {
  return events
    .filter((event) => isSameDay(new Date(event.start), day))
    .sort((a, b) => a.start.localeCompare(b.start));
}

/* ------------------------------------------------------------------ *
 * Month
 * ------------------------------------------------------------------ */

/** How many chips fit in a month cell before the rest collapse into a count. */
const MONTH_CELL_LIMIT = 3;

export function MonthView({ anchor, events, onSelect, onPickDay }: ViewProps) {
  const days = monthGrid(anchor);

  return (
    <div className="overflow-hidden rounded-md border bg-card">
      <div className="grid grid-cols-7 border-b bg-muted/40">
        {WEEKDAY_LABELS.map((label) => (
          <div
            key={label}
            className="px-2 py-1.5 text-center text-[11px] font-medium tracking-wide text-muted-foreground uppercase"
          >
            {label}
          </div>
        ))}
      </div>

      <div className="grid grid-cols-7">
        {days.map((day) => {
          const dayEvents = eventsOn(events, day);
          const overflow = dayEvents.length - MONTH_CELL_LIMIT;
          const outside = !isSameMonth(day, anchor);

          return (
            <button
              key={day.toISOString()}
              type="button"
              onClick={() => onPickDay?.(day)}
              className={cn(
                "flex min-h-[104px] flex-col gap-1 border-r border-b p-1.5 text-left transition-colors last:border-r-0 hover:bg-accent/50",
                outside && "bg-muted/30",
                isWeekend(day) && !outside && "bg-muted/15"
              )}
            >
              <span
                className={cn(
                  "flex size-5 shrink-0 items-center justify-center rounded-full text-[11px] tabular-nums",
                  isToday(day)
                    ? "bg-primary font-semibold text-primary-foreground"
                    : outside
                      ? "text-muted-foreground/50"
                      : "text-muted-foreground"
                )}
              >
                {day.getDate()}
              </span>

              <span className="flex min-w-0 flex-col gap-0.5">
                {dayEvents.slice(0, MONTH_CELL_LIMIT).map((event) => (
                  <EventChip key={event.id} event={event} onSelect={onSelect} />
                ))}
                {overflow > 0 ? (
                  <span className="px-1 text-[10.5px] text-muted-foreground">
                    +{overflow} more
                  </span>
                ) : null}
              </span>
            </button>
          );
        })}
      </div>
    </div>
  );
}

/** One line in a month cell. */
function EventChip({
  event,
  onSelect,
}: {
  event: CalendarEvent;
  onSelect: (event: CalendarEvent) => void;
}) {
  const color = eventColor(event);
  const closed = isClosed(event);

  return (
    <span
      role="button"
      tabIndex={0}
      title={`${formatTime(new Date(event.start))} · ${event.title}`}
      onClick={(e) => {
        e.stopPropagation();
        onSelect(event);
      }}
      onKeyDown={(e) => {
        if (e.key === "Enter" || e.key === " ") {
          e.stopPropagation();
          e.preventDefault();
          onSelect(event);
        }
      }}
      className={cn(
        "flex min-w-0 items-center gap-1 rounded px-1 py-0.5 text-[10.5px] hover:bg-accent",
        closed && "opacity-55"
      )}
    >
      <span
        className={cn(
          "size-1.5 shrink-0 rounded-full",
          // A due date is a moment, a booking is a span — the ring says which
          // without spending a second colour on it.
          event.isPoint && "ring-1 ring-current ring-offset-0"
        )}
        style={{ backgroundColor: event.isPoint ? "transparent" : color, color }}
      />
      <span className="shrink-0 tabular-nums text-muted-foreground">
        {formatTime(new Date(event.start))}
      </span>
      <span className={cn("truncate", closed && "line-through")}>{event.title}</span>
    </span>
  );
}

/* ------------------------------------------------------------------ *
 * Week and day — a time grid
 * ------------------------------------------------------------------ */

const GRID_START_HOUR = 7;
const GRID_END_HOUR = 21;
const HOUR_HEIGHT = 44;

/** The week and day views are the same grid over a different number of columns. */
export function TimeGridView({
  events,
  onSelect,
  days,
}: Omit<ViewProps, "anchor"> & { days: Date[] }) {
  const hours = Array.from(
    { length: GRID_END_HOUR - GRID_START_HOUR },
    (_, index) => GRID_START_HOUR + index
  );

  return (
    <div className="overflow-hidden rounded-md border bg-card">
      {/* Day headers */}
      <div
        className="grid border-b bg-muted/40"
        style={{ gridTemplateColumns: `56px repeat(${days.length}, minmax(0, 1fr))` }}
      >
        <div />
        {days.map((day) => (
          <div key={day.toISOString()} className="px-2 py-1.5 text-center">
            <div className="text-[11px] tracking-wide text-muted-foreground uppercase">
              {WEEKDAY_LABELS[(day.getDay() + 6) % 7]}
            </div>
            <div
              className={cn(
                "mx-auto mt-0.5 flex size-6 items-center justify-center rounded-full text-[12.5px] font-medium tabular-nums",
                isToday(day) && "bg-primary text-primary-foreground"
              )}
            >
              {day.getDate()}
            </div>
          </div>
        ))}
      </div>

      {/* All-day strip for point events, which have no height to draw */}
      <AllDayStrip days={days} events={events} onSelect={onSelect} />

      <div className="max-h-[62vh] overflow-y-auto">
        <div
          className="relative grid"
          style={{ gridTemplateColumns: `56px repeat(${days.length}, minmax(0, 1fr))` }}
        >
          <div>
            {hours.map((hour) => (
              <div
                key={hour}
                className="relative border-b pr-2 text-right text-[10.5px] text-muted-foreground"
                style={{ height: HOUR_HEIGHT }}
              >
                <span className="absolute -top-1.5 right-2">
                  {hour % 12 === 0 ? 12 : hour % 12}
                  {hour < 12 ? "am" : "pm"}
                </span>
              </div>
            ))}
          </div>

          {days.map((day) => (
            <DayColumn
              key={day.toISOString()}
              hours={hours}
              events={eventsOn(events, day).filter((event) => !event.isPoint)}
              onSelect={onSelect}
            />
          ))}
        </div>
      </div>
    </div>
  );
}

function AllDayStrip({
  days,
  events,
  onSelect,
}: {
  days: Date[];
  events: CalendarEvent[];
  onSelect: (event: CalendarEvent) => void;
}) {
  const perDay = days.map((day) =>
    eventsOn(events, day).filter((event) => event.isPoint)
  );

  if (perDay.every((list) => list.length === 0)) return null;

  return (
    <div
      className="grid border-b bg-muted/20"
      style={{ gridTemplateColumns: `56px repeat(${days.length}, minmax(0, 1fr))` }}
    >
      <div className="px-2 py-1 text-right text-[10px] tracking-wide text-muted-foreground uppercase">
        Due
      </div>
      {perDay.map((list, index) => (
        <div key={index} className="flex flex-col gap-0.5 border-l p-1">
          {list.map((event) => (
            <EventChip key={event.id} event={event} onSelect={onSelect} />
          ))}
        </div>
      ))}
    </div>
  );
}

function DayColumn({
  hours,
  events,
  onSelect,
}: {
  hours: number[];
  events: CalendarEvent[];
  onSelect: (event: CalendarEvent) => void;
}) {
  const laidOut = layout(events);

  return (
    <div className="relative border-l">
      {hours.map((hour) => (
        <div key={hour} className="border-b" style={{ height: HOUR_HEIGHT }} />
      ))}

      {laidOut.map(({ event, column, columns }) => {
        const start = new Date(event.start);
        const end = new Date(event.end);

        const top =
          ((minutesFromMidnight(start) - GRID_START_HOUR * 60) / 60) * HOUR_HEIGHT;
        const height = Math.max(
          18,
          ((end.getTime() - start.getTime()) / 3_600_000) * HOUR_HEIGHT
        );

        // Off the top or bottom of the drawn window — the all-day strip and the
        // agenda still carry it, so dropping it here loses nothing.
        if (top + height < 0 || top > (GRID_END_HOUR - GRID_START_HOUR) * HOUR_HEIGHT) {
          return null;
        }

        const color = eventColor(event);

        return (
          <button
            key={event.id}
            type="button"
            onClick={() => onSelect(event)}
            title={`${formatTimeRange(start, end)} · ${event.title}`}
            className={cn(
              "absolute overflow-hidden rounded border-l-2 px-1.5 py-0.5 text-left text-[10.5px] shadow-xs transition-opacity hover:opacity-90",
              isClosed(event) && "opacity-60"
            )}
            style={{
              top: Math.max(0, top),
              height,
              left: `${(column / columns) * 100}%`,
              width: `${(1 / columns) * 100 - 1}%`,
              borderLeftColor: color,
              backgroundColor: `color-mix(in oklab, ${color} 14%, var(--card))`,
            }}
          >
            <span className="block truncate font-medium">{event.title}</span>
            {height > 30 ? (
              <span className="block truncate text-muted-foreground">
                {formatTimeRange(start, end)}
              </span>
            ) : null}
          </button>
        );
      })}
    </div>
  );
}

/**
 * Side-by-side placement for overlapping blocks.
 *
 * Greedy and deliberately simple: walk the day in start order, put each event in
 * the first column whose last event has already finished. Two meetings at the
 * same hour end up beside each other rather than stacked on top of one another,
 * which is the only thing this needs to get right.
 */
function layout(events: CalendarEvent[]) {
  const columnEnds: number[] = [];
  const placed = events.map((event) => {
    const start = new Date(event.start).getTime();
    const end = new Date(event.end).getTime();

    let column = columnEnds.findIndex((finish) => finish <= start);
    if (column === -1) {
      column = columnEnds.length;
      columnEnds.push(end);
    } else {
      columnEnds[column] = end;
    }

    return { event, column, start, end };
  });

  // Every event in an overlapping run shares the run's width, so blocks in the
  // same cluster line up instead of each picking its own.
  return placed.map((item) => {
    const overlapping = placed.filter(
      (other) => other.start < item.end && other.end > item.start
    );
    const columns = Math.max(...overlapping.map((other) => other.column + 1), 1);
    return { ...item, columns };
  });
}

export function WeekView(props: ViewProps) {
  return <TimeGridView {...props} days={weekGrid(props.anchor)} />;
}

export function DayView(props: ViewProps) {
  return <TimeGridView {...props} days={[startOfDay(props.anchor)]} />;
}

/* ------------------------------------------------------------------ *
 * Agenda
 * ------------------------------------------------------------------ */

export function AgendaView({ anchor, events, onSelect }: ViewProps) {
  // Group by day, keeping only the days that have something on them — an agenda
  // listing empty days is mostly empty days.
  const groups = React.useMemo(() => {
    const buckets = new Map<string, { day: Date; items: CalendarEvent[] }>();

    for (const event of [...events].sort((a, b) => a.start.localeCompare(b.start))) {
      const day = startOfDay(new Date(event.start));
      const key = day.toISOString();
      if (!buckets.has(key)) buckets.set(key, { day, items: [] });
      buckets.get(key)!.items.push(event);
    }

    return [...buckets.values()];
  }, [events]);

  if (groups.length === 0) {
    return (
      <div className="flex flex-col items-center justify-center gap-2 rounded-md border border-dashed bg-card py-16 text-center">
        <Clock className="size-5 text-muted-foreground" />
        <p className="text-sm font-medium">Nothing scheduled</p>
        <p className="text-[13px] text-muted-foreground">
          From {relativeDay(anchor).toLowerCase()} onwards, this filter is clear.
        </p>
      </div>
    );
  }

  return (
    <div className="flex flex-col gap-2.5">
      {groups.map(({ day, items }) => (
        <section
          key={day.toISOString()}
          className="overflow-hidden rounded-md border bg-card"
        >
          <header className="flex items-baseline gap-2 border-b bg-muted/30 px-3 py-1.5">
            <h3 className="text-[13px] font-semibold">{relativeDay(day)}</h3>
            <span className="text-[11.5px] text-muted-foreground">
              {day.toLocaleDateString("en-IN", {
                weekday: "long",
                day: "numeric",
                month: "long",
              })}
            </span>
            <span className="ml-auto text-[11px] text-muted-foreground tabular-nums">
              {items.length} item{items.length === 1 ? "" : "s"}
            </span>
          </header>

          <ul>
            {items.map((event) => (
              <li key={event.id}>
                <button
                  type="button"
                  onClick={() => onSelect(event)}
                  className="flex w-full items-center gap-3 border-b px-3 py-2 text-left last:border-0 hover:bg-accent/50"
                >
                  <span
                    className="h-8 w-1 shrink-0 rounded-full"
                    style={{ backgroundColor: eventColor(event) }}
                  />

                  <span className="w-28 shrink-0 text-[12px] tabular-nums text-muted-foreground">
                    {event.isPoint
                      ? formatTime(new Date(event.start))
                      : formatTimeRange(new Date(event.start), new Date(event.end))}
                  </span>

                  <span className="min-w-0 flex-1">
                    <span
                      className={cn(
                        "block truncate text-[13px] font-medium",
                        isClosed(event) && "text-muted-foreground line-through"
                      )}
                    >
                      {event.title}
                    </span>
                    {event.subtitle ? (
                      <span className="block truncate text-[11.5px] text-muted-foreground">
                        {event.subtitle}
                      </span>
                    ) : null}
                  </span>

                  {event.isOverdue ? (
                    <Badge
                      variant="outline"
                      className="h-5 shrink-0 gap-1 border-destructive/40 px-1.5 text-[10px] font-normal text-destructive"
                    >
                      <AlertTriangle className="size-3" /> Overdue
                    </Badge>
                  ) : null}

                  <Badge
                    variant="outline"
                    className={cn(
                      "h-5 shrink-0 px-1.5 text-[10px] font-normal",
                      SOURCE_META[event.source].tint
                    )}
                  >
                    {humanise(event.kind) || SOURCE_META[event.source].short}
                  </Badge>

                  <span className="hidden w-32 shrink-0 items-center gap-1 truncate text-[11.5px] text-muted-foreground lg:flex">
                    <User className="size-3 shrink-0" />
                    {event.ownerName ?? "Unassigned"}
                  </span>

                  <span className="hidden w-28 shrink-0 items-center gap-1 truncate text-[11.5px] text-muted-foreground xl:flex">
                    {event.location ? (
                      <>
                        <MapPin className="size-3 shrink-0" />
                        {event.location}
                      </>
                    ) : null}
                  </span>
                </button>
              </li>
            ))}
          </ul>
        </section>
      ))}
    </div>
  );
}

/* ------------------------------------------------------------------ *
 * Mini month — the sidebar date picker
 * ------------------------------------------------------------------ */

export function MiniMonth({
  anchor,
  selected,
  counts,
  onPick,
}: {
  anchor: Date;
  selected: Date;
  /** ISO day → how many events, for the density dots. */
  counts: Map<string, number>;
  onPick: (day: Date) => void;
}) {
  const days = monthGrid(anchor);

  return (
    <div className="grid grid-cols-7 gap-0.5">
      {WEEKDAY_LABELS.map((label) => (
        <div
          key={label}
          className="pb-1 text-center text-[9.5px] font-medium text-muted-foreground uppercase"
        >
          {label[0]}
        </div>
      ))}

      {days.map((day) => {
        const outside = !isSameMonth(day, anchor);
        const count = counts.get(startOfDay(day).toISOString()) ?? 0;

        return (
          <button
            key={day.toISOString()}
            type="button"
            onClick={() => onPick(day)}
            className={cn(
              "relative flex aspect-square items-center justify-center rounded text-[11px] tabular-nums transition-colors hover:bg-accent",
              outside && "text-muted-foreground/40",
              isSameDay(day, selected) && "bg-primary font-semibold text-primary-foreground",
              !isSameDay(day, selected) && isToday(day) && "font-semibold text-primary"
            )}
          >
            {day.getDate()}
            {count > 0 && !isSameDay(day, selected) ? (
              <span className="absolute bottom-0.5 size-1 rounded-full bg-primary/60" />
            ) : null}
          </button>
        );
      })}
    </div>
  );
}

/** Next/previous step for the current view. */
export function step(view: string, anchor: Date, direction: 1 | -1) {
  switch (view) {
    case "week":
      return addDays(anchor, 7 * direction);
    case "day":
      return addDays(anchor, direction);
    case "agenda":
      return addDays(anchor, 30 * direction);
    default: {
      const next = new Date(anchor);
      next.setDate(1);
      next.setMonth(next.getMonth() + direction);
      return next;
    }
  }
}
