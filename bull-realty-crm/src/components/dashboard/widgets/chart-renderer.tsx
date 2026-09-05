"use client";

import * as React from "react";
import {
  Area,
  AreaChart,
  Bar,
  BarChart,
  CartesianGrid,
  Cell,
  LabelList,
  Line,
  LineChart,
  Pie,
  PieChart,
  PolarAngleAxis,
  RadialBar,
  RadialBarChart,
  Treemap,
  XAxis,
  YAxis,
} from "recharts";

import {
  ChartContainer,
  ChartLegend,
  ChartLegendContent,
  ChartTooltip,
  ChartTooltipContent,
  type ChartConfig,
} from "@/components/ui/chart";
import { colorAt, heatColor } from "@/lib/dashboard/palette";
import { formatValue, share } from "@/lib/dashboard/format";
import type { AggregatePoint, AggregateResponse } from "@/lib/dashboard/analytics";
import type { ChartStyle, WidgetOptions } from "@/lib/dashboard/types";
import { cn } from "@/lib/utils";

/* ------------------------------------------------------------------ *
 * Shared chrome
 * ------------------------------------------------------------------ */

const axisProps = {
  tickLine: false,
  axisLine: false,
  tickMargin: 6,
  className: "text-[10.5px] fill-muted-foreground",
} as const;

export interface ChartProps {
  result: AggregateResponse;
  style: ChartStyle;
  options: WidgetOptions;
  /** Clicking a mark filters the rest of the dashboard by that value. */
  onDrill?: (key: string, label: string) => void;
  className?: string;
}

/** Flat rows in the shape Recharts wants: one object per group. */
function toChartRows(result: AggregateResponse, stack100: boolean) {
  return result.rows.map((row) => {
    const entry: Record<string, string | number> = {
      key: row.key,
      label: row.label,
      __total: row.total,
      __records: row.records,
    };

    for (const series of result.series) {
      const raw = row.values[series] ?? 0;
      entry[series] = stack100 && row.total ? (raw / row.total) * 100 : raw;
    }

    return entry;
  });
}

function buildConfig(series: string[]): ChartConfig {
  return Object.fromEntries(
    series.map((name, index) => [name, { label: name, color: colorAt(index) }])
  );
}

/* ------------------------------------------------------------------ *
 * Renderer
 * ------------------------------------------------------------------ */

export function ChartRenderer({
  result,
  style,
  options,
  onDrill,
  className,
}: ChartProps) {
  // Clicking a legend entry hides that series — the fastest way to read a busy
  // stacked chart is to take the biggest band out of it for a moment.
  const [hidden, setHidden] = React.useState<Set<string>>(new Set());

  // A style or breakdown change can retire a series that is still in the hidden
  // set; dropping the stale names keeps a later series of the same name from
  // coming back invisible. Adjusted during render rather than in an effect —
  // React re-runs this component before committing, so the chart never paints
  // one frame with the wrong series hidden.
  const seriesKey = result.series.join("|");
  const [lastSeriesKey, setLastSeriesKey] = React.useState(seriesKey);
  if (lastSeriesKey !== seriesKey) {
    setLastSeriesKey(seriesKey);
    setHidden(new Set());
  }

  const visible = result.series.filter((name) => !hidden.has(name));
  const config = buildConfig(result.series);
  const rows = toChartRows(result, options.stack100);
  const percent = options.stack100;

  function toggleSeries(name: string) {
    setHidden((current) => {
      const next = new Set(current);
      if (next.has(name)) next.delete(name);
      else next.add(name);
      // Hiding the last visible series leaves an empty chart with no way back.
      return next.size === result.series.length ? current : next;
    });
  }

  if (result.rows.length === 0) {
    return (
      <div className="flex h-full min-h-[120px] items-center justify-center px-4 text-center">
        <p className="text-[12.5px] text-muted-foreground">
          No {result.datasetLabel.toLowerCase()} match this widget&apos;s filters.
        </p>
      </div>
    );
  }

  const shared = { result, rows, config, visible, options, onDrill, percent };

  switch (style) {
    case "metric":
      return <MetricView {...shared} className={className} />;
    case "table":
      return <TableView {...shared} className={className} />;
    case "funnel":
      return <FunnelView {...shared} className={className} />;
    case "heatmap":
      return <HeatmapView {...shared} className={className} />;
    default:
      return (
        <ChartContainer
          config={config}
          className={cn("w-full", className)}
        >
          {renderRechart(style, shared, toggleSeries, hidden)}
        </ChartContainer>
      );
  }
}

/* ------------------------------------------------------------------ *
 * Recharts styles
 * ------------------------------------------------------------------ */

interface ViewProps {
  result: AggregateResponse;
  rows: Record<string, string | number>[];
  config: ChartConfig;
  visible: string[];
  options: WidgetOptions;
  onDrill?: (key: string, label: string) => void;
  percent: boolean;
  className?: string;
}

function renderRechart(
  style: ChartStyle,
  props: ViewProps,
  toggleSeries: (name: string) => void,
  hidden: Set<string>
) {
  const { result, rows, visible, options, onDrill, percent } = props;

  const tick = (value: number) =>
    percent ? `${Math.round(value)}%` : formatValue(value, result.format, "axis");

  const tooltip = (
    <ChartTooltip
      content={
        <ChartTooltipContent
          labelKey="label"
          formatter={(value, name) => (
            <span className="flex w-full items-center justify-between gap-3">
              <span className="text-muted-foreground">{name}</span>
              <span className="font-medium tabular-nums">
                {percent
                  ? `${Number(value).toFixed(1)}%`
                  : formatValue(Number(value), result.format)}
              </span>
            </span>
          )}
        />
      }
    />
  );

  const legend =
    options.legend && result.series.length > 1 ? (
      <ChartLegend
        content={<ChartLegendContent />}
        onClick={(entry: { value?: string }) =>
          entry?.value && toggleSeries(entry.value)
        }
        className="cursor-pointer [&_.recharts-legend-item]:cursor-pointer"
      />
    ) : null;

  const grid = options.grid ? (
    <CartesianGrid
      vertical={style === "horizontalBar"}
      horizontal={style !== "horizontalBar"}
      strokeDasharray="3 3"
      opacity={0.4}
    />
  ) : null;

  // Recharts types the click payload differently for every mark, so this takes
  // `unknown` and narrows — a function accepting `unknown` satisfies all of
  // them, which a hand-written payload shape would not.
  const click = onDrill
    ? (entry: unknown) => {
        const row = entry as { key?: unknown; label?: unknown };
        if (typeof row?.key !== "string") return;
        onDrill(row.key, typeof row.label === "string" ? row.label : row.key);
      }
    : undefined;

  /** Same reason: `LabelFormatter` is wider than `number`. */
  const labelFormatter = (value: unknown) =>
    formatValue(Number(value), result.format, "axis");

  const colorOf = (name: string) =>
    colorAt(result.series.indexOf(name));

  switch (style) {
    case "line":
      return (
        <LineChart data={rows} margin={{ left: -14, right: 10, top: 10, bottom: 0 }}>
          {grid}
          <XAxis dataKey="label" {...axisProps} interval="preserveStartEnd" />
          <YAxis {...axisProps} width={52} tickFormatter={tick} />
          {tooltip}
          {legend}
          {visible.map((name) => (
            <Line
              key={name}
              dataKey={name}
              type={options.curve}
              stroke={colorOf(name)}
              strokeWidth={2}
              dot={rows.length <= 14 ? { r: 2.5, strokeWidth: 0, fill: colorOf(name) } : false}
              activeDot={{ r: 4 }}
              isAnimationActive={false}
            >
              {options.values ? (
                <LabelList
                  dataKey={name}
                  position="top"
                  className="fill-muted-foreground text-[10px]"
                  formatter={labelFormatter}
                />
              ) : null}
            </Line>
          ))}
        </LineChart>
      );

    case "area":
    case "stackedArea":
      return (
        <AreaChart data={rows} margin={{ left: -14, right: 10, top: 10, bottom: 0 }}>
          <defs>
            {visible.map((name) => (
              <linearGradient key={name} id={`fill-${slug(name)}`} x1="0" y1="0" x2="0" y2="1">
                <stop offset="0%" stopColor={colorOf(name)} stopOpacity={0.4} />
                <stop offset="100%" stopColor={colorOf(name)} stopOpacity={0.03} />
              </linearGradient>
            ))}
          </defs>
          {grid}
          <XAxis dataKey="label" {...axisProps} interval="preserveStartEnd" />
          <YAxis {...axisProps} width={52} tickFormatter={tick} />
          {tooltip}
          {legend}
          {visible.map((name) => (
            <Area
              key={name}
              dataKey={name}
              type={options.curve}
              stackId={style === "stackedArea" ? "a" : undefined}
              stroke={colorOf(name)}
              strokeWidth={2}
              fill={`url(#fill-${slug(name)})`}
              isAnimationActive={false}
            />
          ))}
        </AreaChart>
      );

    case "horizontalBar":
      return (
        <BarChart
          data={rows}
          layout="vertical"
          margin={{ left: 4, right: 16, top: 6, bottom: 0 }}
        >
          {grid}
          <XAxis type="number" {...axisProps} tickFormatter={tick} />
          <YAxis
            type="category"
            dataKey="label"
            {...axisProps}
            width={110}
            interval={0}
          />
          {tooltip}
          {legend}
          {visible.map((name) => (
            <Bar
              key={name}
              dataKey={name}
              stackId={result.series.length > 1 ? "a" : undefined}
              fill={colorOf(name)}
              radius={result.series.length > 1 ? 0 : [0, 4, 4, 0]}
              maxBarSize={26}
              onClick={click}
              cursor={onDrill ? "pointer" : undefined}
              isAnimationActive={false}
            >
              {result.series.length === 1
                ? rows.map((row, index) => (
                    <Cell key={String(row.key)} fill={colorAt(index)} />
                  ))
                : null}
              {options.values ? (
                <LabelList
                  dataKey={name}
                  position="right"
                  className="fill-muted-foreground text-[10px]"
                  formatter={labelFormatter}
                />
              ) : null}
            </Bar>
          ))}
        </BarChart>
      );

    case "pie":
    case "donut":
      return (
        <PieChart margin={{ top: 4, bottom: 4 }}>
          {tooltip}
          {legend}
          <Pie
            data={rows}
            dataKey={visible[0] ?? result.series[0]}
            nameKey="label"
            innerRadius={style === "donut" ? "52%" : 0}
            outerRadius="80%"
            paddingAngle={style === "donut" ? 2 : 0}
            strokeWidth={0}
            onClick={click}
            cursor={onDrill ? "pointer" : undefined}
            isAnimationActive={false}
          >
            {rows.map((row, index) => (
              <Cell key={String(row.key)} fill={colorAt(index)} />
            ))}
            {options.values ? (
              <LabelList
                dataKey="label"
                className="fill-background text-[10px] font-medium"
              />
            ) : null}
          </Pie>
        </PieChart>
      );

    case "radial":
      return (
        <RadialBarChart
          data={rows.slice(0, 6).map((row, index) => ({
            ...row,
            fill: colorAt(index),
          }))}
          innerRadius="30%"
          outerRadius="95%"
          startAngle={90}
          endAngle={-270}
        >
          <PolarAngleAxis type="number" domain={[0, maxOf(rows, visible)]} tick={false} />
          {tooltip}
          {legend}
          <RadialBar
            dataKey={visible[0] ?? result.series[0]}
            background={{ fill: "var(--muted)" }}
            cornerRadius={6}
            onClick={click}
            cursor={onDrill ? "pointer" : undefined}
            isAnimationActive={false}
          />
        </RadialBarChart>
      );

    case "treemap":
      return (
        <Treemap
          data={rows.map((row, index) => ({
            name: row.label,
            size: Number(row[visible[0] ?? result.series[0]] ?? 0),
            fill: colorAt(index),
          }))}
          dataKey="size"
          nameKey="name"
          stroke="var(--background)"
          isAnimationActive={false}
        >
          {tooltip}
        </Treemap>
      );

    // bar, stackedBar and groupedBar share one chart; only the stackId differs.
    default: {
      const stacked = style === "stackedBar";
      return (
        <BarChart data={rows} margin={{ left: -14, right: 10, top: 10, bottom: 0 }}>
          {grid}
          <XAxis dataKey="label" {...axisProps} interval="preserveStartEnd" />
          <YAxis {...axisProps} width={52} tickFormatter={tick} />
          {tooltip}
          {legend}
          {visible.map((name) => (
            <Bar
              key={name}
              dataKey={name}
              stackId={stacked ? "a" : undefined}
              fill={colorOf(name)}
              radius={stacked ? 0 : [4, 4, 0, 0]}
              maxBarSize={48}
              onClick={click}
              cursor={onDrill ? "pointer" : undefined}
              isAnimationActive={false}
            >
              {result.series.length === 1 && !hidden.size
                ? rows.map((row, index) => (
                    <Cell key={String(row.key)} fill={colorAt(index)} />
                  ))
                : null}
              {options.values ? (
                <LabelList
                  dataKey={name}
                  position="top"
                  className="fill-muted-foreground text-[10px]"
                  formatter={labelFormatter}
                />
              ) : null}
            </Bar>
          ))}
        </BarChart>
      );
    }
  }
}

/* ------------------------------------------------------------------ *
 * Non-Recharts styles
 * ------------------------------------------------------------------ */

/** The whole query as one number — the KPI tile, driven by the same query. */
function MetricView({ result, className }: ViewProps) {
  // The largest group, not the first row: under a sequence or label sort the
  // first row is whatever comes first alphabetically or in stage order, and
  // calling that "largest" would be wrong.
  const top = result.rows.reduce<AggregatePoint | null>(
    (best, row) => (best === null || row.total > best.total ? row : best),
    null
  );

  return (
    <div className={cn("flex h-full flex-col justify-center gap-1 p-4", className)}>
      <span className="text-3xl leading-none font-semibold tracking-tight tabular-nums">
        {formatValue(result.total, result.format)}
      </span>
      <span className="text-[11.5px] text-muted-foreground">
        {result.measureLabel} across {result.matchedRecords.toLocaleString("en-IN")}{" "}
        record{result.matchedRecords === 1 ? "" : "s"}
      </span>
      {top && result.rows.length > 1 ? (
        <span className="mt-1 flex items-center gap-1.5 text-[11px] text-muted-foreground">
          <span
            className="size-2 shrink-0 rounded-[2px]"
            style={{ backgroundColor: colorAt(0) }}
          />
          Largest: {top.label} ({formatValue(top.total, result.format)})
        </span>
      ) : null}
    </div>
  );
}

/** The result as a dense table — the fallback that always reads exactly. */
function TableView({ result, options, onDrill, className }: ViewProps) {
  const grandTotal = result.rows.reduce((sum, row) => sum + row.total, 0);

  return (
    <div className={cn("overflow-auto", className)}>
      <table className="w-full text-sm">
        <thead className="sticky top-0 bg-card">
          <tr className="border-b text-left text-[10.5px] tracking-wide text-muted-foreground uppercase">
            <th className="px-3 py-1.5 font-medium">{result.dimensionLabel}</th>
            {result.series.map((name) => (
              <th key={name} className="px-2 py-1.5 text-right font-medium">
                {name}
              </th>
            ))}
            {result.series.length > 1 ? (
              <th className="px-2 py-1.5 text-right font-medium">Total</th>
            ) : null}
            {options.showShare ? (
              <th className="px-3 py-1.5 text-right font-medium">Share</th>
            ) : null}
          </tr>
        </thead>
        <tbody>
          {result.rows.map((row, index) => (
            <tr
              key={row.key}
              onClick={() => onDrill?.(row.key, row.label)}
              className={cn(
                "border-b last:border-0",
                onDrill && "cursor-pointer hover:bg-muted/50"
              )}
            >
              <td className="px-3 py-1.5">
                <span className="flex items-center gap-2">
                  <span
                    className="size-2 shrink-0 rounded-[2px]"
                    style={{ backgroundColor: colorAt(index) }}
                  />
                  <span className="truncate text-[12.5px]">{row.label}</span>
                </span>
              </td>
              {result.series.map((name) => (
                <td
                  key={name}
                  className="px-2 py-1.5 text-right text-[12.5px] tabular-nums"
                >
                  {formatValue(row.values[name] ?? 0, result.format)}
                </td>
              ))}
              {result.series.length > 1 ? (
                <td className="px-2 py-1.5 text-right text-[12.5px] font-medium tabular-nums">
                  {formatValue(row.total, result.format)}
                </td>
              ) : null}
              {options.showShare ? (
                <td className="px-3 py-1.5 text-right text-[12px] text-muted-foreground tabular-nums">
                  {share(row.total, grandTotal).toFixed(1)}%
                </td>
              ) : null}
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

/**
 * Stage-to-stage conversion.
 *
 * The drop-off column is the reason this is not just a bar chart: what matters
 * in a funnel is what fell out between two steps, not the height of either.
 */
function FunnelView({ result, onDrill, className }: ViewProps) {
  const max = Math.max(...result.rows.map((row) => row.total), 1);

  return (
    <div className={cn("flex flex-col justify-center gap-2 p-3", className)}>
      {result.rows.map((row, index) => {
        const previous = index === 0 ? null : result.rows[index - 1].total;
        // Stages do not only shrink — a pipeline can hold more deals at
        // Proposal than at Qualification. A negative drop-off is growth, and
        // has to read as "+67%" rather than the "−-67%" that negating twice
        // would print.
        const change =
          previous && previous > 0
            ? Math.round(((row.total - previous) / previous) * 100)
            : null;
        const width = Math.max(4, (row.total / max) * 100);

        return (
          <div
            key={row.key}
            onClick={() => onDrill?.(row.key, row.label)}
            className={cn(
              "flex items-center gap-2.5",
              onDrill && "cursor-pointer"
            )}
          >
            <span className="w-24 shrink-0 truncate text-[11.5px] text-muted-foreground">
              {row.label}
            </span>
            <div className="h-5 flex-1 overflow-hidden rounded bg-muted">
              <div
                className="flex h-full items-center justify-end rounded pr-2 text-[10px] font-semibold text-white"
                style={{ width: `${width}%`, backgroundColor: colorAt(index) }}
              >
                {width > 22 ? formatValue(row.total, result.format, "axis") : null}
              </div>
            </div>
            {width > 22 ? null : (
              <span className="w-14 shrink-0 text-right text-[11.5px] tabular-nums">
                {formatValue(row.total, result.format, "axis")}
              </span>
            )}
            <span
              className={cn(
                "w-14 shrink-0 text-right text-[11px] tabular-nums",
                change === null || change === 0
                  ? "text-muted-foreground"
                  : change < 0
                    ? "text-destructive"
                    : "text-emerald-600 dark:text-emerald-400"
              )}
            >
              {change === null
                ? "—"
                : `${change < 0 ? "−" : "+"}${Math.abs(change)}%`}
            </span>
          </div>
        );
      })}
    </div>
  );
}

/** Dimension against breakdown, read by colour intensity. */
function HeatmapView({ result, onDrill, className }: ViewProps) {
  const peak = Math.max(
    ...result.rows.flatMap((row) => result.series.map((s) => row.values[s] ?? 0)),
    1
  );

  return (
    <div className={cn("overflow-auto p-2", className)}>
      <table className="w-full border-separate border-spacing-0.5 text-[11px]">
        <thead>
          <tr>
            <th />
            {result.series.map((name) => (
              <th
                key={name}
                className="px-1 pb-1 text-center font-medium text-muted-foreground"
              >
                <span className="block max-w-[72px] truncate">{name}</span>
              </th>
            ))}
          </tr>
        </thead>
        <tbody>
          {result.rows.map((row) => (
            <tr key={row.key}>
              <th className="max-w-[110px] truncate pr-2 text-right font-medium text-muted-foreground">
                {row.label}
              </th>
              {result.series.map((name) => {
                const value = row.values[name] ?? 0;
                return (
                  <td
                    key={name}
                    title={`${row.label} · ${name}: ${formatValue(value, result.format)}`}
                    onClick={() => onDrill?.(row.key, row.label)}
                    className={cn(
                      "rounded px-1.5 py-1 text-center tabular-nums",
                      onDrill && "cursor-pointer"
                    )}
                    style={{ backgroundColor: heatColor(value / peak) }}
                  >
                    {value ? formatValue(value, result.format, "axis") : ""}
                  </td>
                );
              })}
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

/* ------------------------------------------------------------------ *
 * Helpers
 * ------------------------------------------------------------------ */

/** Series names become SVG gradient ids, which cannot carry spaces. */
function slug(value: string) {
  return value.replace(/[^a-zA-Z0-9]/g, "-").toLowerCase();
}

function maxOf(rows: Record<string, string | number>[], series: string[]) {
  return Math.max(
    ...rows.flatMap((row) => series.map((name) => Number(row[name] ?? 0))),
    1
  );
}
