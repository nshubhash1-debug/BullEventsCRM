"use client";

import * as React from "react";

import type { BoardUnit } from "@/lib/inventory-api";
import { cn } from "@/lib/utils";

/**
 * The architect's own floor plan, with the CRM's inventory drawn over it.
 *
 * The base layer is the drawing exactly as issued — every wall, door swing,
 * toilet and dimension is the PDF's vector geometry re-framed as an SVG, not a
 * redrawing. The interactive layer is a second SVG stacked on top in the same
 * coordinate space, holding one translucent rectangle per guestroom: status
 * tint, unit number, and the selection ring.
 *
 * The two layers must never merge. The drawing is the architect's document and
 * changes only when they reissue it; the overlay is the CRM's and changes with
 * every booking. Keeping them separate is what lets a new revision of the plan
 * drop in as one file swap.
 */

/** The plan asset's viewBox — set by the export script, mirrored here. */
const VIEW = { x: 58, y: 118, w: 486, h: 606 };

/**
 * Room footprints in the drawing's own coordinates (PDF points), read off the
 * partition walls of the source document rather than estimated.
 *
 * Keyed by unit position: 1–6 run left to right along the bottom band, 7–10
 * climb the right band bottom to top — the architect's numbering, which walks
 * the corridor counter-clockwise from room 6.
 */
const ROOMS: Record<number, { x0: number; y0: number; x1: number; y1: number }> = {
  1: { x0: 69.1, y0: 504.0, x1: 146.6, y1: 689.0 },
  2: { x0: 146.6, y0: 504.0, x1: 222.8, y1: 689.0 },
  3: { x0: 222.8, y0: 504.0, x1: 298.9, y1: 689.0 },
  4: { x0: 298.9, y0: 504.0, x1: 375.1, y1: 689.0 },
  5: { x0: 375.1, y0: 504.0, x1: 451.3, y1: 689.0 },
  6: { x0: 451.3, y0: 504.0, x1: 529.1, y1: 689.0 },
  7: { x0: 335.9, y0: 379.0, x1: 529.1, y1: 460.8 },
  8: { x0: 335.9, y0: 297.2, x1: 529.1, y1: 379.0 },
  9: { x0: 335.9, y0: 215.5, x1: 529.1, y1: 297.2 },
  10: { x0: 335.9, y0: 153.3, x1: 529.1, y1: 215.5 },
};

/** How many rooms this drawing has — the gate for whether a floor fits it. */
export const ARCHITECT_PLAN_ROOMS = Object.keys(ROOMS).length;

// Same palette as the schematic plate, slightly stronger: these tints sit over
// the drawing's own pale blue floor fill rather than over white.
const STATUS_FILL: Record<string, string> = {
  Available: "rgb(16 185 129 / 0.30)",
  Held: "rgb(245 158 11 / 0.34)",
  Blocked: "rgb(139 92 246 / 0.32)",
  Booked: "rgb(14 165 233 / 0.32)",
  Sold: "rgb(244 63 94 / 0.34)",
  NotForSale: "rgb(148 163 184 / 0.32)",
};

const STATUS_STROKE: Record<string, string> = {
  Available: "rgb(5 150 105 / 0.9)",
  Held: "rgb(217 119 6 / 0.9)",
  Blocked: "rgb(124 58 237 / 0.9)",
  Booked: "rgb(2 132 199 / 0.9)",
  Sold: "rgb(225 29 72 / 0.9)",
  NotForSale: "rgb(100 116 139 / 0.85)",
};

export function ArchitectPlan({
  units,
  selectedId,
  onSelect,
  className,
}: {
  units: BoardUnit[];
  selectedId?: number | null;
  onSelect?: (unit: BoardUnit) => void;
  className?: string;
}) {
  // Position is authoritative where it lands inside the drawing; anything the
  // drawing has no room for falls back to slot order so nobody disappears.
  const placed = React.useMemo(() => {
    const bySlot = new Map<number, BoardUnit>();
    const spill: BoardUnit[] = [];

    for (const unit of units) {
      if (unit.position >= 1 && unit.position <= ARCHITECT_PLAN_ROOMS && !bySlot.has(unit.position)) {
        bySlot.set(unit.position, unit);
      } else {
        spill.push(unit);
      }
    }

    for (let slot = 1; slot <= ARCHITECT_PLAN_ROOMS && spill.length > 0; slot++) {
      if (!bySlot.has(slot)) bySlot.set(slot, spill.shift()!);
    }

    return bySlot;
  }, [units]);

  return (
    <div className={cn("relative", className)}>
      {/* eslint-disable-next-line @next/next/no-img-element -- static vector asset */}
      <img
        src="/plans/jeet-homes-hotel-floor.svg"
        alt="Architect's floor plan"
        className="block h-auto w-full select-none"
        draggable={false}
      />

      <svg
        viewBox={`${VIEW.x} ${VIEW.y} ${VIEW.w} ${VIEW.h}`}
        className="absolute inset-0 h-full w-full"
        role="list"
        aria-label="Units on this floor"
      >
        {[...placed].map(([slot, unit]) => {
          const room = ROOMS[slot];
          if (!room) return null;

          const selected = unit.id === selectedId;
          const w = room.x1 - room.x0;
          const h = room.y1 - room.y0;
          const cx = room.x0 + w / 2;

          // The bottom rooms carry their toilets in the upper half of the
          // footprint, so the label sits low; the right-band rooms are wide
          // and clear, so it centres.
          const labelY = h > w ? room.y1 - h * 0.18 : room.y0 + h / 2;

          return (
            <g
              key={unit.id}
              role="listitem"
              aria-label={`${unit.unitNumber} — ${unit.status}`}
              onClick={onSelect ? () => onSelect(unit) : undefined}
              className={onSelect ? "cursor-pointer" : undefined}
            >
              <rect
                x={room.x0}
                y={room.y0}
                width={w}
                height={h}
                fill={STATUS_FILL[unit.status] ?? STATUS_FILL.NotForSale}
                stroke={selected ? "currentColor" : STATUS_STROKE[unit.status] ?? STATUS_STROKE.NotForSale}
                strokeWidth={selected ? 2.4 : 0.8}
                className={selected ? "text-primary" : undefined}
              />

              {/* Inset dashed ring on the selected room — the highlight has to
                  survive a photocopier, and a tint change alone does not. */}
              {selected ? (
                <rect
                  x={room.x0 + 3}
                  y={room.y0 + 3}
                  width={w - 6}
                  height={h - 6}
                  fill="none"
                  stroke="currentColor"
                  strokeWidth={1}
                  strokeDasharray="5 3"
                  className="text-primary"
                />
              ) : null}

              {/* Unit number on a small plate so it reads over the drawing's
                  own linework and dimension text. */}
              <g>
                <rect
                  x={cx - 22}
                  y={labelY - 8}
                  width={44}
                  height={16}
                  rx={3}
                  fill="rgb(255 255 255 / 0.88)"
                  stroke={selected ? "currentColor" : "rgb(0 0 0 / 0.18)"}
                  strokeWidth={selected ? 1.1 : 0.5}
                  className={selected ? "text-primary" : undefined}
                />
                <text
                  x={cx}
                  y={labelY + 3.5}
                  textAnchor="middle"
                  fontSize={9}
                  fontWeight={selected ? 700 : 600}
                  className={selected ? "fill-primary" : "fill-foreground"}
                  style={{ fill: selected ? undefined : "#1f2937" }}
                >
                  {unit.unitNumber}
                </text>
              </g>
            </g>
          );
        })}
      </svg>
    </div>
  );
}
