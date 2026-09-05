"use client";

import * as React from "react";

import { cn } from "@/lib/utils";

/*
 * One loading identity for the whole CRM, drawn in SVG and CSS rather than
 * pulled from a Lottie player: a few kilobytes instead of a runtime plus a JSON
 * payload, it inherits the theme through currentColor, and it keeps rendering
 * when the network is exactly what is being waited on.
 *
 * Four decisions make it read as the product's own rather than as a spinner.
 *
 * The mark is a lockup, not an animation: a tower — what this company sells —
 * set in a brand tile with the weight of an app icon, framed by one thin
 * determinate ring. A boot screen is the one moment every user looks straight
 * at the brand, so the brand is what is put there.
 *
 * The motion is slow. Fast spin is the cheapest signal in software; a ring that
 * takes two and a half seconds to come round, with easing that never snaps,
 * reads as considered where a 0.6s spin reads as a template.
 *
 * The progress is *determinate*. Both the splash and the section state are held
 * to a known minimum, so the ring and the rail report the real fraction of that
 * hold. They stop short of full and wait there until the data actually lands,
 * because the last percent belongs to the response and not to the clock.
 *
 * Everything animates on transform and opacity alone. A loading screen that
 * paints while the main thread is parsing the response is a loading screen that
 * stutters at precisely the moment it exists to cover.
 */

/** The hold every section's loading state is sized against. */
export const SECTION_HOLD_MS = 1200;

/** The hold the boot splash is sized against — a shell has more to set up. */
export const BOOT_HOLD_MS = 1800;

const RING_RADIUS = 46;
const RING_CIRCUMFERENCE = 2 * Math.PI * RING_RADIUS;

/* ------------------------------------------------------------------ *
 * The mark
 * ------------------------------------------------------------------ */

/**
 * The tower, in the brand tile. Its floors arrive in sequence and hold, so the
 * mark is building something rather than merely turning — the difference
 * between "working" and "waiting" as a viewer reads it.
 */
function TowerTile({ size }: { size: number }) {
  // Three towers of different heights, rising in sequence. A skyline reads as
  // real estate at sixteen pixels; a single block reads as a document icon,
  // which is what the first draft of this looked like.
  const towers = [
    { x: 38, y: 46, width: 8, height: 13 },
    { x: 47.5, y: 39, width: 9, height: 20 },
    { x: 58, y: 50, width: 8, height: 9 },
  ];

  return (
    <svg
      viewBox="0 0 100 100"
      width={size}
      height={size}
      role="presentation"
      aria-hidden="true"
      className="absolute inset-0"
    >
      {/* The tile: brand fill with a lighter top edge, the weight of an app icon. */}
      <defs>
        <linearGradient id="crm-tile" x1="0" y1="0" x2="0" y2="1">
          <stop offset="0%" stopColor="var(--primary)" stopOpacity="1" />
          <stop offset="100%" stopColor="var(--primary)" stopOpacity="0.85" />
        </linearGradient>
      </defs>

      <rect x="28" y="28" width="44" height="44" rx="12" fill="url(#crm-tile)" />
      <rect
        x="28"
        y="28"
        width="44"
        height="44"
        rx="12"
        fill="none"
        stroke="var(--primary-foreground)"
        strokeOpacity="0.25"
        strokeWidth="1"
      />

      <g fill="var(--primary-foreground)">
        {towers.map((tower, index) => (
          <rect
            key={tower.x}
            x={tower.x}
            y={tower.y}
            width={tower.width}
            height={tower.height}
            rx="1.5"
            className="crm-loader-floor"
            style={{
              animationDelay: `${index * 0.26}s`,
              // Grown from the base, the way a building goes up.
              transformOrigin: "50% 100%",
            }}
          />
        ))}

        {/* The ground the skyline stands on — never animated, so the mark
            still reads as a building between cycles. */}
        <rect x="35" y="60" width="30" height="2.6" rx="1.3" />
      </g>
    </svg>
  );
}

/** The thin ring that frames the tile, reporting a known fraction. */
function ProgressRing({ value, size }: { value: number; size: number }) {
  const offset = RING_CIRCUMFERENCE * (1 - Math.min(1, Math.max(0, value)));

  return (
    <svg
      viewBox="0 0 100 100"
      width={size}
      height={size}
      role="presentation"
      aria-hidden="true"
      className="absolute inset-0 text-primary"
    >
      <circle
        cx="50"
        cy="50"
        r={RING_RADIUS}
        fill="none"
        stroke="currentColor"
        strokeWidth="2"
        className="opacity-[0.14]"
      />

      {/* From twelve o'clock, so it reads as a dial rather than a shape. */}
      <circle
        cx="50"
        cy="50"
        r={RING_RADIUS}
        fill="none"
        stroke="currentColor"
        strokeWidth="2"
        strokeLinecap="round"
        strokeDasharray={RING_CIRCUMFERENCE}
        strokeDashoffset={offset}
        transform="rotate(-90 50 50)"
        className="transition-[stroke-dashoffset] duration-700 ease-out"
      />
    </svg>
  );
}

/** The same ring, sweeping — for the inline waits that have no duration. */
function SweepRing({ size }: { size: number }) {
  return (
    <svg
      viewBox="0 0 100 100"
      width={size}
      height={size}
      role="presentation"
      aria-hidden="true"
      className="absolute inset-0 text-primary"
    >
      <circle
        cx="50"
        cy="50"
        r={RING_RADIUS}
        fill="none"
        stroke="currentColor"
        strokeWidth="2"
        className="opacity-[0.14]"
      />

      <g className="crm-loader-orbit">
        <circle
          cx="50"
          cy="50"
          r={RING_RADIUS}
          fill="none"
          stroke="currentColor"
          strokeWidth="2"
          strokeLinecap="round"
          strokeDasharray={`${RING_CIRCUMFERENCE * 0.22} ${RING_CIRCUMFERENCE}`}
        />
      </g>
    </svg>
  );
}

/** Tile, ring and the glow that gives the lockup somewhere to stand. */
function Lockup({
  size,
  value,
}: {
  size: number;
  /** Omit for an indeterminate sweep. */
  value?: number;
}) {
  return (
    <div className="relative" style={{ width: size, height: size }}>
      <div
        aria-hidden="true"
        className="crm-loader-halo pointer-events-none absolute inset-[12%] rounded-[28%] bg-primary opacity-25 blur-2xl"
      />

      <TowerTile size={size} />
      {value === undefined ? <SweepRing size={size} /> : <ProgressRing value={value} size={size} />}
    </div>
  );
}

/**
 * The animated mark on its own — use this inside a button or a table cell.
 * Indeterminate, because an inline wait has no duration worth claiming.
 */
export function CrmLoaderMark({
  className,
  size = 72,
}: {
  className?: string;
  size?: number;
}) {
  return (
    <div className={cn("shrink-0", className)}>
      <Lockup size={size} />
    </div>
  );
}

/* ------------------------------------------------------------------ *
 * The rail
 * ------------------------------------------------------------------ */

/** A filled bar with a sheen crossing it, so a paused bar still breathes. */
function ProgressRail({ percent }: { percent: number }) {
  return (
    <div className="h-[3px] w-full overflow-hidden rounded-full bg-border">
      <div
        className="relative h-full overflow-hidden rounded-full bg-primary transition-[width] duration-700 ease-out"
        style={{ width: `${percent}%` }}
      >
        <div
          aria-hidden="true"
          className="crm-loader-shimmer absolute inset-y-0 w-1/2 bg-[linear-gradient(90deg,transparent,var(--primary-foreground),transparent)] opacity-50"
        />
      </div>
    </div>
  );
}

/* ------------------------------------------------------------------ *
 * Progress
 * ------------------------------------------------------------------ */

/**
 * What the boot has done so far, named.
 *
 * The stages are the real ones the shell goes through — the session is read
 * back, the tenant's workspace is resolved, then the first screen's data is
 * fetched — so the line under the mark is a report rather than decoration.
 */
const BOOT_STAGES = [
  { until: 0.34, label: "Restoring your session" },
  { until: 0.68, label: "Loading your workspace" },
  { until: 1, label: "Preparing your data" },
] as const;

/**
 * Advances over a known minimum hold and then waits.
 *
 * Ceilinged below full on purpose: the clock knows when the minimum elapses,
 * not when the data arrives, and a bar sitting at 100% over a blank screen is
 * the one thing worse than a bar sitting at 92%.
 */
function useHoldProgress(ms: number, ceiling = 0.92) {
  const [value, setValue] = React.useState(0);

  React.useEffect(() => {
    const started = Date.now();

    const id = window.setInterval(() => {
      setValue(Math.min(ceiling, ((Date.now() - started) / ms) * ceiling));
    }, 80);

    return () => window.clearInterval(id);
  }, [ms, ceiling]);

  return value;
}

/**
 * Keeps a loading state up for a minimum, however fast the data arrives.
 *
 * A list that resolves from cache in forty milliseconds otherwise flashes a
 * spinner for two frames, which reads as a glitch rather than as work. Returns
 * true while the caller is still loading *or* the floor has not elapsed — so a
 * slow fetch is never cut short, and a fast one is never a flicker.
 *
 * The timer is deliberately not cancelled when `loading` drops back to false —
 * only on unmount. Keying its effect on `[loading, ms]` and clearing the
 * timeout on every re-run reads as the obvious implementation, but it means a
 * fetch that resolves before the floor elapses cancels the very timer that was
 * ever going to set `floorElapsed`, and the hook then reports "loading"
 * forever: `loading` is false, `floorElapsed` never became true, so
 * `loading || !floorElapsed` stays true with no further state change left to
 * fix it. A `ref` remembers whether a floor is already running so a loading
 * flag toggling off mid-floor lets that timer finish on its own.
 */
export function useMinimumLoading(loading: boolean, ms = SECTION_HOLD_MS) {
  const [floorElapsed, setFloorElapsed] = React.useState(!loading);
  const timer = React.useRef<number | null>(null);

  React.useEffect(() => {
    if (!loading || timer.current !== null) return;

    setFloorElapsed(false);
    timer.current = window.setTimeout(() => {
      timer.current = null;
      setFloorElapsed(true);
    }, ms);
  }, [loading, ms]);

  React.useEffect(
    () => () => {
      if (timer.current === null) return;

      window.clearTimeout(timer.current);

      // Releasing the slot matters as much as clearing the timeout. React runs
      // mount / unmount / remount in development, and a handle left behind here
      // makes the remounted effect see a floor already running, take its early
      // return, and never start one — so `floorElapsed` stays false and the
      // spinner outlives the data it was waiting for.
      timer.current = null;
    },
    []
  );

  return loading || !floorElapsed;
}

/* ------------------------------------------------------------------ *
 * In-page state
 * ------------------------------------------------------------------ */

/**
 * The in-page loading state, sized to sit inside a PagePanel body where a table
 * or a chart will land.
 */
export function CrmLoadingState({
  label = "Loading",
  detail,
  size = 84,
  className,
  duration = SECTION_HOLD_MS,
}: {
  label?: string;
  detail?: string;
  size?: number;
  className?: string;
  duration?: number;
}) {
  const value = useHoldProgress(duration);

  return (
    <div
      role="status"
      aria-live="polite"
      aria-busy="true"
      className={cn(
        "flex min-h-64 flex-1 flex-col items-center justify-center gap-5 p-8 text-center",
        className
      )}
    >
      <Lockup size={size} value={value} />

      <div className="space-y-1">
        <p className="text-[13.5px] font-medium text-foreground">{label}</p>
        {detail ? (
          <p className="max-w-xs text-[12px] text-muted-foreground">{detail}</p>
        ) : null}
      </div>

      <div className="w-44">
        <ProgressRail percent={Math.round(value * 100)} />
      </div>
    </div>
  );
}

/* ------------------------------------------------------------------ *
 * Boot splash
 * ------------------------------------------------------------------ */

/**
 * The full-screen boot animation.
 *
 * Shown while the session is being restored, while a tenant is being swapped,
 * and on the first paint of an app — the three moments where the CRM has chrome
 * but nothing to put in it, and a bare white page reads as a broken build.
 */
export function CrmSplash({
  label = "Loading your workspace",
  detail = "Jeet Homes Solution",
  className,
  duration = BOOT_HOLD_MS,
}: {
  label?: string;
  detail?: string;
  className?: string;
  duration?: number;
}) {
  const value = useHoldProgress(duration);
  const stage = BOOT_STAGES.find((s) => value <= s.until) ?? BOOT_STAGES[0];
  const percent = Math.round(value * 100);

  // The caller's own label, where it set one — the company chooser says
  // "Checking your session" rather than the default.
  const custom = label !== "Loading your workspace" ? label : null;

  return (
    <div
      role="status"
      aria-live="polite"
      aria-busy="true"
      className={cn(
        "fixed inset-0 z-[100] flex flex-col items-center justify-center overflow-hidden bg-background",
        className
      )}
    >
      {/* Depth in two layers: a brand wash so the first paint does not read as
          an error page, and a faint grid to give the wash something to fall on. */}
      <div
        aria-hidden="true"
        className="pointer-events-none absolute inset-0 bg-[radial-gradient(55%_45%_at_50%_36%,var(--accent),transparent_70%)] opacity-70"
      />
      <div
        aria-hidden="true"
        className="pointer-events-none absolute inset-0 opacity-[0.04] [background-image:linear-gradient(to_right,var(--foreground)_1px,transparent_1px),linear-gradient(to_bottom,var(--foreground)_1px,transparent_1px)] [background-size:48px_48px]"
      />

      <div className="relative flex w-[min(23rem,calc(100vw-3rem))] flex-col items-center">
        <Lockup size={112} value={value} />

        <p className="mt-8 text-[16px] font-semibold tracking-tight text-foreground">
          {detail}
        </p>
        <p className="mt-1 text-[10.5px] tracking-[0.2em] text-muted-foreground uppercase">
          CRM &amp; ERP
        </p>

        <div className="mt-9 w-full">
          <ProgressRail percent={percent} />
        </div>

        <div className="mt-3 flex w-full items-baseline justify-between gap-3">
          {/* Keyed on the stage so each one animates in as it takes over. */}
          <span key={stage.label} className="crm-loader-stage text-[12.5px] text-foreground">
            {stage.label}
          </span>
          <span className="text-[11px] text-muted-foreground tabular-nums">
            {percent}%
          </span>
        </div>

        {custom ? (
          <p className="mt-6 text-[12px] text-muted-foreground">{custom}</p>
        ) : null}
      </div>
    </div>
  );
}

/**
 * Holds the splash on screen for a minimum duration while background data
 * loads, so the shell does not flash through three states in half a second.
 */
export function useMinimumDelay(ms = BOOT_HOLD_MS) {
  const [elapsed, setElapsed] = React.useState(false);

  React.useEffect(() => {
    const id = setTimeout(() => setElapsed(true), ms);
    return () => clearTimeout(id);
  }, [ms]);

  return elapsed;
}
