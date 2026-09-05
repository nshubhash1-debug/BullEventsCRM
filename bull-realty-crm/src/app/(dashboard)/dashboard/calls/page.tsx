"use client";

import * as React from "react";
import { useRouter } from "next/navigation";
import {
  ArrowDownLeft,
  ArrowUpRight,
  PhoneCall,
  PhoneMissed,
  Smile,
  Timer,
  TrendingUp,
} from "lucide-react";

import type { GridColumn } from "@/components/crm/crm-grid";
import { ListShell, type QuickView } from "@/components/crm/list-shell";
import {
  DistributionBar,
  MiniBars,
  Pill,
  StatusDot,
  TONE_FILL,
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
  callsApi,
  formatDateTime,
  formatDuration,
  humanise,
  type CallRow,
  type CallScorecard,
} from "@/lib/crm-api";
import {
  callOutcomeTone,
  dispositionTone,
  sentimentTone,
} from "@/lib/crm-tones";
import { emptyRoot, type FilterNode } from "@/lib/query";
import { cn } from "@/lib/utils";

const FACETS = ["outcome", "disposition", "direction", "agentName", "sentimentLabel"];
const INITIAL_SORT = [{ field: "startedAt", descending: true }];

function condition(field: string, operator: string, value?: string, values?: string[]): FilterNode {
  const root = emptyRoot();
  return { ...root, children: [{ key: `${root.key}-c`, field, operator, value, values }] };
}

const DIRECTION_ICON = {
  Inbound: ArrowDownLeft,
  Outbound: ArrowUpRight,
  Missed: PhoneMissed,
} as const;

export default function CallsPage() {
  const router = useRouter();
  const state = useCrmList<CallRow>(callsApi, {
    facets: FACETS,
    dateFields: [
      { id: "startedAt", label: "Started" },
      { id: "followUpAt", label: "Follow-up due" },
      { id: "createdAt", label: "Logged" },
    ],
    sort: INITIAL_SORT,
    pageSize: 50,
  });

  const [scorecard, setScorecard] = React.useState<CallScorecard[]>([]);

  React.useEffect(() => {
    callsApi.scorecard(30).then(setScorecard).catch(() => setScorecard([]));
  }, []);

  const aggregates = state.aggregates;
  const connectRate =
    (aggregates.calls ?? 0) > 0
      ? (aggregates.connected ?? 0) / (aggregates.calls ?? 1)
      : 0;

  const metrics: Metric[] = [
    { label: "Calls in view", value: state.total.toLocaleString(), icon: PhoneCall },
    {
      label: "Connected",
      value: (aggregates.connected ?? 0).toLocaleString(),
      tone: "success",
      progress: connectRate,
      hint: `${(connectRate * 100).toFixed(1)}% connect rate across the filtered set.`,
      onClick: () => state.setFilter(condition("outcome", "equals", "Connected")),
    },
    {
      label: "Talk time",
      value: `${Math.round(aggregates.talkMinutes ?? 0).toLocaleString()}m`,
      tone: "primary",
      icon: Timer,
    },
    {
      label: "Positive tone",
      value: (aggregates.positive ?? 0).toLocaleString(),
      tone: "violet",
      icon: Smile,
      hint: "Notes the local sentiment model read as positive. Inference runs in-process — no note leaves the server.",
      onClick: () => state.setFilter(condition("sentimentLabel", "equals", "Positive")),
    },
    {
      label: "Avg duration",
      value: formatDuration(
        (aggregates.connected ?? 0) > 0
          ? Math.round(((aggregates.talkMinutes ?? 0) * 60) / (aggregates.connected ?? 1))
          : 0
      ),
      tone: "info",
    },
  ];

  const quickViews: QuickView[] = [
    {
      id: "today",
      label: "Today",
      build: () => condition("startedAt", "today"),
    },
    {
      id: "week",
      label: "Last 7 days",
      build: () => condition("startedAt", "lastNDays", "7"),
    },
    {
      id: "connected",
      label: "Connected",
      build: () => condition("outcome", "equals", "Connected"),
    },
    {
      id: "interested",
      label: "Interested",
      build: () =>
        condition("disposition", "in", undefined, [
          "Interested",
          "SiteVisitScheduled",
          "Converted",
        ]),
    },
    {
      id: "callback",
      label: "Call back",
      build: () => condition("disposition", "equals", "CallBackLater"),
    },
  ];

  /**
   * The scorecard is the manager's view of the same data: who is dialling, how
   * often they connect, and how often a connected call actually moved something.
   */
  const visuals = (
    <div className="grid gap-3 lg:grid-cols-[1fr_1.2fr]">
      <div className="grid gap-3">
        <DistributionBar
          title="Outcome"
          segments={(state.facets.outcome ?? []).map((bucket) => ({
            key: bucket.value,
            label: humanise(bucket.value),
            count: bucket.count,
            tone: callOutcomeTone(bucket.value),
          }))}
          onSelect={(value) => state.setFilter(condition("outcome", "equals", value))}
        />
        <DistributionBar
          title="Disposition"
          segments={(state.facets.disposition ?? [])
            .filter((bucket) => bucket.value !== "—")
            .map((bucket) => ({
              key: bucket.value,
              label: humanise(bucket.value),
              count: bucket.count,
              tone: dispositionTone(bucket.value),
            }))}
          onSelect={(value) => state.setFilter(condition("disposition", "equals", value))}
        />
      </div>

      {scorecard.length > 0 ? (
        <div className="min-w-0">
          <span className="text-[10.5px] font-medium tracking-wide text-muted-foreground uppercase">
            Agent scorecard · last 30 days
          </span>
          <div className="mt-1 flex flex-col gap-1">
            {scorecard.slice(0, 6).map((agent) => (
              <div key={agent.agentName} className="flex items-center gap-2 text-[11.5px]">
                <span className="w-28 truncate">{agent.agentName}</span>
                <span className="w-10 text-right tabular-nums text-muted-foreground">
                  {agent.calls}
                </span>
                <Tooltip>
                  <TooltipTrigger asChild>
                    <span className="h-2 min-w-0 flex-1 overflow-hidden rounded-full bg-muted">
                      <span
                        className={cn("block h-full", TONE_FILL.success)}
                        style={{ width: `${agent.connectRate}%` }}
                      />
                    </span>
                  </TooltipTrigger>
                  <TooltipContent className="text-[11px]">
                    {agent.connected} of {agent.calls} connected ·{" "}
                    {agent.talkMinutes.toFixed(0)}m talk ·{" "}
                    {agent.progressionRate.toFixed(0)}% progressed
                  </TooltipContent>
                </Tooltip>
                <span className="w-11 text-right tabular-nums">
                  {agent.connectRate.toFixed(0)}%
                </span>
                <MiniBars
                  values={[agent.progressed, agent.connected - agent.progressed]}
                  labels={["Progressed", "No movement"]}
                  tone="primary"
                  height={14}
                  className="w-10"
                />
              </div>
            ))}
          </div>
        </div>
      ) : null}
    </div>
  );

  const columns: GridColumn<CallRow>[] = [
    {
      id: "startedAt",
      label: "When",
      sortField: "startedAt",
      sticky: true,
      width: 128,
      render: (row) => (
        <span className="text-muted-foreground">{formatDateTime(row.startedAt)}</span>
      ),
    },
    {
      id: "direction",
      label: "Dir",
      sortField: "direction",
      width: 56,
      render: (row) => {
        const Icon = DIRECTION_ICON[row.direction as keyof typeof DIRECTION_ICON] ?? PhoneCall;
        const tone: Tone =
          row.direction === "Inbound" ? "info" : row.direction === "Missed" ? "danger" : "primary";

        return (
          <Tooltip>
            <TooltipTrigger asChild>
              <span>
                <Icon
                  className={cn(
                    "size-3.5",
                    tone === "info" && "text-sky-600 dark:text-sky-400",
                    tone === "danger" && "text-red-600 dark:text-red-400",
                    tone === "primary" && "text-primary"
                  )}
                />
              </span>
            </TooltipTrigger>
            <TooltipContent className="text-[11px]">{row.direction}</TooltipContent>
          </Tooltip>
        );
      },
    },
    {
      id: "related",
      label: "Related to",
      sortField: "relatedName",
      width: 200,
      render: (row) => (
        <span className="flex min-w-0 items-center gap-1.5">
          <Pill tone="neutral">{row.relatedType}</Pill>
          <button
            type="button"
            onClick={(event) => {
              event.stopPropagation();
              if (row.relatedType === "Lead") {
                router.push(`/dashboard/leads/${row.relatedId}`);
              }
            }}
            className="truncate text-primary hover:underline"
          >
            {row.relatedName}
          </button>
        </span>
      ),
    },
    {
      id: "phone",
      label: "Number",
      sortField: "phoneNumber",
      width: 118,
      render: (row) => (
        <span className="text-muted-foreground tabular-nums">{row.phoneNumber ?? "—"}</span>
      ),
    },
    {
      id: "outcome",
      label: "Outcome",
      sortField: "outcome",
      width: 118,
      render: (row) => (
        <StatusDot label={humanise(row.outcome)} tone={callOutcomeTone(row.outcome)} />
      ),
    },
    {
      id: "duration",
      label: "Duration",
      sortField: "durationSeconds",
      align: "right",
      width: 84,
      render: (row) => formatDuration(row.durationSeconds),
    },
    {
      id: "disposition",
      label: "Disposition",
      sortField: "disposition",
      width: 138,
      render: (row) =>
        row.disposition ? (
          <Pill tone={dispositionTone(row.disposition)}>{humanise(row.disposition)}</Pill>
        ) : (
          <span className="text-muted-foreground">—</span>
        ),
    },
    {
      id: "sentiment",
      label: "Tone",
      sortField: "sentimentScore",
      width: 92,
      render: (row) =>
        row.sentimentLabel ? (
          <Tooltip>
            <TooltipTrigger asChild>
              <span>
                <Pill tone={sentimentTone(row.sentimentLabel)}>
                  {row.sentimentLabel}
                </Pill>
              </span>
            </TooltipTrigger>
            <TooltipContent className="text-[11px]">
              Local sentiment score {row.sentimentScore?.toFixed(2)} (−1 to +1)
            </TooltipContent>
          </Tooltip>
        ) : (
          <span className="text-muted-foreground">—</span>
        ),
    },
    {
      id: "agent",
      label: "Agent",
      sortField: "agentName",
      width: 128,
      render: (row) => <span className="truncate">{row.agentName}</span>,
    },
    {
      id: "notes",
      label: "Notes",
      width: 300,
      render: (row) => (
        <span className="block truncate text-muted-foreground">{row.notes ?? "—"}</span>
      ),
    },
    {
      id: "followUp",
      label: "Follow-up",
      sortField: "followUpAt",
      width: 118,
      defaultHidden: true,
      render: (row) => (
        <span className="text-muted-foreground">{formatDateTime(row.followUpAt)}</span>
      ),
    },
    {
      id: "wait",
      label: "Wait",
      sortField: "waitSeconds",
      align: "right",
      width: 66,
      defaultHidden: true,
      render: (row) => (row.waitSeconds ? `${row.waitSeconds}s` : "—"),
    },
    {
      id: "branch",
      label: "Branch",
      sortField: "branchName",
      width: 120,
      defaultHidden: true,
      render: (row) => <span className="text-muted-foreground">{row.branchName}</span>,
    },
  ];

  return (
    <ListShell
      icon={PhoneCall}
      title="Calls"
      storageKey="calls"
      hint="Every telephony interaction, with the tone of each note read by a model trained on this workspace's own call outcomes."
      state={state}
      columns={columns}
      metrics={metrics}
      visuals={visuals}
      quickViews={quickViews}
      searchPlaceholder="Contact, number, notes…"
      emptyMessage="No calls match this view."
      actions={
        <span className="inline-flex h-8 items-center gap-1.5 rounded border px-2 text-[12px] text-muted-foreground">
          <TrendingUp className="size-3.5 text-primary" />
          {(connectRate * 100).toFixed(0)}% connect rate
        </span>
      }
    />
  );
}
