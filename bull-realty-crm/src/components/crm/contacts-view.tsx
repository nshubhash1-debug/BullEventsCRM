"use client";

import * as React from "react";
import {
  Ban,
  Contact as ContactIcon,
  Handshake,
  Layers,
  Mail,
  Phone,
  Sparkles,
  Wallet,
} from "lucide-react";
import type { LucideIcon } from "lucide-react";
import { toast } from "sonner";

import type { GridColumn } from "@/components/crm/crm-grid";
import { ListShell, type QuickView } from "@/components/crm/list-shell";
import {
  DistributionBar,
  Pill,
  StatusDot,
  type Metric,
  type Tone,
} from "@/components/crm/metrics";
import { Avatar, AvatarFallback } from "@/components/ui/avatar";
import { Button } from "@/components/ui/button";
import {
  Tooltip,
  TooltipContent,
  TooltipTrigger,
} from "@/components/ui/tooltip";
import { useCrmList } from "@/hooks/use-crm-list";
import { ApiError } from "@/lib/api";
import {
  contactsApi,
  formatMoney,
  humanise,
  initials,
  intelligenceApi,
  timeAgo,
  type ContactRow,
  type SegmentProfile,
} from "@/lib/crm-api";
import { emptyRoot, type FilterNode } from "@/lib/query";
import { cn } from "@/lib/utils";

const FACETS = ["type", "lifecycleStage", "source", "segment", "ownerName", "city"];
const INITIAL_SORT = [{ field: "lifetimeValue", descending: true }];

function condition(field: string, operator: string, value?: string, values?: string[]): FilterNode {
  const root = emptyRoot();
  return { ...root, children: [{ key: `${root.key}-c`, field, operator, value, values }] };
}

const LIFECYCLE_TONE: Record<string, Tone> = {
  Prospect: "neutral",
  Engaged: "info",
  Customer: "success",
  Repeat: "violet",
  Dormant: "warning",
  Churned: "danger",
};

const SEGMENT_TONE: Record<string, Tone> = {
  "Key accounts": "success",
  "At-risk value": "warning",
  Emerging: "info",
  Dormant: "neutral",
};

interface ContactsViewProps {
  icon: LucideIcon;
  title: string;
  hint: string;
  storageKey: string;
  /** Fixed, invisible narrowing — what separates "Customers" from "Contacts". */
  scope?: FilterNode | null;
}

/**
 * Contacts and the customer database are the same object with different
 * populations, so they share one view rather than two near-identical files.
 * The difference is a scope filter the user cannot see or remove.
 */
export function ContactsView({
  icon,
  title,
  hint,
  storageKey,
  scope = null,
}: ContactsViewProps) {
  const state = useCrmList<ContactRow>(contactsApi, {
    facets: FACETS,
    dateFields: [
      { id: "createdAt", label: "Created" },
      { id: "lastActivityAt", label: "Last activity" },
    ],
    sort: INITIAL_SORT,
    pageSize: 50,
    scope,
  });

  const [segments, setSegments] = React.useState<SegmentProfile[]>([]);
  const [segmentEngine, setSegmentEngine] = React.useState<string | null>(null);
  const [segmenting, setSegmenting] = React.useState(false);

  const loadSegments = React.useCallback(() => {
    intelligenceApi
      .segments(4)
      .then((result) => {
        setSegments(result.segments);
        setSegmentEngine(result.engine);
      })
      .catch(() => setSegments([]));
  }, []);

  React.useEffect(loadSegments, [loadSegments]);

  const aggregates = state.aggregates;

  const metrics: Metric[] = [
    { label: "In view", value: state.total.toLocaleString(), icon: ContactIcon },
    {
      label: "Customers",
      value: (aggregates.customers ?? 0).toLocaleString(),
      tone: "success",
      icon: Handshake,
      onClick: () => state.setFilter(condition("type", "equals", "Customer")),
    },
    {
      label: "Lifetime value",
      value: formatMoney(aggregates.lifetimeValue ?? 0),
      tone: "primary",
      icon: Wallet,
    },
    {
      label: "Deals closed",
      value: (aggregates.deals ?? 0).toLocaleString(),
      tone: "violet",
    },
    {
      label: "Avg value",
      value:
        state.total > 0
          ? formatMoney((aggregates.lifetimeValue ?? 0) / state.total)
          : "—",
      tone: "info",
    },
  ];

  const quickViews: QuickView[] = [
    { id: "customers", label: "Customers", build: () => condition("type", "equals", "Customer") },
    {
      id: "repeat",
      label: "Repeat buyers",
      build: () => condition("dealCount", "greaterOrEqual", "2"),
    },
    {
      id: "dormant",
      label: "Dormant",
      build: () => condition("lastActivityAt", "olderThanNDays", "180"),
    },
    {
      id: "highValue",
      label: "Over ₹1 Cr LTV",
      build: () => condition("lifetimeValue", "greaterOrEqual", "10000000"),
    },
    {
      id: "consent",
      label: "Do not contact",
      build: () =>
        ({
          ...emptyRoot(),
          conjunction: "or",
          children: [
            { key: "dnc1", field: "doNotCall", operator: "isTrue" },
            { key: "dnc2", field: "doNotEmail", operator: "isTrue" },
          ],
        }) as FilterNode,
    },
  ];

  async function applySegments() {
    setSegmenting(true);
    try {
      const result = await intelligenceApi.applySegments(4);
      toast.success(result.message);
      loadSegments();
      state.refresh();
    } catch (error) {
      toast.error("Segmentation failed", {
        description: error instanceof ApiError ? error.message : "Network error.",
      });
    } finally {
      setSegmenting(false);
    }
  }

  const visuals = (
    <div className="grid gap-3 lg:grid-cols-[1fr_1fr_1.2fr]">
      <DistributionBar
        title="Lifecycle stage"
        segments={(state.facets.lifecycleStage ?? []).map((bucket) => ({
          key: bucket.value,
          label: humanise(bucket.value),
          count: bucket.count,
          tone: LIFECYCLE_TONE[bucket.value] ?? "neutral",
        }))}
        onSelect={(value) => state.setFilter(condition("lifecycleStage", "equals", value))}
      />

      <DistributionBar
        title="Source"
        segments={(state.facets.source ?? []).map((bucket, index) => ({
          key: bucket.value,
          label: humanise(bucket.value),
          count: bucket.count,
          tone: (["primary", "info", "violet", "success", "warning", "danger", "neutral"] as const)[
            index % 7
          ],
        }))}
        onSelect={(value) => state.setFilter(condition("source", "equals", value))}
      />

      {segments.length > 0 ? (
        <div className="min-w-0">
          <span className="flex items-center gap-1.5 text-[10.5px] font-medium tracking-wide text-muted-foreground uppercase">
            <Sparkles className="size-3" />
            AI segments
            {segmentEngine ? (
              <span className="normal-case opacity-70">· {segmentEngine}</span>
            ) : null}
          </span>
          <div className="mt-1 flex flex-col gap-0.5">
            {segments.map((segment) => (
              <Tooltip key={segment.clusterId}>
                <TooltipTrigger asChild>
                  <button
                    type="button"
                    onClick={() =>
                      state.setFilter(condition("segment", "equals", segment.name))
                    }
                    className="flex items-center gap-2 rounded px-0.5 text-[11.5px] hover:bg-muted"
                  >
                    <span
                      className={cn(
                        "size-1.5 shrink-0 rounded-full",
                        segment.name === "Key accounts"
                          ? "bg-emerald-500"
                          : segment.name === "At-risk value"
                            ? "bg-amber-500"
                            : segment.name === "Emerging"
                              ? "bg-sky-500"
                              : "bg-zinc-400"
                      )}
                    />
                    <span className="w-24 truncate text-left">{segment.name}</span>
                    <span className="w-8 text-right tabular-nums text-muted-foreground">
                      {segment.size}
                    </span>
                    <span className="flex-1 text-right tabular-nums">
                      {formatMoney(segment.averageLifetimeValue)}
                    </span>
                  </button>
                </TooltipTrigger>
                <TooltipContent className="max-w-xs text-[11px]">
                  {segment.description} Average {segment.averageDeals.toFixed(1)} deals,
                  last active {Math.round(segment.averageDaysSinceActivity)} days ago.
                </TooltipContent>
              </Tooltip>
            ))}
          </div>
        </div>
      ) : null}
    </div>
  );

  const columns: GridColumn<ContactRow>[] = [
    {
      id: "name",
      label: "Name",
      sortField: "fullName",
      sticky: true,
      width: 210,
      render: (row) => (
        <span className="flex items-center gap-1.5">
          <Avatar className="size-5 shrink-0">
            <AvatarFallback className="bg-primary/10 text-[8px] text-primary">
              {initials(row.fullName)}
            </AvatarFallback>
          </Avatar>
          <span className="min-w-0">
            <span className="block truncate font-medium text-primary">{row.fullName}</span>
            {row.accountName || row.designation ? (
              <span className="block truncate text-[10.5px] text-muted-foreground">
                {[row.designation, row.accountName].filter(Boolean).join(" · ")}
              </span>
            ) : null}
          </span>
        </span>
      ),
    },
    {
      id: "type",
      label: "Type",
      sortField: "type",
      width: 92,
      render: (row) => <Pill tone="neutral">{humanise(row.type)}</Pill>,
    },
    {
      id: "lifecycle",
      label: "Lifecycle",
      sortField: "lifecycleStage",
      width: 108,
      render: (row) => (
        <StatusDot
          label={humanise(row.lifecycleStage)}
          tone={LIFECYCLE_TONE[row.lifecycleStage] ?? "neutral"}
        />
      ),
    },
    {
      id: "segment",
      label: "AI segment",
      sortField: "segment",
      width: 118,
      render: (row) =>
        row.segment ? (
          <Pill tone={SEGMENT_TONE[row.segment] ?? "info"}>{row.segment}</Pill>
        ) : (
          <span className="text-muted-foreground">—</span>
        ),
    },
    {
      id: "contact",
      label: "Contact",
      width: 180,
      render: (row) => (
        <span className="flex flex-col text-[11px] text-muted-foreground">
          {row.phone ? (
            <span className="flex items-center gap-1">
              <Phone className="size-2.5 shrink-0" />
              {row.phone}
              {row.doNotCall ? (
                <Tooltip>
                  <TooltipTrigger asChild>
                    <span>
                      <Ban className="size-2.5 text-red-500" />
                    </span>
                  </TooltipTrigger>
                  <TooltipContent className="text-[11px]">
                    Marked do-not-call
                  </TooltipContent>
                </Tooltip>
              ) : null}
            </span>
          ) : null}
          {row.email ? (
            <span className="flex items-center gap-1 truncate">
              <Mail className="size-2.5 shrink-0" />
              <span className="truncate">{row.email}</span>
            </span>
          ) : null}
        </span>
      ),
    },
    {
      id: "ltv",
      label: "Lifetime value",
      sortField: "lifetimeValue",
      align: "right",
      width: 118,
      render: (row) => (
        <span className={cn(row.lifetimeValue > 0 && "font-medium")}>
          {row.lifetimeValue > 0 ? formatMoney(row.lifetimeValue) : "—"}
        </span>
      ),
    },
    {
      id: "deals",
      label: "Deals",
      sortField: "dealCount",
      align: "right",
      width: 58,
      render: (row) => (
        <span className={cn(row.dealCount === 0 && "text-muted-foreground")}>
          {row.dealCount}
        </span>
      ),
    },
    {
      id: "open",
      label: "Open",
      align: "right",
      width: 58,
      render: (row) => (
        <span className={cn(row.openOpportunities > 0 ? "font-medium text-primary" : "text-muted-foreground")}>
          {row.openOpportunities}
        </span>
      ),
    },
    {
      id: "budget",
      label: "Budget",
      sortField: "budgetMax",
      align: "right",
      width: 96,
      render: (row) => formatMoney(row.budgetMax),
    },
    {
      id: "preference",
      label: "Prefers",
      sortField: "preferredConfiguration",
      width: 150,
      render: (row) => (
        <span className="block truncate text-muted-foreground">
          {[row.preferredConfiguration, row.preferredLocality].filter(Boolean).join(" · ") ||
            "—"}
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
      id: "lastActivity",
      label: "Last touch",
      sortField: "lastActivityAt",
      width: 96,
      render: (row) => (
        <span className="text-muted-foreground">{timeAgo(row.lastActivityAt)}</span>
      ),
    },
    {
      id: "city",
      label: "City",
      sortField: "city",
      width: 100,
      render: (row) => <span className="text-muted-foreground">{row.city ?? "—"}</span>,
    },
    {
      id: "source",
      label: "Source",
      sortField: "source",
      width: 118,
      defaultHidden: true,
      render: (row) => (
        <span className="text-muted-foreground">{humanise(row.source)}</span>
      ),
    },
    {
      id: "consent",
      label: "Consent",
      width: 110,
      defaultHidden: true,
      render: (row) => (
        <span className="flex gap-1">
          <Pill tone={row.doNotCall ? "danger" : "success"}>
            {row.doNotCall ? "No calls" : "Calls ok"}
          </Pill>
          {row.whatsAppOptIn ? <Pill tone="success">WA</Pill> : null}
        </span>
      ),
    },
    {
      id: "pan",
      label: "PAN",
      sortField: "panNumber",
      width: 110,
      defaultHidden: true,
      render: (row) => (
        <span className="text-muted-foreground tabular-nums">{row.panNumber ?? "—"}</span>
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
      id: "created",
      label: "Added",
      sortField: "createdAt",
      width: 88,
      defaultHidden: true,
      render: (row) => (
        <span className="text-muted-foreground">{timeAgo(row.createdAt)}</span>
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
      visuals={visuals}
      quickViews={quickViews}
      searchPlaceholder="Name, account, mobile, email, tags…"
      emptyMessage="No contacts match this view."
      actions={
        <Button
          variant="outline"
          size="sm"
          className="h-8"
          disabled={segmenting}
          onClick={applySegments}
        >
          <Layers /> Re-segment
        </Button>
      }
    />
  );
}
