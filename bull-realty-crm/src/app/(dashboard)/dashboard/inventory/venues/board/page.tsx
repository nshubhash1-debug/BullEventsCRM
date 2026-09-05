"use client";

import * as React from "react";
import Link from "next/link";
import {
  Building2,
  LayoutGrid,
  List,
  RefreshCw,
  Search,
  ShieldCheck,
  Tag,
} from "lucide-react";
import { toast } from "sonner";

import {
  BlockView,
  BuildingView,
  ListView,
} from "@/components/inventory/board-views";
import { QuoteBuilder } from "@/components/inventory/quote-builder";
import { STATUS_STYLES } from "@/components/inventory/status";
import { UnitSheet } from "@/components/inventory/unit-sheet";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { Skeleton } from "@/components/ui/skeleton";
import { ToggleGroup, ToggleGroupItem } from "@/components/ui/toggle-group";
import { usePersistedState } from "@/lib/persisted-store";
import { ApiError } from "@/lib/api";
import {
  approvalsApi,
  boardApi,
  EVENT_SLOTS,
  formatCompactRupees,
  formatIndian,
  formatRupees,
  UNIT_STATUSES,
  type ApprovalSummary,
  type BoardUnit,
  type EventSlot,
  type InventoryBoard,
  type UnitStatus,
} from "@/lib/inventory-api";
import { inventoryApi, type ProjectRow } from "@/lib/crm-api";
import { cn } from "@/lib/utils";

type ViewMode = "block" | "list" | "building";

function todayIso() {
  const d = new Date();
  const month = String(d.getMonth() + 1).padStart(2, "0");
  const day = String(d.getDate()).padStart(2, "0");
  return `${d.getFullYear()}-${month}-${day}`;
}

/**
 * Venue availability board — date and slot first.
 *
 * Colours come from SpaceBooking for the selected session, not from a permanent
 * "sold" flag on the hall. Block / list / levels are three readings of that day.
 */
export default function InventoryBoardPage() {
  const [projects, setProjects] = React.useState<ProjectRow[] | null>(null);
  const [projectId, setProjectId] = React.useState<string>("");
  const [towerId, setTowerId] = React.useState<string>("all");
  const [eventDate, setEventDate] = React.useState(todayIso);
  const [slot, setSlot] = React.useState<EventSlot>("Evening");

  const [board, setBoard] = React.useState<InventoryBoard | null>(null);
  const [loading, setLoading] = React.useState(true);
  const [approvals, setApprovals] = React.useState<ApprovalSummary | null>(null);

  const [mode, setMode] = usePersistedState<ViewMode>("brg.inventory.view", "block");

  const [search, setSearch] = React.useState("");
  const [statusFilter, setStatusFilter] = React.useState<Set<UnitStatus>>(new Set());

  const [selected, setSelected] = React.useState<BoardUnit | null>(null);
  const [sheetOpen, setSheetOpen] = React.useState(false);
  const [quoteOpen, setQuoteOpen] = React.useState(false);

  /* ---------------- data ---------------- */

  React.useEffect(() => {
    inventoryApi
      .projects()
      .then((loaded) => {
        setProjects(loaded);

        // Prefer a venue that actually has spaces; fall back to the first.
        const preferred =
          loaded.find((p) => p.availableUnits > 0 || p.totalUnits > 0) ?? loaded[0];
        if (preferred) setProjectId(String(preferred.id));
      })
      .catch(() => setProjects([]));

    approvalsApi.summary().then(setApprovals).catch(() => setApprovals(null));
  }, []);

  // A counter rather than a callback the effect calls: the effect may only
  // touch state asynchronously, so "start loading" belongs to the event that
  // asks for a reload, not to the effect that performs it.
  const [reloadToken, setReloadToken] = React.useState(0);

  React.useEffect(() => {
    if (!projectId || !eventDate) return;

    let cancelled = false;

    boardApi
      .board(Number(projectId), {
        towerId: towerId === "all" ? null : Number(towerId),
        eventDate,
        slot,
      })
      .then((loaded) => !cancelled && setBoard(loaded))
      .catch((error) => {
        if (cancelled) return;

        setBoard(null);
        toast.error("Could not load the board", {
          description: error instanceof ApiError ? error.message : "Network error.",
        });
      })
      .finally(() => !cancelled && setLoading(false));

    return () => {
      cancelled = true;
    };
  }, [projectId, towerId, eventDate, slot, reloadToken]);

  const refresh = React.useCallback(() => {
    setLoading(true);
    setReloadToken((token) => token + 1);
    approvalsApi.summary().then(setApprovals).catch(() => {});
  }, []);

  /* ---------------- filtering ---------------- */

  const floors = React.useMemo(() => {
    if (!board) return [];

    const term = search.trim().toLowerCase();

    return board.floors
      .map((floor) => {
        const units = floor.units.filter((unit) => {
          if (statusFilter.size > 0 && !statusFilter.has(unit.status)) return false;
          if (term === "") return true;

          return (
            unit.unitNumber.toLowerCase().includes(term) ||
            (unit.customerName?.toLowerCase().includes(term) ?? false) ||
            (unit.salesPersonName?.toLowerCase().includes(term) ?? false)
          );
        });

        return { ...floor, units };
      })
      .filter((floor) => floor.units.length > 0);
  }, [board, search, statusFilter]);

  const shownCount = floors.reduce((sum, floor) => sum + floor.units.length, 0);
  const filtering = statusFilter.size > 0 || search.trim() !== "";

  function toggleStatus(status: UnitStatus) {
    const next = new Set(statusFilter);
    if (next.has(status)) next.delete(status);
    else next.add(status);
    setStatusFilter(next);
  }

  function openUnit(unit: BoardUnit) {
    setSelected(unit);
    setSheetOpen(true);
  }

  /* ---------------- render ---------------- */

  const View = mode === "list" ? ListView : mode === "building" ? BuildingView : BlockView;

  return (
    <div className="flex min-h-0 flex-1 flex-col gap-3 p-4">
      {/* ---------------- header ---------------- */}

      <header className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <h1 className="flex items-center gap-2 text-lg font-semibold">
            <Building2 className="size-4.5 text-primary" />
            {board?.projectName ?? "Venue board"}
            {board?.towerName ? (
              <span className="text-muted-foreground">· {board.towerName}</span>
            ) : null}
          </h1>

          <p className="text-[12.5px] text-muted-foreground">
            {board?.address
              ? `${board.address} · availability for ${eventDate} · ${slot}`
              : `Space availability for ${eventDate} · ${slot}`}
          </p>
        </div>

        <div className="flex flex-wrap items-center gap-2">
          {board?.activeRateCard ? (
            <span className="inline-flex h-8 items-center gap-1.5 rounded border px-2.5 text-[12px]">
              <Tag className="size-3.5 text-primary" />
              {board.activeRateCard.label}
            </span>
          ) : null}

          {approvals && approvals.pending > 0 ? (
            <Button asChild variant="outline" size="sm" className="h-8">
              <Link href="/dashboard/sales/approvals">
                <ShieldCheck className="size-3.5" />
                {approvals.pending} pending
                {approvals.overdue > 0 ? (
                  <span className="text-amber-600 dark:text-amber-400">
                    · {approvals.overdue} overdue
                  </span>
                ) : null}
              </Link>
            </Button>
          ) : null}

          <Button variant="outline" size="sm" className="h-8" onClick={refresh}>
            <RefreshCw className={cn("size-3.5", loading && "animate-spin")} />
            Refresh
          </Button>
        </div>
      </header>

      {/* ---------------- stats ---------------- */}

      {board ? (
        <div className="grid grid-cols-2 gap-2 sm:grid-cols-3 lg:grid-cols-6">
          <Stat label="Spaces" value={formatIndian(board.stats.total)} />
          <Stat
            label="Free this slot"
            value={formatIndian(board.stats.available)}
            hint={formatCompactRupees(board.stats.availableValue)}
            tone="text-emerald-600 dark:text-emerald-400"
          />
          <Stat label="Held" value={formatIndian(board.stats.held)} tone="text-amber-600 dark:text-amber-400" />
          <Stat label="Blocked" value={formatIndian(board.stats.blocked)} tone="text-violet-600 dark:text-violet-400" />
          <Stat
            label="Booked / confirmed"
            value={formatIndian(board.stats.booked + board.stats.sold)}
            hint={formatCompactRupees(board.stats.soldValue)}
            tone="text-rose-600 dark:text-rose-400"
          />
          <Stat
            label="Taken"
            value={`${board.stats.soldPercent}%`}
            hint="of spaces on this date"
          />
        </div>
      ) : (
        <Skeleton className="h-16 w-full" />
      )}

      {/* ---------------- controls ---------------- */}

      <div className="flex flex-wrap items-center gap-2 rounded-lg border bg-card p-2">
        <Input
          type="date"
          value={eventDate}
          onChange={(event) => {
            setLoading(true);
            setEventDate(event.target.value);
          }}
          className="h-8 w-[10.5rem] text-[12.5px]"
          aria-label="Event date"
        />

        <Select
          value={slot}
          onValueChange={(value) => {
            setLoading(true);
            setSlot(value as EventSlot);
          }}
        >
          <SelectTrigger size="sm" className="w-36 text-[12.5px]">
            <SelectValue placeholder="Slot" />
          </SelectTrigger>
          <SelectContent>
            {EVENT_SLOTS.map((option) => (
              <SelectItem key={option} value={option}>
                {option === "FullDay" ? "Full day" : option}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>

        <Select value={projectId} onValueChange={setProjectId}>
          <SelectTrigger size="sm" className="w-48 text-[12.5px]">
            <SelectValue placeholder="Venue" />
          </SelectTrigger>
          <SelectContent>
            {(projects ?? []).map((project) => (
              <SelectItem key={project.id} value={String(project.id)}>
                {project.name}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>

        {board && board.towers.length > 0 ? (
          <Select value={towerId} onValueChange={setTowerId}>
            <SelectTrigger size="sm" className="w-40 text-[12.5px]">
              <SelectValue placeholder="Block" />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="all">All blocks</SelectItem>
              {board.towers.map((tower) => (
                <SelectItem key={tower.id} value={String(tower.id)}>
                  {tower.name} · {tower.unitCount} spaces
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        ) : null}

        <div className="relative">
          <Search className="pointer-events-none absolute top-1/2 left-2 size-3.5 -translate-y-1/2 text-muted-foreground" />
          <Input
            value={search}
            onChange={(event) => setSearch(event.target.value)}
            placeholder="Space, client…"
            className="h-8 w-56 pl-7 text-[12.5px]"
          />
        </div>

        {/* Status chips double as the legend — one row that both explains the
            colours and filters by them, rather than a legend nobody clicks. */}
        <div className="flex flex-wrap items-center gap-1">
          {UNIT_STATUSES.map((status) => {
            const style = STATUS_STYLES[status];
            const active = statusFilter.has(status);
            const count = board?.stats[
              status === "NotForSale"
                ? "notForSale"
                : (status.toLowerCase() as "available" | "held" | "blocked" | "booked" | "sold")
            ];

            return (
              <button
                key={status}
                type="button"
                onClick={() => toggleStatus(status)}
                className={cn(
                  "inline-flex items-center gap-1.5 rounded-full border px-2 py-1 text-[11px] transition-colors",
                  active ? style.chip : "border-transparent hover:bg-muted",
                  active && "ring-1 ring-current"
                )}
              >
                <span className={cn("size-1.5 rounded-full", style.dot)} />
                {style.label}
                <span className="tabular-nums opacity-70">{count ?? 0}</span>
              </button>
            );
          })}
        </div>

        <ToggleGroup
          type="single"
          value={mode}
          onValueChange={(value) => value && setMode(value as ViewMode)}
          variant="outline"
          size="sm"
          className="ml-auto"
        >
          <ToggleGroupItem value="block" aria-label="Grid view" className="gap-1.5 px-2.5 text-[12px]">
            <LayoutGrid className="size-3.5" /> Grid
          </ToggleGroupItem>
          <ToggleGroupItem value="list" aria-label="List view" className="gap-1.5 px-2.5 text-[12px]">
            <List className="size-3.5" /> List
          </ToggleGroupItem>
          <ToggleGroupItem value="building" aria-label="Levels view" className="gap-1.5 px-2.5 text-[12px]">
            <Building2 className="size-3.5" /> Levels
          </ToggleGroupItem>
        </ToggleGroup>
      </div>

      {/* ---------------- board ---------------- */}

      <div className="min-h-0 flex-1 overflow-auto rounded-lg border bg-card">
        {loading && board === null ? (
          <div className="flex flex-col gap-2 p-4">
            {Array.from({ length: 8 }, (_, i) => (
              <Skeleton key={i} className="h-14 w-full" />
            ))}
          </div>
        ) : floors.length === 0 ? (
          <div className="flex h-56 flex-col items-center justify-center gap-1 text-center">
            <p className="text-[13px] text-muted-foreground">
              {filtering ? "No units match these filters." : "This project has no units yet."}
            </p>
            {filtering ? (
              <Button
                variant="ghost"
                size="sm"
                onClick={() => {
                  setSearch("");
                  setStatusFilter(new Set());
                }}
              >
                Clear filters
              </Button>
            ) : null}
          </div>
        ) : (
          <View floors={floors} selectedId={selected?.id} onSelect={openUnit} />
        )}
      </div>

      {filtering && board ? (
        <p className="text-[11.5px] text-muted-foreground tabular-nums">
          Showing {formatIndian(shownCount)} of {formatIndian(board.stats.total)} units.
        </p>
      ) : null}

      {/* ---------------- panels ---------------- */}

      {/* Both panels remount when the selection changes, which is what resets
          their forms. The keys have to be namespaced: they are siblings, so a
          bare unit id would collide between the two. */}
      <UnitSheet
        key={`unit-${selected?.id ?? "none"}`}
        unit={selected}
        open={sheetOpen}
        onOpenChange={setSheetOpen}
        onChanged={refresh}
        onQuote={(unit) => {
          setSelected(unit);
          setSheetOpen(false);
          setQuoteOpen(true);
        }}
      />

      <QuoteBuilder
        key={`quote-${selected?.id ?? "none"}`}
        unit={selected}
        projectId={projectId ? Number(projectId) : null}
        open={quoteOpen}
        onOpenChange={setQuoteOpen}
        onCreated={refresh}
      />
    </div>
  );
}

/**
 * What the header may honestly claim the stock is priced at.
 *
 * A project that prices by configuration has several cards in force at once —
 * a mall's ground-floor retail, its upper levels and its food court are three
 * different rates effective the same morning. Printing the first of them as
 * "the" rate tells a rep the wrong number for every unit that is not on it, so
 * a spread is stated as a spread.
 */
function rateHeadline(board: InventoryBoard): string {
  const rates = (board.activeRateCards ?? [])
    .map((card) => card.ratePerSqft)
    .filter((rate) => rate > 0);

  if (rates.length === 0) {
    return formatRupees(board.activeRateCard?.ratePerSqft ?? 0);
  }

  const low = Math.min(...rates);
  const high = Math.max(...rates);

  return low === high
    ? formatRupees(low)
    : `${formatRupees(low)} – ${formatRupees(high)}`;
}

function Stat({
  label,
  value,
  hint,
  tone,
}: {
  label: string;
  value: string;
  hint?: string;
  tone?: string;
}) {
  return (
    <div className="rounded-lg border bg-card px-3 py-2">
      <p className="text-[10.5px] tracking-wide text-muted-foreground uppercase">
        {label}
      </p>
      <p className={cn("text-lg font-semibold tabular-nums", tone)}>{value}</p>
      {hint ? (
        <p className="text-[11px] text-muted-foreground tabular-nums">{hint}</p>
      ) : null}
    </div>
  );
}
