"use client";

import * as React from "react";
import type { LucideIcon } from "lucide-react";

import { Skeleton } from "@/components/ui/skeleton";
import {
  Tooltip,
  TooltipContent,
  TooltipTrigger,
} from "@/components/ui/tooltip";
import { cn } from "@/lib/utils";

/* ------------------------------------------------------------------ *
 * Palette
 *
 * One place for the semantic colours the workspace uses, so a "danger"
 * number reads the same on the leads list, the follow-up queue and the
 * inventory board.
 * ------------------------------------------------------------------ */

export type Tone =
  | "neutral"
  | "primary"
  | "success"
  | "warning"
  | "danger"
  | "info"
  | "violet";

export const TONE_TEXT: Record<Tone, string> = {
  neutral: "text-foreground",
  primary: "text-primary",
  success: "text-emerald-600 dark:text-emerald-400",
  warning: "text-amber-600 dark:text-amber-400",
  danger: "text-red-600 dark:text-red-400",
  info: "text-sky-600 dark:text-sky-400",
  violet: "text-violet-600 dark:text-violet-400",
};

export const TONE_FILL: Record<Tone, string> = {
  neutral: "bg-zinc-400",
  primary: "bg-primary",
  success: "bg-emerald-500",
  warning: "bg-amber-500",
  danger: "bg-red-500",
  info: "bg-sky-500",
  violet: "bg-violet-500",
};

export const TONE_SOFT: Record<Tone, string> = {
  neutral: "border-zinc-400/30 bg-zinc-400/10 text-zinc-600 dark:text-zinc-300",
  primary: "border-primary/25 bg-primary/10 text-primary",
  success: "border-emerald-500/25 bg-emerald-500/10 text-emerald-700 dark:text-emerald-400",
  warning: "border-amber-500/25 bg-amber-500/10 text-amber-700 dark:text-amber-400",
  danger: "border-red-500/25 bg-red-500/10 text-red-700 dark:text-red-400",
  info: "border-sky-500/25 bg-sky-500/10 text-sky-700 dark:text-sky-400",
  violet: "border-violet-500/25 bg-violet-500/10 text-violet-700 dark:text-violet-400",
};

/** Stroke colours for the inline SVG visuals, which cannot use Tailwind classes. */
const TONE_STROKE: Record<Tone, string> = {
  neutral: "var(--muted-foreground)",
  primary: "var(--primary)",
  success: "#10b981",
  warning: "#f59e0b",
  danger: "#ef4444",
  info: "#0ea5e9",
  violet: "#8b5cf6",
};

/* ------------------------------------------------------------------ *
 * Metric tiles
 * ------------------------------------------------------------------ */

export interface Metric {
  label: string;
  value: React.ReactNode;
  hint?: string;
  tone?: Tone;
  icon?: LucideIcon;
  /** Small series drawn under the number — trend at a glance. */
  spark?: number[];
  /** Fraction 0–1 rendered as a progress bar under the number. */
  progress?: number;
  onClick?: () => void;
}

/**
 * The strip of numbers above a list.
 *
 * Deliberately tight: on a dense workspace these compete with the table for
 * vertical space, so each tile is one line of label and one of value, with any
 * extra detail moved into a tooltip.
 */
export function MetricStrip({
  metrics,
  loading = false,
  className,
}: {
  metrics: Metric[];
  loading?: boolean;
  className?: string;
}) {
  if (loading) {
    return (
      <div className={cn("grid gap-px overflow-hidden rounded border bg-border", className)}
        style={{ gridTemplateColumns: `repeat(auto-fit, minmax(140px, 1fr))` }}
      >
        {Array.from({ length: 5 }, (_, i) => (
          <div key={i} className="bg-card p-2.5">
            <Skeleton className="mb-1.5 h-3 w-16" />
            <Skeleton className="h-5 w-12" />
          </div>
        ))}
      </div>
    );
  }

  return (
    <div
      className={cn(
        "grid gap-px overflow-hidden rounded border bg-border",
        className
      )}
      style={{ gridTemplateColumns: `repeat(auto-fit, minmax(140px, 1fr))` }}
    >
      {metrics.map((metric) => {
        const tone = metric.tone ?? "neutral";
        const Icon = metric.icon;

        const body = (
          <div
            className={cn(
              "flex min-w-0 flex-col gap-0.5 bg-card p-2.5 text-left transition-colors",
              metric.onClick && "hover:bg-muted/50"
            )}
          >
            <span className="flex items-center gap-1 truncate text-[10.5px] font-medium tracking-wide text-muted-foreground uppercase">
              {Icon ? <Icon className="size-3 shrink-0" /> : null}
              {metric.label}
            </span>

            <span
              className={cn(
                "text-[19px] leading-tight font-semibold tabular-nums",
                TONE_TEXT[tone]
              )}
            >
              {metric.value}
            </span>

            {metric.spark && metric.spark.length > 1 ? (
              <Sparkline values={metric.spark} tone={tone} className="mt-0.5" />
            ) : metric.progress !== undefined ? (
              <span className="mt-1 block h-1 w-full overflow-hidden rounded-full bg-muted">
                <span
                  className={cn("block h-full rounded-full", TONE_FILL[tone])}
                  style={{ width: `${Math.min(100, Math.max(0, metric.progress * 100))}%` }}
                />
              </span>
            ) : null}
          </div>
        );

        const wrapped = metric.onClick ? (
          <button type="button" onClick={metric.onClick} className="min-w-0 text-left">
            {body}
          </button>
        ) : (
          body
        );

        if (!metric.hint) {
          return <React.Fragment key={metric.label}>{wrapped}</React.Fragment>;
        }

        return (
          <Tooltip key={metric.label}>
            <TooltipTrigger asChild>{wrapped}</TooltipTrigger>
            <TooltipContent className="max-w-xs">{metric.hint}</TooltipContent>
          </Tooltip>
        );
      })}
    </div>
  );
}

/* ------------------------------------------------------------------ *
 * Inline visuals
 * ------------------------------------------------------------------ */

/** A trend line small enough to sit inside a table cell or a metric tile. */
export function Sparkline({
  values,
  tone = "primary",
  width = 90,
  height = 18,
  className,
}: {
  values: number[];
  tone?: Tone;
  width?: number;
  height?: number;
  className?: string;
}) {
  if (values.length < 2) return null;

  const min = Math.min(...values);
  const max = Math.max(...values);
  const span = max - min || 1;

  const points = values.map((value, index) => {
    const x = (index / (values.length - 1)) * width;
    // SVG y grows downward, so the value is inverted to draw the right way up.
    const y = height - ((value - min) / span) * (height - 2) - 1;
    return `${x.toFixed(1)},${y.toFixed(1)}`;
  });

  const stroke = TONE_STROKE[tone];

  return (
    <svg
      viewBox={`0 0 ${width} ${height}`}
      width={width}
      height={height}
      className={cn("overflow-visible", className)}
      role="img"
      aria-label="Trend"
      preserveAspectRatio="none"
    >
      <polyline
        points={`0,${height} ${points.join(" ")} ${width},${height}`}
        fill={stroke}
        fillOpacity={0.12}
        stroke="none"
      />
      <polyline
        points={points.join(" ")}
        fill="none"
        stroke={stroke}
        strokeWidth={1.4}
        strokeLinecap="round"
        strokeLinejoin="round"
      />
    </svg>
  );
}

/** A column chart at cell scale — used for per-day and per-weekday counts. */
export function MiniBars({
  values,
  labels,
  tone = "primary",
  height = 34,
  className,
}: {
  values: number[];
  labels?: string[];
  tone?: Tone;
  height?: number;
  className?: string;
}) {
  if (values.length === 0) return null;
  const max = Math.max(...values, 1);

  return (
    <div className={cn("flex items-end gap-[2px]", className)} style={{ height }}>
      {values.map((value, index) => (
        <Tooltip key={index}>
          <TooltipTrigger asChild>
            <div
              className={cn(
                "min-w-[3px] flex-1 rounded-t-[2px] transition-opacity hover:opacity-70",
                TONE_FILL[tone]
              )}
              style={{ height: `${Math.max(2, (value / max) * height)}px` }}
            />
          </TooltipTrigger>
          <TooltipContent className="text-[11px]">
            {labels?.[index] ? `${labels[index]}: ` : ""}
            {value.toLocaleString()}
          </TooltipContent>
        </Tooltip>
      ))}
    </div>
  );
}

/* ------------------------------------------------------------------ *
 * Distribution bar
 * ------------------------------------------------------------------ */

export interface DistributionSegment {
  key: string;
  label: string;
  count: number;
  tone: Tone;
}

/**
 * A single stacked bar showing how a result set splits across one dimension,
 * with the legend doubling as a filter control.
 *
 * This is the densest honest way to answer "what is in this list" — it costs one
 * row of height and replaces a pie chart, a legend and a table of counts.
 */
export function DistributionBar({
  title,
  segments,
  onSelect,
  className,
}: {
  title?: string;
  segments: DistributionSegment[];
  onSelect?: (key: string) => void;
  className?: string;
}) {
  const total = segments.reduce((sum, segment) => sum + segment.count, 0);
  if (total === 0) return null;

  return (
    <div className={cn("flex min-w-0 flex-col gap-1", className)}>
      {title ? (
        <span className="text-[10.5px] font-medium tracking-wide text-muted-foreground uppercase">
          {title}
        </span>
      ) : null}

      <div className="flex h-2 w-full overflow-hidden rounded-full bg-muted">
        {segments.map((segment) => (
          <Tooltip key={segment.key}>
            <TooltipTrigger asChild>
              <div
                className={cn("h-full transition-opacity hover:opacity-75", TONE_FILL[segment.tone])}
                style={{ width: `${(segment.count / total) * 100}%` }}
              />
            </TooltipTrigger>
            <TooltipContent className="text-[11px]">
              {segment.label}: {segment.count.toLocaleString()} (
              {((segment.count / total) * 100).toFixed(0)}%)
            </TooltipContent>
          </Tooltip>
        ))}
      </div>

      <div className="flex flex-wrap items-center gap-x-2.5 gap-y-0.5">
        {segments.map((segment) => {
          const content = (
            <span className="inline-flex items-center gap-1 text-[11px] whitespace-nowrap">
              <span className={cn("size-1.5 rounded-full", TONE_FILL[segment.tone])} />
              <span className="text-muted-foreground">{segment.label}</span>
              <span className="font-medium tabular-nums">{segment.count.toLocaleString()}</span>
            </span>
          );

          return onSelect ? (
            <button
              key={segment.key}
              type="button"
              onClick={() => onSelect(segment.key)}
              className="rounded px-0.5 hover:bg-muted"
            >
              {content}
            </button>
          ) : (
            <React.Fragment key={segment.key}>{content}</React.Fragment>
          );
        })}
      </div>
    </div>
  );
}

/* ------------------------------------------------------------------ *
 * Small primitives used across list cells
 * ------------------------------------------------------------------ */

/** A 0–100 score with a proportional bar — the AI score cell. */
export function ScoreCell({
  score,
  band,
}: {
  score: number | null | undefined;
  band?: string | null;
}) {
  if (score === null || score === undefined) {
    return <span className="text-muted-foreground">—</span>;
  }

  const tone: Tone =
    band === "Hot" || score >= 75
      ? "danger"
      : band === "Warm" || score >= 50
        ? "warning"
        : band === "Cool" || score >= 25
          ? "info"
          : "neutral";

  return (
    <span className="flex items-center gap-1.5">
      <span className={cn("w-6 text-right font-semibold tabular-nums", TONE_TEXT[tone])}>
        {score}
      </span>
      <span className="h-1.5 w-9 overflow-hidden rounded-full bg-muted">
        <span
          className={cn("block h-full rounded-full", TONE_FILL[tone])}
          style={{ width: `${score}%` }}
        />
      </span>
    </span>
  );
}

/** A coloured dot plus label — status and stage cells. */
export function StatusDot({
  label,
  tone = "neutral",
}: {
  label: string;
  tone?: Tone;
}) {
  return (
    <span className="inline-flex items-center gap-1.5 whitespace-nowrap">
      <span className={cn("size-1.5 shrink-0 rounded-full", TONE_FILL[tone])} />
      {label}
    </span>
  );
}

/** A compact outlined pill for enumerated values. */
export function Pill({
  children,
  tone = "neutral",
  className,
}: {
  children: React.ReactNode;
  tone?: Tone;
  className?: string;
}) {
  return (
    <span
      className={cn(
        "inline-flex h-[18px] items-center rounded border px-1.5 text-[11px] font-normal whitespace-nowrap",
        TONE_SOFT[tone],
        className
      )}
    >
      {children}
    </span>
  );
}
