"use client";

import * as React from "react";
import Link from "next/link";
import {
  AlertTriangle,
  CalendarDays,
  ChevronLeft,
  ChevronRight,
  ExternalLink,
  LayoutDashboard,
  Loader2,
  MapPin,
  RefreshCw,
  User,
} from "lucide-react";

import {
  AgendaView,
  DayView,
  MiniMonth,
  MonthView,
  WeekView,
  step,
} from "@/components/calendar/calendar-views";
import { Badge } from "@/components/ui/badge";
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
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import {
  FOLLOW_UP_CHANNELS,
  SOURCE_META,
  calendarApi,
  eventColor,
  humanise,
  type CalendarEvent,
  type CalendarOwner,
  type CalendarResponse,
  type EventSource,
} from "@/lib/calendar/api";
import {
  addMonths,
  formatFullDate,
  formatTimeRange,
  rangeLabel,
  startOfDay,
  startOfMonth,
  visibleRange,
  type CalendarView,
} from "@/lib/calendar/dates";
import { usePersistedState } from "@/lib/persisted-store";
import { cn } from "@/lib/utils";

const VIEW_KEY = "brg.calendarView.v1";
const SOURCES: EventSource[] = ["siteVisit", "obmVisit", "followUp"];

const VIEWS: { value: CalendarView; label: string }[] = [
  { value: "month", label: "Month" },
  { value: "week", label: "Week" },
  { value: "day", label: "Day" },
  { value: "agenda", label: "Agenda" },
];

export default function CalendarPage() {
  const [view, setView] = usePersistedState<CalendarView>(VIEW_KEY, "month");
  const [anchor, setAnchor] = React.useState(() => startOfDay(new Date()));
  const [selected, setSelected] = React.useState<CalendarEvent | null>(null);

  const [owners, setOwners] = React.useState<CalendarOwner[]>([]);
  const [ownerId, setOwnerId] = React.useState<string>("all");
  const [sources, setSources] = React.useState<EventSource[]>(SOURCES);
  const [channels, setChannels] = React.useState<string[]>([]);
  const [refreshToken, setRefreshToken] = React.useState(0);

  const { from, to } = visibleRange(view, anchor);

  /* ---------------- data ---------------- */

  const signature = JSON.stringify({
    from: from.toDateString(),
    to: to.toDateString(),
    ownerId,
    sources,
    channels,
    refreshToken,
  });

  const [state, setState] = React.useState<{
    token: string | null;
    data: CalendarResponse | null;
    error: string | null;
  }>({ token: null, data: null, error: null });

  React.useEffect(() => {
    let live = true;
    const request = JSON.parse(signature) as { from: string; to: string };

    calendarApi
      .events({
        from: new Date(request.from),
        to: new Date(request.to),
        ownerId: ownerId === "all" ? null : Number(ownerId),
        sources,
        channels,
      })
      .then((data) => {
        if (live) setState({ token: signature, data, error: null });
      })
      .catch((cause: Error) => {
        if (live) setState({ token: signature, data: null, error: cause.message });
      });

    return () => {
      live = false;
    };
    // `signature` already encodes every input; listing them again would refetch
    // on identical values.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [signature]);

  React.useEffect(() => {
    let live = true;
    calendarApi
      .owners()
      .then((result) => {
        if (live) setOwners(result);
      })
      .catch(() => {
        /* the owner filter simply stays empty */
      });
    return () => {
      live = false;
    };
  }, []);

  const loading = state.token !== signature;
  const data = state.data;
  const events = React.useMemo(() => data?.events ?? [], [data]);

  /** Day → count, for the mini month's density dots. */
  const counts = React.useMemo(() => {
    const map = new Map<string, number>();
    for (const event of events) {
      const key = startOfDay(new Date(event.start)).toISOString();
      map.set(key, (map.get(key) ?? 0) + 1);
    }
    return map;
  }, [events]);

  function toggleSource(source: EventSource) {
    setSources((current) => {
      const next = current.includes(source)
        ? current.filter((entry) => entry !== source)
        : [...current, source];
      // Turning everything off would read as "the calendar is broken" rather
      // than "you filtered everything out".
      return next.length === 0 ? current : next;
    });
  }

  const viewProps = {
    anchor,
    events,
    onSelect: setSelected,
    onPickDay: (day: Date) => {
      setAnchor(day);
      if (view === "month") setView("day");
    },
  };

  return (
    <div className="flex flex-col gap-2.5">
      {/* Header */}
      <div className="overflow-hidden rounded-md border bg-card shadow-xs">
        <div className="flex flex-wrap items-center justify-between gap-2 px-3.5 py-2">
          <div className="flex items-center gap-5">
            <Link
              href="/dashboard"
              className="flex items-center gap-1.5 text-[17px] font-semibold tracking-tight text-muted-foreground transition-colors hover:text-foreground"
            >
              <LayoutDashboard className="size-[18px]" />
              Dashboard
            </Link>
            <span className="flex items-center gap-1.5 text-[17px] font-semibold tracking-tight text-primary">
              <CalendarDays className="size-[18px]" />
              Calendar
            </span>
          </div>

          <div className="flex items-center gap-1.5">
            <Button
              variant="outline"
              size="sm"
              className="h-8"
              onClick={() => setRefreshToken((token) => token + 1)}
            >
              <RefreshCw /> Refresh
            </Button>
          </div>
        </div>
      </div>

      {/* Toolbar */}
      <div className="flex flex-wrap items-center gap-2 rounded-md border bg-card px-3 py-2 shadow-xs">
        <div className="flex items-center gap-1">
          <Button
            variant="outline"
            size="icon"
            aria-label="Previous"
            className="size-8"
            onClick={() => setAnchor(step(view, anchor, -1))}
          >
            <ChevronLeft className="size-4" />
          </Button>
          <Button
            variant="outline"
            size="sm"
            className="h-8"
            onClick={() => setAnchor(startOfDay(new Date()))}
          >
            Today
          </Button>
          <Button
            variant="outline"
            size="icon"
            aria-label="Next"
            className="size-8"
            onClick={() => setAnchor(step(view, anchor, 1))}
          >
            <ChevronRight className="size-4" />
          </Button>
        </div>

        <span className="min-w-[190px] text-[14px] font-semibold tracking-tight">
          {rangeLabel(view, anchor)}
        </span>

        <div className="flex overflow-hidden rounded border">
          {VIEWS.map((entry) => (
            <button
              key={entry.value}
              type="button"
              onClick={() => setView(entry.value)}
              className={cn(
                "px-2.5 py-1 text-[12.5px] transition-colors",
                view === entry.value
                  ? "bg-primary font-medium text-primary-foreground"
                  : "bg-card hover:bg-accent"
              )}
            >
              {entry.label}
            </button>
          ))}
        </div>

        {/* Source chips double as the legend */}
        <div className="flex flex-wrap items-center gap-1">
          {SOURCES.map((source) => {
            const active = sources.includes(source);
            const meta = SOURCE_META[source];
            const count =
              source === "siteVisit"
                ? data?.summary.siteVisits
                : source === "obmVisit"
                  ? data?.summary.obmVisits
                  : data?.summary.followUps;

            return (
              <button
                key={source}
                type="button"
                onClick={() => toggleSource(source)}
                className={cn(
                  "flex items-center gap-1.5 rounded-full border px-2.5 py-1 text-[12px] transition-colors",
                  active ? "bg-accent" : "opacity-50 hover:opacity-80"
                )}
              >
                <span
                  className="size-2 rounded-full"
                  style={{ backgroundColor: meta.color }}
                />
                {meta.label}
                {active && count !== undefined ? (
                  <span className="tabular-nums text-muted-foreground">{count}</span>
                ) : null}
              </button>
            );
          })}
        </div>

        <div className="ml-auto flex items-center gap-1.5">
          {data && data.summary.overdue > 0 ? (
            <Badge
              variant="outline"
              className="h-7 gap-1 border-destructive/40 font-normal text-destructive"
            >
              <AlertTriangle className="size-3" />
              {data.summary.overdue} overdue
            </Badge>
          ) : null}

          <Select value={ownerId} onValueChange={setOwnerId}>
            <SelectTrigger className="h-8 w-44 text-[13px]">
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="all">Everyone</SelectItem>
              {owners.map((owner) => (
                <SelectItem key={owner.id} value={String(owner.id)}>
                  {owner.name}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>

          <Select
            value={channels.length === 1 ? channels[0] : "all"}
            onValueChange={(value) => setChannels(value === "all" ? [] : [value])}
          >
            <SelectTrigger className="h-8 w-40 text-[13px]">
              <SelectValue placeholder="All channels" />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="all">All channels</SelectItem>
              {FOLLOW_UP_CHANNELS.map((channel) => (
                <SelectItem key={channel} value={channel}>
                  {humanise(channel)}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </div>
      </div>

      {/* Body */}
      {state.error ? (
        <div className="flex flex-col items-center justify-center gap-2 rounded-md border border-dashed bg-card py-16 text-center">
          <AlertTriangle className="size-5 text-destructive" />
          <p className="text-sm font-medium">Could not load the calendar</p>
          <p className="text-[13px] text-muted-foreground">{state.error}</p>
        </div>
      ) : (
        <div className="flex gap-2.5">
          <div className={cn("min-w-0 flex-1", loading && "opacity-60 transition-opacity")}>
            {view === "month" ? <MonthView {...viewProps} /> : null}
            {view === "week" ? <WeekView {...viewProps} /> : null}
            {view === "day" ? <DayView {...viewProps} /> : null}
            {view === "agenda" ? <AgendaView {...viewProps} /> : null}
          </div>

          {/* Sidebar — hidden where it would squeeze the grid */}
          <aside className="hidden w-56 shrink-0 flex-col gap-2.5 xl:flex">
            <div className="rounded-md border bg-card p-2.5 shadow-xs">
              <div className="flex items-center justify-between pb-1.5">
                <span className="text-[12px] font-semibold">
                  {startOfMonth(anchor).toLocaleDateString("en-IN", {
                    month: "long",
                    year: "numeric",
                  })}
                </span>
                <span className="flex gap-0.5">
                  <button
                    type="button"
                    aria-label="Previous month"
                    onClick={() => setAnchor(addMonths(anchor, -1))}
                    className="rounded p-0.5 text-muted-foreground hover:bg-accent"
                  >
                    <ChevronLeft className="size-3.5" />
                  </button>
                  <button
                    type="button"
                    aria-label="Next month"
                    onClick={() => setAnchor(addMonths(anchor, 1))}
                    className="rounded p-0.5 text-muted-foreground hover:bg-accent"
                  >
                    <ChevronRight className="size-3.5" />
                  </button>
                </span>
              </div>
              <MiniMonth
                anchor={anchor}
                selected={anchor}
                counts={counts}
                onPick={(day) => setAnchor(day)}
              />
            </div>

            {data ? (
              <div className="flex flex-col gap-1.5 rounded-md border bg-card p-2.5 text-[12px] shadow-xs">
                <span className="text-[11px] font-semibold tracking-wide text-muted-foreground uppercase">
                  In this window
                </span>
                <Row label="Scheduled" value={data.summary.total} />
                <Row label="Completed" value={data.summary.completed} />
                <Row
                  label="Overdue"
                  value={data.summary.overdue}
                  tone={data.summary.overdue > 0 ? "bad" : undefined}
                />
              </div>
            ) : null}
          </aside>
        </div>
      )}

      {loading && !data ? (
        <div className="flex items-center justify-center py-10">
          <Loader2 className="size-4 animate-spin text-muted-foreground" />
        </div>
      ) : null}

      <EventDialog event={selected} onOpenChange={() => setSelected(null)} />
    </div>
  );
}

function Row({
  label,
  value,
  tone,
}: {
  label: string;
  value: number;
  tone?: "bad";
}) {
  return (
    <span className="flex items-center justify-between">
      <span className="text-muted-foreground">{label}</span>
      <span
        className={cn(
          "font-medium tabular-nums",
          tone === "bad" && "text-destructive"
        )}
      >
        {value}
      </span>
    </span>
  );
}

/* ------------------------------------------------------------------ *
 * Event detail
 * ------------------------------------------------------------------ */

function EventDialog({
  event,
  onOpenChange,
}: {
  event: CalendarEvent | null;
  onOpenChange: (open: boolean) => void;
}) {
  if (!event) return null;

  const start = new Date(event.start);
  const end = new Date(event.end);

  return (
    <Dialog open onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-md">
        <DialogHeader>
          <div className="flex items-center gap-2">
            <span
              className="size-2.5 shrink-0 rounded-full"
              style={{ backgroundColor: eventColor(event) }}
            />
            <Badge
              variant="outline"
              className={cn("h-5 px-1.5 text-[10px] font-normal", SOURCE_META[event.source].tint)}
            >
              {SOURCE_META[event.source].label}
            </Badge>
            {event.isOverdue ? (
              <Badge
                variant="outline"
                className="h-5 gap-1 border-destructive/40 px-1.5 text-[10px] font-normal text-destructive"
              >
                <AlertTriangle className="size-3" /> Overdue
              </Badge>
            ) : null}
          </div>
          <DialogTitle className="text-base">{event.title}</DialogTitle>
          {event.subtitle ? (
            <DialogDescription>{event.subtitle}</DialogDescription>
          ) : null}
        </DialogHeader>

        <dl className="grid grid-cols-[88px_1fr] gap-x-3 gap-y-2 text-[13px]">
          <Detail label="When">
            {formatFullDate(start)}
            <span className="block text-muted-foreground">
              {event.isPoint ? `Due ${formatTimeRange(start, null)}` : formatTimeRange(start, end)}
            </span>
          </Detail>

          <Detail label="Status">
            {humanise(event.status)}
            {event.kind ? (
              <span className="text-muted-foreground"> · {humanise(event.kind)}</span>
            ) : null}
          </Detail>

          <Detail label="Owner">
            <span className="flex items-center gap-1.5">
              <User className="size-3.5 text-muted-foreground" />
              {event.ownerName ?? "Unassigned"}
            </span>
          </Detail>

          {event.branchName ? (
            <Detail label="Branch">{event.branchName}</Detail>
          ) : null}

          {event.location ? (
            <Detail label="Location">
              <span className="flex items-center gap-1.5">
                <MapPin className="size-3.5 text-muted-foreground" />
                {event.location}
              </span>
            </Detail>
          ) : null}

          {event.priority ? (
            <Detail label="Priority">{humanise(event.priority)}</Detail>
          ) : null}
        </dl>

        <DialogFooter>
          <Button variant="outline" onClick={() => onOpenChange(false)}>
            Close
          </Button>
          <Button asChild>
            <Link href={event.url}>
              <ExternalLink /> Open record
            </Link>
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

function Detail({
  label,
  children,
}: {
  label: string;
  children: React.ReactNode;
}) {
  return (
    <>
      <dt className="text-[12px] text-muted-foreground">{label}</dt>
      <dd className="min-w-0">{children}</dd>
    </>
  );
}
