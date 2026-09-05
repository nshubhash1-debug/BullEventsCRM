"use client";

import * as React from "react";
import { CalendarRange, Search } from "lucide-react";

import { PropThumb } from "@/components/props/prop-thumb";
import { PagePanel } from "@/components/shell/page-panel";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import {
  Tooltip,
  TooltipContent,
  TooltipTrigger,
} from "@/components/ui/tooltip";
import {
  propsApi,
  type PropAvailability,
  type PropCalendarRow,
  type PropCategory,
} from "@/lib/props-api";
import { cn } from "@/lib/utils";

/** Matches the day-strip endpoint's own ceiling on rows. */
const CALENDAR_LIMIT = 100;

function isoToday(offsetDays = 0) {
  const date = new Date();
  date.setDate(date.getDate() + offsetDays);
  return date.toISOString().slice(0, 10);
}

/** How full a day's cell reads, from free through to nothing left. */
function loadTone(available: number, stock: number) {
  if (stock === 0) return "bg-muted text-muted-foreground";
  if (available <= 0) return "bg-rose-100 text-rose-800 dark:bg-rose-950/50 dark:text-rose-300";
  if (available < stock * 0.25) {
    return "bg-amber-100 text-amber-800 dark:bg-amber-950/50 dark:text-amber-300";
  }
  if (available < stock) {
    return "bg-sky-50 text-sky-800 dark:bg-sky-950/40 dark:text-sky-300";
  }
  return "bg-emerald-50 text-emerald-700 dark:bg-emerald-950/30 dark:text-emerald-400";
}

export default function PropAvailabilityPage() {
  const [categories, setCategories] = React.useState<PropCategory[]>([]);
  const [categoryId, setCategoryId] = React.useState<number | null>(null);
  const [from, setFrom] = React.useState(() => isoToday());
  const [to, setTo] = React.useState(() => isoToday(6));
  const [search, setSearch] = React.useState("");

  const [rows, setRows] = React.useState<PropAvailability[]>([]);
  const [calendar, setCalendar] = React.useState<PropCalendarRow[]>([]);
  const [error, setError] = React.useState<string | null>(null);

  React.useEffect(() => {
    propsApi.categories().then(setCategories).catch(() => setCategories([]));
  }, []);

  /**
   * The dates and category the results on screen were fetched for.
   *
   * Pressing Check bumps <c>run</c>; the effect below does the fetching and is
   * the only thing that writes results, which keeps the loading flag derived
   * rather than toggled by hand in three places.
   *
   * It runs once on arrival too, so the page opens on this week instead of on
   * an empty state that has to be asked for.
   */
  const [run, setRun] = React.useState({ from: isoToday(), to: isoToday(6), categoryId: null as number | null });
  const [loadedKey, setLoadedKey] = React.useState<string | null>(null);

  const queryKey = JSON.stringify(run);
  const loading = loadedKey !== queryKey;

  React.useEffect(() => {
    let cancelled = false;

    const request = {
      from: run.from,
      to: run.to,
      categoryId: run.categoryId ?? undefined,
    };

    propsApi
      .availability(request)
      .then((availability) => {
        if (cancelled) return;
        setError(null);
        setRows(availability);
      })
      .catch(() => {
        if (cancelled) return;
        setError("Could not read availability for those dates.");
        setRows([]);
      })
      .finally(() => {
        if (!cancelled) setLoadedKey(queryKey);
      });

    return () => {
      cancelled = true;
    };
  }, [run, queryKey]);

  /**
   * The rows actually on screen.
   *
   * Capped at the same hundred the day-strip endpoint will draw, so every
   * visible row has a strip — the summary call answers for up to five hundred
   * items, and letting the table show rows the calendar was never asked about
   * left a header full of dates above a run of empty cells.
   */
  const visible = React.useMemo(() => {
    const needle = search.trim().toLowerCase();
    const filtered = needle
      ? rows.filter(
          (r) =>
            r.itemName.toLowerCase().includes(needle) ||
            r.itemCode.toLowerCase().includes(needle)
        )
      : rows;

    // Anything under pressure first — a planner opens this page to find what
    // they cannot have, not to admire what is sitting free.
    return [...filtered]
      .sort((a, b) => {
        if (a.reservedQuantity !== b.reservedQuantity) {
          return b.reservedQuantity - a.reservedQuantity;
        }
        return a.itemName.localeCompare(b.itemName);
      })
      .slice(0, CALENDAR_LIMIT);
  }, [rows, search]);

  /*
   * The day strip follows the visible rows rather than the whole result, which
   * is what keeps the two in step as the filter box narrows things down.
   */
  const visibleIds = visible.map((r) => r.propItemId).join(",");

  React.useEffect(() => {
    let cancelled = false;

    // Nothing on screen means nothing to ask about. The previous strip is left
    // alone rather than cleared — no row renders it, and the header below is
    // gated on there being visible rows.
    if (!visibleIds) return;

    propsApi
      .calendar({
        from: run.from,
        to: run.to,
        itemIds: visibleIds.split(",").map(Number),
      })
      .then((strip) => !cancelled && setCalendar(strip))
      .catch(() => !cancelled && setCalendar([]));

    return () => {
      cancelled = true;
    };
  }, [visibleIds, run.from, run.to]);

  const calendarById = React.useMemo(
    () => new Map(calendar.map((c) => [c.propItemId, c])),
    [calendar]
  );

  const dates = visible.length > 0 ? calendar[0]?.days.map((d) => d.date) ?? [] : [];
  const contested = rows.filter((r) => r.reservedQuantity > 0).length;

  return (
    <PagePanel
      icon={CalendarRange}
      title="Prop availability"
      hint="What the godown can actually promise across a date window — dispatch day to return day, not just the event date."
      toolbar={
        <div className="flex flex-wrap items-end gap-2">
          <div>
            <Label className="text-[11px] text-muted-foreground">Dispatch</Label>
            <Input
              type="date"
              className="mt-1 h-9 w-40"
              value={from}
              onChange={(e) => setFrom(e.target.value)}
            />
          </div>
          <div>
            <Label className="text-[11px] text-muted-foreground">Back by</Label>
            <Input
              type="date"
              className="mt-1 h-9 w-40"
              value={to}
              onChange={(e) => setTo(e.target.value)}
            />
          </div>
          <div>
            <Label className="text-[11px] text-muted-foreground">Category</Label>
            <select
              className="mt-1 h-9 w-52 rounded-md border bg-background px-2 text-sm"
              value={categoryId ?? ""}
              onChange={(e) => setCategoryId(e.target.value ? Number(e.target.value) : null)}
            >
              <option value="">All categories</option>
              {categories.map((c) => (
                <option key={c.id} value={c.id}>
                  {c.name}
                </option>
              ))}
            </select>
          </div>

          <Button
            className="h-9"
            disabled={loading}
            onClick={() => setRun({ from, to, categoryId })}
          >
            {loading ? "Checking…" : "Check"}
          </Button>

          <div className="relative">
            <Search className="pointer-events-none absolute left-2.5 top-1/2 size-3.5 -translate-y-1/2 text-muted-foreground" />
            <Input
              className="h-9 w-52 pl-8"
              placeholder="Filter these results"
              value={search}
              onChange={(e) => setSearch(e.target.value)}
            />
          </div>
        </div>
      }
      subToolbar={
        <span className="text-[12px] text-muted-foreground">
          {error
            ? error
            : `${visible.length} items${
                rows.length > visible.length
                  ? ` of ${rows.length} — narrow the search to see the rest`
                  : ""
              } · ${contested} with something already held over these dates`}
        </span>
      }
      flush
    >
      <div className="min-h-0 flex-1 overflow-auto">
        <table className="w-full text-[13px]">
          <thead className="sticky top-0 z-10 bg-card shadow-[0_1px_0_var(--border)]">
            <tr className="text-left text-[11px] uppercase tracking-wide text-muted-foreground">
              <th className="px-4 py-2 font-medium">Item</th>
              <th className="px-2 py-2 text-right font-medium">Stock</th>
              <th className="px-2 py-2 text-right font-medium">Held</th>
              <th className="px-2 py-2 text-right font-medium">Free</th>
              {dates.map((date) => (
                <th key={date} className="px-1 py-2 text-center font-medium">
                  {new Date(date).toLocaleDateString(undefined, {
                    day: "2-digit",
                    month: "short",
                  })}
                </th>
              ))}
            </tr>
          </thead>

          <tbody>
            {visible.map((row) => {
              const strip = calendarById.get(row.propItemId);

              return (
                <tr key={row.propItemId} className="border-b last:border-0 hover:bg-muted/40">
                  <td className="px-4 py-1.5">
                    <div className="flex items-center gap-2">
                      <PropThumb
                        src={row.primaryThumbnailUrl}
                        alt={row.itemName}
                        className="size-8 shrink-0"
                      />
                      <div className="min-w-0">
                        <div className="truncate font-medium">{row.itemName}</div>
                        <div className="font-mono text-[10.5px] text-muted-foreground">
                          {row.itemCode}
                        </div>
                      </div>
                    </div>
                  </td>

                  <td className="px-2 py-1.5 text-right tabular-nums text-muted-foreground">
                    {row.goodQuantity}
                  </td>

                  <td className="px-2 py-1.5 text-right tabular-nums">
                    {row.reservedQuantity > 0 ? (
                      <Tooltip>
                        <TooltipTrigger asChild>
                          <span className="cursor-help font-semibold text-amber-600 dark:text-amber-400">
                            {row.reservedQuantity}
                          </span>
                        </TooltipTrigger>
                        <TooltipContent className="max-w-72">
                          <ul className="space-y-1 text-[11px]">
                            {row.holds.map((h) => (
                              <li key={h.reservationId}>
                                <span className="font-medium">{h.quantity}</span>
                                {" · "}
                                {h.eventName ?? h.clientName ?? "Held"}
                                {" · "}
                                {h.fromDate.slice(5)} → {h.toDate.slice(5)}
                                {h.issueCode ? ` · ${h.issueCode}` : ""}
                              </li>
                            ))}
                          </ul>
                        </TooltipContent>
                      </Tooltip>
                    ) : (
                      <span className="text-muted-foreground">—</span>
                    )}
                  </td>

                  <td
                    className={cn(
                      "px-2 py-1.5 text-right font-semibold tabular-nums",
                      row.availableQuantity === 0
                        ? "text-rose-600 dark:text-rose-400"
                        : "text-emerald-600 dark:text-emerald-400"
                    )}
                  >
                    {row.availableQuantity}
                  </td>

                  {(strip?.days ?? []).map((day) => (
                    <td key={day.date} className="px-1 py-1.5 text-center">
                      <span
                        className={cn(
                          "inline-block min-w-8 rounded px-1 py-0.5 text-[11px] font-medium tabular-nums",
                          loadTone(day.available, row.goodQuantity)
                        )}
                      >
                        {day.available}
                      </span>
                    </td>
                  ))}
                </tr>
              );
            })}
          </tbody>
        </table>

        {!loading && visible.length === 0 ? (
          <p className="py-16 text-center text-[13px] text-muted-foreground">
            Nothing to show for those dates.
          </p>
        ) : null}
      </div>
    </PagePanel>
  );
}
