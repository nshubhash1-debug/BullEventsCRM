"use client";

import * as React from "react";
import { useRouter } from "next/navigation";
import {
  BellRing,
  CalendarClock,
  CheckCircle2,
  CircleDot,
  TriangleAlert,
} from "lucide-react";
import type { LucideIcon } from "lucide-react";
import { toast } from "sonner";

import type { GridColumn } from "@/components/crm/crm-grid";
import { ListShell, type QuickView } from "@/components/crm/list-shell";
import { StatusMenu, type StatusChange } from "@/components/crm/status-menu";
import {
  DistributionBar,
  Pill,
  StatusDot,
  type Metric,
  type Tone,
} from "@/components/crm/metrics";
import { Button } from "@/components/ui/button";
import { useCrmList } from "@/hooks/use-crm-list";
import { ApiError } from "@/lib/api";
import {
  followUpsApi,
  formatDateTime,
  humanise,
  timeAgo,
  type Agenda,
  type FollowUpRow,
} from "@/lib/crm-api";
import { priorityTone } from "@/lib/crm-tones";
import { emptyRoot, type FilterNode } from "@/lib/query";
import { cn } from "@/lib/utils";

const FACETS = ["status", "channel", "priority", "relatedType", "ownerName"];
const INITIAL_SORT = [{ field: "dueAt", descending: false }];

function condition(field: string, operator: string, value?: string, values?: string[]): FilterNode {
  const root = emptyRoot();
  return { ...root, children: [{ key: `${root.key}-c`, field, operator, value, values }] };
}

/** Open + past due — the definition used everywhere, including on the server. */
function overdueFilter(): FilterNode {
  const root = emptyRoot();
  return {
    ...root,
    children: [
      { key: "o1", field: "status", operator: "in", values: ["Open", "InProgress"] },
      { key: "o2", field: "dueAt", operator: "overdue" },
    ],
  };
}

export const CHANNEL_TONE: Record<string, Tone> = {
  Call: "primary",
  Email: "info",
  WhatsApp: "success",
  Meeting: "violet",
  SiteVisit: "warning",
  Task: "neutral",
};

/** A fixed channel scope — how Tasks, WhatsApp and Email reuse this view. */
export function channelScope(channel: string): FilterNode {
  const root = emptyRoot();
  return {
    ...root,
    children: [
      { key: `scope-${channel}`, field: "channel", operator: "equals", value: channel },
    ],
  };
}

interface FollowUpsViewProps {
  icon: LucideIcon;
  title: string;
  hint: string;
  storageKey: string;
  /** Fixed narrowing the user cannot see or remove. */
  scope?: FilterNode | null;
  /** Hides the channel column and distribution when the view is one channel. */
  singleChannel?: boolean;
  /** Rendered above the metric strip — integration notices, mostly. */
  notice?: React.ReactNode;
  actions?: React.ReactNode;
}

/**
 * The follow-up queue.
 *
 * Tasks, WhatsApp and Email are the same object filtered to one channel, so
 * they share this view rather than existing as four near-identical pages.
 */
export function FollowUpsView({
  icon,
  title,
  hint,
  storageKey,
  scope = null,
  singleChannel = false,
  notice,
  actions,
}: FollowUpsViewProps) {
  const router = useRouter();
  const state = useCrmList<FollowUpRow>(followUpsApi, {
    facets: FACETS,
    dateFields: [
      { id: "dueAt", label: "Due" },
      { id: "completedAt", label: "Completed" },
      { id: "createdAt", label: "Created" },
    ],
    sort: INITIAL_SORT,
    pageSize: 50,
    scope,
  });

  const [agenda, setAgenda] = React.useState<Agenda | null>(null);

  const loadAgenda = React.useCallback(() => {
    followUpsApi.agenda().then(setAgenda).catch(() => setAgenda(null));
  }, []);

  React.useEffect(loadAgenda, [loadAgenda]);

  const aggregates = state.aggregates;

  const metrics: Metric[] = [
    {
      label: "In view",
      value: state.total.toLocaleString(),
      icon: BellRing,
    },
    {
      label: "Overdue",
      value: (aggregates.overdue ?? 0).toLocaleString(),
      tone: (aggregates.overdue ?? 0) > 0 ? "danger" : "success",
      icon: TriangleAlert,
      hint: "Open or in progress with a due date in the past.",
      onClick: () => state.setFilter(overdueFilter()),
    },
    {
      label: "Due today",
      value: (aggregates.dueToday ?? 0).toLocaleString(),
      tone: "warning",
      icon: CalendarClock,
      onClick: () => state.setFilter(condition("dueAt", "today")),
    },
    {
      label: "Open",
      value: (aggregates.open ?? 0).toLocaleString(),
      tone: "info",
      icon: CircleDot,
      onClick: () => state.setFilter(condition("status", "equals", "Open")),
    },
    {
      label: "Completed",
      value: (aggregates.completed ?? 0).toLocaleString(),
      tone: "success",
      icon: CheckCircle2,
      onClick: () => state.setFilter(condition("status", "equals", "Completed")),
    },
  ];

  const quickViews: QuickView[] = [
    { id: "overdue", label: "Overdue", build: overdueFilter },
    { id: "today", label: "Today", build: () => condition("dueAt", "today") },
    {
      id: "week",
      label: "Next 7 days",
      build: () => condition("dueAt", "nextNDays", "7"),
    },
    {
      id: "open",
      label: "Open",
      build: () => condition("status", "in", undefined, ["Open", "InProgress"]),
    },
    {
      id: "hot",
      label: "Hot priority",
      build: () => condition("priority", "equals", "Hot"),
    },
  ];

  /**
   * The agenda strip is the reason this page exists: buckets that answer
   * "what does today look like" before any filtering happens.
   */
  const visuals = agenda ? (
    <div className={cn("grid gap-3", !singleChannel && "md:grid-cols-2")}>
      <DistributionBar
        title="When it's due"
        segments={[
          { key: "overdue", label: "Overdue", count: agenda.overdue, tone: "danger" },
          { key: "today", label: "Today", count: agenda.today, tone: "warning" },
          { key: "tomorrow", label: "Tomorrow", count: agenda.tomorrow, tone: "primary" },
          { key: "week", label: "This week", count: agenda.thisWeek, tone: "info" },
          { key: "later", label: "Later", count: agenda.later, tone: "neutral" },
        ]}
        onSelect={(key) => {
          const filters: Record<string, () => FilterNode> = {
            overdue: overdueFilter,
            today: () => condition("dueAt", "today"),
            tomorrow: () => condition("dueAt", "nextNDays", "2"),
            week: () => condition("dueAt", "nextNDays", "7"),
            later: () => condition("dueAt", "nextNDays", "90"),
          };
          state.setFilter(filters[key]());
        }}
      />

      {!singleChannel ? (
        <DistributionBar
          title="Channel"
          segments={agenda.byChannel.map((bucket) => ({
            key: bucket.key,
            label: bucket.label,
            count: bucket.count,
            tone: CHANNEL_TONE[bucket.key] ?? "neutral",
          }))}
          onSelect={(key) => state.setFilter(condition("channel", "equals", key))}
        />
      ) : null}
    </div>
  ) : null;

  async function changeStatus(followUp: FollowUpRow, change: StatusChange) {
    try {
      await followUpsApi.setStatus(followUp.id, {
        status: change.status,
        dueAt: change.at,
        outcome: change.note,
      });

      toast.success(`“${followUp.subject}” updated`, {
        description: change.at
          ? `Now due ${formatDateTime(change.at)}.`
          : `Marked ${humanise(change.status).toLowerCase()}.`,
      });

      state.refresh();
      loadAgenda();
    } catch (error) {
      toast.error("Could not update this follow-up", {
        description: error instanceof ApiError ? error.message : "Network error.",
      });
    }
  }

  function openRelated(followUp: FollowUpRow) {
    if (followUp.relatedType === "Lead") {
      router.push(`/dashboard/leads/${followUp.relatedId}`);
    }
  }

  const columns: GridColumn<FollowUpRow>[] = [
    {
      id: "due",
      label: "Due",
      sortField: "dueAt",
      sticky: true,
      width: 132,
      render: (row) => (
        <span className="flex flex-col leading-tight">
          <span
            className={cn(
              "font-medium",
              row.isOverdue && "text-red-600 dark:text-red-400"
            )}
          >
            {timeAgo(row.dueAt)}
          </span>
          <span className="text-[10.5px] text-muted-foreground">
            {formatDateTime(row.dueAt)}
          </span>
        </span>
      ),
    },
    {
      id: "subject",
      label: "Subject",
      sortField: "subject",
      width: 230,
      render: (row) => (
        <span className="block truncate font-medium">{row.subject}</span>
      ),
    },
    {
      id: "related",
      label: "Related to",
      sortField: "relatedName",
      width: 190,
      render: (row) => (
        <span className="flex min-w-0 items-center gap-1.5">
          <Pill tone="neutral">{row.relatedType}</Pill>
          <button
            type="button"
            onClick={(event) => {
              event.stopPropagation();
              openRelated(row);
            }}
            className="truncate text-primary hover:underline"
          >
            {row.relatedName}
          </button>
        </span>
      ),
    },
    ...(singleChannel
      ? []
      : ([
          {
            id: "channel",
            label: "Channel",
            sortField: "channel",
            width: 100,
            render: (row: FollowUpRow) => (
              <StatusDot
                label={humanise(row.channel)}
                tone={CHANNEL_TONE[row.channel] ?? "neutral"}
              />
            ),
          },
        ] satisfies GridColumn<FollowUpRow>[])),
    {
      id: "status",
      label: "Status",
      sortField: "status",
      width: 132,
      // The badge people already look at is the thing they click; a separate
      // action column would put the state and the way to change it in two
      // different places.
      render: (row) => (
        <span className="flex items-center gap-1">
          <StatusMenu
            family="followUp"
            status={row.status}
            scheduledAt={row.dueAt}
            isOverdue={row.isOverdue}
            onChange={(change) => changeStatus(row, change)}
          />
          {row.isOverdue && row.status !== "Completed" ? (
            <Pill tone="danger">Overdue</Pill>
          ) : null}
        </span>
      ),
    },
    {
      id: "priority",
      label: "Priority",
      sortField: "priority",
      width: 80,
      render: (row) => <Pill tone={priorityTone(row.priority)}>{row.priority}</Pill>,
    },
    {
      id: "owner",
      label: "Owner",
      sortField: "ownerName",
      width: 130,
      render: (row) => (
        <span className={cn("truncate", !row.ownerName && "text-muted-foreground italic")}>
          {row.ownerName ?? "Unassigned"}
        </span>
      ),
    },
    {
      id: "sla",
      label: "SLA",
      sortField: "slaMinutes",
      align: "right",
      width: 72,
      defaultHidden: true,
      render: (row) => (
        <span className="text-muted-foreground">
          {row.slaMinutes >= 1440
            ? `${Math.round(row.slaMinutes / 1440)}d`
            : `${Math.round(row.slaMinutes / 60)}h`}
        </span>
      ),
    },
    {
      id: "description",
      label: "Detail",
      width: 260,
      defaultHidden: true,
      render: (row) => (
        <span className="block truncate text-muted-foreground">
          {row.description ?? "—"}
        </span>
      ),
    },
    {
      id: "outcome",
      label: "Outcome",
      width: 210,
      defaultHidden: true,
      render: (row) => (
        <span className="block truncate text-muted-foreground">{row.outcome ?? "—"}</span>
      ),
    },
    {
      id: "completed",
      label: "Completed",
      sortField: "completedAt",
      width: 96,
      defaultHidden: true,
      render: (row) => (
        <span className="text-muted-foreground">{timeAgo(row.completedAt)}</span>
      ),
    },
    {
      id: "branch",
      label: "Branch",
      sortField: "branchName",
      width: 120,
      defaultHidden: true,
      render: (row) => <span className="text-muted-foreground">{row.branchName}</span>,
    },
    {
      id: "actions",
      label: "",
      width: 96,
      fixedWidth: true,
      render: (row) =>
        row.status === "Completed" || row.status === "Cancelled" ? (
          <span className="text-[11px] text-muted-foreground">Closed</span>
        ) : (
          <Button
            variant="outline"
            size="sm"
            className="h-6 text-[11px]"
            onClick={(event) => {
              event.stopPropagation();
              void changeStatus(row, { status: "Completed" });
            }}
          >
            <CheckCircle2 className="size-3" /> Done
          </Button>
        ),
    },
  ];

  return (
    <ListShell
      icon={icon}
      title={title}
      storageKey={storageKey}
      hint={hint}
      state={state}
      columns={columns}
      metrics={metrics}
      visuals={
        notice ? (
          <div className="flex flex-col gap-3">
            {notice}
            {visuals}
          </div>
        ) : (
          visuals
        )
      }
      quickViews={quickViews}
      searchPlaceholder="Subject, related record, outcome…"
      emptyMessage="Nothing due in this view."
      actions={actions}
    />
  );
}
