import { apiRequest } from "@/lib/api";
import type { FilterNode, MeasureFormat } from "@/lib/dashboard/analytics";

/* ------------------------------------------------------------------ *
 * Server contract — mirrors BullEvents.Api/Dtos/GoalDtos.cs
 * ------------------------------------------------------------------ */

export type GoalPeriod = "Month" | "Quarter" | "Year" | "Custom";
export type GoalScope = "Company" | "Branch" | "User";
export type GoalHealth =
  | "NotStarted"
  | "OnTrack"
  | "Behind"
  | "Achieved"
  | "Missed";

export interface Goal {
  id: number;
  name: string;
  description: string | null;
  dataset: string;
  datasetLabel: string;
  measure: string | null;
  measureLabel: string;
  aggregation: string;
  dateField: string;
  isRatio: boolean;
  format: MeasureFormat;
  periodType: GoalPeriod;
  startDate: string;
  endDate: string;
  scopeType: GoalScope;
  branchId: number | null;
  branchName: string | null;
  ownerId: number | null;
  ownerName: string | null;
  targetValue: number;
  status: string;

  /* progress */
  actual: number;
  percentComplete: number;
  expectedByNow: number;
  pacePercent: number;
  projected: number;
  daysElapsed: number;
  daysRemaining: number;
  daysTotal: number;
  health: GoalHealth;
}

export interface GoalSummary {
  total: number;
  achieved: number;
  onTrack: number;
  behind: number;
  missed: number;
  averagePercent: number;
}

export interface GoalListResponse {
  goals: Goal[];
  summary: GoalSummary;
}

export interface GoalInput {
  name: string;
  description?: string;
  dataset: string;
  measure?: string | null;
  aggregation: string;
  filter?: FilterNode | null;
  dateField: string;
  isRatio?: boolean;
  ratioDataset?: string | null;
  ratioMeasure?: string | null;
  ratioAggregation?: string | null;
  ratioFilter?: FilterNode | null;
  ratioDateField?: string | null;
  format: MeasureFormat;
  periodType: GoalPeriod;
  startDate?: string | null;
  endDate?: string | null;
  anchor?: string | null;
  scopeType: GoalScope;
  branchId?: number | null;
  ownerId?: number | null;
  targetValue: number;
  status?: string;
}

/* ------------------------------------------------------------------ *
 * Client
 * ------------------------------------------------------------------ */

export const goalsApi = {
  list: (options?: {
    status?: string;
    scopeType?: GoalScope;
    ownerId?: number;
    branchId?: number;
    activeOnly?: boolean;
  }) => {
    const params = new URLSearchParams();
    if (options?.status) params.set("status", options.status);
    if (options?.scopeType) params.set("scopeType", options.scopeType);
    if (options?.ownerId) params.set("ownerId", String(options.ownerId));
    if (options?.branchId) params.set("branchId", String(options.branchId));
    if (options?.activeOnly) params.set("activeOnly", "true");

    const query = params.toString();
    return apiRequest<GoalListResponse>(
      `/api/goals${query ? `?${query}` : ""}`,
      { auth: true }
    );
  },

  get: (id: number) => apiRequest<Goal>(`/api/goals/${id}`, { auth: true }),

  create: (input: GoalInput) =>
    apiRequest<Goal>("/api/goals", {
      method: "POST",
      body: JSON.stringify(input),
      auth: true,
    }),

  update: (id: number, input: GoalInput) =>
    apiRequest<Goal>(`/api/goals/${id}`, {
      method: "PUT",
      body: JSON.stringify(input),
      auth: true,
    }),

  remove: (id: number) =>
    apiRequest<void>(`/api/goals/${id}`, { method: "DELETE", auth: true }),
};

/* ------------------------------------------------------------------ *
 * Presentation
 * ------------------------------------------------------------------ */

export const HEALTH_META: Record<
  GoalHealth,
  { label: string; color: string; className: string }
> = {
  Achieved: {
    label: "Achieved",
    color: "#04844b",
    className: "border-emerald-500/40 text-emerald-700 dark:text-emerald-400",
  },
  OnTrack: {
    label: "On track",
    color: "#0176d3",
    className: "border-blue-500/40 text-blue-700 dark:text-blue-400",
  },
  Behind: {
    label: "Behind",
    color: "#fe9339",
    className: "border-amber-500/40 text-amber-700 dark:text-amber-400",
  },
  Missed: {
    label: "Missed",
    color: "#c23934",
    className: "border-destructive/40 text-destructive",
  },
  NotStarted: {
    label: "Not started",
    color: "#8c8c8c",
    className: "border-border text-muted-foreground",
  },
};

export const PERIOD_LABELS: Record<GoalPeriod, string> = {
  Month: "This month",
  Quarter: "This quarter",
  Year: "This year",
  Custom: "Custom dates",
};

export const SCOPE_LABELS: Record<GoalScope, string> = {
  Company: "Whole company",
  Branch: "Branch",
  User: "Individual",
};

/** Who or what the goal belongs to, as one line. */
export function scopeLabel(goal: Goal) {
  if (goal.scopeType === "User") return goal.ownerName ?? "Unassigned";
  if (goal.scopeType === "Branch") return goal.branchName ?? "Branch";
  return "Whole company";
}
