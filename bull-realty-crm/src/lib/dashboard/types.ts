import type { FilterCondition, MatchMode } from "@/lib/advanced-filter";
import type {
  Aggregation,
  DateBucketKey,
  PeriodKey,
  SortMode,
} from "@/lib/dashboard/analytics";

/* ------------------------------------------------------------------ *
 * Chart styles
 * ------------------------------------------------------------------ */

/**
 * How a widget draws its result.
 *
 * Style is independent of the query: the same "leads by source" result can be
 * a donut, a bar or a table, and switching between them is a one-field change
 * rather than a different widget. That separation is the whole point of the
 * builder — you pick what you want to know first, then how to look at it.
 */
export type ChartStyle =
  | "bar"
  | "stackedBar"
  | "groupedBar"
  | "horizontalBar"
  | "line"
  | "area"
  | "stackedArea"
  | "pie"
  | "donut"
  | "radial"
  | "funnel"
  | "treemap"
  | "heatmap"
  | "metric"
  | "table";

export interface ChartStyleMeta {
  value: ChartStyle;
  label: string;
  /** Only offered when the query has a breakdown (or, when false, when it has none). */
  needsBreakdown?: boolean;
  /** Reads badly with more than one series — hidden when a breakdown is set. */
  singleSeriesOnly?: boolean;
  group: "Comparison" | "Trend" | "Composition" | "Summary";
}

export const CHART_STYLES: ChartStyleMeta[] = [
  { value: "bar", label: "Column", group: "Comparison" },
  { value: "groupedBar", label: "Grouped columns", needsBreakdown: true, group: "Comparison" },
  { value: "stackedBar", label: "Stacked columns", needsBreakdown: true, group: "Comparison" },
  { value: "horizontalBar", label: "Bar", group: "Comparison" },
  { value: "line", label: "Line", group: "Trend" },
  { value: "area", label: "Area", group: "Trend" },
  { value: "stackedArea", label: "Stacked area", needsBreakdown: true, group: "Trend" },
  { value: "pie", label: "Pie", singleSeriesOnly: true, group: "Composition" },
  { value: "donut", label: "Donut", singleSeriesOnly: true, group: "Composition" },
  { value: "funnel", label: "Funnel", singleSeriesOnly: true, group: "Composition" },
  { value: "treemap", label: "Treemap", singleSeriesOnly: true, group: "Composition" },
  { value: "radial", label: "Radial gauge", singleSeriesOnly: true, group: "Summary" },
  { value: "heatmap", label: "Heatmap", needsBreakdown: true, group: "Comparison" },
  { value: "metric", label: "Metric tile", singleSeriesOnly: true, group: "Summary" },
  { value: "table", label: "Table", group: "Summary" },
];

/** The styles that make sense for a query, given whether it has a breakdown. */
export function stylesFor(hasBreakdown: boolean): ChartStyleMeta[] {
  return CHART_STYLES.filter((style) => {
    if (style.needsBreakdown && !hasBreakdown) return false;
    if (style.singleSeriesOnly && hasBreakdown) return false;
    return true;
  });
}

export function styleLabel(style: ChartStyle) {
  return CHART_STYLES.find((meta) => meta.value === style)?.label ?? style;
}

/* ------------------------------------------------------------------ *
 * Widget model
 * ------------------------------------------------------------------ */

/** Columns spanned in the 4-column dashboard grid. */
export type WidgetSpan = 1 | 2 | 3 | 4;

/** Body height, so a dense table and a sparkline can share a row. */
export type WidgetHeight = "short" | "medium" | "tall";

export const HEIGHT_CLASS: Record<WidgetHeight, string> = {
  short: "h-[160px]",
  medium: "h-[230px]",
  tall: "h-[330px]",
};

export const SPAN_LABEL: Record<WidgetSpan, string> = {
  1: "Quarter width",
  2: "Half width",
  3: "Three quarters",
  4: "Full width",
};

export const HEIGHT_LABEL: Record<WidgetHeight, string> = {
  short: "Short",
  medium: "Medium",
  tall: "Tall",
};

/** The query half of a widget — what to ask the server. */
export interface WidgetQuery {
  dataset: string;
  dimension: string;
  bucket: DateBucketKey;
  breakdown: string | null;
  breakdownBucket: DateBucketKey;
  measure: string;
  aggregation: Aggregation;
  /** Widget-level window; `inherit` follows the dashboard's period filter. */
  period: PeriodKey | "inherit";
  dateField: string | null;
  filters: FilterCondition[];
  matchMode: MatchMode;
  sort: SortMode;
  limit: number | null;
  groupOther: boolean;
}

/** The presentation half — how to draw it. */
export interface WidgetOptions {
  legend: boolean;
  grid: boolean;
  /** Data labels printed on the marks. */
  values: boolean;
  /** Normalise a stacked chart to 100%. */
  stack100: boolean;
  curve: "monotone" | "linear" | "step";
  /** Show each row's share of the total. */
  showShare: boolean;
}

export interface WidgetDef {
  id: string;
  /** Overrides the generated title when the user renames the widget. */
  title?: string;
  subtitle?: string;
  style: ChartStyle;
  span: WidgetSpan;
  height: WidgetHeight;
  query: WidgetQuery;
  options: WidgetOptions;
}

export interface DashboardVersion {
  id: string;
  name: string;
  description: string;
  widgets: WidgetDef[];
  /** Preset dashboards can be restored; user-made ones cannot. */
  preset?: boolean;
}

/* ------------------------------------------------------------------ *
 * Defaults
 * ------------------------------------------------------------------ */

export const DEFAULT_OPTIONS: WidgetOptions = {
  legend: true,
  grid: true,
  values: false,
  stack100: false,
  curve: "monotone",
  showShare: false,
};

export const DEFAULT_QUERY: WidgetQuery = {
  dataset: "leads",
  dimension: "stage",
  bucket: "month",
  breakdown: null,
  breakdownBucket: "month",
  measure: "*",
  aggregation: "count",
  period: "inherit",
  dateField: null,
  filters: [],
  matchMode: "all",
  sort: "auto",
  limit: 12,
  groupOther: false,
};

export function newWidgetId() {
  return `w-${Math.random().toString(36).slice(2, 10)}`;
}

/**
 * Fills in whatever a stored widget is missing.
 *
 * Layouts live in the browser, so a dashboard saved before a field existed will
 * come back without it. Reading every widget through this means an older saved
 * layout keeps working instead of rendering as a broken card.
 */
export function normaliseWidget(widget: Partial<WidgetDef>): WidgetDef {
  return {
    id: widget.id ?? newWidgetId(),
    title: widget.title,
    subtitle: widget.subtitle,
    style: widget.style ?? "bar",
    span: widget.span ?? 2,
    height: widget.height ?? "medium",
    query: { ...DEFAULT_QUERY, ...(widget.query ?? {}) },
    options: { ...DEFAULT_OPTIONS, ...(widget.options ?? {}) },
  };
}

/* ------------------------------------------------------------------ *
 * Titles
 * ------------------------------------------------------------------ */

/**
 * The title a widget shows when it has not been renamed.
 *
 * Generated rather than stored so that changing the measure or the group-by
 * updates the heading with it — a chart labelled "Leads by source" that is
 * actually grouped by owner is worse than no label.
 */
export function describeWidget(
  query: WidgetQuery,
  labels: {
    dataset?: string;
    dimension?: string;
    breakdown?: string;
    measure?: string;
  }
): string {
  const measure =
    query.aggregation === "count"
      ? labels.dataset ?? "Records"
      : `${aggregationVerb(query.aggregation)} ${labels.measure ?? "value"}`;

  const by = labels.dimension ? ` by ${labels.dimension.toLowerCase()}` : "";
  const split = labels.breakdown ? ` and ${labels.breakdown.toLowerCase()}` : "";

  return `${measure}${by}${split}`;
}

function aggregationVerb(aggregation: Aggregation) {
  switch (aggregation) {
    case "sum":
      return "Total";
    case "avg":
      return "Average";
    case "min":
      return "Lowest";
    case "max":
      return "Highest";
    case "countDistinct":
      return "Distinct";
    default:
      return "Count of";
  }
}
