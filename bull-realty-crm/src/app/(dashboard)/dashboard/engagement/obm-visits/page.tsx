"use client";

import * as React from "react";
import Link from "next/link";
import { toast } from "sonner";
import {
  Banknote,
  MapPin,
  Navigation,
  Route,
  Target,
  Wallet,
} from "lucide-react";

import type { GridColumn } from "@/components/crm/crm-grid";
import { ListShell, type QuickView } from "@/components/crm/list-shell";
import { StatusMenu, type StatusChange } from "@/components/crm/status-menu";
import {
  DistributionBar,
  Pill,
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
import { ApiError } from "@/lib/api";
import {
  formatDateTime,
  formatMoney,
  humanise,
  obmVisitsApi,
  timeAgo,
  type FieldProductivity,
  type ObmVisitRow,
} from "@/lib/crm-api";
import { visitStatusTone } from "@/lib/crm-tones";
import { emptyRoot, type FilterNode } from "@/lib/query";
import { cn } from "@/lib/utils";

const FACETS = ["status", "partnerType", "agentName", "city"];
const INITIAL_SORT = [{ field: "scheduledAt", descending: true }];

function condition(field: string, operator: string, value?: string, values?: string[]): FilterNode {
  const root = emptyRoot();
  return { ...root, children: [{ key: `${root.key}-c`, field, operator, value, values }] };
}

const PARTNER_TONE: Record<string, Tone> = {
  ChannelPartner: "primary",
  Builder: "violet",
  Broker: "info",
  Corporate: "warning",
  Bank: "success",
  Society: "neutral",
};

export default function ObmVisitsPage() {
  const state = useCrmList<ObmVisitRow>(obmVisitsApi, {
    facets: FACETS,
    dateFields: [
      { id: "scheduledAt", label: "Scheduled" },
      { id: "checkInAt", label: "Checked in" },
      { id: "nextMeetingAt", label: "Next meeting" },
      { id: "createdAt", label: "Created" },
    ],
    sort: INITIAL_SORT,
    pageSize: 50,
  });

  const [productivity, setProductivity] = React.useState<FieldProductivity[]>([]);

  React.useEffect(() => {
    obmVisitsApi.productivity(60).then(setProductivity).catch(() => setProductivity([]));
  }, []);

  const aggregates = state.aggregates;
  const leadsGenerated = aggregates.leadsGenerated ?? 0;
  const expense = aggregates.expense ?? 0;

  const metrics: Metric[] = [
    { label: "Meetings in view", value: state.total.toLocaleString(), icon: Route },
    {
      label: "Completed",
      value: (aggregates.completed ?? 0).toLocaleString(),
      tone: "success",
      onClick: () => state.setFilter(condition("status", "equals", "Completed")),
    },
    {
      label: "Leads generated",
      value: leadsGenerated.toLocaleString(),
      tone: "primary",
      icon: Target,
      hint: "The number this view exists to move — meetings are an input, referred leads are the output.",
    },
    {
      label: "Business value",
      value: formatMoney(aggregates.businessValue ?? 0),
      tone: "violet",
      icon: Banknote,
    },
    {
      label: "Field spend",
      value: formatMoney(expense),
      tone: "warning",
      icon: Wallet,
    },
    {
      label: "Cost per lead",
      value: leadsGenerated > 0 ? formatMoney(expense / leadsGenerated) : "—",
      tone: "info",
      hint: "Total travel and meeting expense divided by leads generated across the filtered set.",
    },
  ];

  const quickViews: QuickView[] = [
    {
      id: "upcoming",
      label: "Upcoming",
      build: () => condition("scheduledAt", "nextNDays", "14"),
    },
    {
      id: "completed",
      label: "Completed",
      build: () => condition("status", "equals", "Completed"),
    },
    {
      id: "productive",
      label: "Produced leads",
      build: () => condition("leadsGenerated", "greaterThan", "0"),
    },
    {
      id: "partners",
      label: "Channel partners",
      build: () => condition("partnerType", "equals", "ChannelPartner"),
    },
    {
      id: "followUp",
      label: "Next meeting set",
      build: () => condition("nextMeetingAt", "isNotNull"),
    },
  ];

  const maxLeads = Math.max(1, ...productivity.map((agent) => agent.leadsGenerated));

  const visuals = (
    <div className="grid gap-3 lg:grid-cols-[1fr_1fr_1.1fr]">
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

      <DistributionBar
        title="Partner type"
        segments={(state.facets.partnerType ?? []).map((bucket) => ({
          key: bucket.value,
          label: humanise(bucket.value),
          count: bucket.count,
          tone: PARTNER_TONE[bucket.value] ?? "neutral",
        }))}
        onSelect={(value) => state.setFilter(condition("partnerType", "equals", value))}
      />

      {productivity.length > 0 ? (
        <div className="min-w-0">
          <span className="text-[10.5px] font-medium tracking-wide text-muted-foreground uppercase">
            Field productivity · last 60 days
          </span>
          <div className="mt-1 flex flex-col gap-0.5">
            {productivity.slice(0, 5).map((agent) => (
              <Tooltip key={agent.agentName}>
                <TooltipTrigger asChild>
                  <button
                    type="button"
                    onClick={() =>
                      state.setFilter(condition("agentName", "equals", agent.agentName))
                    }
                    className="flex items-center gap-2 rounded px-0.5 text-[11.5px] hover:bg-muted"
                  >
                    <span className="w-28 truncate text-left">{agent.agentName}</span>
                    <span className="h-2 min-w-0 flex-1 overflow-hidden rounded-full bg-muted">
                      <span
                        className={cn("block h-full", TONE_FILL.primary)}
                        style={{ width: `${(agent.leadsGenerated / maxLeads) * 100}%` }}
                      />
                    </span>
                    <span className="w-7 text-right tabular-nums">
                      {agent.leadsGenerated}
                    </span>
                  </button>
                </TooltipTrigger>
                <TooltipContent className="text-[11px]">
                  {agent.visits} meetings · {agent.distanceKm.toFixed(0)} km ·{" "}
                  {formatMoney(agent.expense)} spend ·{" "}
                  {agent.costPerLead > 0 ? formatMoney(agent.costPerLead) : "—"} per lead
                </TooltipContent>
              </Tooltip>
            ))}
          </div>
        </div>
      ) : null}
    </div>
  );

  async function changeStatus(visit: ObmVisitRow, change: StatusChange) {
    try {
      await obmVisitsApi.setStatus(visit.id, {
        status: change.status,
        scheduledAt: change.at,
        reason: change.note,
      });

      toast.success(`${visit.partnerName} meeting updated`, {
        description: change.at
          ? `Moved to ${formatDateTime(change.at)}.`
          : `Marked ${humanise(change.status).toLowerCase()}.`,
      });

      state.refresh();
    } catch (error) {
      toast.error("Could not update this meeting", {
        description: error instanceof ApiError ? error.message : "Network error.",
      });
    }
  }

  const columns: GridColumn<ObmVisitRow>[] = [
    {
      id: "partner",
      label: "Partner",
      sortField: "partnerName",
      sticky: true,
      width: 190,
      render: (row) => (
        <span className="flex flex-col leading-tight">
          <span className="truncate font-medium">{row.partnerName}</span>
          <span className="text-[10.5px] text-muted-foreground">{row.visitCode}</span>
        </span>
      ),
    },
    {
      id: "type",
      label: "Type",
      sortField: "partnerType",
      width: 128,
      render: (row) => (
        <Pill tone={PARTNER_TONE[row.partnerType] ?? "neutral"}>
          {humanise(row.partnerType)}
        </Pill>
      ),
    },
    {
      id: "scheduled",
      label: "Scheduled",
      sortField: "scheduledAt",
      width: 130,
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
      id: "lead",
      label: "Lead",
      width: 150,
      render: (row) =>
        row.leadName ? (
          <Link
            href={`/dashboard/leads/${row.leadId}`}
            onClick={(event) => event.stopPropagation()}
            className="truncate text-primary hover:underline"
          >
            {row.leadName}
          </Link>
        ) : (
          <span className="text-muted-foreground">—</span>
        ),
    },
    {
      id: "status",
      label: "Status",
      sortField: "status",
      width: 128,
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
      id: "agent",
      label: "Agent",
      sortField: "agentName",
      width: 126,
      render: (row) => <span className="truncate">{row.agentName}</span>,
    },
    {
      id: "location",
      label: "Location",
      sortField: "locationLabel",
      width: 150,
      render: (row) => (
        <span className="flex min-w-0 items-center gap-1 text-muted-foreground">
          {row.latitude && row.longitude ? (
            <Tooltip>
              <TooltipTrigger asChild>
                <span>
                  <MapPin className="size-3 shrink-0 text-primary" />
                </span>
              </TooltipTrigger>
              <TooltipContent className="text-[11px] tabular-nums">
                {row.latitude.toFixed(4)}, {row.longitude.toFixed(4)}
              </TooltipContent>
            </Tooltip>
          ) : (
            <MapPin className="size-3 shrink-0 opacity-40" />
          )}
          <span className="truncate">{row.locationLabel ?? "—"}</span>
        </span>
      ),
    },
    {
      id: "leads",
      label: "Leads",
      sortField: "leadsGenerated",
      align: "right",
      width: 66,
      render: (row) => (
        <span
          className={cn(
            row.leadsGenerated > 0
              ? "font-semibold text-emerald-600 dark:text-emerald-400"
              : "text-muted-foreground"
          )}
        >
          {row.leadsGenerated}
        </span>
      ),
    },
    {
      id: "value",
      label: "Value",
      sortField: "businessValue",
      align: "right",
      width: 92,
      render: (row) => formatMoney(row.businessValue),
    },
    {
      id: "distance",
      label: "Distance",
      sortField: "distanceKm",
      align: "right",
      width: 82,
      render: (row) =>
        row.distanceKm ? `${row.distanceKm.toFixed(1)} km` : <span className="text-muted-foreground">—</span>,
    },
    {
      id: "expense",
      label: "Expense",
      sortField: "expenseAmount",
      align: "right",
      width: 86,
      render: (row) => formatMoney(row.expenseAmount),
    },
    {
      id: "duration",
      label: "Duration",
      align: "right",
      width: 78,
      defaultHidden: true,
      render: (row) =>
        row.durationMinutes ? `${row.durationMinutes}m` : <span className="text-muted-foreground">—</span>,
    },
    {
      id: "purpose",
      label: "Purpose",
      sortField: "purpose",
      width: 210,
      render: (row) => (
        <span className="block truncate text-muted-foreground">{row.purpose ?? "—"}</span>
      ),
    },
    {
      id: "outcome",
      label: "Outcome",
      width: 260,
      defaultHidden: true,
      render: (row) => (
        <span className="block truncate text-muted-foreground">{row.outcome ?? "—"}</span>
      ),
    },
    {
      id: "next",
      label: "Next meeting",
      sortField: "nextMeetingAt",
      width: 122,
      defaultHidden: true,
      render: (row) => (
        <span className="text-muted-foreground">{formatDateTime(row.nextMeetingAt)}</span>
      ),
    },
    {
      id: "contact",
      label: "Contact person",
      sortField: "contactPerson",
      width: 150,
      defaultHidden: true,
      render: (row) => (
        <span className="flex flex-col leading-tight">
          <span className="truncate">{row.contactPerson ?? "—"}</span>
          <span className="text-[10.5px] text-muted-foreground">
            {row.contactPhone ?? ""}
          </span>
        </span>
      ),
    },
  ];

  return (
    <ListShell
      icon={Route}
      title="OBM visits"
      storageKey="obm-visits"
      hint="Outdoor Business Meetings — field calls on channel partners, builders, banks and corporates. Measured on leads generated and cost per lead rather than on interest level."
      state={state}
      columns={columns}
      metrics={metrics}
      visuals={visuals}
      quickViews={quickViews}
      searchPlaceholder="Partner, contact person, purpose, notes…"
      emptyMessage="No field meetings match this view."
      actions={
        <span className="inline-flex h-8 items-center gap-1.5 rounded border px-2 text-[12px] text-muted-foreground">
          <Navigation className="size-3.5 text-primary" />
          {productivity.reduce((sum, agent) => sum + agent.distanceKm, 0).toFixed(0)} km
          travelled
        </span>
      }
    />
  );
}
