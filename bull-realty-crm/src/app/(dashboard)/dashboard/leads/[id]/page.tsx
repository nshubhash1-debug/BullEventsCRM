"use client";

import * as React from "react";
import Link from "next/link";
import { useParams, useRouter } from "next/navigation";
import {
  ArrowLeft,
  ChevronDown,
  Copy,
  Mail,
  Pencil,
  Phone,
  Trash2,
  UserRoundCheck,
  Waypoints,
  ClipboardList,
} from "lucide-react";
import { toast } from "sonner";

import { LeadActivityPanel } from "@/components/dashboard/lead-activity-panel";
import { LeadFormDialog } from "@/components/dashboard/lead-form-dialog";
import { LeadPipelineStepper } from "@/components/dashboard/lead-pipeline-stepper";
import {
  StatusMenu,
  type StatusChange,
  type StatusFamily,
} from "@/components/crm/status-menu";
import {
  HighlightsPanel,
  RecordFieldGrid,
  RecordSection,
  type RecordField,
} from "@/components/dashboard/record-layout";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
import { Skeleton } from "@/components/ui/skeleton";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import {
  ApiError,
  EVENT_CATEGORIES,
  EVENT_SLOTS,
  EVENT_TYPES,
  getBranches,
  getUsers,
  LEAD_PRIORITIES,
  LEAD_SOURCES,
  LEAD_STAGES,
  MEAL_PREFERENCES,
  PAYMENT_PREFERENCES,
  SALUTATIONS,
  type Branch,
  type UserListItem,
} from "@/lib/api";
import {
  followUpsApi,
  formatDateTime,
  formatMoney,
  humanise,
  leadsApi,
  obmVisitsApi,
  siteVisitsApi,
  timeAgo,
  type FieldChange,
  type LeadActivityRow,
  type LeadRelated,
  type LeadRow,
  type RelatedRecord,
} from "@/lib/crm-api";
import { getStageStyle } from "@/lib/lead-stage-styles";
import { cn } from "@/lib/utils";

const priorityStyle: Record<string, string> = {
  Low: "border-zinc-400/30 bg-zinc-400/10 text-zinc-600 dark:text-zinc-400",
  Medium: "border-blue-500/25 bg-blue-500/10 text-blue-700 dark:text-blue-400",
  High: "border-orange-500/25 bg-orange-500/10 text-orange-700 dark:text-orange-400",
  Hot: "border-red-500/25 bg-red-500/10 text-red-700 dark:text-red-400",
};

const slaStyle: Record<string, string> = {
  Breached: "border-red-500/25 bg-red-500/10 text-red-700 dark:text-red-400",
  AtRisk: "border-amber-500/25 bg-amber-500/10 text-amber-700 dark:text-amber-400",
  OnTrack: "border-sky-500/25 bg-sky-500/10 text-sky-700 dark:text-sky-400",
  Met: "border-emerald-500/25 bg-emerald-500/10 text-emerald-700 dark:text-emerald-400",
};

const SLA_LABEL: Record<string, string> = {
  Breached: "SLA breached",
  AtRisk: "SLA at risk",
  OnTrack: "SLA on track",
  Met: "Responded",
};

/** Option lists for the select-type fields. */
const choices = (values: readonly string[]) =>
  values.map((value) => ({ label: humanise(value), value }));

function formatDate(iso: string | null | undefined) {
  if (!iso) return null;
  return new Date(iso).toLocaleDateString(undefined, {
    day: "numeric",
    month: "short",
    year: "numeric",
  });
}

/**
 * The event date with its lead time attached — "14 Feb 2027 · in 172 days".
 *
 * The countdown is the half a planner reads: the date alone says when, the
 * countdown says how hard to push, and having to work it out from a calendar
 * is what stops the field being looked at.
 */
function eventDateLabel(iso: string | null, days: number | null) {
  const date = formatDate(iso);
  if (!date) return null;
  if (days === null) return date;

  if (days < 0) return `${date} · ${Math.abs(days)} days ago`;
  if (days === 0) return `${date} · today`;
  if (days === 1) return `${date} · tomorrow`;

  return `${date} · in ${days} days`;
}

/** "Venue,Catering,Decor" → "Venue · Catering · Décor". */
function listLabel(value: string | null) {
  if (!value) return null;

  return value
    .split(",")
    .map((item) => humanise(item.trim()))
    .filter(Boolean)
    .join(" · ");
}

function stageLabel(stage: string) {
  return LEAD_STAGES.find((s) => s.value === stage)?.label ?? stage;
}

/* ------------------------------------------------------------------ *
 * Related list — one block per attached object
 * ------------------------------------------------------------------ */

/**
 * Kinds whose status is a schedulable state the rep can move from here. The
 * rest — quotations, calls, deals — have their own lifecycles and are read-only
 * on this tab.
 */
const STATUS_FAMILY: Record<string, StatusFamily> = {
  SiteVisit: "visit",
  ObmVisit: "visit",
  FollowUp: "followUp",
};

function RelatedBlock({
  title,
  records,
  emptyMessage,
  onStatusChange,
}: {
  title: string;
  records: RelatedRecord[];
  emptyMessage: string;
  /** Omitted for blocks whose records are not schedulable. */
  onStatusChange?: (record: RelatedRecord, change: StatusChange) => Promise<void>;
}) {
  return (
    <RecordSection title={`${title} (${records.length})`}>
      {records.length === 0 ? (
        <p className="py-2 text-[12.5px] text-muted-foreground">{emptyMessage}</p>
      ) : (
        <ul className="flex flex-col">
          {records.map((record) => (
            <li
              key={`${record.kind}-${record.id}`}
              className="flex items-center gap-3 border-b py-2 text-[13px] last:border-b-0"
            >
              <span className="min-w-0 flex-1">
                <span className="block truncate font-medium">{record.title}</span>
                {record.subtitle ? (
                  <span className="block truncate text-[11.5px] text-muted-foreground">
                    {record.subtitle}
                  </span>
                ) : null}
              </span>

              {record.amount ? (
                <span className="shrink-0 tabular-nums">
                  {formatMoney(record.amount)}
                </span>
              ) : null}

              {record.status ? (
                onStatusChange && STATUS_FAMILY[record.kind] ? (
                  <span className="shrink-0">
                    <StatusMenu
                      family={STATUS_FAMILY[record.kind]}
                      status={record.status}
                      scheduledAt={record.at}
                      onChange={(change) => onStatusChange(record, change)}
                    />
                  </span>
                ) : (
                  <span className="shrink-0 text-[11.5px] text-muted-foreground">
                    {humanise(record.status)}
                  </span>
                )
              ) : null}

              <span className="w-16 shrink-0 text-right text-[11.5px] text-muted-foreground">
                {timeAgo(record.at)}
              </span>
            </li>
          ))}
        </ul>
      )}
    </RecordSection>
  );
}

/* ------------------------------------------------------------------ *
 * Page
 * ------------------------------------------------------------------ */

export default function LeadDetailPage() {
  const params = useParams<{ id: string }>();
  const router = useRouter();
  const leadId = Number(params.id);

  const [lead, setLead] = React.useState<LeadRow | null | undefined>(undefined);
  const [activities, setActivities] = React.useState<LeadActivityRow[]>([]);
  const [activitiesLoading, setActivitiesLoading] = React.useState(true);
  const [related, setRelated] = React.useState<LeadRelated | null>(null);
  const [history, setHistory] = React.useState<FieldChange[]>([]);
  const [branches, setBranches] = React.useState<Branch[]>([]);
  const [users, setUsers] = React.useState<UserListItem[]>([]);
  const [saving, setSaving] = React.useState(false);

  const loadActivities = React.useCallback(() => {
    leadsApi
      .activities(leadId)
      .then(setActivities)
      .catch(() => setActivities([]))
      .finally(() => setActivitiesLoading(false));
  }, [leadId]);

  const loadRelated = React.useCallback(() => {
    leadsApi.related(leadId).then(setRelated).catch(() => setRelated(null));
    leadsApi.history(leadId).then(setHistory).catch(() => setHistory([]));
  }, [leadId]);

  React.useEffect(() => {
    if (!Number.isFinite(leadId)) return;

    leadsApi.one(leadId).then(setLead).catch(() => setLead(null));
    loadActivities();
    loadRelated();

    getBranches().then(setBranches).catch(() => setBranches([]));
    getUsers().then(setUsers).catch(() => setUsers([]));
  }, [leadId, loadActivities, loadRelated]);

  /* ---------------- writes ---------------- */

  /**
   * Moving a linked record from here goes through the same endpoints the lists
   * use, so the rule about what a status change means lives on the server
   * rather than being reimplemented per screen.
   *
   * Everything is reloaded afterwards, not just the related tab: the API writes
   * the change onto the lead's timeline and stamps its last activity, so the
   * header and the activity feed are stale too.
   */
  async function changeRelatedStatus(record: RelatedRecord, change: StatusChange) {
    try {
      if (record.kind === "SiteVisit") {
        await siteVisitsApi.setStatus(record.id, {
          status: change.status,
          scheduledAt: change.at,
          reason: change.note,
        });
      } else if (record.kind === "ObmVisit") {
        await obmVisitsApi.setStatus(record.id, {
          status: change.status,
          scheduledAt: change.at,
          reason: change.note,
        });
      } else if (record.kind === "FollowUp") {
        await followUpsApi.setStatus(record.id, {
          status: change.status,
          dueAt: change.at,
          outcome: change.note,
        });
      } else {
        return;
      }

      toast.success(`${record.title} updated`, {
        description: change.at
          ? `Moved to ${formatDateTime(change.at)}.`
          : `Marked ${humanise(change.status).toLowerCase()}.`,
      });

      leadsApi.one(leadId).then(setLead).catch(() => {});
      loadActivities();
      loadRelated();
    } catch (error) {
      toast.error("Could not update this record", {
        description: error instanceof ApiError ? error.message : "Network error.",
      });
    }
  }

  /**
   * Saves one field. The audit interceptor on the API records what changed, so
   * the field history at the bottom of Related fills itself in — but it has to
   * be refetched rather than patched locally.
   */
  async function saveField(field: RecordField, value: string | null) {
    if (!field.name) return;

    try {
      const updated = await leadsApi.patchField(leadId, field.name, value);
      setLead(updated);
      leadsApi.history(leadId).then(setHistory).catch(() => {});
      toast.success(`${field.label} updated`);
    } catch (error) {
      toast.error(`Could not update ${field.label.toLowerCase()}`, {
        description: error instanceof ApiError ? error.message : "Network error.",
      });
      throw error;
    }
  }

  async function moveStage(stage: string) {
    if (!lead || stage === lead.stage) return;

    setSaving(true);
    try {
      await leadsApi.bulkStage([lead.id], stage);
      const refreshed = await leadsApi.one(lead.id);
      setLead(refreshed);
      loadActivities();
      toast.success(`Moved to ${stageLabel(stage)}`);
    } catch (error) {
      toast.error("Could not update stage", {
        description: error instanceof ApiError ? error.message : "Network error.",
      });
    } finally {
      setSaving(false);
    }
  }

  async function convert() {
    if (!lead) return;

    try {
      const result = await leadsApi.convert(lead.id, {
        createOpportunity: false,
        createEvent: true,
      });
      toast.success(result.message, {
        description: [result.contactName, result.bookingNumber, result.opportunityName]
          .filter(Boolean)
          .join(" · "),
      });
      leadsApi.one(leadId).then(setLead).catch(() => {});
      loadActivities();
      loadRelated();
    } catch (error) {
      toast.error("Could not convert this lead", {
        description: error instanceof ApiError ? error.message : "Network error.",
      });
    }
  }

  async function sendQuestionnaire() {
    if (!lead) return;
    try {
      const updated = await leadsApi.sendQuestionnaire(lead.id);
      setLead(updated);
      loadActivities();
      const url = `${window.location.origin}/questionnaire/${updated.questionnaireToken}`;
      await navigator.clipboard.writeText(url);
      toast.success("Questionnaire sent", { description: "Link copied to clipboard." });
    } catch (error) {
      toast.error("Could not send the questionnaire", {
        description: error instanceof ApiError ? error.message : "Network error.",
      });
    }
  }

  async function remove() {
    try {
      await leadsApi.bulkDelete([leadId]);
      toast.success("Lead deleted");
      router.push("/dashboard/leads");
    } catch (error) {
      toast.error("Could not delete this lead", {
        description: error instanceof ApiError ? error.message : "Network error.",
      });
    }
  }

  /* ---------------- states ---------------- */

  if (lead === undefined) {
    return (
      <div className="flex flex-col gap-3">
        <Skeleton className="h-28 w-full" />
        <Skeleton className="h-14 w-full" />
        <div className="grid grid-cols-1 gap-3 lg:grid-cols-5">
          <Skeleton className="h-96 w-full lg:col-span-3" />
          <Skeleton className="h-96 w-full lg:col-span-2" />
        </div>
      </div>
    );
  }

  if (lead === null) {
    return (
      <div className="flex flex-col items-center gap-3 rounded border bg-card py-20 text-center">
        <p className="text-sm text-muted-foreground">
          This lead doesn&apos;t exist or you don&apos;t have access to it.
        </p>
        <Button variant="outline" asChild>
          <Link href="/dashboard/leads">
            <ArrowLeft /> Back to leads
          </Link>
        </Button>
      </div>
    );
  }

  const stage = getStageStyle(lead.stage);
  const fullName = `${lead.salutation ? `${lead.salutation} ` : ""}${lead.name}`;

  function refreshAfterSave() {
    leadsApi.one(leadId).then(setLead).catch(() => {});
    loadActivities();
  }

  return (
    <div className="flex flex-col gap-3">
      <HighlightsPanel
        icon={Waypoints}
        objectLabel="Lead"
        recordName={fullName}
        breadcrumb={
          <Link
            href="/dashboard/leads"
            className="inline-flex items-center gap-1 text-primary hover:underline"
          >
            <ArrowLeft className="size-3" /> Leads
          </Link>
        }
        subtitle={
          <span className="flex flex-wrap items-center gap-2">
            {lead.companyName ? <span>{lead.companyName}</span> : null}
            <Badge
              variant="outline"
              className={cn(
                "h-5 px-1.5 text-[11px] font-normal",
                priorityStyle[lead.priority]
              )}
            >
              {lead.priority} priority
            </Badge>
            {lead.slaState !== "None" ? (
              <Badge
                variant="outline"
                className={cn(
                  "h-5 px-1.5 text-[11px] font-normal",
                  slaStyle[lead.slaState]
                )}
              >
                {SLA_LABEL[lead.slaState]}
              </Badge>
            ) : null}
            {lead.isConverted ? (
              <Badge
                variant="outline"
                className="h-5 border-emerald-500/25 bg-emerald-500/10 px-1.5 text-[11px] font-normal text-emerald-700 dark:text-emerald-400"
              >
                Converted
              </Badge>
            ) : null}
            <span className="font-mono text-[11px] text-muted-foreground">
              #{lead.id}
            </span>
          </span>
        }
        fields={[
          {
            label: "Lead status",
            value: (
              <span className="inline-flex items-center gap-1.5">
                <span className={cn("size-1.5 rounded-full", stage.dot)} />
                {stageLabel(lead.stage)}
              </span>
            ),
          },
          { label: "Mobile", value: lead.phone ?? "—" },
          { label: "Email", value: lead.email ?? "—" },
          { label: "Lead owner", value: lead.ownerName ?? "Unassigned" },
          { label: "Branch", value: lead.branchName },
        ]}
        actions={
          <>
            <Button variant="outline" size="sm" className="h-8" asChild>
              <a href={lead.phone ? `tel:${lead.phone}` : "#"}>
                <Phone /> Call
              </a>
            </Button>
            <Button variant="outline" size="sm" className="h-8" asChild>
              <a href={lead.email ? `mailto:${lead.email}` : "#"}>
                <Mail /> Email
              </a>
            </Button>
            <LeadFormDialog
              lead={lead}
              branches={branches}
              users={users}
              onSaved={refreshAfterSave}
              trigger={
                <Button size="sm" className="h-8">
                  <Pencil /> Edit
                </Button>
              }
            />
            <DropdownMenu>
              <DropdownMenuTrigger asChild>
                <Button
                  variant="outline"
                  size="icon"
                  aria-label="More actions"
                  className="size-8"
                >
                  <ChevronDown className="size-4" />
                </Button>
              </DropdownMenuTrigger>
              <DropdownMenuContent align="end" className="w-48">
                <DropdownMenuItem disabled={lead.isConverted} onClick={convert}>
                  <UserRoundCheck /> Book as client
                </DropdownMenuItem>
                <DropdownMenuItem onClick={() => void sendQuestionnaire()}>
                  <ClipboardList /> Send questionnaire
                </DropdownMenuItem>
                <DropdownMenuItem
                  onClick={() => {
                    navigator.clipboard.writeText(
                      [lead.name, lead.phone, lead.email].filter(Boolean).join(" · ")
                    );
                    toast.success("Contact details copied");
                  }}
                >
                  <Copy /> Copy contact
                </DropdownMenuItem>
                <DropdownMenuItem variant="destructive" onClick={remove}>
                  <Trash2 /> Delete
                </DropdownMenuItem>
              </DropdownMenuContent>
            </DropdownMenu>
          </>
        }
      />

      <LeadPipelineStepper
        stage={lead.stage}
        onSelect={moveStage}
        disabled={saving}
      />

      <div className="grid min-w-0 grid-cols-1 gap-3 lg:grid-cols-5">
        <div className="min-w-0 lg:col-span-3">
          <Tabs defaultValue="details" className="gap-0">
            <div className="rounded-t-lg border border-b-0 bg-card px-2 pt-1.5 shadow-sm">
              <TabsList className="h-9 bg-transparent p-0">
                <TabsTrigger value="details" className="text-[13px]">
                  Details
                </TabsTrigger>
                <TabsTrigger value="preferences" className="text-[13px]">
                  Preferences
                </TabsTrigger>
                <TabsTrigger value="related" className="text-[13px]">
                  Related
                </TabsTrigger>
              </TabsList>
            </div>

            <div className="rounded-b-lg border bg-card p-4 shadow-sm">
              <TabsContent value="details" className="mt-0 flex flex-col gap-4">
                <RecordSection title="Customer information">
                  <RecordFieldGrid
                    onEditField={saveField}
                    fields={[
                      {
                        label: "Salutation",
                        name: "salutation",
                        value: lead.salutation,
                        raw: lead.salutation,
                        type: "select",
                        options: choices(SALUTATIONS),
                        editable: true,
                      },
                      {
                        label: "Full name",
                        name: "name",
                        value: lead.name,
                        raw: lead.name,
                        editable: true,
                      },
                      {
                        label: "Company",
                        name: "companyName",
                        value: lead.companyName,
                        raw: lead.companyName,
                        editable: true,
                      },
                      {
                        label: "Designation",
                        name: "designation",
                        value: lead.designation,
                        raw: lead.designation,
                        editable: true,
                      },
                      {
                        label: "Occupation",
                        name: "occupation",
                        value: lead.occupation,
                        raw: lead.occupation,
                        type: "select",
                        options: choices([
                          "Salaried",
                          "Self-employed",
                          "Business owner",
                          "Professional",
                          "Retired",
                          "NRI professional",
                        ]),
                        editable: true,
                      },
                      {
                        label: "Date of birth",
                        name: "dateOfBirth",
                        value: formatDate(lead.dateOfBirth),
                        raw: lead.dateOfBirth,
                        type: "date",
                        editable: true,
                      },
                      {
                        label: "Marital status",
                        name: "maritalStatus",
                        value: lead.maritalStatus,
                        raw: lead.maritalStatus,
                        type: "select",
                        options: choices(["Married", "Single", "Divorced"]),
                        editable: true,
                      },
                      {
                        label: "Father / spouse name",
                        name: "fatherOrSpouseName",
                        value: lead.fatherOrSpouseName,
                        raw: lead.fatherOrSpouseName,
                        editable: true,
                      },
                      {
                        label: "Anniversary",
                        name: "anniversaryDate",
                        value: formatDate(lead.anniversaryDate),
                        raw: lead.anniversaryDate,
                        type: "date",
                        editable: true,
                      },
                      {
                        label: "Nationality",
                        name: "nationality",
                        value: lead.nationality,
                        raw: lead.nationality,
                        type: "select",
                        options: choices(["Indian", "NRI", "OCI", "Foreign national"]),
                        editable: true,
                      },
                    ]}
                  />
                </RecordSection>

                <RecordSection title="Contact information">
                  <RecordFieldGrid
                    onEditField={saveField}
                    fields={[
                      {
                        label: "Mobile",
                        name: "phone",
                        value: lead.phone,
                        raw: lead.phone,
                        editable: true,
                      },
                      {
                        label: "Alternate mobile",
                        name: "phone2",
                        value: lead.phone2,
                        raw: lead.phone2,
                        editable: true,
                      },
                      {
                        label: "Email",
                        name: "email",
                        value: lead.email,
                        raw: lead.email,
                        editable: true,
                      },
                      {
                        label: "Address",
                        name: "address",
                        value: lead.address,
                        raw: lead.address,
                        type: "textarea",
                        full: true,
                        editable: true,
                      },
                      {
                        label: "City",
                        name: "city",
                        value: lead.city,
                        raw: lead.city,
                        editable: true,
                      },
                      {
                        label: "State",
                        name: "state",
                        value: lead.state,
                        raw: lead.state,
                        editable: true,
                      },
                      {
                        label: "Pincode",
                        name: "pincode",
                        value: lead.pincode,
                        raw: lead.pincode,
                        editable: true,
                      },
                      {
                        label: "Zone",
                        name: "zone",
                        value: lead.zone,
                        raw: lead.zone,
                        editable: true,
                      },
                      {
                        label: "Country",
                        name: "country",
                        value: lead.country,
                        raw: lead.country,
                        editable: true,
                      },
                    ]}
                  />
                </RecordSection>

                <RecordSection title="Lead information">
                  <RecordFieldGrid
                    onEditField={saveField}
                    fields={[
                      { label: "Lead status", value: stageLabel(lead.stage) },
                      {
                        label: "Sub-status",
                        name: "subStatus",
                        value: lead.subStatus,
                        raw: lead.subStatus,
                        editable: true,
                      },
                      {
                        label: "Lead source",
                        name: "source",
                        value: humanise(lead.source),
                        raw: lead.source,
                        type: "select",
                        options: LEAD_SOURCES.map((s) => ({
                          label: s.label,
                          value: s.value,
                        })),
                        editable: true,
                      },
                      {
                        label: "Priority",
                        name: "priority",
                        value: lead.priority,
                        raw: lead.priority,
                        type: "select",
                        options: LEAD_PRIORITIES.map((p) => ({
                          label: p.label,
                          value: p.value,
                        })),
                        editable: true,
                      },
                      { label: "Lead owner", value: lead.ownerName ?? "Unassigned" },
                      { label: "Branch", value: lead.branchName },
                      {
                        label: "SLA",
                        value:
                          lead.slaState === "None" ? null : (
                            <span className="flex flex-wrap items-center gap-1.5">
                              <Badge
                                variant="outline"
                                className={cn(
                                  "h-5 px-1.5 text-[11px] font-normal",
                                  slaStyle[lead.slaState]
                                )}
                              >
                                {SLA_LABEL[lead.slaState]}
                              </Badge>
                              <span className="text-[11.5px] text-muted-foreground">
                                {lead.firstResponseAt
                                  ? `responded ${timeAgo(lead.firstResponseAt)}`
                                  : `due ${timeAgo(lead.slaDueAt)}`}
                              </span>
                            </span>
                          ),
                      },
                      {
                        label: "AI score",
                        value:
                          lead.score === null ? null : (
                            <span className="flex items-center gap-2">
                              <span className="font-medium tabular-nums">
                                {lead.score}
                              </span>
                              <span className="h-1.5 w-16 overflow-hidden rounded-full bg-muted">
                                <span
                                  className="block h-full rounded-full bg-primary"
                                  style={{ width: `${lead.score}%` }}
                                />
                              </span>
                              <span className="text-[11.5px] text-muted-foreground">
                                {lead.band}
                              </span>
                            </span>
                          ),
                      },
                      {
                        label: "Remarks",
                        name: "notes",
                        value: lead.notes,
                        raw: lead.notes,
                        type: "textarea",
                        full: true,
                        editable: true,
                      },
                    ]}
                  />
                </RecordSection>

                <RecordSection title="System information">
                  <RecordFieldGrid
                    fields={[
                      { label: "Record ID", value: `#${lead.id}` },
                      { label: "Created date", value: formatDate(lead.createdAt) },
                      { label: "Last modified", value: formatDate(lead.updatedAt) },
                      { label: "Last activity", value: timeAgo(lead.lastActivityAt) },
                      { label: "Activities logged", value: lead.activityCount },
                      {
                        label: "Converted",
                        value: lead.isConverted ? (
                          <span className="flex flex-wrap items-center gap-2">
                            <span>{timeAgo(lead.convertedAt)}</span>
                            {lead.convertedContactId ? (
                              <Link
                                href="/dashboard/leads/contacts"
                                className="text-[12px] text-primary hover:underline"
                              >
                                Contact #{lead.convertedContactId}
                              </Link>
                            ) : null}
                            {lead.convertedOpportunityId ? (
                              <Link
                                href="/dashboard/leads/opportunities"
                                className="text-[12px] text-primary hover:underline"
                              >
                                Opportunity #{lead.convertedOpportunityId}
                              </Link>
                            ) : null}
                          </span>
                        ) : (
                          "Not converted"
                        ),
                      },
                    ]}
                  />
                </RecordSection>
              </TabsContent>

              <TabsContent value="preferences" className="mt-0 flex flex-col gap-4">
                <RecordSection title="The event">
                  <RecordFieldGrid
                    onEditField={saveField}
                    fields={[
                      {
                        label: "Occasion",
                        name: "eventType",
                        value: humanise(lead.eventType),
                        raw: lead.eventType,
                        type: "select",
                        options: EVENT_TYPES.map((t) => ({
                          label: t.label,
                          value: t.value,
                        })),
                        editable: true,
                      },
                      {
                        label: "Category",
                        name: "eventCategory",
                        value: humanise(lead.eventCategory),
                        raw: lead.eventCategory,
                        type: "select",
                        options: EVENT_CATEGORIES.map((c) => ({
                          label: c.label,
                          value: c.value,
                        })),
                        editable: true,
                      },
                      {
                        label: "Event date",
                        name: "eventDate",
                        value: eventDateLabel(lead.eventDate, lead.daysToEvent),
                        raw: lead.eventDate?.slice(0, 10),
                        type: "date",
                        editable: true,
                      },
                      {
                        label: "Ends on",
                        name: "eventEndDate",
                        value: formatDate(lead.eventEndDate),
                        raw: lead.eventEndDate?.slice(0, 10),
                        type: "date",
                        editable: true,
                      },
                      {
                        label: "Slot",
                        name: "eventSlot",
                        value: humanise(lead.eventSlot),
                        raw: lead.eventSlot,
                        type: "select",
                        options: EVENT_SLOTS.map((s) => ({
                          label: s.label,
                          value: s.value,
                        })),
                        editable: true,
                      },
                      {
                        label: "Date flexible",
                        name: "isDateFlexible",
                        value: lead.isDateFlexible ? "Yes" : "No",
                        raw: String(lead.isDateFlexible),
                        type: "select",
                        options: [
                          { label: "Yes", value: "true" },
                          { label: "No", value: "false" },
                        ],
                        editable: true,
                      },
                      {
                        label: "Guest count",
                        name: "guestCount",
                        value: lead.guestCount
                          ? `${lead.guestCount.toLocaleString("en-IN")} guests`
                          : null,
                        raw: lead.guestCount,
                        type: "number",
                        editable: true,
                      },
                      {
                        label: "Functions",
                        name: "functions",
                        value: listLabel(lead.functions),
                        raw: lead.functions,
                        editable: true,
                      },
                      {
                        label: "Services needed",
                        name: "servicesNeeded",
                        value: listLabel(lead.servicesNeeded),
                        raw: lead.servicesNeeded,
                        editable: true,
                      },
                      {
                        label: "Meal preference",
                        name: "mealPreference",
                        value: humanise(lead.mealPreference),
                        raw: lead.mealPreference,
                        type: "select",
                        options: MEAL_PREFERENCES.map((m) => ({
                          label: m.label,
                          value: m.value,
                        })),
                        editable: true,
                      },
                      {
                        label: "Budget from",
                        name: "budgetMin",
                        value: lead.budgetMin ? formatMoney(lead.budgetMin) : null,
                        raw: lead.budgetMin,
                        type: "number",
                        editable: true,
                      },
                      {
                        label: "Budget to",
                        name: "budgetMax",
                        value: lead.budgetMax ? formatMoney(lead.budgetMax) : null,
                        raw: lead.budgetMax,
                        type: "number",
                        editable: true,
                      },
                      {
                        label: "Payment mode",
                        name: "paymentMode",
                        value: humanise(lead.paymentMode),
                        raw: lead.paymentMode,
                        type: "select",
                        options: PAYMENT_PREFERENCES.map((p) => ({
                          label: p.label,
                          value: p.value,
                        })),
                        editable: true,
                      },
                      {
                        label: "Preferred location",
                        name: "preferredLocality",
                        value: lead.preferredLocality,
                        raw: lead.preferredLocality,
                        editable: true,
                      },
                      {
                        label: "Preferred venue",
                        value: lead.interestedProjectName,
                      },
                    ]}
                  />
                </RecordSection>

                <RecordSection title="Source and attribution">
                  <RecordFieldGrid
                    onEditField={saveField}
                    fields={[
                      {
                        label: "Campaign",
                        name: "campaign",
                        value: lead.campaign,
                        raw: lead.campaign,
                        editable: true,
                      },
                      {
                        label: "UTM source",
                        name: "utmSource",
                        value: lead.utmSource,
                        raw: lead.utmSource,
                        editable: true,
                      },
                      {
                        label: "UTM medium",
                        name: "utmMedium",
                        value: lead.utmMedium,
                        raw: lead.utmMedium,
                        editable: true,
                      },
                      {
                        label: "Referred by",
                        name: "referredBy",
                        value: lead.referredBy,
                        raw: lead.referredBy,
                        editable: true,
                      },
                      {
                        label: "Tags",
                        name: "tags",
                        value: lead.tags ? (
                          <span className="flex flex-wrap gap-1">
                            {lead.tags.split(",").map((tag) => (
                              <Badge
                                key={tag}
                                variant="outline"
                                className="h-5 px-1.5 text-[11px] font-normal"
                              >
                                {tag.trim()}
                              </Badge>
                            ))}
                          </span>
                        ) : null,
                        raw: lead.tags,
                        full: true,
                        editable: true,
                      },
                    ]}
                  />
                </RecordSection>
              </TabsContent>

              <TabsContent value="related" className="mt-0 flex flex-col gap-4">
                {related === null ? (
                  <Skeleton className="h-40 w-full" />
                ) : (
                  <>
                    <RelatedBlock
                      title="Calls"
                      records={related.calls}
                      emptyMessage="No calls logged against this lead."
                    />
                    <RelatedBlock
                      title="Venue visits"
                      records={related.siteVisits}
                      emptyMessage="No venue visits scheduled."
                      onStatusChange={changeRelatedStatus}
                    />
                    <RelatedBlock
                      title="OBM meetings"
                      records={related.obmVisits}
                      emptyMessage="No outdoor meetings held for this lead."
                      onStatusChange={changeRelatedStatus}
                    />
                    <RelatedBlock
                      title="Follow-ups"
                      records={related.followUps}
                      emptyMessage="Nothing in the follow-up queue."
                      onStatusChange={changeRelatedStatus}
                    />
                    <RelatedBlock
                      title="Quotations"
                      records={related.quotations}
                      emptyMessage="No quotation issued yet."
                    />
                    <RelatedBlock
                      title="Opportunities"
                      records={related.opportunities}
                      emptyMessage="Convert this lead to open a deal."
                    />

                    <RecordSection title={`Field history (${history.length})`}>
                      {history.length === 0 ? (
                        <p className="py-2 text-[12.5px] text-muted-foreground">
                          Nothing has been edited since this lead was created.
                        </p>
                      ) : (
                        <ul className="flex flex-col">
                          {history.slice(0, 40).map((change, index) => (
                            <li
                              key={index}
                              className="flex items-baseline gap-3 border-b py-2 text-[12.5px] last:border-b-0"
                            >
                              <span className="w-24 shrink-0 text-muted-foreground">
                                {timeAgo(change.at)}
                              </span>
                              <span className="w-32 shrink-0 truncate font-medium">
                                {change.field}
                              </span>
                              <span className="min-w-0 flex-1 truncate text-muted-foreground">
                                <span className="line-through">
                                  {change.from ?? "—"}
                                </span>{" "}
                                →{" "}
                                <span className="text-foreground">
                                  {change.to ?? "—"}
                                </span>
                              </span>
                              <span className="shrink-0 text-muted-foreground">
                                {change.userName}
                              </span>
                            </li>
                          ))}
                        </ul>
                      )}
                    </RecordSection>
                  </>
                )}
              </TabsContent>
            </div>
          </Tabs>
        </div>

        <div className="min-w-0 lg:col-span-2">
          <LeadActivityPanel
            lead={lead}
            users={users}
            activities={activities}
            loading={activitiesLoading}
            onChanged={({ lead: updated, related: relatedChanged }) => {
              if (updated) setLead(updated);
              else leadsApi.one(leadId).then(setLead).catch(() => {});
              loadActivities();
              if (relatedChanged) loadRelated();
            }}
          />
        </div>
      </div>
    </div>
  );
}
