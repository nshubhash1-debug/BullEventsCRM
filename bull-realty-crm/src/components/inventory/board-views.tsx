"use client";

import * as React from "react";
import { Lock, ShieldAlert, Star, User } from "lucide-react";

import { ScrollArea, ScrollBar } from "@/components/ui/scroll-area";
import {
  Tooltip,
  TooltipContent,
  TooltipTrigger,
} from "@/components/ui/tooltip";
import { statusStyle } from "@/components/inventory/status";
import {
  formatCompactRupees,
  formatIndian,
  formatRupees,
  type BoardUnit,
  type InventoryFloor,
} from "@/lib/inventory-api";
import { cn } from "@/lib/utils";

/**
 * The three ways to read a tower.
 *
 * They share one dataset and one status palette, and differ only in how much
 * they show per unit — which is the whole point of having three. The block view
 * is for working a floor, the list for filtering and exporting, the elevation
 * for seeing where the tower is selling.
 */

interface ViewProps {
  floors: InventoryFloor[];
  selectedId?: number | null;
  onSelect: (unit: BoardUnit) => void;
}

/* ------------------------------------------------------------------ *
 * Shared bits
 * ------------------------------------------------------------------ */

function UnitTooltip({ unit }: { unit: BoardUnit }) {
  const style = statusStyle(unit.status);

  return (
    <div className="flex min-w-44 flex-col gap-1 text-[11.5px]">
      <div className="flex items-center justify-between gap-3">
        <span className="font-medium">{unit.unitNumber}</span>
        <span className="inline-flex items-center gap-1">
          <span className={cn("size-1.5 rounded-full", style.dot)} />
          {style.label}
        </span>
      </div>

      <div className="text-muted-foreground">
        {formatIndian(unit.superArea)} sq ft · {unit.configuration}
        {unit.plcPerSqft > 0 ? " · PLC" : ""}
      </div>

      <div className="tabular-nums">{formatRupees(unit.totalPrice)}</div>

      {unit.customerName ? (
        <div className="text-muted-foreground">Buyer: {unit.customerName}</div>
      ) : null}

      {unit.salesPersonName ? (
        <div className="text-muted-foreground">Sold by: {unit.salesPersonName}</div>
      ) : null}

      {unit.heldByName ? (
        <div className="text-muted-foreground">
          Held by {unit.heldByName}
          {unit.heldUntil
            ? ` until ${new Date(unit.heldUntil).toLocaleString(undefined, {
                day: "2-digit",
                month: "short",
                hour: "2-digit",
                minute: "2-digit",
              })}`
            : ""}
        </div>
      ) : null}

      {unit.pendingApprovalId ? (
        <div className="text-amber-600 dark:text-amber-400">Awaiting approval</div>
      ) : null}
    </div>
  );
}

function FloorLabel({ floor, available, total }: InventoryFloor) {
  return (
    <div className="flex w-16 shrink-0 flex-col justify-center border-r pr-2">
      <span className="text-[13px] font-semibold tabular-nums">{floor}</span>
      <span className="text-[10px] text-muted-foreground tabular-nums">
        {available}/{total} open
      </span>
    </div>
  );
}

/* ------------------------------------------------------------------ *
 * Block view
 * ------------------------------------------------------------------ */

/**
 * Floor by floor, unit by unit — the view a rep works a floor from.
 *
 * Each tile carries the four things asked at the desk: which unit, how big,
 * what it costs, and who has it. Anything more turns the wall of tiles into a
 * wall of text.
 */
export function BlockView({ floors, selectedId, onSelect }: ViewProps) {
  return (
    <div className="flex flex-col">
      {floors.map((floor) => (
        <div
          key={floor.floor}
          className="flex gap-3 border-b px-3 py-2.5 last:border-b-0"
        >
          <FloorLabel {...floor} />

          <div className="flex flex-1 flex-wrap gap-1.5">
            {floor.units.map((unit) => {
              const style = statusStyle(unit.status);

              return (
                <Tooltip key={unit.id}>
                  <TooltipTrigger asChild>
                    <button
                      type="button"
                      onClick={() => onSelect(unit)}
                      className={cn(
                        "relative flex w-[124px] flex-col gap-0.5 rounded-md border px-2 py-1.5 text-left transition-colors",
                        style.tile,
                        selectedId === unit.id &&
                          "ring-2 ring-primary ring-offset-1 ring-offset-background"
                      )}
                    >
                      <span className="flex items-center gap-1">
                        <span className="truncate text-[11.5px] font-semibold">
                          {unit.unitNumber.replace(/^Studio-/, "")}
                        </span>

                        {unit.plcPerSqft > 0 ? (
                          <Star className="size-2.5 shrink-0 opacity-70" />
                        ) : null}

                        {unit.pendingApprovalId ? (
                          <ShieldAlert className="ml-auto size-3 shrink-0" />
                        ) : unit.status === "Held" ? (
                          <Lock className="ml-auto size-2.5 shrink-0" />
                        ) : null}
                      </span>

                      <span className="text-[10px] opacity-80 tabular-nums">
                        {formatIndian(unit.superArea)} sqft
                      </span>

                      <span className="text-[10.5px] font-medium tabular-nums">
                        {formatCompactRupees(unit.totalPrice)}
                      </span>

                      {unit.customerName ? (
                        <span className="flex items-center gap-0.5 truncate text-[9.5px] opacity-75">
                          <User className="size-2.5 shrink-0" />
                          <span className="truncate">{unit.customerName}</span>
                        </span>
                      ) : null}
                    </button>
                  </TooltipTrigger>

                  <TooltipContent side="top" className="p-2">
                    <UnitTooltip unit={unit} />
                  </TooltipContent>
                </Tooltip>
              );
            })}
          </div>
        </div>
      ))}
    </div>
  );
}

/* ------------------------------------------------------------------ *
 * List view
 * ------------------------------------------------------------------ */

/** The dense table — every field, scannable and comparable down a column. */
export function ListView({ floors, selectedId, onSelect }: ViewProps) {
  const units = React.useMemo(
    () => floors.flatMap((floor) => floor.units),
    [floors]
  );

  return (
    <ScrollArea className="w-full">
      <table className="w-full min-w-[1020px] caption-bottom border-separate border-spacing-0 text-[12px]">
        <thead className="sticky top-0 z-10 bg-muted/70 backdrop-blur">
          <tr className="[&>th]:h-8 [&>th]:border-b [&>th]:px-2.5 [&>th]:text-left [&>th]:font-medium [&>th]:text-muted-foreground">
            <th>Unit</th>
            <th>Floor</th>
            <th>Type</th>
            <th className="text-right">Saleable</th>
            <th className="text-right">Rate</th>
            <th className="text-right">PLC</th>
            <th className="text-right">Price</th>
            <th>Status</th>
            <th>Customer</th>
            <th>Salesperson</th>
          </tr>
        </thead>

        <tbody>
          {units.map((unit) => {
            const style = statusStyle(unit.status);

            return (
              <tr
                key={unit.id}
                onClick={() => onSelect(unit)}
                className={cn(
                  "cursor-pointer transition-colors [&>td]:h-8 [&>td]:border-b [&>td]:px-2.5",
                  selectedId === unit.id ? "bg-primary/5" : "hover:bg-muted/40"
                )}
              >
                <td className="font-medium">{unit.unitNumber}</td>
                <td className="tabular-nums">{unit.floor}</td>
                <td className="text-muted-foreground">{unit.configuration}</td>
                <td className="text-right tabular-nums">
                  {formatIndian(unit.superArea)}
                </td>
                <td className="text-right tabular-nums">
                  {formatRupees(unit.ratePerSqft)}
                </td>
                <td className="text-right tabular-nums text-muted-foreground">
                  {unit.plcPerSqft > 0 ? formatRupees(unit.plcPerSqft) : "—"}
                </td>
                <td className="text-right font-medium tabular-nums">
                  {formatRupees(unit.totalPrice)}
                </td>
                <td>
                  <span className="inline-flex items-center gap-1.5 whitespace-nowrap">
                    <span className={cn("size-1.5 rounded-full", style.dot)} />
                    {style.label}
                    {unit.pendingApprovalId ? (
                      <ShieldAlert className="size-3 text-amber-500" />
                    ) : null}
                  </span>
                </td>
                <td className="max-w-[150px] truncate text-muted-foreground">
                  {unit.customerName ?? "—"}
                </td>
                <td className="max-w-[150px] truncate text-muted-foreground">
                  {unit.salesPersonName ?? unit.heldByName ?? "—"}
                </td>
              </tr>
            );
          })}
        </tbody>
      </table>
      <ScrollBar orientation="horizontal" />
    </ScrollArea>
  );
}

/* ------------------------------------------------------------------ *
 * Building view
 * ------------------------------------------------------------------ */

/**
 * Levels the tower has but sells nothing on, read off the architect's
 * section AA'.
 *
 * They matter because a sales board showing only sellable floors is not a
 * building. The gaps in this tower's floor numbering are its refuge and service
 * levels, and drawing them — along with the podium the towers sit on — is what
 * makes the stack read as the real building rather than a chart with rows
 * missing.
 */
const PODIUM_LEVELS: { label: string; kind: keyof typeof LEVEL_STYLE }[] = [
  { label: "Service floor", kind: "service" },
  { label: "Hotel spa · Health spa", kind: "amenity" },
  { label: "Refuge · Game zone · Restaurant", kind: "refuge" },
  { label: "Banquet", kind: "amenity" },
  { label: "Food court · Shops · Hotel amenities", kind: "amenity" },
  { label: "Atrium · Shops · Hotel entrance", kind: "entry" },
  { label: "Basement parking", kind: "basement" },
];

const LEVEL_STYLE = {
  service: "bg-fuchsia-500/15 text-fuchsia-700 dark:text-fuchsia-300",
  amenity: "bg-stone-500/20 text-stone-700 dark:text-stone-300",
  refuge: "bg-amber-400/25 text-amber-800 dark:text-amber-200",
  entry: "bg-indigo-500/15 text-indigo-700 dark:text-indigo-300",
  basement: "bg-zinc-500/20 text-zinc-700 dark:text-zinc-300",
};

/**
 * The tower as an elevation — every floor at once, one cell per unit.
 *
 * This is the view that answers the question the other two cannot: where in the
 * building is the stock moving. Cells are deliberately tiny and unlabelled, so
 * a thirty-six floor tower fits on one screen and the pattern — sold-out
 * podium, open middle, sold-out top — is visible without reading a number.
 *
 * Floors absent from the inventory are drawn as refuge bands rather than
 * skipped over, so floor 21 sits where floor 21 actually is in the building.
 * The per-floor bar on the right restates availability numerically, because a
 * colour block alone cannot be quoted in a meeting.
 */
export function BuildingView({ floors, selectedId, onSelect }: ViewProps) {
  const widest = Math.max(1, ...floors.map((f) => f.units.length));

  // Walk the whole numbering range, not just the floors carrying stock.
  const rows = React.useMemo(() => {
    if (floors.length === 0) return [];

    const byFloor = new Map(floors.map((floor) => [floor.floor, floor]));
    const top = Math.max(...floors.map((f) => f.floor));
    const bottom = Math.min(...floors.map((f) => f.floor));

    const out: Array<
      { kind: "units"; floor: InventoryFloor } | { kind: "gap"; number: number }
    > = [];

    for (let number = top; number >= bottom; number--) {
      const floor = byFloor.get(number);
      out.push(floor ? { kind: "units", floor } : { kind: "gap", number });
    }

    return out;
  }, [floors]);

  const bandWidth = widest * 22 + (widest - 1) * 3;

  return (
    <div className="flex justify-center px-4 py-5">
      <div className="flex flex-col gap-[3px]">
        {/* Mumty and terrace, the way the section caps the tower. */}
        <div className="flex items-center gap-2">
          <span className="w-7 shrink-0" />
          <div className="flex flex-col items-center" style={{ width: bandWidth }}>
            <div className="h-3 w-14 rounded-t-sm bg-muted-foreground/30" title="Mumty" />
            <div className="mt-[3px] flex h-4 w-full items-center justify-center rounded-[3px] bg-muted-foreground/20 text-[8.5px] tracking-wide text-muted-foreground uppercase">
              Terrace
            </div>
          </div>
          <div className="ml-2 w-32 shrink-0" />
        </div>

        {rows.map((row) =>
          row.kind === "units" ? (
            <div key={`floor-${row.floor.floor}`} className="flex items-center gap-2">
              <span className="w-7 shrink-0 text-right text-[10px] text-muted-foreground tabular-nums">
                {row.floor.floor}
              </span>

              <div
                className="grid gap-[3px]"
                style={{ gridTemplateColumns: `repeat(${widest}, 22px)` }}
              >
                {row.floor.units.map((unit) => {
                  const style = statusStyle(unit.status);

                  return (
                    <Tooltip key={unit.id}>
                      <TooltipTrigger asChild>
                        <button
                          type="button"
                          onClick={() => onSelect(unit)}
                          aria-label={`${unit.unitNumber} — ${style.label}`}
                          className={cn(
                            "h-5 rounded-[3px] border transition-transform hover:z-10 hover:scale-125",
                            style.tile,
                            selectedId === unit.id &&
                              "z-10 scale-125 ring-2 ring-primary ring-offset-1 ring-offset-background"
                          )}
                        />
                      </TooltipTrigger>

                      <TooltipContent side="right" className="p-2">
                        <UnitTooltip unit={unit} />
                      </TooltipContent>
                    </Tooltip>
                  );
                })}
              </div>

              <div className="ml-2 flex w-32 shrink-0 items-center gap-2">
                <div className="h-1 flex-1 overflow-hidden rounded-full bg-muted">
                  <div
                    className="h-full rounded-full bg-emerald-500"
                    style={{
                      width: `${(row.floor.available / Math.max(1, row.floor.total)) * 100}%`,
                    }}
                  />
                </div>
                <span className="w-10 text-[10px] text-muted-foreground tabular-nums">
                  {row.floor.available}/{row.floor.total}
                </span>
              </div>
            </div>
          ) : (
            <div key={`gap-${row.number}`} className="flex items-center gap-2">
              <span className="w-7 shrink-0 text-right text-[10px] text-muted-foreground/70 tabular-nums">
                {row.number}
              </span>

              <div
                className={cn(
                  "flex h-4 items-center justify-center rounded-[3px] text-[8.5px] tracking-wide uppercase",
                  LEVEL_STYLE.refuge
                )}
                style={{ width: bandWidth }}
                title={`Floor ${row.number} — refuge / service level, no sellable units`}
              >
                Refuge
              </div>

              <div className="ml-2 w-32 shrink-0" />
            </div>
          )
        )}

        {/* Podium and basement, stacked as the section stacks them. */}
        {PODIUM_LEVELS.map((level) => (
          <div key={level.label} className="flex items-center gap-2">
            <span className="w-7 shrink-0" />
            <div
              className={cn(
                "flex h-4 items-center justify-center overflow-hidden rounded-[3px] px-2 text-[8.5px] tracking-wide uppercase",
                LEVEL_STYLE[level.kind]
              )}
              style={{ width: bandWidth }}
              title={level.label}
            >
              <span className="truncate">{level.label}</span>
            </div>
            <div className="ml-2 w-32 shrink-0" />
          </div>
        ))}

        {/* Ground, wider than the tower — the plinth in the section. */}
        <div className="flex items-center gap-2">
          <span className="w-7 shrink-0" />
          <div
            className="h-2 rounded-b-sm bg-muted-foreground/25"
            style={{ width: bandWidth + 20, marginLeft: -10 }}
          />
          <div className="ml-2 w-32 shrink-0" />
        </div>

        <p className="mt-3 max-w-[430px] self-center text-center text-[10.5px] text-muted-foreground">
          Levels below the tower are drawn from section AA&rsquo;. Floors carrying no
          sellable units are the building&rsquo;s refuge and service levels.
        </p>
      </div>
    </div>
  );
}
