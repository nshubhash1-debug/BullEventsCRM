"use client";

import * as React from "react";
import {
  Building2,
  Grid3x3,
  Lock,
  LockOpen,
  PackageCheck,
  Ruler,
  Warehouse,
} from "lucide-react";
import { toast } from "sonner";

import type { GridColumn } from "@/components/crm/crm-grid";
import { ListShell, type QuickView } from "@/components/crm/list-shell";
import {
  DistributionBar,
  Pill,
  StatusDot,
  TONE_FILL,
  type Metric,
} from "@/components/crm/metrics";
import { Button } from "@/components/ui/button";
import {
  Tooltip,
  TooltipContent,
  TooltipTrigger,
} from "@/components/ui/tooltip";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { ScrollArea, ScrollBar } from "@/components/ui/scroll-area";
import { useCrmList } from "@/hooks/use-crm-list";
import { ApiError } from "@/lib/api";
import {
  formatDateTime,
  formatMoney,
  humanise,
  inventoryApi,
  type AvailabilityGrid,
  type ProjectRow,
  type UnitRow,
} from "@/lib/crm-api";
import { unitStatusTone } from "@/lib/crm-tones";
import { EVENT_SLOTS } from "@/lib/inventory-api";
import { emptyRoot, type FilterNode } from "@/lib/query";
import { cn } from "@/lib/utils";

const FACETS = ["status", "configuration", "projectName", "towerName", "isOutdoor"];
const INITIAL_SORT = [{ field: "totalPrice", descending: true }];

function condition(field: string, operator: string, value?: string, values?: string[]): FilterNode {
  const root = emptyRoot();
  return { ...root, children: [{ key: `${root.key}-c`, field, operator, value, values }] };
}

/* ------------------------------------------------------------------ *
 * Availability grid
 * ------------------------------------------------------------------ */

function AvailabilityBoard({ projectId }: { projectId: number }) {
  const [grid, setGrid] = React.useState<AvailabilityGrid | null>(null);

  React.useEffect(() => {
    inventoryApi
      .availability(projectId)
      .then(setGrid)
      .catch(() => setGrid(null));
  }, [projectId]);

  if (!grid) {
    return (
      <p className="py-8 text-center text-[13px] text-muted-foreground">
        Loading availability…
      </p>
    );
  }

  return (
    <div className="flex flex-col gap-3">
      <div className="grid gap-3 md:grid-cols-2">
        <DistributionBar
          title="Availability"
          segments={grid.byStatus.map((bucket) => ({
            key: bucket.key,
            label: bucket.label,
            count: bucket.count,
            tone: unitStatusTone(bucket.key),
          }))}
        />
        <div className="min-w-0">
          <span className="text-[10.5px] font-medium tracking-wide text-muted-foreground uppercase">
            By space type
          </span>
          <div className="mt-1 flex flex-col gap-0.5">
            {grid.byConfiguration.map((config) => (
              <div key={config.configuration} className="flex items-center gap-2 text-[11.5px]">
                <span className="w-24 truncate">{humanise(config.configuration)}</span>
                <span className="h-2 min-w-0 flex-1 overflow-hidden rounded-full bg-muted">
                  <span
                    className={cn("block h-full", TONE_FILL.success)}
                    style={{ width: `${(config.available / Math.max(config.total, 1)) * 100}%` }}
                  />
                </span>
                <span className="w-14 text-right tabular-nums text-muted-foreground">
                  {config.available}/{config.total}
                </span>
                <span className="w-16 text-right tabular-nums">
                  {formatMoney(config.averagePrice)}
                </span>
              </div>
            ))}
          </div>
        </div>
      </div>

      {/*
        One row per level, highest first — the way a venue is walked, rather
        than the way a database returns rows.
      */}
      <ScrollArea className="max-h-[26rem] w-full rounded border">
        <div className="flex flex-col">
          {grid.floors.map((floor) => (
            <div key={floor.floor} className="flex items-stretch border-b last:border-b-0">
              <div className="flex w-14 shrink-0 items-center justify-center border-r bg-muted/40 text-[11.5px] font-medium tabular-nums">
                {floor.floor === 0 ? "G" : `L${floor.floor}`}
              </div>
              <div className="flex flex-wrap gap-1 p-1">
                {floor.units.map((unit) => (
                  <Tooltip key={unit.unitId}>
                    <TooltipTrigger asChild>
                      <span
                        className={cn(
                          "inline-flex h-7 min-w-16 items-center justify-center gap-1 rounded border px-1.5 text-[11px] font-medium",
                          unit.status === "Available" &&
                            "border-emerald-500/40 bg-emerald-500/10 text-emerald-700 dark:text-emerald-400",
                          unit.status === "Held" &&
                            "border-amber-500/40 bg-amber-500/10 text-amber-700 dark:text-amber-400",
                          unit.status === "Blocked" &&
                            "border-violet-500/40 bg-violet-500/10 text-violet-700 dark:text-violet-400",
                          unit.status === "Booked" &&
                            "border-primary/40 bg-primary/10 text-primary",
                          unit.status === "Sold" &&
                            "border-red-500/40 bg-red-500/10 text-red-700 dark:text-red-400",
                          unit.status === "NotForSale" &&
                            "border-zinc-400/40 bg-zinc-400/10 text-muted-foreground",
                          unit.status === "Blackout" &&
                            "border-zinc-500/40 bg-zinc-700/10 text-zinc-800 dark:text-zinc-200"
                        )}
                      >
                        {unit.unitNumber}
                      </span>
                    </TooltipTrigger>
                    <TooltipContent className="text-[11px]">
                      {humanise(unit.configuration)} · {formatMoney(unit.totalPrice)} ·{" "}
                      {humanise(unit.status)}
                    </TooltipContent>
                  </Tooltip>
                ))}
              </div>
            </div>
          ))}
        </div>
        <ScrollBar orientation="vertical" />
      </ScrollArea>
    </div>
  );
}

/* ------------------------------------------------------------------ *
 * Page
 * ------------------------------------------------------------------ */

export default function InventoryPage() {
  const state = useCrmList<UnitRow>(inventoryApi, {
    facets: FACETS,
    dateFields: [
      { id: "bookedAt", label: "Booked" },
      { id: "heldUntil", label: "Held until" },
      { id: "createdAt", label: "Created" },
    ],
    sort: INITIAL_SORT,
    pageSize: 50,
  });

  const [projects, setProjects] = React.useState<ProjectRow[]>([]);
  const [gridProject, setGridProject] = React.useState<number | null>(null);
  const [lock, setLock] = React.useState<{
    mode: "hold" | "release";
    unit: UnitRow;
  } | null>(null);
  const [lockDate, setLockDate] = React.useState("");
  const [lockSlot, setLockSlot] = React.useState("Evening");

  React.useEffect(() => {
    inventoryApi.projects().then(setProjects).catch(() => setProjects([]));
  }, []);

  const aggregates = state.aggregates;
  const units = aggregates.units ?? 0;
  const available = aggregates.available ?? 0;

  const metrics: Metric[] = [
    { label: "Spaces in view", value: units.toLocaleString(), icon: Warehouse },
    {
      label: "Available",
      value: available.toLocaleString(),
      tone: "success",
      progress: units > 0 ? available / units : 0,
      onClick: () => state.setFilter(condition("status", "equals", "Available")),
    },
    {
      label: "On hold",
      value: (aggregates.held ?? 0).toLocaleString(),
      tone: "warning",
      icon: Lock,
      hint: "Soft locks placed by planners. An expired hold reads as available again — no sweeper job involved.",
      onClick: () => state.setFilter(condition("status", "equals", "Held")),
    },
    {
      label: "Booked / confirmed",
      value: (aggregates.booked ?? 0).toLocaleString(),
      tone: "primary",
      icon: PackageCheck,
      onClick: () =>
        state.setFilter(condition("status", "in", undefined, ["Booked", "Sold"])),
    },
    {
      label: "Bookable value",
      value: formatMoney(aggregates.inventoryValue ?? 0),
      tone: "violet",
      hint: "Indicative total of every available space in the filtered set — rental plus catering at seated capacity.",
    },
  ];

  const quickViews: QuickView[] = [
    {
      id: "available",
      label: "Available",
      build: () => condition("status", "equals", "Available"),
    },
    {
      id: "held",
      label: "On hold",
      build: () => condition("status", "equals", "Held"),
    },
    {
      id: "expiring",
      label: "Holds expiring",
      build: () => condition("heldUntil", "nextNDays", "1"),
    },
    // The two questions a planner shortlists on: can it hold the party, and is
    // it under a roof. Everything else is negotiable; a 600-guest wedding
    // simply does not fit in a 250-seat hall.
    {
      id: "large",
      label: "Seats 400+",
      build: () => condition("seatingCapacity", "greaterThan", "400"),
    },
    {
      id: "outdoor",
      label: "Outdoor",
      build: () => condition("isOutdoor", "isTrue"),
    },
    {
      id: "airConditioned",
      label: "Air conditioned",
      build: () => condition("isAirConditioned", "isTrue"),
    },
  ];

  const visuals = (
    <div className="grid gap-3 lg:grid-cols-3">
      <DistributionBar
        title="Status"
        segments={(state.facets.status ?? []).map((bucket) => ({
          key: bucket.value,
          label: humanise(bucket.value),
          count: bucket.count,
          tone: unitStatusTone(bucket.value),
        }))}
        onSelect={(value) => state.setFilter(condition("status", "equals", value))}
      />
      <DistributionBar
        title="Space type"
        segments={(state.facets.configuration ?? []).map((bucket, index) => ({
          key: bucket.value,
          label: humanise(bucket.value),
          count: bucket.count,
          tone: (["primary", "info", "violet", "success", "warning", "danger", "neutral"] as const)[
            index % 7
          ],
        }))}
        onSelect={(value) => state.setFilter(condition("configuration", "equals", value))}
      />
      <div className="min-w-0">
        <span className="text-[10.5px] font-medium tracking-wide text-muted-foreground uppercase">
          Projects
        </span>
        <div className="mt-1 flex flex-col gap-0.5">
          {projects.map((project) => (
            <button
              key={project.id}
              type="button"
              onClick={() =>
                state.setFilter(condition("projectName", "equals", project.name))
              }
              className="flex items-center gap-2 rounded px-0.5 text-[11.5px] hover:bg-muted"
            >
              <Building2 className="size-3 shrink-0 text-muted-foreground" />
              <span className="w-32 truncate text-left">{project.name}</span>
              <span className="h-2 min-w-0 flex-1 overflow-hidden rounded-full bg-muted">
                <span
                  className={cn("block h-full", TONE_FILL.success)}
                  style={{
                    width: `${
                      (project.availableUnits / Math.max(project.totalUnits, 1)) * 100
                    }%`,
                  }}
                />
              </span>
              <span className="w-14 text-right tabular-nums text-muted-foreground">
                {project.availableUnits}/{project.totalUnits}
              </span>
            </button>
          ))}
        </div>
      </div>
    </div>
  );

  function todayIso() {
    const d = new Date();
    const m = String(d.getMonth() + 1).padStart(2, "0");
    const day = String(d.getDate()).padStart(2, "0");
    return `${d.getFullYear()}-${m}-${day}`;
  }

  function openLock(mode: "hold" | "release", unit: UnitRow) {
    setLock({ mode, unit });
    setLockDate(todayIso());
    setLockSlot("Evening");
  }

  async function confirmLock() {
    if (!lock || !lockDate) {
      toast.error("Pick the event date.");
      return;
    }
    try {
      if (lock.mode === "hold") {
        await inventoryApi.hold(lock.unit.id, 24, "Held from the inventory list", {
          eventDate: lockDate,
          eventSlot: lockSlot,
        });
        toast.success(`${lock.unit.unitNumber} held for ${lockDate}`);
      } else {
        await inventoryApi.release(lock.unit.id, {
          eventDate: lockDate,
          eventSlot: lockSlot,
        });
        toast.success(`${lock.unit.unitNumber} released for ${lockDate}`);
      }
      setLock(null);
      state.refresh();
    } catch (error) {
      toast.error(
        lock.mode === "hold" ? "Could not hold this space" : "Could not release this space",
        {
          description: error instanceof ApiError ? error.message : "Network error.",
        }
      );
    }
  }

  const columns: GridColumn<UnitRow>[] = [
    {
      id: "unit",
      label: "Space",
      sortField: "unitNumber",
      sticky: true,
      width: 150,
      render: (row) => (
        <span className="flex flex-col leading-tight">
          <span className="font-medium tabular-nums">{row.unitNumber}</span>
          <span className="truncate text-[10.5px] text-muted-foreground">
            {row.projectName}
          </span>
        </span>
      ),
    },
    {
      id: "status",
      label: "Status",
      sortField: "status",
      width: 110,
      render: (row) => (
        <StatusDot label={humanise(row.status)} tone={unitStatusTone(row.status)} />
      ),
    },
    {
      id: "config",
      label: "Type",
      sortField: "configuration",
      width: 110,
      render: (row) => <Pill tone="neutral">{humanise(row.configuration)}</Pill>,
    },
    {
      id: "capacity",
      label: "Capacity",
      sortField: "seatingCapacity",
      align: "right",
      width: 108,
      // Seated over floating, because the seated number is the one a wedding is
      // shortlisted on and the floating one is what venues quote to look bigger.
      render: (row) => (
        <span className="flex flex-col leading-tight tabular-nums">
          <span>{row.seatingCapacity.toLocaleString()} seated</span>
          <span className="text-[10.5px] text-muted-foreground">
            {row.floatingCapacity.toLocaleString()} floating
          </span>
        </span>
      ),
    },
    {
      id: "setting",
      label: "Setting",
      width: 96,
      render: (row) => (
        <span className="text-[11.5px] text-muted-foreground">
          {row.isOutdoor ? "Outdoor" : row.isAirConditioned ? "Indoor · AC" : "Indoor"}
        </span>
      ),
    },
    {
      id: "tower",
      label: "Block",
      sortField: "towerName",
      width: 82,
      defaultHidden: true,
      render: (row) => (
        <span className="text-muted-foreground">{row.towerName ?? "—"}</span>
      ),
    },
    {
      id: "floor",
      label: "Level",
      sortField: "floor",
      align: "right",
      width: 58,
      defaultHidden: true,
      render: (row) => row.floor,
    },
    {
      id: "carpet",
      label: "Area",
      sortField: "carpetArea",
      align: "right",
      width: 82,
      defaultHidden: true,
      render: (row) => (
        <span className="tabular-nums">
          {row.carpetArea.toLocaleString()}
          <span className="ml-0.5 text-[10px] text-muted-foreground">{row.areaUnit}</span>
        </span>
      ),
    },
    {
      id: "rental",
      label: "Rental",
      sortField: "basePrice",
      align: "right",
      width: 96,
      render: (row) => formatMoney(row.basePrice),
    },
    {
      id: "perPlate",
      label: "Per plate",
      sortField: "pricePerPlate",
      align: "right",
      width: 104,
      // The minimum sits under the rate because quoting a 200-guest event into
      // a hall with a 400-plate minimum is the commonest way an events quote
      // goes wrong, and the two numbers are only useful together.
      render: (row) => (
        <span className="flex flex-col leading-tight tabular-nums">
          <span>{formatMoney(row.pricePerPlate)}</span>
          <span className="text-[10.5px] text-muted-foreground">
            min {row.minimumPlates.toLocaleString()}
          </span>
        </span>
      ),
    },
    {
      id: "total",
      label: "Indicative",
      sortField: "totalPrice",
      align: "right",
      width: 108,
      render: (row) => <span className="font-medium">{formatMoney(row.totalPrice)}</span>,
    },
    {
      id: "base",
      label: "Base",
      sortField: "basePrice",
      align: "right",
      width: 96,
      defaultHidden: true,
      render: (row) => formatMoney(row.basePrice),
    },
    {
      id: "premiums",
      label: "Premiums",
      align: "right",
      width: 100,
      defaultHidden: true,
      render: (row) => (
        <Tooltip>
          <TooltipTrigger asChild>
            <span>{formatMoney(row.floorRisePremium + row.plcCharges)}</span>
          </TooltipTrigger>
          <TooltipContent className="text-[11px]">
            Floor rise {formatMoney(row.floorRisePremium)} · PLC {formatMoney(row.plcCharges)}
          </TooltipContent>
        </Tooltip>
      ),
    },
    {
      id: "facing",
      label: "Facing",
      sortField: "facing",
      width: 96,
      defaultHidden: true,
      render: (row) => (
        <span className="text-muted-foreground">{row.facing ?? "—"}</span>
      ),
    },
    {
      id: "view",
      label: "View",
      sortField: "viewType",
      width: 106,
      defaultHidden: true,
      render: (row) => (
        <span className="text-muted-foreground">{row.viewType ?? "—"}</span>
      ),
    },
    {
      id: "attributes",
      label: "Attributes",
      width: 150,
      render: (row) => (
        <span className="flex flex-wrap gap-0.5">
          {row.isCornerUnit ? <Pill tone="violet">Corner</Pill> : null}
          {row.vastuCompliant ? <Pill tone="success">Vastu</Pill> : null}
          <Pill tone="neutral">{row.parkingSlots}P</Pill>
        </span>
      ),
    },
    {
      id: "hold",
      label: "Hold",
      sortField: "heldUntil",
      width: 156,
      render: (row) =>
        row.heldUntil ? (
          <span className="flex flex-col leading-tight">
            <span className="truncate">{row.heldByName ?? "Held"}</span>
            <span className="text-[10.5px] text-muted-foreground">
              until {formatDateTime(row.heldUntil)}
            </span>
          </span>
        ) : (
          <span className="text-muted-foreground">—</span>
        ),
    },
    {
      id: "actions",
      label: "",
      width: 92,
      fixedWidth: true,
      render: (row) =>
        row.status === "Available" ? (
          <Button
            variant="outline"
            size="sm"
            className="h-6 text-[11px]"
            onClick={(event) => {
              event.stopPropagation();
              openLock("hold", row);
            }}
          >
            <Lock className="size-3" /> Hold
          </Button>
        ) : row.status === "Held" ? (
          <Button
            variant="outline"
            size="sm"
            className="h-6 text-[11px]"
            onClick={(event) => {
              event.stopPropagation();
              openLock("release", row);
            }}
          >
            <LockOpen className="size-3" /> Release
          </Button>
        ) : (
          <span className="text-[11px] text-muted-foreground">
            {humanise(row.status)}
          </span>
        ),
    },
  ];

  return (
    <>
    <ListShell
      icon={Warehouse}
      title="Venues & spaces"
      storageKey="inventory"
      hint="Catalogue of halls and lawns. Holds and blackouts are per event date — use the venue diary for the week view."
      state={state}
      columns={columns}
      metrics={metrics}
      visuals={visuals}
      quickViews={quickViews}
      searchPlaceholder="Space, venue, block…"
      emptyMessage="No spaces match this view."
      actions={
        <Dialog>
          <DialogTrigger asChild>
            <Button
              variant="outline"
              size="sm"
              className="h-8"
              onClick={() => setGridProject(projects[0]?.id ?? null)}
            >
              <Grid3x3 /> Stack plan
            </Button>
          </DialogTrigger>
          <DialogContent className="max-w-5xl">
            <DialogHeader>
              <DialogTitle className="flex items-center gap-2">
                <Ruler className="size-4 text-primary" />
                Availability stack plan
              </DialogTitle>
              <DialogDescription>
                Every space in the venue, highest level first.
              </DialogDescription>
            </DialogHeader>

            <div className="flex flex-wrap gap-1">
              {projects.map((project) => (
                <button
                  key={project.id}
                  type="button"
                  onClick={() => setGridProject(project.id)}
                  className={cn(
                    "h-7 rounded px-2.5 text-[12px] transition-colors",
                    gridProject === project.id
                      ? "bg-primary font-medium text-primary-foreground"
                      : "border hover:bg-accent"
                  )}
                >
                  {project.name}
                </button>
              ))}
            </div>

            {gridProject ? <AvailabilityBoard projectId={gridProject} /> : null}
          </DialogContent>
        </Dialog>
      }
    />
      <Dialog open={lock !== null} onOpenChange={(open) => !open && setLock(null)}>
        <DialogContent className="sm:max-w-md">
          <DialogHeader>
            <DialogTitle>
              {lock?.mode === "release" ? "Release date" : "Hold for a date"}
            </DialogTitle>
            <DialogDescription>
              {lock?.unit.unitNumber} — locks apply to one event date and slot, not
              the whole catalogue.
            </DialogDescription>
          </DialogHeader>
          <div className="grid gap-3 sm:grid-cols-2">
            <div className="flex flex-col gap-1.5">
              <Label htmlFor="lock-date">Event date</Label>
              <Input
                id="lock-date"
                type="date"
                value={lockDate}
                onChange={(e) => setLockDate(e.target.value)}
              />
            </div>
            <div className="flex flex-col gap-1.5">
              <Label htmlFor="lock-slot">Slot</Label>
              <select
                id="lock-slot"
                className="h-9 w-full rounded-md border bg-background px-2 text-sm"
                value={lockSlot}
                onChange={(e) => setLockSlot(e.target.value)}
              >
                {EVENT_SLOTS.map((s) => (
                  <option key={s} value={s}>
                    {s === "FullDay" ? "Full day" : s}
                  </option>
                ))}
              </select>
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setLock(null)}>
              Cancel
            </Button>
            <Button onClick={() => void confirmLock()}>
              {lock?.mode === "release" ? "Release" : "Hold 24 hours"}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </>
  );
}
