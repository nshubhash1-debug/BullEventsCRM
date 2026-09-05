"use client";

import * as React from "react";
import {
  CalendarCheck2,
  CheckCircle2,
  LogIn,
  Star,
  TrendingUp,
  UserX,
} from "lucide-react";
import { toast } from "sonner";

import type { GridColumn } from "@/components/crm/crm-grid";
import { ListShell, type QuickView } from "@/components/crm/list-shell";
import { StatusMenu, type StatusChange } from "@/components/crm/status-menu";
import {
  DistributionBar,
  MiniBars,
  Pill,
  type Metric,
} from "@/components/crm/metrics";
import { Button } from "@/components/ui/button";
import {
  Tooltip,
  TooltipContent,
  TooltipTrigger,
} from "@/components/ui/tooltip";
import { useCrmList } from "@/hooks/use-crm-list";
import { ApiError } from "@/lib/api";
import {
  formatDateTime,
  formatMoney,
  humanise,
  siteVisitsApi,
  timeAgo,
  type SiteVisitInsights,
  type SiteVisitRow,
} from "@/lib/crm-api";
import { interestTone, visitStatusTone } from "@/lib/crm-tones";
import { emptyRoot, type FilterNode } from "@/lib/query";
import { cn } from "@/lib/utils";

const FACETS = ["status", "visitType", "interestLevel", "projectName", "hostName"];
const INITIAL_SORT = [{ field: "scheduledAt", descending: true }];

function condition(field: string, operator: string, value?: string, values?: string[]): FilterNode {
  const root = emptyRoot();
  return { ...root, children: [{ key: `${root.key}-c`, field, operator, value, values }] };
}

export default function SiteVisitsPage() {
  const state = useCrmList<SiteVisitRow>(siteVisitsApi, {
    facets: FACETS,
    dateFields: [
      { id: "scheduledAt", label: "Scheduled" },
      { id: "checkInAt", label: "Checked in" },
      { id: "createdAt", label: "Created" },
    ],
    sort: INITIAL_SORT,
    pageSize: 50,
  });

  const [insights, setInsights] = React.useState<SiteVisitInsights | null>(null);

  const loadInsights = React.useCallback(() => {
    siteVisitsApi.insights(90).then(setInsights).catch(() => setInsights(null));
  }, []);

  React.useEffect(loadInsights, [loadInsights]);

  const aggregates = state.aggregates;

  const metrics: Metric[] = [
    { label: "Visits in view", value: state.total.toLocaleString(), icon: CalendarCheck2 },
    {
      label: "Upcoming",
      value: (aggregates.scheduled ?? 0).toLocaleString(),
      tone: "info",
      onClick: () =>
        state.setFilter(condition("status", "in", undefined, ["Scheduled", "Confirmed"])),
    },
    {
      label: "Completed",
      value: (aggregates.completed ?? 0).toLocaleString(),
      tone: "success",
      icon: CheckCircle2,
      onClick: () => state.setFilter(condition("status", "equals", "Completed")),
    },
    {
      label: "No-shows",
      value: (aggregates.noShow ?? 0).toLocaleString(),
      tone: "danger",
      icon: UserX,
      hint: insights
        ? `${insights.noShowRate.toFixed(1)}% of visits scheduled in the last 90 days.`
        : undefined,
      onClick: () => state.setFilter(condition("status", "equals", "NoShow")),
    },
    {
      label: "High interest",
      value: (aggregates.highInterest ?? 0).toLocaleString(),
      tone: "violet",
      icon: Star,
      onClick: () => state.setFilter(condition("interestLevel", "equals", "High")),
    },
    {
      label: "Avg on site",
      value: insights ? `${Math.round(insights.averageDurationMinutes)}m` : "—",
      tone: "primary",
      hint: "Median time between check-in and check-out on completed visits.",
    },
  ];

  const quickViews: QuickView[] = [
    {
      id: "upcoming",
      label: "Upcoming",
      build: () =>
        ({
          ...emptyRoot(),
          children: [
            { key: "u1", field: "status", operator: "in", values: ["Scheduled", "Confirmed"] },
            { key: "u2", field: "scheduledAt", operator: "nextNDays", value: "14" },
          ],
        }) as FilterNode,
    },
    { id: "today", label: "Today", build: () => condition("scheduledAt", "today") },
    {
      id: "completed",
      label: "Completed",
      build: () => condition("status", "equals", "Completed"),
    },
    {
      id: "hot",
      label: "High interest",
      build: () => condition("interestLevel", "equals", "High"),
    },
    {
      id: "noshow",
      label: "No-shows",
      build: () => condition("status", "equals", "NoShow"),
    },
  ];

  const visuals = (
    <div className="grid gap-3 lg:grid-cols-[1fr_1fr_auto]">
      <DistributionBar
        title="Status"
        segments={(state.facets.status ?? []).map((bucket) => ({
          key: bucket.value,
          label: humanise(bucket.value),
          count: bucket.count,
          tone: visitStatusTone(bucket.value),
        }))}
        onSelect={(value) => state.setFilter(condition("status", "equals", value))}
      />

      <div className="min-w-0">
        <span className="text-[10.5px] font-medium tracking-wide text-muted-foreground uppercase">
          By project · last 90 days
        </span>
        <div className="mt-1 flex flex-col gap-0.5">
          {(insights?.byProject ?? []).slice(0, 5).map((project) => (
            <Tooltip key={project.projectName}>
              <TooltipTrigger asChild>
                <button
                  type="button"
                  onClick={() =>
                    state.setFilter(condition("projectName", "equals", project.projectName))
                  }
                  className="flex items-center gap-2 rounded px-0.5 text-[11.5px] hover:bg-muted"
                >
                  <span className="w-32 truncate text-left">{project.projectName}</span>
                  <span className="h-2 min-w-0 flex-1 overflow-hidden rounded-full bg-muted">
                    <span
                      className="block h-full bg-primary"
                      style={{
                        width: `${
                          (project.visits /
                            Math.max(...(insights?.byProject ?? [{ visits: 1 }]).map((p) => p.visits))) *
                          100
                        }%`,
                      }}
                    />
                  </span>
                  <span className="w-8 text-right tabular-nums">{project.visits}</span>
                </button>
              </TooltipTrigger>
              <TooltipContent className="text-[11px]">
                {project.completed} completed · {project.highInterest} high interest · avg
                rating {project.averageRating.toFixed(1)}
              </TooltipContent>
            </Tooltip>
          ))}
        </div>
      </div>

      {insights && insights.byWeekday.length > 0 ? (
        <div className="min-w-40">
          <span className="text-[10.5px] font-medium tracking-wide text-muted-foreground uppercase">
            By weekday
          </span>
          <MiniBars
            values={insights.byWeekday.map((day) => day.count)}
            labels={insights.byWeekday.map((day) => day.label)}
            tone="violet"
            height={34}
            className="mt-1 w-40"
          />
        </div>
      ) : null}
    </div>
  );

  async function checkIn(visit: SiteVisitRow) {
    try {
      await siteVisitsApi.checkIn(visit.id);
      toast.success(`${visit.visitorName} checked in`);
      state.refresh();
      loadInsights();
    } catch (error) {
      toast.error("Check-in failed", {
        description: error instanceof ApiError ? error.message : "Network error.",
      });
    }
  }

  async function checkOut(visit: SiteVisitRow) {
    try {
      await siteVisitsApi.checkOut(visit.id, { interestLevel: "Medium" });
      toast.success(`${visit.visitorName} checked out`, {
        description: "Marked complete — add feedback from the record page.",
      });
      state.refresh();
      loadInsights();
    } catch (error) {
      toast.error("Check-out failed", {
        description: error instanceof ApiError ? error.message : "Network error.",
      });
    }
  }

  async function changeStatus(visit: SiteVisitRow, change: StatusChange) {
    try {
      await siteVisitsApi.setStatus(visit.id, {
        status: change.status,
        scheduledAt: change.at,
        reason: change.note,
      });

      toast.success(`${visit.visitorName}’s visit updated`, {
        description: change.at
          ? `Moved to ${formatDateTime(change.at)}.`
          : `Marked ${humanise(change.status).toLowerCase()}.`,
      });

      state.refresh();
      loadInsights();
    } catch (error) {
      toast.error("Could not update this visit", {
        description: error instanceof ApiError ? error.message : "Network error.",
      });
    }
  }

  const columns: GridColumn<SiteVisitRow>[] = [
    {
      id: "code",
      label: "Visit",
      sortField: "visitCode",
      sticky: true,
      width: 150,
      render: (row) => (
        <span className="flex flex-col leading-tight">
          <span className="font-medium">{row.visitorName}</span>
          <span className="text-[10.5px] text-muted-foreground">{row.visitCode}</span>
        </span>
      ),
    },
    {
      id: "scheduled",
      label: "Scheduled",
      sortField: "scheduledAt",
      width: 132,
      render: (row) => (
        <span className="flex flex-col leading-tight">
          <span>{formatDateTime(row.scheduledAt)}</span>
          <span className="text-[10.5px] text-muted-foreground">
            {timeAgo(row.scheduledAt)}
          </span>
        </span>
      ),
    },
    {
      id: "status",
      label: "Status",
      sortField: "status",
      width: 128,
      // Clicking the state is how it gets changed — the same control the
      // follow-up queue and the lead record use.
      render: (row) => (
        <StatusMenu
          family="visit"
          status={row.status}
          scheduledAt={row.scheduledAt}
          onChange={(change) => changeStatus(row, change)}
        />
      ),
    },
    {
      id: "type",
      label: "Type",
      sortField: "visitType",
      width: 104,
      render: (row) => <Pill tone="neutral">{humanise(row.visitType)}</Pill>,
    },
    {
      id: "project",
      label: "Project",
      sortField: "projectName",
      width: 160,
      render: (row) => (
        <span className="block truncate">{row.projectName ?? "—"}</span>
      ),
    },
    {
      id: "unit",
      label: "Unit",
      sortField: "unitNumber",
      width: 86,
      render: (row) => (
        <span className="text-muted-foreground tabular-nums">{row.unitNumber ?? "—"}</span>
      ),
    },
    {
      id: "host",
      label: "Host",
      sortField: "hostName",
      width: 126,
      render: (row) => (
        <span className={cn("truncate", !row.hostName && "text-muted-foreground italic")}>
          {row.hostName ?? "Unassigned"}
        </span>
      ),
    },
    {
      id: "party",
      label: "Party",
      sortField: "partySize",
      align: "right",
      width: 58,
      render: (row) => row.partySize,
    },
    {
      id: "interest",
      label: "Interest",
      sortField: "interestLevel",
      width: 88,
      render: (row) =>
        row.interestLevel ? (
          <Pill tone={interestTone(row.interestLevel)}>{row.interestLevel}</Pill>
        ) : (
          <span className="text-muted-foreground">—</span>
        ),
    },
    {
      id: "rating",
      label: "Rating",
      sortField: "rating",
      width: 78,
      render: (row) =>
        row.rating ? (
          <span className="flex items-center gap-0.5">
            {Array.from({ length: 5 }, (_, i) => (
              <Star
                key={i}
                className={cn(
                  "size-2.5",
                  i < row.rating!
                    ? "fill-amber-400 text-amber-400"
                    : "text-muted-foreground/30"
                )}
              />
            ))}
          </span>
        ) : (
          <span className="text-muted-foreground">—</span>
        ),
    },
    {
      id: "duration",
      label: "On site",
      sortField: "checkOutAt",
      align: "right",
      width: 76,
      render: (row) =>
        row.durationMinutes ? (
          `${row.durationMinutes}m`
        ) : (
          <span className="text-muted-foreground">—</span>
        ),
    },
    {
      id: "budget",
      label: "Budget",
      sortField: "budgetDiscussed",
      align: "right",
      width: 92,
      render: (row) => formatMoney(row.budgetDiscussed),
    },
    {
      id: "feedback",
      label: "Feedback",
      width: 280,
      defaultHidden: true,
      render: (row) => (
        <span className="block truncate text-muted-foreground">{row.feedback ?? "—"}</span>
      ),
    },
    {
      id: "nextAction",
      label: "Next action",
      width: 200,
      defaultHidden: true,
      render: (row) => (
        <span className="block truncate text-muted-foreground">{row.nextAction ?? "—"}</span>
      ),
    },
    {
      id: "transport",
      label: "Transport",
      sortField: "transportMode",
      width: 110,
      defaultHidden: true,
      render: (row) => (
        <span className="text-muted-foreground">{row.transportMode ?? "—"}</span>
      ),
    },
    {
      id: "actions",
      label: "",
      width: 96,
      fixedWidth: true,
      render: (row) => {
        if (row.status === "Completed") {
          return <span className="text-[11px] text-muted-foreground">Done</span>;
        }
        if (row.status === "Cancelled" || row.status === "NoShow") {
          return <span className="text-[11px] text-muted-foreground">Closed</span>;
        }

        return row.checkInAt ? (
          <Button
            variant="outline"
            size="sm"
            className="h-6 text-[11px]"
            onClick={(event) => {
              event.stopPropagation();
              checkOut(row);
            }}
          >
            <CheckCircle2 className="size-3" /> Check out
          </Button>
        ) : (
          <Button
            variant="outline"
            size="sm"
            className="h-6 text-[11px]"
            onClick={(event) => {
              event.stopPropagation();
              checkIn(row);
            }}
          >
            <LogIn className="size-3" /> Check in
          </Button>
        );
      },
    },
  ];

  return (
    <ListShell
      icon={CalendarCheck2}
      title="Site visits"
      storageKey="site-visits"
      hint="Scheduling, check-in and post-visit feedback. Completing a visit moves its lead to the Site Visit stage automatically — the strongest buying signal in residential sales shouldn't wait on a manual edit."
      state={state}
      columns={columns}
      metrics={metrics}
      visuals={visuals}
      quickViews={quickViews}
      searchPlaceholder="Visitor, code, phone, feedback…"
      emptyMessage="No visits match this view."
      actions={
        insights ? (
          <span className="inline-flex h-8 items-center gap-1.5 rounded border px-2 text-[12px] text-muted-foreground">
            <TrendingUp className="size-3.5 text-primary" />
            {insights.completionRate.toFixed(0)}% completion
          </span>
        ) : null
      }
    />
  );
}
