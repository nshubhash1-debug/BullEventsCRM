import {
  DEFAULT_OPTIONS,
  DEFAULT_QUERY,
  type ChartStyle,
  type DashboardVersion,
  type WidgetDef,
  type WidgetHeight,
  type WidgetSpan,
} from "@/lib/dashboard/types";

/* ------------------------------------------------------------------ *
 * Builder
 * ------------------------------------------------------------------ */

interface WidgetSeed {
  id: string;
  title?: string;
  style: ChartStyle;
  span?: WidgetSpan;
  height?: WidgetHeight;
  dataset: string;
  dimension: string;
  bucket?: "day" | "month" | "quarter" | "year";
  breakdown?: string;
  measure?: string;
  aggregation?: WidgetDef["query"]["aggregation"];
  period?: WidgetDef["query"]["period"];
  dateField?: string;
  sort?: WidgetDef["query"]["sort"];
  limit?: number | null;
  groupOther?: boolean;
  filters?: WidgetDef["query"]["filters"];
  options?: Partial<WidgetDef["options"]>;
}

/** Keeps the preset definitions to the fields that actually differ. */
function widget(seed: WidgetSeed): WidgetDef {
  return {
    id: seed.id,
    title: seed.title,
    style: seed.style,
    span: seed.span ?? 2,
    height: seed.height ?? "medium",
    query: {
      ...DEFAULT_QUERY,
      dataset: seed.dataset,
      dimension: seed.dimension,
      bucket: seed.bucket ?? "month",
      breakdown: seed.breakdown ?? null,
      measure: seed.measure ?? "*",
      aggregation: seed.aggregation ?? (seed.measure ? "sum" : "count"),
      period: seed.period ?? "inherit",
      dateField: seed.dateField ?? null,
      sort: seed.sort ?? "auto",
      limit: seed.limit === undefined ? 12 : seed.limit,
      groupOther: seed.groupOther ?? false,
      filters: seed.filters ?? [],
    },
    options: { ...DEFAULT_OPTIONS, ...(seed.options ?? {}) },
  };
}

/* ------------------------------------------------------------------ *
 * Shipped dashboards
 * ------------------------------------------------------------------ */

/**
 * Four dashboards, each built around the question its name asks.
 *
 * They deliberately do not share a widget set. A scorecard that showed the same
 * charts as the overview would be a second copy of the overview, and the tab
 * strip would be decoration — so every one of these groups by something the
 * others do not, and reads a different object where that is the honest source.
 */
export const DEFAULT_DASHBOARDS: DashboardVersion[] = [
  {
    id: "sales-overview",
    name: "Sales Overview",
    description: "The daily driver — pipeline health, revenue and what closed",
    preset: true,
    widgets: [
      widget({
        id: "so-kpi-pipeline",
        title: "Open pipeline",
        style: "metric",
        span: 1,
        height: "short",
        dataset: "opportunities",
        dimension: "stage",
        measure: "amount",
        aggregation: "sum",
        period: "all",
      }),
      widget({
        id: "so-kpi-booked",
        title: "Booked revenue",
        style: "metric",
        span: 1,
        height: "short",
        dataset: "opportunities",
        dimension: "branchName",
        measure: "amount",
        aggregation: "sum",
        dateField: "actualCloseDate",
        filters: [
          { id: "so-f1", field: "stage", operator: "equals", value: "ClosedWon" },
        ],
      }),
      widget({
        id: "so-kpi-leads",
        title: "New leads",
        style: "metric",
        span: 1,
        height: "short",
        dataset: "leads",
        dimension: "stage",
        dateField: "createdAt",
      }),
      widget({
        id: "so-kpi-visits",
        title: "Site visits",
        style: "metric",
        span: 1,
        height: "short",
        dataset: "siteVisits",
        dimension: "status",
        dateField: "scheduledAt",
      }),
      widget({
        id: "so-revenue-trend",
        title: "Deal value by month",
        style: "area",
        span: 2,
        dataset: "opportunities",
        dimension: "createdAt",
        bucket: "month",
        measure: "amount",
        aggregation: "sum",
        period: "last12Months",
        sort: "keyAsc",
        limit: null,
      }),
      widget({
        id: "so-funnel",
        title: "Open pipeline by stage",
        style: "funnel",
        span: 2,
        dataset: "opportunities",
        dimension: "stage",
        period: "all",
        // Stage order, not value order — the drop-off column between two bars
        // only means anything if they are consecutive stages.
        sort: "sequence",
        // Closed deals are not in the pipeline, and leaving them in makes the
        // funnel widen at the bottom, which reads as a broken chart.
        filters: [
          { id: "so-f2", field: "stage", operator: "notEquals", value: "ClosedWon" },
          { id: "so-f3", field: "stage", operator: "notEquals", value: "ClosedLost" },
        ],
      }),
      widget({
        id: "so-owner",
        title: "Deal value by owner",
        style: "horizontalBar",
        span: 2,
        dataset: "opportunities",
        dimension: "ownerName",
        measure: "amount",
        aggregation: "sum",
        period: "all",
        limit: 8,
      }),
      widget({
        id: "so-source",
        title: "Lead source mix",
        style: "donut",
        span: 1,
        dataset: "leads",
        dimension: "source",
        period: "all",
        limit: 6,
        groupOther: true,
      }),
      widget({
        id: "so-followups",
        title: "Follow-up queue",
        style: "bar",
        span: 1,
        dataset: "followUps",
        dimension: "status",
        period: "all",
      }),
    ],
  },

  {
    id: "lead-performance",
    name: "Lead Performance",
    description: "Where leads come from, how good they are and where they stall",
    preset: true,
    widgets: [
      widget({
        id: "lp-intake",
        title: "Lead intake by month",
        style: "line",
        span: 2,
        dataset: "leads",
        dimension: "createdAt",
        bucket: "month",
        period: "last12Months",
        sort: "keyAsc",
        limit: null,
      }),
      widget({
        id: "lp-source-stage",
        title: "Source quality — stage mix per channel",
        style: "stackedBar",
        span: 2,
        dataset: "leads",
        dimension: "source",
        breakdown: "stage",
        period: "all",
        limit: 7,
        options: { stack100: true },
      }),
      widget({
        id: "lp-funnel",
        title: "Lead stage funnel",
        style: "funnel",
        span: 2,
        dataset: "leads",
        dimension: "stage",
        period: "all",
      }),
      widget({
        id: "lp-score",
        title: "Average AI score by source",
        style: "bar",
        span: 2,
        dataset: "leads",
        dimension: "source",
        measure: "cachedScore",
        aggregation: "avg",
        period: "all",
        limit: 8,
      }),
      widget({
        id: "lp-priority",
        title: "Leads by priority",
        style: "donut",
        span: 1,
        dataset: "leads",
        dimension: "priority",
        period: "all",
      }),
      widget({
        id: "lp-city",
        title: "Top cities",
        style: "horizontalBar",
        span: 1,
        dataset: "leads",
        dimension: "city",
        period: "all",
        limit: 6,
      }),
      widget({
        id: "lp-requirement",
        title: "Occasion mix",
        style: "donut",
        span: 1,
        dataset: "leads",
        dimension: "eventType",
        period: "all",
        limit: 6,
        groupOther: true,
      }),
      widget({
        id: "lp-loss",
        title: "Why leads are lost",
        style: "horizontalBar",
        span: 1,
        dataset: "leads",
        dimension: "lossReason",
        period: "all",
        limit: 6,
        filters: [
          { id: "lp-f1", field: "stage", operator: "equals", value: "Lost" },
        ],
      }),
    ],
  },

  {
    id: "branch-scorecard",
    name: "Branch Scorecard",
    description: "Branch-by-branch contribution across every object",
    preset: true,
    widgets: [
      widget({
        id: "bs-revenue",
        title: "Deal value by branch",
        style: "bar",
        span: 2,
        dataset: "opportunities",
        dimension: "branchName",
        measure: "amount",
        aggregation: "sum",
        period: "all",
        limit: 10,
        options: { values: true },
      }),
      widget({
        id: "bs-heat",
        title: "Lead stages by branch",
        style: "heatmap",
        span: 2,
        dataset: "leads",
        dimension: "branchName",
        breakdown: "stage",
        period: "all",
        limit: 10,
      }),
      widget({
        id: "bs-visits",
        title: "Site visits by branch",
        style: "horizontalBar",
        span: 2,
        dataset: "siteVisits",
        dimension: "branchName",
        period: "all",
        limit: 8,
      }),
      widget({
        id: "bs-calls",
        title: "Call outcomes by branch",
        style: "stackedBar",
        span: 2,
        dataset: "calls",
        dimension: "branchName",
        breakdown: "outcome",
        period: "all",
        limit: 8,
      }),
      widget({
        id: "bs-table",
        title: "Branch league table",
        style: "table",
        span: 2,
        height: "tall",
        dataset: "opportunities",
        dimension: "branchName",
        measure: "amount",
        aggregation: "sum",
        period: "all",
        limit: 12,
        options: { showShare: true },
      }),
      widget({
        id: "bs-quotes",
        title: "Quoted value by branch",
        style: "bar",
        span: 2,
        height: "tall",
        dataset: "quotations",
        dimension: "branchName",
        measure: "total",
        aggregation: "sum",
        period: "all",
        limit: 10,
      }),
    ],
  },

  {
    id: "executive-summary",
    name: "Executive Summary",
    description: "Board-level roll-up — the year in eight numbers",
    preset: true,
    widgets: [
      widget({
        id: "es-kpi-pipeline",
        title: "Total pipeline",
        style: "metric",
        span: 1,
        height: "short",
        dataset: "opportunities",
        dimension: "stage",
        measure: "amount",
        aggregation: "sum",
        period: "all",
      }),
      widget({
        id: "es-kpi-avg",
        title: "Average deal size",
        style: "metric",
        span: 1,
        height: "short",
        dataset: "opportunities",
        dimension: "stage",
        measure: "amount",
        aggregation: "avg",
        period: "all",
      }),
      widget({
        id: "es-kpi-contacts",
        title: "Customer lifetime value",
        style: "metric",
        span: 1,
        height: "short",
        dataset: "contacts",
        dimension: "lifecycleStage",
        measure: "lifetimeValue",
        aggregation: "sum",
        period: "all",
      }),
      widget({
        id: "es-kpi-leads",
        title: "Leads this year",
        style: "metric",
        span: 1,
        height: "short",
        dataset: "leads",
        dimension: "stage",
        period: "thisYear",
      }),
      widget({
        id: "es-quarter",
        title: "Deal value by quarter",
        style: "bar",
        span: 3,
        dataset: "opportunities",
        dimension: "createdAt",
        bucket: "quarter",
        measure: "amount",
        aggregation: "sum",
        period: "all",
        sort: "keyAsc",
        limit: null,
        options: { values: true },
      }),
      widget({
        id: "es-forecast",
        title: "Forecast categories",
        style: "donut",
        span: 1,
        dataset: "opportunities",
        dimension: "forecastCategory",
        measure: "amount",
        aggregation: "sum",
        period: "all",
      }),
      widget({
        id: "es-mix",
        title: "Lead intake by source over time",
        style: "stackedArea",
        span: 2,
        dataset: "leads",
        dimension: "createdAt",
        bucket: "month",
        breakdown: "source",
        period: "last12Months",
        sort: "keyAsc",
        limit: null,
      }),
      widget({
        id: "es-branch",
        title: "Contribution by branch",
        style: "treemap",
        span: 2,
        dataset: "opportunities",
        dimension: "branchName",
        measure: "amount",
        aggregation: "sum",
        period: "all",
        limit: 10,
      }),
    ],
  },
];

/* ------------------------------------------------------------------ *
 * Widget gallery
 * ------------------------------------------------------------------ */

export interface WidgetBlueprint {
  id: string;
  title: string;
  description: string;
  category: string;
  build: () => WidgetDef;
}

function blueprint(
  id: string,
  title: string,
  description: string,
  category: string,
  seed: Omit<WidgetSeed, "id">
): WidgetBlueprint {
  return {
    id,
    title,
    description,
    category,
    build: () => widget({ ...seed, id: `w-${Math.random().toString(36).slice(2, 10)}`, title }),
  };
}

/**
 * Starting points, not a fixed menu.
 *
 * Every entry drops in a fully-formed widget that the editor can then take
 * anywhere — a different object, measure or chart type. They exist so nobody
 * has to configure a chart from an empty state to see something useful.
 */
export const WIDGET_BLUEPRINTS: WidgetBlueprint[] = [
  /* --- Revenue --- */
  blueprint("bp-revenue-month", "Deal value by month", "Booked and open value over time", "Revenue", {
    style: "area", dataset: "opportunities", dimension: "createdAt", bucket: "month",
    measure: "amount", aggregation: "sum", period: "last12Months", sort: "keyAsc", limit: null,
  }),
  blueprint("bp-revenue-branch", "Deal value by branch", "Which branches carry the number", "Revenue", {
    style: "bar", dataset: "opportunities", dimension: "branchName",
    measure: "amount", aggregation: "sum", period: "all", limit: 10,
  }),
  blueprint("bp-revenue-owner", "Deal value by owner", "Per-agent contribution", "Revenue", {
    style: "horizontalBar", dataset: "opportunities", dimension: "ownerName",
    measure: "amount", aggregation: "sum", period: "all", limit: 10,
  }),
  blueprint("bp-avg-deal", "Average deal size", "One number across the filter", "Revenue", {
    style: "metric", span: 1, height: "short", dataset: "opportunities", dimension: "stage",
    measure: "amount", aggregation: "avg", period: "all",
  }),
  blueprint("bp-quote-status", "Quoted value by status", "Where proposals sit", "Revenue", {
    style: "donut", span: 1, dataset: "quotations", dimension: "status",
    measure: "total", aggregation: "sum", period: "all",
  }),

  /* --- Pipeline --- */
  blueprint("bp-pipeline-funnel", "Pipeline funnel", "Stage-to-stage drop-off", "Pipeline", {
    style: "funnel", dataset: "opportunities", dimension: "stage", period: "all",
  }),
  blueprint("bp-forecast", "Forecast categories", "Commit, best case and pipeline", "Pipeline", {
    style: "donut", span: 1, dataset: "opportunities", dimension: "forecastCategory",
    measure: "amount", aggregation: "sum", period: "all",
  }),
  blueprint("bp-stage-branch", "Stages by branch", "Where every branch's deals sit", "Pipeline", {
    style: "heatmap", dataset: "opportunities", dimension: "branchName",
    breakdown: "stage", period: "all", limit: 10,
  }),
  blueprint("bp-loss", "Loss reasons", "Why deals were lost", "Pipeline", {
    style: "horizontalBar", dataset: "opportunities", dimension: "lossReason",
    period: "all", limit: 8,
  }),

  /* --- Leads --- */
  blueprint("bp-lead-funnel", "Lead stage funnel", "Enquiry through to booking", "Leads", {
    style: "funnel", dataset: "leads", dimension: "stage", period: "all",
  }),
  blueprint("bp-lead-source", "Lead source mix", "Where enquiries come from", "Leads", {
    style: "donut", span: 1, dataset: "leads", dimension: "source",
    period: "all", limit: 6, groupOther: true,
  }),
  blueprint("bp-lead-intake", "Lead intake over time", "Volume by month", "Leads", {
    style: "line", dataset: "leads", dimension: "createdAt", bucket: "month",
    period: "last12Months", sort: "keyAsc", limit: null,
  }),
  blueprint("bp-source-quality", "Source quality", "Stage mix per channel", "Leads", {
    style: "stackedBar", dataset: "leads", dimension: "source", breakdown: "stage",
    period: "all", limit: 7, options: { stack100: true },
  }),
  blueprint("bp-lead-score", "Average AI score by source", "Which channels send good leads", "Leads", {
    style: "bar", dataset: "leads", dimension: "source",
    measure: "cachedScore", aggregation: "avg", period: "all", limit: 8,
  }),
  blueprint("bp-lead-city", "Leads by city", "Geographic concentration", "Leads", {
    style: "horizontalBar", span: 1, dataset: "leads", dimension: "city",
    period: "all", limit: 8,
  }),
  blueprint("bp-lead-priority", "Leads by priority", "How the desk is triaged", "Leads", {
    style: "donut", span: 1, dataset: "leads", dimension: "priority", period: "all",
  }),
  blueprint("bp-lead-campaign", "Campaign performance", "Leads per campaign", "Leads", {
    style: "table", dataset: "leads", dimension: "campaign",
    period: "all", limit: 12, options: { showShare: true },
  }),

  /* --- Activity --- */
  blueprint("bp-call-outcome", "Call outcomes", "Connected, missed and the rest", "Activity", {
    style: "bar", dataset: "calls", dimension: "outcome", period: "last30",
  }),
  blueprint("bp-call-agent", "Calls by agent", "Who is on the phone", "Activity", {
    style: "horizontalBar", dataset: "calls", dimension: "agentName",
    period: "last30", limit: 10,
  }),
  blueprint("bp-call-sentiment", "Call sentiment", "Tone read off the notes", "Activity", {
    style: "donut", span: 1, dataset: "calls", dimension: "sentimentLabel", period: "last30",
  }),
  blueprint("bp-talk-time", "Talk time by agent", "Total minutes on calls", "Activity", {
    style: "bar", dataset: "calls", dimension: "agentName",
    measure: "durationSeconds", aggregation: "sum", period: "last30", limit: 10,
  }),
  blueprint("bp-visits-status", "Site visits by status", "Scheduled through to completed", "Activity", {
    style: "bar", dataset: "siteVisits", dimension: "status", period: "all",
  }),
  blueprint("bp-visits-project", "Site visits by project", "Which projects draw traffic", "Activity", {
    style: "horizontalBar", dataset: "siteVisits", dimension: "projectName",
    period: "all", limit: 8,
  }),
  blueprint("bp-visit-interest", "Interest level after visits", "How walkthroughs land", "Activity", {
    style: "donut", span: 1, dataset: "siteVisits", dimension: "interestLevel", period: "all",
  }),
  blueprint("bp-followup-queue", "Follow-up queue", "Open work by status", "Activity", {
    style: "bar", span: 1, dataset: "followUps", dimension: "status", period: "all",
  }),
  blueprint("bp-followup-channel", "Follow-ups by channel", "Call, WhatsApp, email, meeting", "Activity", {
    style: "donut", span: 1, dataset: "followUps", dimension: "channel", period: "all",
  }),

  /* --- Partners --- */
  blueprint("bp-obm-partner", "OBM visits by partner type", "Channel-partner field activity", "Partners", {
    style: "bar", dataset: "obmVisits", dimension: "partnerType", period: "all",
  }),
  blueprint("bp-obm-value", "Business value by agent", "What field meetings brought in", "Partners", {
    style: "horizontalBar", dataset: "obmVisits", dimension: "agentName",
    measure: "businessValue", aggregation: "sum", period: "all", limit: 8,
  }),
  blueprint("bp-obm-expense", "Field expense by branch", "Cost of the field team", "Partners", {
    style: "bar", dataset: "obmVisits", dimension: "branchName",
    measure: "expenseAmount", aggregation: "sum", period: "all", limit: 8,
  }),

  /* --- Customers --- */
  blueprint("bp-contact-stage", "Contacts by lifecycle stage", "The customer database at a glance", "Customers", {
    style: "funnel", dataset: "contacts", dimension: "lifecycleStage", period: "all",
  }),
  blueprint("bp-contact-segment", "AI segments", "K-means clusters of the customer base", "Customers", {
    style: "donut", span: 1, dataset: "contacts", dimension: "segment", period: "all",
  }),
  blueprint("bp-ltv", "Lifetime value by owner", "Book value per account manager", "Customers", {
    style: "treemap", dataset: "contacts", dimension: "ownerName",
    measure: "lifetimeValue", aggregation: "sum", period: "all", limit: 10,
  }),
];

export const BLUEPRINT_CATEGORIES = [
  "Revenue",
  "Pipeline",
  "Leads",
  "Activity",
  "Partners",
  "Customers",
];
