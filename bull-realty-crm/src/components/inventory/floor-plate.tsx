"use client";

import * as React from "react";

import { formatIndian, type BoardUnit } from "@/lib/inventory-api";
import { cn } from "@/lib/utils";

/**
 * The hotel floor plate — the architect's drawing itself, not a trace of it.
 *
 * The linework, dimensions, room labels and core layout come straight from
 * "Jeet Homes at Varanasi — Hotel Floor, Option 2" (Hafeez Contractor,
 * 28.07.2026, 1:100, 660 sq m). The vector artwork was lifted out of the issued
 * PDF and is served as a static asset; this component draws it and lays the
 * CRM's own information over the top.
 *
 * <p>
 * That split is the whole point. A hand-drawn schematic is a second source of
 * truth about a building, and it goes stale the moment the architect reissues.
 * Here the drawing is the drawing — if it changes, the asset is replaced and
 * every room stays exactly where the architect put it. What the CRM adds is the
 * part the drawing cannot know: which rooms are sold, which is being looked at.
 * </p>
 *
 * <p>
 * The room rectangles below are measured from the PDF's own partition walls
 * rather than divided evenly, because the rooms are not even — the topmost of
 * the four right-hand rooms is 64.6 pt deep against 81.8 for the other three,
 * though the drawing labels all four 3.92 m. Every rectangle lands within about
 * two points of the guestroom label it belongs to, which is how they were
 * checked.
 * </p>
 */

/* ------------------------------------------------------------------ *
 * Geometry, in the drawing's own coordinates
 * ------------------------------------------------------------------ */

/** The issued drawing, as exported from the architect's PDF. */
const PLAN_SRC = "/floor-plans/jeet-hotel-floor.svg";

/** The PDF page the artwork is positioned against. */
const PAGE = { w: 595, h: 842 };

/** Cropped to the plan, so the title block and revision panel fall outside. */
const VIEW = { x: 63.1, y: 147.3, w: 472, h: 548 };

interface Slot {
  x: number;
  y: number;
  w: number;
  h: number;
}

/**
 * Guestroom rectangles, keyed by the position the CRM stores on the unit.
 *
 * Bottom band: partition walls at x = 72.1, 146.6, 222.8, 298.9, 375.1, 451.3,
 * 526.0, spanning y 504.0 to 689.0.
 *
 * Right band: partitions at y = 152.4, 217.0, 298.7, 380.5, 462.3, spanning
 * x 335.9 to 526.0. The plan numbers this band from the bottom up, so
 * guestroom 7 is the lowest and 10 the highest.
 */
const SLOTS: Record<number, Slot> = {
  1: { x: 72.1, y: 504.0, w: 74.5, h: 185.0 },
  2: { x: 146.6, y: 504.0, w: 76.2, h: 185.0 },
  3: { x: 222.8, y: 504.0, w: 76.1, h: 185.0 },
  4: { x: 298.9, y: 504.0, w: 76.2, h: 185.0 },
  5: { x: 375.1, y: 504.0, w: 76.2, h: 185.0 },
  6: { x: 451.3, y: 504.0, w: 74.7, h: 185.0 },

  7: { x: 335.9, y: 380.5, w: 190.1, h: 81.8 },
  8: { x: 335.9, y: 298.7, w: 190.1, h: 81.8 },
  9: { x: 335.9, y: 217.0, w: 190.1, h: 81.7 },
  10: { x: 335.9, y: 152.4, w: 190.1, h: 64.6 },
};

/* ------------------------------------------------------------------ *
 * Colour
 *
 * Translucent, because the architect's linework, dimensions and room labels
 * have to stay readable underneath. A status wash that hides the drawing
 * defeats the reason for using the drawing.
 * ------------------------------------------------------------------ */

const STATUS_FILL: Record<string, string> = {
  Available: "rgb(16 185 129 / 0.26)",
  Held: "rgb(245 158 11 / 0.30)",
  Blocked: "rgb(139 92 246 / 0.28)",
  Booked: "rgb(14 165 233 / 0.28)",
  Sold: "rgb(244 63 94 / 0.30)",
  NotForSale: "rgb(148 163 184 / 0.28)",
};

const STATUS_STROKE: Record<string, string> = {
  Available: "rgb(16 185 129 / 0.8)",
  Held: "rgb(245 158 11 / 0.85)",
  Blocked: "rgb(139 92 246 / 0.8)",
  Booked: "rgb(14 165 233 / 0.8)",
  Sold: "rgb(244 63 94 / 0.85)",
  NotForSale: "rgb(148 163 184 / 0.75)",
};

/* ------------------------------------------------------------------ *
 * Component
 * ------------------------------------------------------------------ */

export function FloorPlate({
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
  const placed = React.useMemo(
    () =>
      [...units]
        .sort((a, b) => a.position - b.position)
        .map((unit) => ({ unit, slot: SLOTS[unit.position] }))
        .filter((entry): entry is { unit: BoardUnit; slot: Slot } => entry.slot !== undefined),
    [units]
  );

  if (placed.length === 0) return null;

  return (
    <svg
      viewBox={`${VIEW.x} ${VIEW.y} ${VIEW.w} ${VIEW.h}`}
      className={cn("h-auto w-full", className)}
      role="img"
      aria-label="Architect's floor plan with the selected unit highlighted"
    >
      {/* The drawing is black on white and stays that way in dark mode — an
          inverted architectural plan reads as a negative, not as a theme. */}
      <rect x={VIEW.x} y={VIEW.y} width={VIEW.w} height={VIEW.h} fill="#ffffff" />

      <image href={PLAN_SRC} x={0} y={0} width={PAGE.w} height={PAGE.h} />

      {placed.map(({ unit, slot }) => {
        const selected = unit.id === selectedId;

        return (
          <g
            key={unit.id}
            onClick={onSelect ? () => onSelect(unit) : undefined}
            className={onSelect ? "cursor-pointer" : undefined}
          >
            <rect
              x={slot.x}
              y={slot.y}
              width={slot.w}
              height={slot.h}
              fill={STATUS_FILL[unit.status] ?? STATUS_FILL.NotForSale}
              stroke={selected ? "currentColor" : STATUS_STROKE[unit.status] ?? STATUS_STROKE.NotForSale}
              strokeWidth={selected ? 2.6 : 0.7}
              className={selected ? "text-primary" : undefined}
            />

            {/* A second inset outline on the selected room. On a printed or
                photocopied plan a colour change alone can vanish, and the room
                a buyer is being shown has to survive the photocopier. */}
            {selected ? (
              <rect
                x={slot.x + 3.2}
                y={slot.y + 3.2}
                width={slot.w - 6.4}
                height={slot.h - 6.4}
                fill="none"
                stroke="currentColor"
                strokeWidth={0.9}
                strokeDasharray="4 3"
                className="text-primary"
              />
            ) : null}

            {/* The CRM's unit number, set against the architect's own room
                label rather than over it. */}
            <text
              x={slot.x + slot.w / 2}
              y={slot.y + 13}
              textAnchor="middle"
              style={{ fontSize: 8, fontWeight: 700 }}
              className={selected ? "fill-primary" : "fill-foreground"}
            >
              {unit.unitNumber.replace(/^Studio-/, "")}
            </text>

            <text
              x={slot.x + slot.w / 2}
              y={slot.y + 21}
              textAnchor="middle"
              style={{ fontSize: 5.6 }}
              fill="#444444"
            >
              {formatIndian(unit.superArea ?? 0)} sq ft
              {unit.plcPerSqft > 0 ? " · PLC" : ""}
            </text>
          </g>
        );
      })}
    </svg>
  );
}
