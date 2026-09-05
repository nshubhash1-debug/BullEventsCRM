import type { UnitStatus } from "@/lib/inventory-api";

/**
 * One colour vocabulary for space status, shared by all three view modes.
 *
 * The board is read at a glance and often from across a desk, so the palette is
 * chosen for separation rather than prettiness: available is the only green,
 * confirmed is the only red, and everything in between is a distinct hue rather
 * than a shade of the same one. A legend that needs studying defeats the point.
 *
 * These describe the space's state <b>for the date being looked at</b>, not for
 * all time — a hall free in March and taken in April is one space with two
 * answers, and the board asks the question one date at a time.
 *
 * Every entry also carries a `dot` and a `text` class, because the list and the
 * detail panel need the same meaning without a filled tile.
 */
export interface StatusStyle {
  label: string;
  /** Filled tile — block and building views. */
  tile: string;
  /** Small indicator dot — list rows and headings. */
  dot: string;
  /** Text colour on a plain background. */
  text: string;
  /** Soft chip background for counts and filters. */
  chip: string;
  description: string;
}

export const STATUS_STYLES: Record<UnitStatus, StatusStyle> = {
  Available: {
    label: "Available",
    tile: "bg-emerald-500/15 text-emerald-800 border-emerald-500/40 hover:bg-emerald-500/25 dark:text-emerald-200",
    dot: "bg-emerald-500",
    text: "text-emerald-700 dark:text-emerald-400",
    chip: "bg-emerald-500/10 text-emerald-700 border-emerald-500/30 dark:text-emerald-300",
    description: "Free on this date",
  },
  Held: {
    label: "Held",
    tile: "bg-amber-500/20 text-amber-900 border-amber-500/50 hover:bg-amber-500/30 dark:text-amber-100",
    dot: "bg-amber-500",
    text: "text-amber-700 dark:text-amber-400",
    chip: "bg-amber-500/10 text-amber-700 border-amber-500/30 dark:text-amber-300",
    description: "Soft lock — expires on its own",
  },
  Blocked: {
    label: "Blocked",
    tile: "bg-violet-500/20 text-violet-900 border-violet-500/45 hover:bg-violet-500/30 dark:text-violet-100",
    dot: "bg-violet-500",
    text: "text-violet-700 dark:text-violet-400",
    chip: "bg-violet-500/10 text-violet-700 border-violet-500/30 dark:text-violet-300",
    description: "Off the calendar by management",
  },
  Booked: {
    label: "Booked",
    tile: "bg-sky-500/20 text-sky-900 border-sky-500/45 hover:bg-sky-500/30 dark:text-sky-100",
    dot: "bg-sky-500",
    text: "text-sky-700 dark:text-sky-400",
    chip: "bg-sky-500/10 text-sky-700 border-sky-500/30 dark:text-sky-300",
    description: "Committed to a client",
  },
  Sold: {
    label: "Confirmed",
    tile: "bg-rose-500/20 text-rose-900 border-rose-500/45 hover:bg-rose-500/30 dark:text-rose-100",
    dot: "bg-rose-500",
    text: "text-rose-700 dark:text-rose-400",
    chip: "bg-rose-500/10 text-rose-700 border-rose-500/30 dark:text-rose-300",
    description: "Contract signed and paid",
  },
  NotForSale: {
    label: "Not bookable",
    tile: "bg-muted text-muted-foreground border-border hover:bg-muted/70",
    dot: "bg-zinc-400",
    text: "text-muted-foreground",
    chip: "bg-muted text-muted-foreground border-border",
    description: "Never released — house use, storage, staff areas",
  },
  Blackout: {
    label: "Blackout",
    tile: "bg-zinc-700/20 text-zinc-900 border-zinc-500/45 hover:bg-zinc-700/30 dark:text-zinc-100",
    dot: "bg-zinc-600",
    text: "text-zinc-700 dark:text-zinc-300",
    chip: "bg-zinc-500/10 text-zinc-700 border-zinc-500/30 dark:text-zinc-300",
    description: "Ops close — maintenance, weather, festival",
  },
};

export function statusStyle(status: string): StatusStyle {
  return STATUS_STYLES[status as UnitStatus] ?? STATUS_STYLES.NotForSale;
}

/** Statuses a planner can still quote against on this date. */
export const SELLABLE: UnitStatus[] = ["Available", "Held"];

/**
 * Where each status can go next.
 *
 * Encoded here rather than as a flat list of every status so the action menu
 * cannot offer a move the server will reject — releasing an available unit, or
 * booking one that is already sold.
 */
export const NEXT_STATUSES: Record<UnitStatus, UnitStatus[]> = {
  Available: ["Held", "Blocked", "Booked", "Blackout", "NotForSale"],
  Held: ["Available", "Blocked", "Booked", "Blackout"],
  Blocked: ["Available", "Booked", "NotForSale"],
  Booked: ["Sold", "Available", "Held"],
  Sold: ["Booked"],
  NotForSale: ["Available", "Blocked"],
  Blackout: ["Available", "Held"],
};

/** Moves that stop for a manager unless the user is one. */
export const NEEDS_APPROVAL: UnitStatus[] = ["Blocked", "Booked"];
