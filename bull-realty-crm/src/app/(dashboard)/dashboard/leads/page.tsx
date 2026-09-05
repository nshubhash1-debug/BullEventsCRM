"use client";

import * as React from "react";
import { useRouter } from "next/navigation";
import {
  Brain,
  Copy,
  Download,
  Flame,
  Gauge,
  Mail,
  MoreHorizontal,
  Phone,
  Sparkles,
  Trash2,
  TriangleAlert,
  Upload,
  UserRoundCheck,
  Users,
  Waypoints,
} from "lucide-react";
import { toast } from "sonner";

import type { GridColumn } from "@/components/crm/crm-grid";
import { ListShell, type QuickView } from "@/components/crm/list-shell";
import {
  DistributionBar,
  Pill,
  ScoreCell,
  StatusDot,
  type Metric,
} from "@/components/crm/metrics";
import { LeadFormDialog } from "@/components/dashboard/lead-form-dialog";
import { useSession } from "@/components/dashboard/session-provider";
import { Avatar, AvatarFallback } from "@/components/ui/avatar";
import { Button } from "@/components/ui/button";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
import { useCrmList } from "@/hooks/use-crm-list";
import { ApiError, getBranches, getUsers, type Branch, type UserListItem } from "@/lib/api";
import {
  formatDateTimeFull,
  formatMoney,
  humanise,
  initials,
  intelligenceApi,
  leadsApi,
  timeAgo,
  type LeadRow,
} from "@/lib/crm-api";
import {
  leadStageTone,
  priorityTone,
  slaTone,
  visitStatusTone,
} from "@/lib/crm-tones";
import { emptyRoot, type FilterNode } from "@/lib/query";
import { cn } from "@/lib/utils";

const FACETS = [
  "stage", "priority", "source", "ownerName", "cachedBand", "city",
  "eventType", "eventCategory",
];

/**
 * Nearest event first.
 *
 * An events desk works its pipeline in date order — the wedding in three weeks
 * outranks this morning's enquiry for next December — which is a different
 * default from a property desk sorting by score. Undated enquiries sort to the
 * end, where the "Date not fixed" view picks them up.
 */
const INITIAL_SORT = [{ field: "eventDate", descending: false }];

/** Builds a single-condition tree — what the quick-view pills apply. */
function condition(field: string, operator: string, value?: string, values?: string[]): FilterNode {
  const root = emptyRoot();
  return {
    ...root,
    children: [{ key: `${root.key}-c`, field, operator, value, values }],
  };
}

const SLA_LABEL: Record<string, string> = {
  Breached: "Breached",
  AtRisk: "At risk",
  OnTrack: "On track",
  Met: "Responded",
  None: "—",
};

/**
 * Colours for the distribution bars whose buckets carry no meaning of their own.
 *
 * Occasion and source have no natural good-to-bad ordering — a sangeet is not
 * worse than a reception — so they cycle a fixed palette rather than borrowing
 * the semantic tones that stage and priority use.
 */
const TONE_CYCLE = [
  "primary", "info", "violet", "success", "warning", "danger", "neutral",
] as const;

/**
 * The event date, with how far off it is under it.
 *
 * The countdown is tinted rather than badged: on an events desk almost every
 * row has a date, so a badge on each one would be noise. Colour appears only
 * where it means something — inside three weeks, or already past.
 */
function EventDateCell({ lead }: { lead: LeadRow }) {
  if (!lead.eventDate) {
    return <span className="text-muted-foreground">Date not fixed</span>;
  }

  const days = lead.daysToEvent;
  const date = new Date(lead.eventDate).toLocaleDateString(undefined, {
    day: "numeric",
    month: "short",
    year: "numeric",
  });

  const away =
    days === null
      ? null
      : days < 0
        ? `${Math.abs(days)}d ago`
        : days === 0
          ? "Today"
          : days === 1
            ? "Tomorrow"
            : `in ${days}d`;

  return (
    <div className="flex flex-col leading-tight">
      <span>{date}</span>
      {away ? (
        <span
          className={cn(
            "text-[11px]",
            days !== null && days < 0
              ? "text-muted-foreground"
              : days !== null && days <= 21
                ? "font-medium text-orange-600 dark:text-orange-400"
                : "text-muted-foreground"
          )}
        >
          {away}
          {lead.isDateFlexible ? " · flexible" : ""}
        </span>
      ) : null}
    </div>
  );
}

export default function LeadsPage() {
  const router = useRouter();
  const { user } = useSession();

  const state = useCrmList<LeadRow>(leadsApi, {
    facets: FACETS,
    dateFields: [
      { id: "eventDate", label: "Event date" },
      { id: "createdAt", label: "Created" },
      { id: "lastActivityAt", label: "Last activity" },
      { id: "convertedAt", label: "Converted" },
      { id: "slaDueAt", label: "SLA due" },
    ],
    sort: INITIAL_SORT,
    pageSize: 50,
  });

  const [branches, setBranches] = React.useState<Branch[]>([]);
  const [users, setUsers] = React.useState<UserListItem[]>([]);
  const [engine, setEngine] = React.useState<string | null>(null);
  const [assigning, setAssigning] = React.useState(false);

  React.useEffect(() => {
    getBranches().then(setBranches).catch(() => setBranches([]));
    getUsers().then(setUsers).catch(() => setUsers([]));
    intelligenceApi
      .models()
      .then((models) => setEngine(models[0]?.engine ?? null))
      .catch(() => setEngine(null));
  }, []);

  /* ---------------- metrics ---------------- */

  const aggregates = state.aggregates;

  const metrics: Metric[] = [
    {
      label: "Leads in view",
      value: state.total.toLocaleString(),
      icon: Users,
      hint: "Every lead matching the current search and filters — not just this page.",
    },
    {
      label: "Unassigned",
      value: (aggregates.unassigned ?? 0).toLocaleString(),
      tone: (aggregates.unassigned ?? 0) > 0 ? "warning" : "success",
      icon: UserRoundCheck,
      hint: "No owner means nobody is accountable for the first response.",
      onClick: () => state.setFilter(condition("ownerId", "isNull")),
    },
    {
      label: "Hot priority",
      value: (aggregates.hot ?? 0).toLocaleString(),
      tone: "danger",
      icon: Flame,
      onClick: () => state.setFilter(condition("priority", "equals", "Hot")),
    },
    {
      label: "SLA breached",
      value: (aggregates.breached ?? 0).toLocaleString(),
      tone: (aggregates.breached ?? 0) > 0 ? "danger" : "success",
      icon: TriangleAlert,
      hint: "First response was due and nothing has been logged yet.",
      onClick: () =>
        state.setFilter({
          ...emptyRoot(),
          children: [
            { key: "sla-1", field: "firstResponseAt", operator: "isNull" },
            { key: "sla-2", field: "slaDueAt", operator: "overdue" },
          ],
        }),
    },
    {
      label: "Budget in view",
      value: formatMoney(aggregates.pipelineValue ?? 0),
      tone: "primary",
      hint: "Sum of the upper budget on every matching lead.",
    },
    {
      label: "Avg AI score",
      value: (aggregates.averageScore ?? 0).toFixed(0),
      tone: "violet",
      icon: Gauge,
      progress: (aggregates.averageScore ?? 0) / 100,
      hint: engine
        ? `Scored locally by ${engine}. Nothing leaves this server.`
        : undefined,
    },
  ];

  /* ---------------- quick views ---------------- */

  const facetCount = (facet: string, value: string) =>
    state.facets[facet]?.find((bucket) => bucket.value === value)?.count;

  const quickViews: QuickView[] = [
    {
      id: "open",
      label: "Open",
      build: () =>
        condition("stage", "in", undefined, [
          "New",
          "Contacted",
          "Qualified",
          "ObmVisit",
          "SiteVisit",
          "FollowUp",
          "Negotiation",
        ]),
    },
    {
      id: "mine",
      label: "My leads",
      build: () => condition("ownerName", "equals", user.name),
    },
    {
      id: "unassigned",
      label: "Unassigned",
      build: () => condition("ownerId", "isNull"),
    },
    {
      id: "hot",
      label: "Hot",
      count: facetCount("priority", "Hot"),
      build: () => condition("priority", "equals", "Hot"),
    },
    {
      id: "stale",
      label: "Going cold",
      build: () => condition("lastActivityAt", "olderThanNDays", "14"),
    },
    // The three views an events desk actually works from. A date inside six
    // weeks is a closing conversation, one inside six months is a nurture, and
    // an enquiry with no date at all cannot be qualified until somebody rings
    // and asks — so each is a separate queue rather than a sort of one list.
    {
      id: "closing",
      label: "Event within 45 days",
      build: () => condition("eventDate", "nextNDays", "45"),
    },
    {
      id: "upcoming",
      label: "Next 6 months",
      build: () => condition("eventDate", "nextNDays", "180"),
    },
    {
      id: "undated",
      label: "Date not fixed",
      build: () => condition("eventDate", "isNull"),
    },
    {
      id: "booked",
      label: "Booked",
      count: facetCount("stage", "Booked"),
      build: () => condition("stage", "equals", "Booked"),
    },
  ];

  /* ---------------- visuals ---------------- */

  const visuals = (
    <div className="grid gap-3 md:grid-cols-3">
      <DistributionBar
        title="Stage"
        segments={(state.facets.stage ?? []).map((bucket) => ({
          key: bucket.value,
          label: humanise(bucket.value),
          count: bucket.count,
          tone: leadStageTone(bucket.value),
        }))}
        onSelect={(value) => state.setFilter(condition("stage", "equals", value))}
      />
      <DistributionBar
        title="Occasion"
        segments={(state.facets.eventType ?? []).map((bucket, index) => ({
          key: bucket.value,
          label: humanise(bucket.value),
          count: bucket.count,
          tone: TONE_CYCLE[index % TONE_CYCLE.length],
        }))}
        onSelect={(value) => state.setFilter(condition("eventType", "equals", value))}
      />
      <DistributionBar
        title="Priority"
        segments={(state.facets.priority ?? []).map((bucket) => ({
          key: bucket.value,
          label: bucket.value,
          count: bucket.count,
          tone: priorityTone(bucket.value),
        }))}
        onSelect={(value) => state.setFilter(condition("priority", "equals", value))}
      />
      <DistributionBar
        title="Source"
        segments={(state.facets.source ?? []).map((bucket, index) => ({
          key: bucket.value,
          label: humanise(bucket.value),
          count: bucket.count,
          tone: TONE_CYCLE[index % TONE_CYCLE.length],
        }))}
        onSelect={(value) => state.setFilter(condition("source", "equals", value))}
      />
    </div>
  );

  /* ---------------- columns ---------------- */

  const columns: GridColumn<LeadRow>[] = [
    {
      id: "score",
      label: "AI",
      sortField: "cachedScore",
      width: 92,
      render: (lead) => <ScoreCell score={lead.score} band={lead.band} />,
    },
    {
      id: "name",
      label: "Lead",
      sortField: "name",
      sticky: true,
      width: 210,
      render: (lead) => (
        <span className="flex items-center gap-1.5">
          <Avatar className="size-5 shrink-0">
            <AvatarFallback className="bg-primary/10 text-[8px] text-primary">
              {initials(lead.name)}
            </AvatarFallback>
          </Avatar>
          <span className="min-w-0">
            <span className="block truncate font-medium text-primary">{lead.name}</span>
            {lead.companyName ? (
              <span className="block truncate text-[10.5px] text-muted-foreground">
                {lead.companyName}
              </span>
            ) : null}
          </span>
        </span>
      ),
    },
    {
      id: "contact",
      label: "Contact",
      width: 170,
      render: (lead) => (
        <span className="flex flex-col text-[11px] text-muted-foreground">
          {lead.phone ? (
            <span className="flex items-center gap-1">
              <Phone className="size-2.5 shrink-0" /> {lead.phone}
            </span>
          ) : null}
          {lead.email ? (
            <span className="flex items-center gap-1 truncate">
              <Mail className="size-2.5 shrink-0" />
              <span className="truncate">{lead.email}</span>
            </span>
          ) : null}
        </span>
      ),
    },
    {
      id: "stage",
      label: "Stage",
      sortField: "stage",
      width: 108,
      render: (lead) => (
        <StatusDot label={humanise(lead.stage)} tone={leadStageTone(lead.stage)} />
      ),
    },
    {
      id: "priority",
      label: "Priority",
      sortField: "priority",
      width: 80,
      render: (lead) => <Pill tone={priorityTone(lead.priority)}>{lead.priority}</Pill>,
    },
    {
      id: "sla",
      label: "SLA",
      sortField: "slaDueAt",
      width: 92,
      render: (lead) =>
        lead.slaState === "None" ? (
          <span className="text-muted-foreground">—</span>
        ) : (
          <Pill tone={slaTone(lead.slaState)}>{SLA_LABEL[lead.slaState]}</Pill>
        ),
    },
    {
      id: "owner",
      label: "Owner",
      sortField: "ownerName",
      width: 120,
      render: (lead) => (
        <span
          className={cn(
            "truncate",
            !lead.ownerName && "text-muted-foreground italic"
          )}
        >
          {lead.ownerName ?? "Unassigned"}
        </span>
      ),
    },
    {
      id: "supportingManager",
      label: "Supporting manager",
      sortField: "supportingManagerName",
      width: 150,
      render: (lead) => (
        <span
          className={cn(
            "truncate",
            !lead.supportingManagerName && "text-muted-foreground italic"
          )}
        >
          {lead.supportingManagerName ?? "None"}
        </span>
      ),
    },
    {
      id: "siteVisitStatus",
      label: "Venue visit",
      width: 118,
      render: (lead) =>
        lead.siteVisitStatus ? (
          // The scheduled time rides along as a tooltip: the status is what the
          // list is scanned for, the date is what gets asked about next.
          <span title={formatDateTimeFull(lead.siteVisitAt)}>
            <StatusDot
              label={humanise(lead.siteVisitStatus)}
              tone={visitStatusTone(lead.siteVisitStatus)}
            />
          </span>
        ) : (
          <span className="text-muted-foreground">—</span>
        ),
    },
    {
      id: "obmStatus",
      label: "OBM",
      width: 118,
      render: (lead) =>
        lead.obmStatus ? (
          <span title={formatDateTimeFull(lead.obmVisitAt)}>
            <StatusDot
              label={humanise(lead.obmStatus)}
              tone={visitStatusTone(lead.obmStatus)}
            />
          </span>
        ) : (
          <span className="text-muted-foreground">—</span>
        ),
    },
    {
      id: "budget",
      label: "Budget",
      sortField: "budgetMax",
      align: "right",
      width: 96,
      render: (lead) => formatMoney(lead.budgetMax),
    },
    {
      id: "eventDate",
      label: "Event date",
      sortField: "eventDate",
      width: 132,
      render: (lead) => <EventDateCell lead={lead} />,
    },
    {
      id: "eventType",
      label: "Occasion",
      sortField: "eventType",
      width: 110,
      render: (lead) => humanise(lead.eventType) ?? "—",
    },
    {
      id: "guests",
      label: "Guests",
      sortField: "guestCount",
      align: "right",
      width: 72,
      render: (lead) =>
        lead.guestCount ? lead.guestCount.toLocaleString("en-IN") : "—",
    },
    {
      id: "locality",
      label: "Location",
      sortField: "preferredLocality",
      width: 120,
      render: (lead) => (
        <span className="truncate text-muted-foreground">
          {lead.preferredLocality ?? "—"}
        </span>
      ),
    },
    {
      id: "source",
      label: "Source",
      sortField: "source",
      width: 118,
      render: (lead) => (
        <span className="text-muted-foreground">{humanise(lead.source)}</span>
      ),
    },
    {
      id: "campaign",
      label: "Campaign",
      sortField: "campaign",
      width: 150,
      defaultHidden: true,
      render: (lead) => (
        <span className="truncate text-muted-foreground">{lead.campaign ?? "—"}</span>
      ),
    },
    {
      id: "utm",
      label: "UTM",
      width: 120,
      defaultHidden: true,
      render: (lead) => (
        <span className="text-[11px] text-muted-foreground">
          {lead.utmSource ? `${lead.utmSource} / ${lead.utmMedium ?? "—"}` : "—"}
        </span>
      ),
    },
    {
      id: "services",
      label: "Services",
      sortField: "servicesNeeded",
      width: 160,
      defaultHidden: true,
      render: (lead) => (
        <span className="truncate text-muted-foreground">
          {lead.servicesNeeded?.split(",").map(humanise).join(" · ") ?? "—"}
        </span>
      ),
    },
    {
      id: "venue",
      label: "Venue",
      sortField: "projectName",
      width: 130,
      defaultHidden: true,
      render: (lead) => (
        <span className="truncate text-muted-foreground">
          {lead.interestedProjectName ?? "—"}
        </span>
      ),
    },
    {
      id: "payment",
      label: "Payment",
      sortField: "paymentMode",
      width: 110,
      defaultHidden: true,
      render: (lead) => humanise(lead.paymentMode),
    },
    {
      id: "activity",
      label: "Acts",
      align: "right",
      width: 58,
      render: (lead) => (
        <span className={cn(lead.activityCount === 0 && "text-muted-foreground")}>
          {lead.activityCount}
        </span>
      ),
    },
    {
      id: "lastActivity",
      label: "Last touch",
      sortField: "lastActivityAt",
      width: 92,
      render: (lead) => (
        <span className="text-muted-foreground">{timeAgo(lead.lastActivityAt)}</span>
      ),
    },
    {
      id: "lastActivityType",
      label: "Last activity",
      width: 190,
      render: (lead) =>
        lead.lastActivityType ? (
          <span
            className="flex min-w-0 items-baseline gap-1.5"
            title={lead.lastActivitySummary ?? undefined}
          >
            <span className="shrink-0">{humanise(lead.lastActivityType)}</span>
            {lead.lastActivitySummary ? (
              <span className="truncate text-[11px] text-muted-foreground">
                {lead.lastActivitySummary}
              </span>
            ) : null}
          </span>
        ) : (
          <span className="text-muted-foreground">—</span>
        ),
    },
    {
      id: "lastActivityAt",
      label: "Last activity at",
      sortField: "lastActivityAt",
      width: 140,
      render: (lead) => (
        <span className="text-muted-foreground tabular-nums">
          {formatDateTimeFull(lead.lastActivityAt)}
        </span>
      ),
    },
    {
      id: "city",
      label: "City",
      sortField: "city",
      width: 96,
      defaultHidden: true,
      render: (lead) => <span className="text-muted-foreground">{lead.city ?? "—"}</span>,
    },
    {
      id: "branch",
      label: "Branch",
      sortField: "branchName",
      width: 118,
      defaultHidden: true,
      render: (lead) => (
        <span className="text-muted-foreground">{lead.branchName}</span>
      ),
    },
    {
      id: "tags",
      label: "Tags",
      width: 140,
      defaultHidden: true,
      render: (lead) =>
        lead.tags ? (
          <span className="flex flex-wrap gap-0.5">
            {lead.tags.split(",").map((tag) => (
              <Pill key={tag} tone="info">
                {tag}
              </Pill>
            ))}
          </span>
        ) : (
          <span className="text-muted-foreground">—</span>
        ),
    },
    {
      id: "created",
      label: "Created",
      sortField: "createdAt",
      width: 88,
      render: (lead) => (
        <span className="text-muted-foreground">{timeAgo(lead.createdAt)}</span>
      ),
    },
    {
      id: "createdAt",
      label: "Created at",
      sortField: "createdAt",
      width: 140,
      render: (lead) => (
        <span className="text-muted-foreground tabular-nums">
          {formatDateTimeFull(lead.createdAt)}
        </span>
      ),
    },
    {
      id: "actions",
      label: "",
      width: 40,
      fixedWidth: true,
      render: (lead) => (
        <DropdownMenu>
          <DropdownMenuTrigger asChild>
            <Button
              variant="ghost"
              size="icon"
              aria-label={`Actions for ${lead.name}`}
              className="size-6 text-muted-foreground"
              onClick={(event) => event.stopPropagation()}
            >
              <MoreHorizontal className="size-3.5" />
            </Button>
          </DropdownMenuTrigger>
          <DropdownMenuContent
            align="end"
            className="w-48"
            onClick={(event) => event.stopPropagation()}
          >
            <DropdownMenuItem onClick={() => router.push(`/dashboard/leads/${lead.id}`)}>
              Open record
            </DropdownMenuItem>
            <DropdownMenuItem
              disabled={lead.isConverted}
              onClick={() => convertOne(lead)}
            >
              <UserRoundCheck /> Convert
            </DropdownMenuItem>
            <DropdownMenuSeparator />
            <DropdownMenuItem
              variant="destructive"
              onClick={() => removeOne(lead)}
            >
              <Trash2 /> Delete
            </DropdownMenuItem>
          </DropdownMenuContent>
        </DropdownMenu>
      ),
    },
  ];

  /* ---------------- actions ---------------- */

  async function convertOne(lead: LeadRow) {
    try {
      const result = await leadsApi.convert(lead.id, {
        createOpportunity: false,
        createEvent: true,
      });
      toast.success(result.message, {
        description: `${result.contactName}${
          result.opportunityName ? ` · ${result.opportunityName}` : ""
        }`,
      });
      state.refresh();
    } catch (error) {
      toast.error("Could not convert this lead", {
        description: error instanceof ApiError ? error.message : "Network error.",
      });
    }
  }

  async function removeOne(lead: LeadRow) {
    try {
      await leadsApi.bulkDelete([lead.id]);
      toast.success(`${lead.name} deleted`);
      state.refresh();
    } catch (error) {
      toast.error("Could not delete this lead", {
        description: error instanceof ApiError ? error.message : "Network error.",
      });
    }
  }

  async function autoAssign() {
    setAssigning(true);
    try {
      const result = await intelligenceApi.autoAssign();
      toast.success(result.message, {
        description: result.assignments
          .slice(0, 3)
          .map((a) => `${a.leadName} → ${a.ownerName}`)
          .join(" · "),
      });
      state.refresh();
    } catch (error) {
      toast.error("Auto-assignment failed", {
        description: error instanceof ApiError ? error.message : "Network error.",
      });
    } finally {
      setAssigning(false);
    }
  }

  async function bulkStage(ids: number[], stage: string) {
    try {
      const result = await leadsApi.bulkStage(ids, stage);
      toast.success(result.message);
      state.clearSelection();
      state.refresh();
    } catch (error) {
      toast.error("Bulk update failed", {
        description: error instanceof ApiError ? error.message : "Network error.",
      });
    }
  }

  async function bulkAssign(ids: number[], ownerId: number | null) {
    try {
      const result = await leadsApi.bulkAssign(ids, ownerId);
      toast.success(result.message);
      state.clearSelection();
      state.refresh();
    } catch (error) {
      toast.error("Bulk assignment failed", {
        description: error instanceof ApiError ? error.message : "Network error.",
      });
    }
  }

  return (
    <ListShell
      icon={Waypoints}
      title="Leads"
      storageKey="leads"
      hint="Every enquiry captured across websites, portals, channel partners and walk-ins. Filtering, sorting and paging all run in the database, so the counts above describe the whole result set — not the visible page."
      state={state}
      columns={columns}
      metrics={metrics}
      visuals={visuals}
      quickViews={quickViews}
      searchPlaceholder="Name, mobile, email, company, notes…"
      onRowClick={(lead) => router.push(`/dashboard/leads/${lead.id}`)}
      actions={
        <>
          <LeadFormDialog branches={branches} users={users} onSaved={() => state.refresh()} />
          <Button
            variant="outline"
            size="sm"
            className="h-8"
            disabled={assigning}
            onClick={autoAssign}
          >
            <Sparkles /> Auto-assign
          </Button>
          <Button variant="outline" size="sm" className="h-8">
            <Copy /> Duplicates
          </Button>
          <Button
            variant="ghost"
            size="icon"
            aria-label="Import leads"
            title="Import leads"
            className="size-8 text-muted-foreground"
          >
            <Upload className="size-4" />
          </Button>
          <Button
            variant="ghost"
            size="icon"
            aria-label="Export leads"
            title="Export leads"
            className="size-8 text-muted-foreground"
          >
            <Download className="size-4" />
          </Button>
          {engine ? (
            <span
              className="inline-flex h-8 items-center gap-1.5 rounded border px-2 text-[12px]"
              title={`Scored locally by ${engine}`}
            >
              <Brain className="size-3.5 text-primary" />
              {engine.startsWith("MLNet") ? "ML model" : "Heuristic"}
            </span>
          ) : null}
        </>
      }
      bulkActions={(ids) => (
        <>
          <DropdownMenu>
            <DropdownMenuTrigger asChild>
              <Button variant="outline" size="sm" className="h-6 text-[11.5px]">
                <UserRoundCheck className="size-3" /> Assign
              </Button>
            </DropdownMenuTrigger>
            <DropdownMenuContent align="start" className="max-h-72 w-52 overflow-y-auto">
              <DropdownMenuLabel className="text-[11px] tracking-wide text-muted-foreground uppercase">
                Assign to
              </DropdownMenuLabel>
              <DropdownMenuItem onClick={() => bulkAssign(ids, null)}>
                Unassign
              </DropdownMenuItem>
              <DropdownMenuSeparator />
              {users.map((member) => (
                <DropdownMenuItem
                  key={member.id}
                  onClick={() => bulkAssign(ids, member.id)}
                >
                  {member.name}
                </DropdownMenuItem>
              ))}
            </DropdownMenuContent>
          </DropdownMenu>

          <DropdownMenu>
            <DropdownMenuTrigger asChild>
              <Button variant="outline" size="sm" className="h-6 text-[11.5px]">
                Move stage
              </Button>
            </DropdownMenuTrigger>
            <DropdownMenuContent align="start" className="w-44">
              {["New", "Contacted", "Qualified", "ObmVisit", "SiteVisit", "FollowUp", "Negotiation", "Booked", "Lost"].map(
                (stage) => (
                  <DropdownMenuItem key={stage} onClick={() => bulkStage(ids, stage)}>
                    <StatusDot label={humanise(stage)} tone={leadStageTone(stage)} />
                  </DropdownMenuItem>
                )
              )}
            </DropdownMenuContent>
          </DropdownMenu>

          <Button
            variant="outline"
            size="sm"
            className="h-6 text-[11.5px] text-destructive"
            onClick={async () => {
              try {
                const result = await leadsApi.bulkDelete(ids);
                toast.success(result.message);
                state.clearSelection();
                state.refresh();
              } catch (error) {
                toast.error("Bulk delete failed", {
                  description:
                    error instanceof ApiError ? error.message : "Network error.",
                });
              }
            }}
          >
            <Trash2 className="size-3" /> Delete
          </Button>
        </>
      )}
    />
  );
}
