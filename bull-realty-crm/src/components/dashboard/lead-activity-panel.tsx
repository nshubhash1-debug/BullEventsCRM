"use client";

import * as React from "react";
import {
  ArrowRightLeft,
  CalendarClock,
  CheckSquare,
  Loader2,
  Mail,
  MapPinned,
  MessageCircle,
  Phone,
  Plus,
  StickyNote,
  UserCog,
  UserRoundCheck,
} from "lucide-react";
import { toast } from "sonner";

import {
  EmailDialog,
  ObmVisitDialog,
  SiteVisitDialog,
  TaskDialog,
} from "@/components/crm/lead-dialogs";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Textarea } from "@/components/ui/textarea";
import { ApiError, LEAD_STAGES, type UserListItem } from "@/lib/api";
import {
  callsApi,
  leadsApi,
  type LeadActivityRow,
  type LeadRow,
} from "@/lib/crm-api";
import { cn } from "@/lib/utils";

const actionButtons = [
  { type: "Note", label: "Note", icon: StickyNote, tint: "text-amber-600 dark:text-amber-400" },
  { type: "Call", label: "Call", icon: Phone, tint: "text-emerald-600 dark:text-emerald-400" },
  { type: "Email", label: "Email", icon: Mail, tint: "text-blue-600 dark:text-blue-400" },
  { type: "WhatsApp", label: "WhatsApp", icon: MessageCircle, tint: "text-green-600 dark:text-green-400" },
  { type: "SiteVisit", label: "Venue visit", icon: CalendarClock, tint: "text-rose-600 dark:text-rose-400" },
  { type: "ObmVisit", label: "Client meeting", icon: MapPinned, tint: "text-cyan-600 dark:text-cyan-400" },
  { type: "Task", label: "Task", icon: CheckSquare, tint: "text-violet-600 dark:text-violet-400" },
  { type: "Transfer", label: "Transfer", icon: UserCog, tint: "text-orange-600 dark:text-orange-400" },
  { type: "Convert", label: "Convert", icon: UserRoundCheck, tint: "text-primary" },
] as const;

type ActionType = (typeof actionButtons)[number]["type"];

/**
 * The four that open a full window.
 *
 * Scheduling needs a slot grid and a dozen fields; an email needs a subject,
 * templates and room to write. Those never fit the inline strip, so they get a
 * dialog — and the quick ones (note, call, WhatsApp, transfer, convert) keep the
 * inline composer, where a dialog would only be in the way.
 */
const DIALOG_ACTIONS = new Set<ActionType>(["Email", "SiteVisit", "ObmVisit", "Task"]);

type InlineType = Exclude<ActionType, "Email" | "SiteVisit" | "ObmVisit" | "Task">;

const typeMeta: Record<
  string,
  { icon: React.ComponentType<{ className?: string }>; tint: string; bg: string }
> = {
  Created: { icon: Plus, tint: "text-primary", bg: "bg-primary/10" },
  Note: { icon: StickyNote, tint: "text-amber-600 dark:text-amber-400", bg: "bg-amber-500/10" },
  Call: { icon: Phone, tint: "text-emerald-600 dark:text-emerald-400", bg: "bg-emerald-500/10" },
  Email: { icon: Mail, tint: "text-blue-600 dark:text-blue-400", bg: "bg-blue-500/10" },
  WhatsApp: { icon: MessageCircle, tint: "text-green-600 dark:text-green-400", bg: "bg-green-500/10" },
  SiteVisit: { icon: CalendarClock, tint: "text-rose-600 dark:text-rose-400", bg: "bg-rose-500/10" },
  Task: { icon: CheckSquare, tint: "text-violet-600 dark:text-violet-400", bg: "bg-violet-500/10" },
  StageChange: { icon: ArrowRightLeft, tint: "text-sky-600 dark:text-sky-400", bg: "bg-sky-500/10" },
  OwnerChange: { icon: UserCog, tint: "text-orange-600 dark:text-orange-400", bg: "bg-orange-500/10" },
};

function stageLabel(value: string | null) {
  return LEAD_STAGES.find((s) => s.value === value)?.label ?? value ?? "—";
}

function formatDay(iso: string) {
  return new Date(iso).toLocaleDateString(undefined, {
    weekday: "long",
    month: "short",
    day: "numeric",
    year: "numeric",
  });
}

/** "Email" -> "an email", "ObmVisit" -> "an obm visit". */
function describeActivity(type: string) {
  const phrase = type.replace(/([a-z])([A-Z])/g, "$1 $2").toLowerCase();
  return `${/^[aeiou]/.test(phrase) ? "an" : "a"} ${phrase}`;
}

function formatTime(iso: string) {
  return new Date(iso).toLocaleTimeString(undefined, {
    hour: "numeric",
    minute: "2-digit",
  });
}

function Labelled({
  label,
  children,
}: {
  label: string;
  children: React.ReactNode;
}) {
  return (
    <label className="flex min-w-0 flex-col gap-1">
      <span className="text-[11px] font-medium text-muted-foreground">{label}</span>
      {children}
    </label>
  );
}

function Choice({
  value,
  onChange,
  options,
}: {
  value: string;
  onChange: (value: string) => void;
  options: readonly string[] | { label: string; value: string }[];
}) {
  const normalised = options.map((option) =>
    typeof option === "string" ? { label: option, value: option } : option
  );

  return (
    <select
      value={value}
      onChange={(event) => onChange(event.target.value)}
      className="h-8 rounded-md border bg-background px-2 text-[13px] outline-none focus-visible:ring-[3px] focus-visible:ring-ring/50"
    >
      {normalised.map((option) => (
        <option key={option.value} value={option.value}>
          {option.label}
        </option>
      ))}
    </select>
  );
}

export function LeadActivityPanel({
  lead,
  users,
  activities,
  loading,
  onChanged,
}: {
  lead: LeadRow;
  users: UserListItem[];
  activities: LeadActivityRow[];
  loading: boolean;
  onChanged: (changed: { lead?: LeadRow; related?: boolean }) => void;
}) {
  const [composerType, setComposerType] = React.useState<InlineType | null>(null);
  const [dialog, setDialog] = React.useState<ActionType | null>(null);
  const [remarks, setRemarks] = React.useState("");
  const [submitting, setSubmitting] = React.useState(false);

  // ---- call ----
  const [callOutcome, setCallOutcome] = React.useState("Connected");
  const [callDisposition, setCallDisposition] = React.useState("Interested");
  const [callMinutes, setCallMinutes] = React.useState("3");

  // ---- transfer ----
  const [transferTo, setTransferTo] = React.useState("");

  function close() {
    setComposerType(null);
    setRemarks("");
    setTransferTo("");
  }

  function pick(type: ActionType) {
    if (DIALOG_ACTIONS.has(type)) {
      close();
      setDialog(type);
      return;
    }

    setComposerType((current) =>
      current === type ? null : (type as InlineType)
    );
  }

  async function submit() {
    if (!composerType) return;

    setSubmitting(true);
    try {
      switch (composerType) {
        case "Note":
        case "WhatsApp": {
          await leadsApi.logActivity(lead.id, {
            type: composerType,
            remarks: remarks.trim() || undefined,
          });
          toast.success(`${composerType} logged`);
          onChanged({});
          break;
        }

        case "Call": {
          // Two writes on purpose: the call log is what the telephony
          // dashboards and the agent scorecard read, and the timeline entry is
          // what the next rep to open this lead actually sees.
          await callsApi.create({
            relatedType: "Lead",
            relatedId: lead.id,
            relatedName: lead.name,
            direction: "Outbound",
            outcome: callOutcome,
            disposition: callOutcome === "Connected" ? callDisposition : null,
            phoneNumber: lead.phone,
            startedAt: new Date().toISOString(),
            durationSeconds:
              callOutcome === "Connected"
                ? Math.max(0, Math.round(Number(callMinutes) * 60))
                : 0,
            notes: remarks.trim() || null,
            branchId: lead.branchId,
          });
          toast.success("Call logged", {
            description:
              callOutcome === "Connected"
                ? `${callMinutes} min · ${callDisposition}`
                : callOutcome,
          });
          onChanged({ related: true });
          break;
        }

        case "Transfer": {
          if (!transferTo) {
            toast.error("Pick who should own this lead");
            return;
          }
          const updated = await leadsApi.transfer(
            lead.id,
            Number(transferTo),
            remarks.trim() || undefined
          );
          toast.success(`Transferred to ${updated.ownerName}`);
          onChanged({ lead: updated });
          break;
        }

        case "Convert": {
          const result = await leadsApi.convert(lead.id, {
            createOpportunity: false,
            createEvent: true,
          });
          toast.success(result.message, {
            description: [result.contactName, result.opportunityName]
              .filter(Boolean)
              .join(" · "),
          });
          onChanged({ related: true });
          break;
        }
      }

      close();
    } catch (error) {
      toast.error("Could not save that", {
        description: error instanceof ApiError ? error.message : "Network error.",
      });
    } finally {
      setSubmitting(false);
    }
  }

  const groups = React.useMemo(() => {
    const byDay = new Map<string, LeadActivityRow[]>();
    for (const activity of activities) {
      const key = formatDay(activity.createdAt);
      byDay.set(key, [...(byDay.get(key) ?? []), activity]);
    }
    return Array.from(byDay.entries());
  }, [activities]);

  const submitLabel: Record<InlineType, string> = {
    Note: "Log Note",
    Call: "Log Call",
    WhatsApp: "Log WhatsApp",
    Transfer: "Transfer lead",
    Convert: "Convert lead",
  };

  const dialogProps = {
    lead,
    users,
    onDone: onChanged,
  };

  return (
    <div className="flex flex-col gap-4 rounded-lg border bg-card p-4 shadow-sm">
      <div>
        <p className="mb-2 text-[11px] font-semibold tracking-wide text-muted-foreground uppercase">
          Log an activity
        </p>
        <div className="grid grid-cols-3 gap-2">
          {actionButtons.map((action) => (
            <button
              key={action.type}
              type="button"
              onClick={() => pick(action.type)}
              disabled={action.type === "Convert" && lead.isConverted}
              className={cn(
                "flex flex-col items-center gap-1 rounded-lg border p-2 text-[11px] font-medium transition-colors hover:bg-muted disabled:cursor-not-allowed disabled:opacity-40",
                composerType === action.type &&
                  "border-primary bg-primary/5 ring-1 ring-primary/30"
              )}
            >
              <action.icon className={cn("size-4", action.tint)} />
              {action.label}
            </button>
          ))}
        </div>

        {composerType ? (
          <div className="mt-3 flex flex-col gap-2 rounded-lg border bg-muted/30 p-3">
            {composerType === "Call" ? (
              <div className="grid grid-cols-2 gap-2">
                <Labelled label="Outcome">
                  <Choice
                    value={callOutcome}
                    onChange={setCallOutcome}
                    options={[
                      "Connected",
                      "NoAnswer",
                      "Busy",
                      "Voicemail",
                      "SwitchedOff",
                      "WrongNumber",
                    ]}
                  />
                </Labelled>
                {callOutcome === "Connected" ? (
                  <>
                    <Labelled label="Disposition">
                      <Choice
                        value={callDisposition}
                        onChange={setCallDisposition}
                        options={[
                          "Interested",
                          "CallBackLater",
                          "SiteVisitScheduled",
                          "NotInterested",
                          "BudgetMismatch",
                          "Converted",
                          "DoNotCall",
                        ]}
                      />
                    </Labelled>
                    <Labelled label="Minutes">
                      <Input
                        type="number"
                        min={0}
                        value={callMinutes}
                        onChange={(event) => setCallMinutes(event.target.value)}
                        className="h-8 bg-background text-[13px]"
                      />
                    </Labelled>
                  </>
                ) : null}
              </div>
            ) : null}

            {composerType === "Transfer" ? (
              <Labelled label="Transfer to">
                <Choice
                  value={transferTo}
                  onChange={setTransferTo}
                  options={[
                    { label: "Select an owner…", value: "" },
                    ...users
                      .filter((user) => user.id !== lead.ownerId)
                      .map((user) => ({
                        label: `${user.name} · ${user.role}`,
                        value: String(user.id),
                      })),
                  ]}
                />
              </Labelled>
            ) : null}

            {composerType === "Convert" ? (
              <p className="text-[12.5px] text-muted-foreground">
                Creates a contact and an opportunity from this lead, in one
                transaction. The lead stays on record, linked to both.
              </p>
            ) : (
              <Textarea
                autoFocus
                placeholder={
                  composerType === "Transfer"
                    ? "Why is this moving? The reason goes on the timeline."
                    : composerType === "Call"
                      ? "What was said — this is what the sentiment model reads."
                      : `Add a note about this ${composerType.toLowerCase()}…`
                }
                value={remarks}
                onChange={(event) => setRemarks(event.target.value)}
                className="min-h-16 resize-none bg-background"
              />
            )}

            <div className="flex items-center justify-between gap-2">
              <span className="text-[11px] text-muted-foreground">
                {composerType === "Call" ? "Also lands in the call log." : ""}
              </span>

              <div className="flex gap-2">
                <Button type="button" variant="ghost" size="sm" onClick={close}>
                  Cancel
                </Button>
                <Button
                  type="button"
                  size="sm"
                  onClick={submit}
                  disabled={submitting}
                >
                  {submitting ? <Loader2 className="animate-spin" /> : null}
                  {submitLabel[composerType]}
                </Button>
              </div>
            </div>
          </div>
        ) : null}
      </div>

      <div className="border-t pt-3">
        <p className="mb-3 text-[11px] font-semibold tracking-wide text-muted-foreground uppercase">
          Timeline
        </p>

        {loading ? (
          <p className="text-sm text-muted-foreground">Loading…</p>
        ) : groups.length === 0 ? (
          <p className="text-sm text-muted-foreground">No activity logged yet.</p>
        ) : (
          <div className="flex flex-col gap-5">
            {groups.map(([day, items]) => (
              <div key={day}>
                <p className="mb-2 text-xs font-medium text-muted-foreground">{day}</p>
                <div className="flex flex-col gap-3">
                  {items.map((activity) => {
                    const meta = typeMeta[activity.type] ?? typeMeta.Note;
                    return (
                      <div key={activity.id} className="flex gap-2.5">
                        <span
                          className={cn(
                            "mt-0.5 flex size-7 shrink-0 items-center justify-center rounded-full",
                            meta.bg
                          )}
                        >
                          <meta.icon className={cn("size-3.5", meta.tint)} />
                        </span>
                        <div className="min-w-0 flex-1 rounded-lg border bg-muted/20 px-3 py-2">
                          <div className="flex items-center justify-between gap-2">
                            <p className="text-[13px] font-medium">
                              {activity.actorName}
                              <span className="ml-1.5 font-normal text-muted-foreground">
                                {activity.type === "StageChange"
                                  ? `moved stage ${stageLabel(activity.fromStage)} → ${stageLabel(activity.toStage)}`
                                  : `logged ${describeActivity(activity.type)}`}
                              </span>
                            </p>
                            <span className="shrink-0 text-[11px] text-muted-foreground tabular-nums">
                              {formatTime(activity.createdAt)}
                            </span>
                          </div>
                          {activity.remarks ? (
                            <p className="mt-0.5 text-sm whitespace-pre-line text-muted-foreground">
                              {activity.remarks}
                            </p>
                          ) : null}
                        </div>
                      </div>
                    );
                  })}
                </div>
              </div>
            ))}
          </div>
        )}
      </div>

      {/* Mounted only while open so each one starts from a clean draft. */}
      {dialog === "Email" ? (
        <EmailDialog open onOpenChange={() => setDialog(null)} {...dialogProps} />
      ) : null}
      {dialog === "SiteVisit" ? (
        <SiteVisitDialog open onOpenChange={() => setDialog(null)} {...dialogProps} />
      ) : null}
      {dialog === "ObmVisit" ? (
        <ObmVisitDialog open onOpenChange={() => setDialog(null)} {...dialogProps} />
      ) : null}
      {dialog === "Task" ? (
        <TaskDialog open onOpenChange={() => setDialog(null)} {...dialogProps} />
      ) : null}
    </div>
  );
}
