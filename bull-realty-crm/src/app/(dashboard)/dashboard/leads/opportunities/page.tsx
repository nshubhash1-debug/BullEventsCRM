"use client";

import * as React from "react";
import {
  Brain,
  Handshake,
  Hourglass,
  Target,
  TrendingUp,
  Trophy,
} from "lucide-react";

import type { GridColumn } from "@/components/crm/crm-grid";
import { ListShell, type QuickView } from "@/components/crm/list-shell";
import {
  DistributionBar,
  Pill,
  ScoreCell,
  Sparkline,
  StatusDot,
  type Metric,
  type Tone,
} from "@/components/crm/metrics";
import {
  Tooltip,
  TooltipContent,
  TooltipTrigger,
} from "@/components/ui/tooltip";
import { useCrmList } from "@/hooks/use-crm-list";
import {
  formatDate,
  formatMoney,
  humanise,
  intelligenceApi,
  opportunitiesApi,
  timeAgo,
  type ForecastResult,
  type OpportunityRow,
} from "@/lib/crm-api";
import { opportunityStageTone } from "@/lib/crm-tones";
import { emptyRoot, type FilterNode } from "@/lib/query";
import { cn } from "@/lib/utils";

const FACETS = ["stage", "type", "forecastCategory", "ownerName", "projectName", "source"];
const INITIAL_SORT = [{ field: "amount", descending: true }];

function condition(field: string, operator: string, value?: string, values?: string[]): FilterNode {
  const root = emptyRoot();
  return { ...root, children: [{ key: `${root.key}-c`, field, operator, value, values }] };
}

const FORECAST_TONE: Record<string, Tone> = {
  Pipeline: "neutral",
  BestCase: "info",
  Commit: "success",
  Closed: "primary",
  Omitted: "danger",
};

export default function OpportunitiesPage() {
  const state = useCrmList<OpportunityRow>(opportunitiesApi, {
    facets: FACETS,
    dateFields: [
      { id: "createdAt", label: "Created" },
      { id: "expectedCloseDate", label: "Expected close" },
      { id: "actualCloseDate", label: "Actual close" },
      { id: "stageEnteredAt", label: "Stage entered" },
    ],
    sort: INITIAL_SORT,
    pageSize: 50,
  });

  const [forecast, setForecast] = React.useState<ForecastResult | null>(null);

  React.useEffect(() => {
    intelligenceApi
      .forecast("revenue", 3)
      .then(setForecast)
      .catch(() => setForecast(null));
  }, []);

  const aggregates = state.aggregates;

  const metrics: Metric[] = [
    { label: "Deals in view", value: state.total.toLocaleString(), icon: Handshake },
    {
      label: "Pipeline value",
      value: formatMoney(aggregates.pipelineValue ?? 0),
      tone: "primary",
      icon: Target,
    },
    {
      label: "Weighted",
      value: formatMoney(aggregates.weightedValue ?? 0),
      tone: "violet",
      hint: "Amount × probability, summed across the filtered set.",
    },
    {
      label: "Commission",
      value: formatMoney(aggregates.commission ?? 0),
      tone: "success",
    },
    {
      label: "Closed won",
      value: (aggregates.won ?? 0).toLocaleString(),
      tone: "success",
      icon: Trophy,
      onClick: () => state.setFilter(condition("stage", "equals", "ClosedWon")),
    },
    {
      label: "Next month forecast",
      value: forecast ? formatMoney(forecast.nextPeriodValue) : "—",
      tone: "info",
      icon: TrendingUp,
      spark: forecast?.series.slice(-12).map((point) => Number(point.value)),
      hint: forecast?.message,
    },
  ];

  const quickViews: QuickView[] = [
    {
      id: "open",
      label: "Open",
      build: () =>
        condition("stage", "in", undefined, [
          "Qualification",
          "NeedsAnalysis",
          "Proposal",
          "Negotiation",
        ]),
    },
    {
      id: "commit",
      label: "Commit",
      build: () => condition("forecastCategory", "equals", "Commit"),
    },
    {
      id: "closingSoon",
      label: "Closing in 30 days",
      build: () => condition("expectedCloseDate", "nextNDays", "30"),
    },
    {
      id: "slipping",
      label: "Slipping",
      build: () =>
        ({
          ...emptyRoot(),
          children: [
            {
              key: "s1",
              field: "stage",
              operator: "in",
              values: ["Qualification", "NeedsAnalysis", "Proposal", "Negotiation"],
            },
            { key: "s2", field: "expectedCloseDate", operator: "overdue" },
          ],
        }) as FilterNode,
    },
    {
      id: "big",
      label: "Over ₹2 Cr",
      build: () => condition("amount", "greaterOrEqual", "20000000"),
    },
  ];

  const visuals = (
    <div className="grid gap-3 lg:grid-cols-3">
      <DistributionBar
        title="Stage"
        segments={(state.facets.stage ?? []).map((bucket) => ({
          key: bucket.value,
          label: humanise(bucket.value),
          count: bucket.count,
          tone: opportunityStageTone(bucket.value),
        }))}
        onSelect={(value) => state.setFilter(condition("stage", "equals", value))}
      />
      <DistributionBar
        title="Forecast category"
        segments={(state.facets.forecastCategory ?? []).map((bucket) => ({
          key: bucket.value,
          label: humanise(bucket.value),
          count: bucket.count,
          tone: FORECAST_TONE[bucket.value] ?? "neutral",
        }))}
        onSelect={(value) =>
          state.setFilter(condition("forecastCategory", "equals", value))
        }
      />

      {forecast ? (
        <div className="min-w-0">
          <span className="flex items-center gap-1.5 text-[10.5px] font-medium tracking-wide text-muted-foreground uppercase">
            <Brain className="size-3" />
            Won revenue · {forecast.engine}
          </span>
          <Tooltip>
            <TooltipTrigger asChild>
              <div className="mt-1">
                <Sparkline
                  values={forecast.series.map((point) => Number(point.value))}
                  tone="primary"
                  width={240}
                  height={34}
                  className="w-full"
                />
              </div>
            </TooltipTrigger>
            <TooltipContent className="max-w-xs text-[11px]">
              {forecast.message}
            </TooltipContent>
          </Tooltip>
          <div className="mt-0.5 flex justify-between text-[10.5px] text-muted-foreground tabular-nums">
            <span>{forecast.series[0]?.period}</span>
            <span>
              Next 3 months: {formatMoney(forecast.horizonTotal)}
            </span>
            <span>{forecast.series.at(-1)?.period}</span>
          </div>
        </div>
      ) : null}
    </div>
  );

  const columns: GridColumn<OpportunityRow>[] = [
    {
      id: "name",
      label: "Opportunity",
      sortField: "name",
      sticky: true,
      width: 250,
      render: (row) => (
        <span className="flex flex-col leading-tight">
          <span className="truncate font-medium text-primary">{row.name}</span>
          {row.contactName ? (
            <span className="truncate text-[10.5px] text-muted-foreground">
              {row.contactName}
            </span>
          ) : null}
        </span>
      ),
    },
    {
      id: "stage",
      label: "Stage",
      sortField: "stage",
      width: 122,
      render: (row) => (
        <StatusDot label={humanise(row.stage)} tone={opportunityStageTone(row.stage)} />
      ),
    },
    {
      id: "amount",
      label: "Amount",
      sortField: "amount",
      align: "right",
      width: 106,
      render: (row) => <span className="font-medium">{formatMoney(row.amount)}</span>,
    },
    {
      id: "probability",
      label: "Rep %",
      sortField: "probability",
      align: "right",
      width: 70,
      render: (row) => `${row.probability}%`,
    },
    {
      id: "ai",
      label: "AI win",
      width: 100,
      render: (row) =>
        row.aiProbability === null ? (
          <span className="text-muted-foreground">—</span>
        ) : (
          <Tooltip>
            <TooltipTrigger asChild>
              <span>
                <ScoreCell score={row.aiProbability} band={row.aiBand} />
              </span>
            </TooltipTrigger>
            <TooltipContent className="text-[11px]">
              {row.aiBand} — predicted locally from stage, engagement and deal shape.
            </TooltipContent>
          </Tooltip>
        ),
    },
    {
      id: "forecast",
      label: "Forecast",
      sortField: "forecastCategory",
      width: 104,
      render: (row) => (
        <Pill tone={FORECAST_TONE[row.forecastCategory] ?? "neutral"}>
          {humanise(row.forecastCategory)}
        </Pill>
      ),
    },
    {
      id: "closeDate",
      label: "Close date",
      sortField: "expectedCloseDate",
      width: 104,
      render: (row) => {
        const overdue =
          !row.actualCloseDate && new Date(row.expectedCloseDate) < new Date();

        return (
          <span className={cn(overdue && "text-red-600 dark:text-red-400")}>
            {formatDate(row.actualCloseDate ?? row.expectedCloseDate)}
          </span>
        );
      },
    },
    {
      id: "daysInStage",
      label: "In stage",
      sortField: "stageEnteredAt",
      align: "right",
      width: 78,
      render: (row) => (
        <span
          className={cn(
            row.daysInStage > 45
              ? "text-red-600 dark:text-red-400"
              : row.daysInStage > 21
                ? "text-amber-600 dark:text-amber-400"
                : "text-muted-foreground"
          )}
        >
          {row.daysInStage}d
        </span>
      ),
    },
    {
      id: "owner",
      label: "Owner",
      sortField: "ownerName",
      width: 126,
      render: (row) => (
        <span className={cn("truncate", !row.ownerName && "text-muted-foreground italic")}>
          {row.ownerName ?? "Unassigned"}
        </span>
      ),
    },
    {
      id: "project",
      label: "Project",
      sortField: "projectName",
      width: 156,
      render: (row) => (
        <span className="block truncate text-muted-foreground">
          {row.projectName ?? "—"}
        </span>
      ),
    },
    {
      id: "unit",
      label: "Unit",
      sortField: "unitNumber",
      width: 84,
      render: (row) => (
        <span className="text-muted-foreground tabular-nums">
          {row.unitNumber ?? "—"}
        </span>
      ),
    },
    {
      id: "nextStep",
      label: "Next step",
      sortField: "nextStep",
      width: 220,
      render: (row) => (
        <span className="block truncate text-muted-foreground">
          {row.nextStep ?? "—"}
        </span>
      ),
    },
    {
      id: "type",
      label: "Type",
      sortField: "type",
      width: 116,
      defaultHidden: true,
      render: (row) => <Pill tone="neutral">{humanise(row.type)}</Pill>,
    },
    {
      id: "commission",
      label: "Commission",
      sortField: "expectedCommission",
      align: "right",
      width: 104,
      defaultHidden: true,
      render: (row) => formatMoney(row.expectedCommission),
    },
    {
      id: "loss",
      label: "Loss reason",
      sortField: "lossReason",
      width: 150,
      defaultHidden: true,
      render: (row) => (
        <span className="block truncate text-muted-foreground">
          {row.lossReason ?? "—"}
        </span>
      ),
    },
    {
      id: "competitor",
      label: "Competitor",
      sortField: "competitorName",
      width: 130,
      defaultHidden: true,
      render: (row) => (
        <span className="text-muted-foreground">{row.competitorName ?? "—"}</span>
      ),
    },
    {
      id: "created",
      label: "Age",
      sortField: "createdAt",
      width: 84,
      defaultHidden: true,
      render: (row) => (
        <span className="text-muted-foreground">{timeAgo(row.createdAt)}</span>
      ),
    },
  ];

  return (
    <ListShell
      icon={Handshake}
      title="Opportunities"
      storageKey="opportunities"
      hint="Revenue-bearing deals. Rep probability and the model's own view sit side by side — the model never overwrites what a rep entered."
      state={state}
      columns={columns}
      metrics={metrics}
      visuals={visuals}
      quickViews={quickViews}
      searchPlaceholder="Deal, contact, competitor, description…"
      emptyMessage="No opportunities match this view."
      actions={
        <span className="inline-flex h-8 items-center gap-1.5 rounded border px-2 text-[12px] text-muted-foreground">
          <Hourglass className="size-3.5 text-primary" />
          {(aggregates.pipelineValue ?? 0) > 0
            ? `${(((aggregates.weightedValue ?? 0) / (aggregates.pipelineValue ?? 1)) * 100).toFixed(0)}% weighted`
            : "—"}
        </span>
      }
    />
  );
}
