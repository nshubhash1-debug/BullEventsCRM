/**
 * Date maths for the calendar.
 *
 * Hand-rolled rather than pulling in a date library: the calendar needs about a
 * dozen operations, all of them on local civil dates, and every one of them is
 * a few lines. A dependency here would be larger than the code it replaced.
 *
 * Everything works in the browser's local timezone. The API sends UTC with a
 * trailing Z, so `new Date(iso)` lands on the right local instant and the grid
 * places it on the day the user would say it happened.
 */

export const DAY_MS = 86_400_000;

/** Monday-first, matching how a working week is read in this market. */
export const WEEKDAY_LABELS = ["Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun"];

export type CalendarView = "month" | "week" | "day" | "agenda";

/* ------------------------------------------------------------------ *
 * Construction
 * ------------------------------------------------------------------ */

/** Midnight local on the same civil day. */
export function startOfDay(date: Date) {
  const next = new Date(date);
  next.setHours(0, 0, 0, 0);
  return next;
}

export function endOfDay(date: Date) {
  const next = new Date(date);
  next.setHours(23, 59, 59, 999);
  return next;
}

export function addDays(date: Date, days: number) {
  const next = new Date(date);
  next.setDate(next.getDate() + days);
  return next;
}

export function addMonths(date: Date, months: number) {
  const next = new Date(date);
  const day = next.getDate();
  next.setDate(1);
  next.setMonth(next.getMonth() + months);
  // Clamp so 31 Jan + 1 month lands on 28/29 Feb rather than spilling to March.
  next.setDate(Math.min(day, daysInMonth(next.getFullYear(), next.getMonth())));
  return next;
}

export function daysInMonth(year: number, month: number) {
  return new Date(year, month + 1, 0).getDate();
}

/** Monday of the week containing `date`. */
export function startOfWeek(date: Date) {
  const next = startOfDay(date);
  // getDay() is Sunday-0; shift so Monday is 0.
  const offset = (next.getDay() + 6) % 7;
  return addDays(next, -offset);
}

export function startOfMonth(date: Date) {
  return new Date(date.getFullYear(), date.getMonth(), 1);
}

export function endOfMonth(date: Date) {
  return endOfDay(new Date(date.getFullYear(), date.getMonth() + 1, 0));
}

/* ------------------------------------------------------------------ *
 * Comparison
 * ------------------------------------------------------------------ */

export function isSameDay(a: Date, b: Date) {
  return (
    a.getFullYear() === b.getFullYear() &&
    a.getMonth() === b.getMonth() &&
    a.getDate() === b.getDate()
  );
}

export function isSameMonth(a: Date, b: Date) {
  return a.getFullYear() === b.getFullYear() && a.getMonth() === b.getMonth();
}

export function isToday(date: Date) {
  return isSameDay(date, new Date());
}

export function isWeekend(date: Date) {
  const day = date.getDay();
  return day === 0 || day === 6;
}

/* ------------------------------------------------------------------ *
 * Ranges
 * ------------------------------------------------------------------ */

/**
 * The 6×7 grid a month view draws: whole weeks, padded with the neighbouring
 * months' days so every row is full. Always six rows, so the grid does not
 * change height as the user pages through months.
 */
export function monthGrid(anchor: Date): Date[] {
  const first = startOfWeek(startOfMonth(anchor));
  return Array.from({ length: 42 }, (_, index) => addDays(first, index));
}

export function weekGrid(anchor: Date): Date[] {
  const first = startOfWeek(anchor);
  return Array.from({ length: 7 }, (_, index) => addDays(first, index));
}

/** The window to ask the API for, given the view and the anchor date. */
export function visibleRange(view: CalendarView, anchor: Date) {
  switch (view) {
    case "week":
      return { from: startOfWeek(anchor), to: endOfDay(addDays(startOfWeek(anchor), 6)) };
    case "day":
      return { from: startOfDay(anchor), to: endOfDay(anchor) };
    case "agenda":
      // A rolling window rather than a calendar month: an agenda is read as
      // "what is coming up", which does not stop at a month boundary.
      return { from: startOfDay(anchor), to: endOfDay(addDays(anchor, 30)) };
    default: {
      const grid = monthGrid(anchor);
      return { from: grid[0], to: endOfDay(grid[grid.length - 1]) };
    }
  }
}

/* ------------------------------------------------------------------ *
 * Formatting
 * ------------------------------------------------------------------ */

const monthYear = new Intl.DateTimeFormat("en-IN", {
  month: "long",
  year: "numeric",
});
const dayMonth = new Intl.DateTimeFormat("en-IN", {
  day: "numeric",
  month: "short",
});
const fullDate = new Intl.DateTimeFormat("en-IN", {
  weekday: "long",
  day: "numeric",
  month: "long",
  year: "numeric",
});
const timeOnly = new Intl.DateTimeFormat("en-IN", {
  hour: "numeric",
  minute: "2-digit",
  hour12: true,
});

export function formatMonthYear(date: Date) {
  return monthYear.format(date);
}

export function formatDayMonth(date: Date) {
  return dayMonth.format(date);
}

export function formatFullDate(date: Date) {
  return fullDate.format(date);
}

export function formatTime(date: Date) {
  return timeOnly.format(date).toLowerCase().replace(" ", "");
}

/** "9:00am – 10:30am", collapsing to a single time when there is no duration. */
export function formatTimeRange(start: Date, end: Date | null) {
  if (!end || end.getTime() <= start.getTime()) return formatTime(start);
  return `${formatTime(start)} – ${formatTime(end)}`;
}

/** The heading over the grid, e.g. "18 – 24 Aug 2026" for a week. */
export function rangeLabel(view: CalendarView, anchor: Date) {
  if (view === "month") return formatMonthYear(anchor);
  if (view === "day") return formatFullDate(anchor);

  const { from, to } = visibleRange(view, anchor);
  const sameYear = from.getFullYear() === to.getFullYear();
  const left = sameYear ? formatDayMonth(from) : `${formatDayMonth(from)} ${from.getFullYear()}`;
  return `${left} – ${formatDayMonth(to)} ${to.getFullYear()}`;
}

/** "in 2 days", "3 hours ago" — relative phrasing for the agenda. */
export function relativeDay(date: Date) {
  const days = Math.round(
    (startOfDay(date).getTime() - startOfDay(new Date()).getTime()) / DAY_MS
  );

  if (days === 0) return "Today";
  if (days === 1) return "Tomorrow";
  if (days === -1) return "Yesterday";
  if (days > 1 && days < 7) return `In ${days} days`;
  if (days < -1 && days > -7) return `${Math.abs(days)} days ago`;
  return formatDayMonth(date);
}

/** ISO date (`2026-08-23`) in local time — the API's window bounds. */
export function toIsoDate(date: Date) {
  const year = date.getFullYear();
  const month = String(date.getMonth() + 1).padStart(2, "0");
  const day = String(date.getDate()).padStart(2, "0");
  return `${year}-${month}-${day}`;
}

/** Minutes from midnight — how an event is positioned in a time grid. */
export function minutesFromMidnight(date: Date) {
  return date.getHours() * 60 + date.getMinutes();
}
