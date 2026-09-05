import { apiRequest } from "@/lib/api";
import type { FilterCondition, MatchMode } from "@/lib/advanced-filter";

/* ------------------------------------------------------------------ *
 * Server contract — mirrors BullEvents.Api/Dtos/AnalyticsDtos.cs
 * ------------------------------------------------------------------ */

export interface DimensionMeta {
  id: string;
  label: string;
  /** "category" or "date" — a date dimension offers bucket options. */
  kind: "category" | "date";
  group: string | null;
  /** True when the field has a canonical order, e.g. pipeline stages. */
  ordered: boolean;
}

export interface MeasureMeta {
  id: string;
  label: string;
  format: MeasureFormat;
  aggregations: Aggregation[];
}

export interface FilterFieldMeta {
  id: string;
  label: string;
  type: string;
  options: { label: string; value: string }[] | null;
  group: string | null;
}

export interface DatasetMeta {
  id: string;
  label: string;
  description: string;
  recordLabel: string;
  dimensions: DimensionMeta[];
  measures: MeasureMeta[];
  dateFields: DimensionMeta[];
  defaultDateField: string;
  defaultDimension: string;
  filterFields: FilterFieldMeta[];
}

export type MeasureFormat = "number" | "currency" | "percent";

export type Aggregation =
  | "count"
  | "sum"
  | "avg"
  | "min"
  | "max"
  | "countDistinct";

export type DateBucketKey = "day" | "month" | "quarter" | "year";

export type SortMode =
  | "auto"
  | "valueDesc"
  | "valueAsc"
  | "keyAsc"
  | "keyDesc"
  /** Follows the field's own vocabulary, e.g. pipeline stage order. */
  | "sequence";

/** Relative windows the server knows how to resolve. */
export type PeriodKey =
  | "all"
  | "today"
  | "yesterday"
  | "last7"
  | "last30"
  | "last90"
  | "last180"
  | "last12Months"
  | "thisWeek"
  | "thisMonth"
  | "lastMonth"
  | "thisQuarter"
  | "lastQuarter"
  | "thisYear"
  | "lastYear";

export const PERIODS: { value: PeriodKey; label: string }[] = [
  { value: "today", label: "Today" },
  { value: "yesterday", label: "Yesterday" },
  { value: "thisWeek", label: "This week" },
  { value: "last7", label: "Last 7 days" },
  { value: "last30", label: "Last 30 days" },
  { value: "last90", label: "Last 90 days" },
  { value: "thisMonth", label: "This month" },
  { value: "lastMonth", label: "Last month" },
  { value: "thisQuarter", label: "This quarter" },
  { value: "lastQuarter", label: "Last quarter" },
  { value: "last12Months", label: "Last 12 months" },
  { value: "thisYear", label: "This year" },
  { value: "lastYear", label: "Last year" },
  { value: "all", label: "All time" },
];

export const AGGREGATION_LABELS: Record<Aggregation, string> = {
  count: "Count of records",
  sum: "Sum",
  avg: "Average",
  min: "Minimum",
  max: "Maximum",
  countDistinct: "Distinct count",
};

export const BUCKET_LABELS: Record<DateBucketKey, string> = {
  day: "Day",
  month: "Month",
  quarter: "Quarter",
  year: "Year",
};

/* ---------------- filter tree ---------------- */

/** The server's nested filter node. */
export interface FilterNode {
  field?: string;
  operator?: string;
  value?: string;
  value2?: string;
  values?: string[];
  conjunction?: "and" | "or";
  negate?: boolean;
  children?: FilterNode[];
}

/* ---------------- request / response ---------------- */

export interface AggregateRequest {
  dataset: string;
  dimension: string;
  bucket?: DateBucketKey;
  breakdown?: string | null;
  breakdownBucket?: DateBucketKey;
  measure?: string | null;
  aggregation: Aggregation;
  search?: string;
  filter?: FilterNode | null;
  dateField?: string;
  period?: PeriodKey;
  sort?: SortMode;
  limit?: number | null;
  groupOther?: boolean;
}

export interface AggregatePoint {
  key: string;
  label: string;
  values: Record<string, number>;
  total: number;
  records: number;
}

export interface AggregateResponse {
  dataset: string;
  datasetLabel: string;
  dimension: string;
  dimensionLabel: string;
  dimensionKind: "category" | "date";
  bucket: string | null;
  breakdown: string | null;
  measure: string;
  measureLabel: string;
  format: MeasureFormat;
  aggregation: string;
  series: string[];
  rows: AggregatePoint[];
  total: number;
  matchedRecords: number;
  truncatedGroups: number;
}

/* ------------------------------------------------------------------ *
 * Client
 * ------------------------------------------------------------------ */

export const analyticsApi = {
  datasets: () => apiRequest<DatasetMeta[]>("/api/analytics/datasets", { auth: true }),

  aggregate: (request: AggregateRequest) =>
    apiRequest<AggregateResponse>("/api/analytics/aggregate", {
      method: "POST",
      body: JSON.stringify(request),
      auth: true,
    }),

  /** One round trip for a whole dashboard's worth of widgets. */
  batch: (requests: AggregateRequest[]) =>
    apiRequest<AggregateResponse[]>("/api/analytics/aggregate/batch", {
      method: "POST",
      body: JSON.stringify(requests),
      auth: true,
    }),

  values: (dataset: string, field: string) =>
    apiRequest<{ value: string; count: number }[]>(
      `/api/analytics/datasets/${dataset}/values?field=${encodeURIComponent(field)}`,
      { auth: true }
    ),
};

/* ------------------------------------------------------------------ *
 * Filter translation
 * ------------------------------------------------------------------ */

/**
 * Turns the filter builder's flat condition list into the server's filter tree.
 *
 * The builder is deliberately flat — one match mode across a list of
 * conditions — because that is what people can hold in their head while
 * configuring a chart. The server's tree is more expressive than that, so this
 * only ever produces one level of nesting.
 */
export function toFilterNode(
  conditions: FilterCondition[],
  matchMode: MatchMode
): FilterNode | null {
  const children = conditions
    .filter((condition) => condition.field && condition.operator)
    .map<FilterNode>((condition) => ({
      field: condition.field,
      operator: condition.operator,
      value: condition.value || undefined,
    }));

  if (children.length === 0) return null;

  return { conjunction: matchMode === "any" ? "or" : "and", children };
}
