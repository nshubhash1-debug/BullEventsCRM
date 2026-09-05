import type { WireFilterNode } from "@/lib/query";

/**
 * The date window a list view is narrowed to.
 *
 * Every preset resolves to an explicit `between` on the client rather than
 * leaning on the server's relative operators. `lastNDays` on the server is an
 * open-ended `>= cutoff`, which is right for a created-at column but wrong for
 * a scheduled-at one — "last 7 days" would quietly include every visit booked
 * for next month. Computing both ends here means a window means the same thing
 * whichever date column it is pointed at.
 */

export type DatePresetKey =
  | "all"
  | "today"
  | "yesterday"
  | "last7"
  | "last14"
  | "last30"
  | "last90"
  | "thisMonth"
  | "lastMonth"
  | "thisQuarter"
  | "thisYear"
  | "next7"
  | "next30"
  | "custom";

export interface DateRange {
  preset: DatePresetKey;
  /** Which date column the window applies to. */
  field: string;
  /** ISO `yyyy-MM-dd`, only read when the preset is `custom`. */
  from?: string;
  to?: string;
}

export interface DatePresetDef {
  value: DatePresetKey;
  label: string;
  /** Groups the menu into past / upcoming so the two do not read as one list. */
  group: "Recent" | "Upcoming" | "Other";
}

export const DATE_PRESETS: DatePresetDef[] = [
  { value: "all", label: "Any time", group: "Other" },
  { value: "today", label: "Today", group: "Recent" },
  { value: "yesterday", label: "Yesterday", group: "Recent" },
  { value: "last7", label: "Last 7 days", group: "Recent" },
  { value: "last14", label: "Last 14 days", group: "Recent" },
  { value: "last30", label: "Last 30 days", group: "Recent" },
  { value: "last90", label: "Last 90 days", group: "Recent" },
  { value: "thisMonth", label: "This month", group: "Recent" },
  { value: "lastMonth", label: "Last month", group: "Recent" },
  { value: "thisQuarter", label: "This quarter", group: "Recent" },
  { value: "thisYear", label: "This year", group: "Recent" },
  { value: "next7", label: "Next 7 days", group: "Upcoming" },
  { value: "next30", label: "Next 30 days", group: "Upcoming" },
  { value: "custom", label: "Custom range…", group: "Other" },
];

export const PRESET_LABELS: Record<DatePresetKey, string> = Object.fromEntries(
  DATE_PRESETS.map((preset) => [preset.value, preset.label])
) as Record<DatePresetKey, string>;

/* ------------------------------------------------------------------ *
 * Resolution
 * ------------------------------------------------------------------ */

function iso(date: Date) {
  const year = date.getFullYear();
  const month = String(date.getMonth() + 1).padStart(2, "0");
  const day = String(date.getDate()).padStart(2, "0");
  return `${year}-${month}-${day}`;
}

function addDays(date: Date, days: number) {
  const next = new Date(date);
  next.setDate(next.getDate() + days);
  return next;
}

/**
 * The window as two inclusive civil dates, or null for "any time".
 *
 * Resolved at call time rather than when the preset was picked, so a list left
 * open overnight on "Today" means today, not yesterday.
 */
export function resolveRange(
  range: DateRange
): { from: string; to: string } | null {
  const today = new Date();
  today.setHours(0, 0, 0, 0);

  switch (range.preset) {
    case "all":
      return null;

    case "today":
      return { from: iso(today), to: iso(today) };

    case "yesterday": {
      const day = addDays(today, -1);
      return { from: iso(day), to: iso(day) };
    }

    case "last7":
      return { from: iso(addDays(today, -6)), to: iso(today) };
    case "last14":
      return { from: iso(addDays(today, -13)), to: iso(today) };
    case "last30":
      return { from: iso(addDays(today, -29)), to: iso(today) };
    case "last90":
      return { from: iso(addDays(today, -89)), to: iso(today) };

    case "thisMonth": {
      const start = new Date(today.getFullYear(), today.getMonth(), 1);
      const end = new Date(today.getFullYear(), today.getMonth() + 1, 0);
      return { from: iso(start), to: iso(end) };
    }

    case "lastMonth": {
      const start = new Date(today.getFullYear(), today.getMonth() - 1, 1);
      const end = new Date(today.getFullYear(), today.getMonth(), 0);
      return { from: iso(start), to: iso(end) };
    }

    case "thisQuarter": {
      const quarter = Math.floor(today.getMonth() / 3);
      const start = new Date(today.getFullYear(), quarter * 3, 1);
      const end = new Date(today.getFullYear(), quarter * 3 + 3, 0);
      return { from: iso(start), to: iso(end) };
    }

    case "thisYear":
      return {
        from: iso(new Date(today.getFullYear(), 0, 1)),
        to: iso(new Date(today.getFullYear(), 11, 31)),
      };

    case "next7":
      return { from: iso(today), to: iso(addDays(today, 6)) };
    case "next30":
      return { from: iso(today), to: iso(addDays(today, 29)) };

    case "custom": {
      if (!range.from && !range.to) return null;
      // A half-open custom range is still useful — "everything since March".
      return {
        from: range.from || "1900-01-01",
        to: range.to || iso(addDays(today, 365 * 10)),
      };
    }

    default:
      return null;
  }
}

/** The window as a filter node the server can compile, or null when unset. */
export function toDateFilterNode(range: DateRange): WireFilterNode | null {
  const resolved = resolveRange(range);
  if (!resolved || !range.field) return null;

  return {
    field: range.field,
    operator: "between",
    value: resolved.from,
    value2: resolved.to,
  };
}

/** Chip text, e.g. "Created · Last 7 days" or "Due · 1 Mar – 31 Mar". */
export function describeRange(range: DateRange, fieldLabel: string): string {
  if (range.preset === "all") return "";

  if (range.preset === "custom") {
    const resolved = resolveRange(range);
    if (!resolved) return "";
    const format = (value: string) =>
      new Date(`${value}T00:00:00`).toLocaleDateString("en-IN", {
        day: "numeric",
        month: "short",
        year: "numeric",
      });
    return `${fieldLabel} · ${range.from ? format(range.from) : "any"} – ${
      range.to ? format(range.to) : "any"
    }`;
  }

  return `${fieldLabel} · ${PRESET_LABELS[range.preset]}`;
}

export function isRangeActive(range: DateRange) {
  return resolveRange(range) !== null;
}
