"use client";

import * as React from "react";
import {
  CalendarClock,
  CheckSquare,
  Loader2,
  Mail,
  MapPinned,
  Paperclip,
  Plus,
  Repeat,
  Send,
} from "lucide-react";
import { toast } from "sonner";

import { IntegrationNotice } from "@/components/crm/integration-notice";
import { SlotPicker, type SlotSelection } from "@/components/crm/slot-picker";
import { Button } from "@/components/ui/button";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { Textarea } from "@/components/ui/textarea";
import { ApiError, type UserListItem } from "@/lib/api";
import {
  followUpsApi,
  formatMoney,
  humanise,
  inventoryApi,
  leadsApi,
  obmVisitsApi,
  siteVisitsApi,
  type LeadRow,
  type ProjectRow,
  type UnitRow,
} from "@/lib/crm-api";
import { useSession } from "@/components/dashboard/session-provider";
import { cn } from "@/lib/utils";

/* ------------------------------------------------------------------ *
 * Shell
 * ------------------------------------------------------------------ */

/**
 * The base `DialogContent` caps at `sm:max-w-sm` — 384px, which clipped every
 * one of these forms. Each shell states its own width at the `sm` breakpoint so
 * it actually wins, and caps height so a long form scrolls instead of running
 * off the screen.
 */
function DialogShell({
  title,
  description,
  icon: Icon,
  iconClass,
  width = "sm:max-w-3xl",
  children,
  footer,
  onOpenChange,
}: {
  title: string;
  description: React.ReactNode;
  icon: React.ComponentType<{ className?: string }>;
  iconClass?: string;
  width?: string;
  children: React.ReactNode;
  footer: React.ReactNode;
  onOpenChange: (open: boolean) => void;
}) {
  return (
    <DialogContent
      className={cn(
        // The base content is a grid, so the scroll body's `flex-1` would do
        // nothing and the footer would fall off the bottom of a long form.
        "flex max-h-[88vh] flex-col gap-0 overflow-hidden p-0",
        width
      )}
      onOpenAutoFocus={(event) => event.preventDefault()}
    >
      <DialogHeader className="shrink-0 border-b px-4 py-3 text-left">
        <DialogTitle className="flex items-center gap-2 text-[15px]">
          <Icon className={cn("size-4", iconClass)} />
          {title}
        </DialogTitle>
        <DialogDescription className="text-[12.5px]">
          {description}
        </DialogDescription>
      </DialogHeader>

      <div className="min-h-0 flex-1 overflow-y-auto px-4 py-3">{children}</div>

      <DialogFooter className="mx-0 mb-0 shrink-0 border-t px-4 py-3">
        <Button variant="ghost" onClick={() => onOpenChange(false)}>
          Cancel
        </Button>
        {footer}
      </DialogFooter>
    </DialogContent>
  );
}

const DURATIONS = [
  { label: "30 min", value: "30" },
  { label: "45 min", value: "45" },
  { label: "1 hour", value: "60" },
  { label: "1.5 hours", value: "90" },
  { label: "2 hours", value: "120" },
  { label: "3 hours", value: "180" },
];

function Field({
  label,
  children,
  className,
  hint,
}: {
  label: string;
  children: React.ReactNode;
  className?: string;
  hint?: string;
}) {
  return (
    <label className={cn("flex min-w-0 flex-col gap-1", className)}>
      <span className="flex items-baseline gap-1 text-[11px] font-medium text-muted-foreground">
        {label}
        {hint ? (
          <span className="font-normal text-muted-foreground/70">{hint}</span>
        ) : null}
      </span>
      {children}
    </label>
  );
}

function Choice({
  value,
  onChange,
  options,
  className,
}: {
  value: string;
  onChange: (value: string) => void;
  options: readonly string[] | { label: string; value: string }[];
  className?: string;
}) {
  const normalised = options.map((option) =>
    typeof option === "string" ? { label: option, value: option } : option
  );

  return (
    <select
      value={value}
      onChange={(event) => onChange(event.target.value)}
      className={cn(
        "h-8 w-full min-w-0 rounded-md border bg-background px-2 text-[13px] outline-none focus-visible:ring-[3px] focus-visible:ring-ring/50",
        className
      )}
    >
      {normalised.map((option) => (
        <option key={option.value} value={option.value}>
          {option.label}
        </option>
      ))}
    </select>
  );
}

function Toggle({
  checked,
  onChange,
  children,
}: {
  checked: boolean;
  onChange: (next: boolean) => void;
  children: React.ReactNode;
}) {
  return (
    <label className="flex cursor-pointer items-center gap-1.5 text-[12.5px]">
      <input
        type="checkbox"
        checked={checked}
        onChange={(event) => onChange(event.target.checked)}
        className="size-3.5 accent-[var(--primary)]"
      />
      {children}
    </label>
  );
}

/** A titled block inside a dialog — keeps long forms readable. */
function Section({
  title,
  children,
  className,
}: {
  title: string;
  children: React.ReactNode;
  className?: string;
}) {
  return (
    <section className={cn("flex flex-col gap-2", className)}>
      <h3 className="text-[11px] font-semibold tracking-wide text-muted-foreground uppercase">
        {title}
      </h3>
      {children}
    </section>
  );
}

interface DialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  lead: LeadRow;
  users: UserListItem[];
  onDone: (changed: { related?: boolean }) => void;
}

/** Wraps a submit handler with the busy flag and one error path. */
function useSubmit(onOpenChange: (open: boolean) => void) {
  const [busy, setBusy] = React.useState(false);

  const run = React.useCallback(
    async (work: () => Promise<boolean | void>) => {
      setBusy(true);
      try {
        const keepOpen = (await work()) === false;
        if (!keepOpen) onOpenChange(false);
      } catch (error) {
        toast.error("Could not save that", {
          description: error instanceof ApiError ? error.message : "Network error.",
        });
      } finally {
        setBusy(false);
      }
    },
    [onOpenChange]
  );

  return { busy, run };
}

/* ------------------------------------------------------------------ *
 * Email
 * ------------------------------------------------------------------ */

const EMAIL_TEMPLATES = [
  { label: "Write from scratch", value: "" },
  { label: "Project brochure", value: "brochure" },
  { label: "Price list and payment plan", value: "pricing" },
  { label: "Site visit confirmation", value: "visit" },
  { label: "Following up", value: "followup" },
  { label: "Thanks for visiting", value: "postvisit" },
];

/** Placeholders a rep can drop into the body; resolved when the mail is logged. */
const MERGE_FIELDS = [
  { label: "First name", token: "{{first_name}}" },
  { label: "Full name", token: "{{name}}" },
  { label: "Venue", token: "{{venue}}" },
  { label: "Occasion", token: "{{event}}" },
  { label: "Event date", token: "{{event_date}}" },
  { label: "Guest count", token: "{{guests}}" },
  { label: "Budget", token: "{{budget}}" },
  { label: "Owner", token: "{{owner}}" },
];

function resolveMergeFields(text: string, lead: LeadRow) {
  const eventDate = lead.eventDate
    ? new Date(lead.eventDate).toLocaleDateString(undefined, {
        day: "numeric",
        month: "long",
        year: "numeric",
      })
    : "your date";

  return text
    .replaceAll("{{first_name}}", lead.name.split(" ")[0])
    .replaceAll("{{name}}", lead.name)
    .replaceAll("{{venue}}", lead.interestedProjectName ?? "the venue")
    .replaceAll("{{project}}", lead.interestedProjectName ?? "the venue")
    .replaceAll("{{event}}", humanise(lead.eventType) ?? "your event")
    .replaceAll("{{event_date}}", eventDate)
    .replaceAll(
      "{{guests}}",
      lead.guestCount ? lead.guestCount.toLocaleString("en-IN") : "your guest count"
    )
    .replaceAll("{{budget}}", lead.budgetMax ? formatMoney(lead.budgetMax) : "your budget")
    .replaceAll("{{owner}}", lead.ownerName ?? "your event planner");
}

const TEMPLATE_BODIES: Record<string, string> = {
  brochure:
    "Dear {{first_name}},\n\nThank you for your enquiry about {{event}} at {{venue}}. I've attached our planning brochure covering packages, typical timelines and how we work with couples.\n\nHappy to walk you through it on a call whenever suits you.\n\nBest regards,\n{{owner}}",
  pricing:
    "Dear {{first_name}},\n\nAs discussed, here is the package range for {{event}} ({{guests}} guests) at {{venue}}.\n\nFigures are indicative until we issue a written proposal. Do let me know if you'd like that next.\n\nBest regards,\n{{owner}}",
  visit:
    "Dear {{first_name}},\n\nYour venue walkthrough is confirmed. We will meet you at {{venue}} and take you through the spaces, capacity and inclusions for {{event_date}}.\n\nPlease let me know if the timing needs to change.\n\nBest regards,\n{{owner}}",
  followup:
    "Dear {{first_name}},\n\nJust following up on our last conversation about {{event}} on {{event_date}}. Let me know if you'd like any more detail, or if you'd prefer to lock a consult.\n\nBest regards,\n{{owner}}",
  postvisit:
    "Dear {{first_name}},\n\nThank you for walking {{venue}} today. I hope the spaces gave you a good sense of how {{event}} will sit.\n\nI've noted your preferences and will send options that match {{budget}}. Do reach out with any questions in the meantime.\n\nBest regards,\n{{owner}}",
};

const TEMPLATE_SUBJECTS: Record<string, string> = {
  brochure: "{{event}} — planning brochure",
  pricing: "{{event}} — packages and pricing",
  visit: "Your venue walkthrough at {{venue}} is confirmed",
  followup: "Following up, {{first_name}}",
  postvisit: "Thank you for visiting {{venue}}",
};

export function EmailDialog({ open, onOpenChange, lead, onDone }: DialogProps) {
  const { busy, run } = useSubmit(onOpenChange);
  const { user } = useSession();

  const [to, setTo] = React.useState(lead.email ?? "");
  const [cc, setCc] = React.useState("");
  const [bcc, setBcc] = React.useState("");
  const [showCc, setShowCc] = React.useState(false);
  const [subject, setSubject] = React.useState("");
  const [template, setTemplate] = React.useState("");
  const [body, setBody] = React.useState("");
  const [logFollowUp, setLogFollowUp] = React.useState(false);

  const bodyRef = React.useRef<HTMLTextAreaElement>(null);

  function applyTemplate(next: string) {
    setTemplate(next);
    if (!next) return;

    setBody(TEMPLATE_BODIES[next] ?? "");
    setSubject(TEMPLATE_SUBJECTS[next] ?? "");
  }

  /** Drops a merge token in at the caret rather than at the end. */
  function insertToken(token: string) {
    const textarea = bodyRef.current;
    if (!textarea) {
      setBody((current) => `${current}${token}`);
      return;
    }

    const start = textarea.selectionStart ?? body.length;
    const end = textarea.selectionEnd ?? body.length;

    setBody(`${body.slice(0, start)}${token}${body.slice(end)}`);

    // Put the caret after the inserted token once React has repainted.
    requestAnimationFrame(() => {
      textarea.focus();
      textarea.setSelectionRange(start + token.length, start + token.length);
    });
  }

  const resolvedSubject = resolveMergeFields(subject, lead);
  const resolvedBody = resolveMergeFields(body, lead);

  const submit = () =>
    run(async () => {
      await leadsApi.logActivity(lead.id, {
        type: "Email",
        remarks: [
          `To: ${to}`,
          cc ? `Cc: ${cc}` : null,
          bcc ? `Bcc: ${bcc}` : null,
          `Subject: ${resolvedSubject}`,
          "",
          resolvedBody,
        ]
          .filter((line) => line !== null)
          .join("\n"),
      });

      if (logFollowUp) {
        await followUpsApi.create({
          subject: `Chase reply: ${resolvedSubject || lead.name}`,
          relatedType: "Lead",
          relatedId: lead.id,
          relatedName: lead.name,
          channel: "Email",
          status: "Open",
          priority: lead.priority,
          dueAt: new Date(Date.now() + 3 * 86_400_000).toISOString(),
          slaMinutes: 1440,
          branchId: lead.branchId,
          ownerId: lead.ownerId,
        });
      }

      toast.success("Email recorded on the timeline", {
        description: logFollowUp ? "Chase-up scheduled in 3 days." : undefined,
      });
      onDone({ related: logFollowUp });
    });

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogShell
        icon={Mail}
        iconClass="text-blue-600 dark:text-blue-400"
        title={`New message — ${lead.name}`}
        description="Composed and recorded against this lead. Nothing sends until a delivery provider is connected."
        width="sm:max-w-3xl"
        onOpenChange={onOpenChange}
        footer={
          <Button onClick={submit} disabled={busy || !to.trim() || !body.trim()}>
            {busy ? <Loader2 className="animate-spin" /> : <Send />}
            Log email
          </Button>
        }
      >
        <div className="flex flex-col gap-3">
          <IntegrationNotice
            provider="Email delivery"
            showing="The message is logged on the timeline so the conversation stays on the record."
          />

          {/* ---- envelope ---- */}
          <div className="rounded-md border">
            <div className="flex items-center gap-2 border-b px-3 py-1.5 text-[12.5px]">
              <span className="w-12 shrink-0 text-muted-foreground">From</span>
              <span className="min-w-0 flex-1 truncate">
                {user.name}{" "}
                <span className="text-muted-foreground">&lt;{user.email}&gt;</span>
              </span>
            </div>

            <div className="flex items-center gap-2 border-b px-3 py-1">
              <span className="w-12 shrink-0 text-[12.5px] text-muted-foreground">
                To
              </span>
              <Input
                type="email"
                value={to}
                onChange={(event) => setTo(event.target.value)}
                placeholder="name@example.com"
                className="h-7 border-0 px-0 text-[12.5px] shadow-none focus-visible:ring-0"
              />
              {!showCc ? (
                <button
                  type="button"
                  onClick={() => setShowCc(true)}
                  className="shrink-0 text-[11.5px] text-primary hover:underline"
                >
                  Cc / Bcc
                </button>
              ) : null}
            </div>

            {showCc ? (
              <>
                <div className="flex items-center gap-2 border-b px-3 py-1">
                  <span className="w-12 shrink-0 text-[12.5px] text-muted-foreground">
                    Cc
                  </span>
                  <Input
                    type="email"
                    value={cc}
                    onChange={(event) => setCc(event.target.value)}
                    className="h-7 border-0 px-0 text-[12.5px] shadow-none focus-visible:ring-0"
                  />
                </div>
                <div className="flex items-center gap-2 border-b px-3 py-1">
                  <span className="w-12 shrink-0 text-[12.5px] text-muted-foreground">
                    Bcc
                  </span>
                  <Input
                    type="email"
                    value={bcc}
                    onChange={(event) => setBcc(event.target.value)}
                    className="h-7 border-0 px-0 text-[12.5px] shadow-none focus-visible:ring-0"
                  />
                </div>
              </>
            ) : null}

            <div className="flex items-center gap-2 px-3 py-1">
              <span className="w-12 shrink-0 text-[12.5px] text-muted-foreground">
                Subject
              </span>
              <Input
                value={subject}
                onChange={(event) => setSubject(event.target.value)}
                placeholder="Subject"
                className="h-7 border-0 px-0 text-[12.5px] font-medium shadow-none focus-visible:ring-0"
              />
            </div>
          </div>

          {/* ---- composer ---- */}
          <div className="flex flex-wrap items-center gap-2">
            <Choice
              value={template}
              onChange={applyTemplate}
              options={EMAIL_TEMPLATES}
              className="h-7 w-auto text-[12px]"
            />
            <span className="text-[11px] text-muted-foreground">Insert:</span>
            {MERGE_FIELDS.map((field) => (
              <button
                key={field.token}
                type="button"
                onClick={() => insertToken(field.token)}
                className="rounded border px-1.5 py-0.5 text-[11px] text-muted-foreground transition-colors hover:bg-muted hover:text-foreground"
              >
                {field.label}
              </button>
            ))}
          </div>

          <Textarea
            ref={bodyRef}
            value={body}
            onChange={(event) => setBody(event.target.value)}
            rows={12}
            placeholder="Write your message…"
            className="min-h-56 text-[13px] leading-relaxed"
          />

          {/* Merge tokens are only useful if the rep can see what they resolve to. */}
          {body.includes("{{") ? (
            <details className="rounded-md border bg-muted/30 px-3 py-2">
              <summary className="cursor-pointer text-[11.5px] font-medium text-muted-foreground">
                Preview with this lead&apos;s details
              </summary>
              <p className="mt-2 text-[12.5px] whitespace-pre-line">
                <span className="font-medium">{resolvedSubject}</span>
                {"\n\n"}
                {resolvedBody}
              </p>
            </details>
          ) : null}

          <div className="flex flex-wrap items-center justify-between gap-2">
            <span className="flex items-center gap-1.5 text-[11.5px] text-muted-foreground">
              <Paperclip className="size-3" />
              Attachments arrive with the document library.
            </span>
            <Toggle checked={logFollowUp} onChange={setLogFollowUp}>
              Chase a reply in 3 days
            </Toggle>
          </div>
        </div>
      </DialogShell>
    </Dialog>
  );
}

/* ------------------------------------------------------------------ *
 * Site visit
 * ------------------------------------------------------------------ */

export function SiteVisitDialog({
  open,
  onOpenChange,
  lead,
  users,
  onDone,
}: DialogProps) {
  const { busy, run } = useSubmit(onOpenChange);

  const [projects, setProjects] = React.useState<ProjectRow[]>([]);
  // Keyed by the project that produced them, so switching project shows an
  // empty list immediately without a setState inside the effect.
  const [loadedUnits, setLoadedUnits] = React.useState<{
    key: string;
    items: UnitRow[];
  } | null>(null);
  const [projectId, setProjectId] = React.useState("");
  const [unitId, setUnitId] = React.useState("");
  const [visitType, setVisitType] = React.useState("FirstVisit");
  const [partySize, setPartySize] = React.useState("2");
  const [attendees, setAttendees] = React.useState("");
  const [hostId, setHostId] = React.useState(String(lead.ownerId ?? ""));
  const [duration, setDuration] = React.useState("60");
  const [transport, setTransport] = React.useState("Own vehicle");
  const [pickup, setPickup] = React.useState("");
  const [meetingPoint, setMeetingPoint] = React.useState("");
  const [budget, setBudget] = React.useState(
    lead.budgetMax ? String(lead.budgetMax) : ""
  );
  const [notes, setNotes] = React.useState("");
  const [slot, setSlot] = React.useState<SlotSelection | null>(null);
  const [remind, setRemind] = React.useState(true);
  const [moveStage, setMoveStage] = React.useState(lead.stage === "New");

  React.useEffect(() => {
    if (!open) return;
    inventoryApi.projects().then(setProjects).catch(() => setProjects([]));
  }, [open]);

  const units = loadedUnits?.key === projectId ? loadedUnits.items : [];

  // Units are per project and there are hundreds, so they load only once a
  // project is chosen — and only the ones a customer could actually be shown.
  React.useEffect(() => {
    if (!projectId) return;

    let cancelled = false;

    inventoryApi
      .query({
        page: 1,
        pageSize: 200,
        sort: [{ field: "unitNumber", descending: false }],
        filter: {
          conjunction: "and",
          children: [
            { field: "projectId", operator: "equals", value: projectId },
            { field: "status", operator: "in", values: ["Available", "Held"] },
          ],
        },
      })
      .then((page) => {
        if (!cancelled) setLoadedUnits({ key: projectId, items: page.items });
      })
      .catch(() => {
        if (!cancelled) setLoadedUnits({ key: projectId, items: [] });
      });

    return () => {
      cancelled = true;
    };
  }, [projectId]);

  const selectedProject = projects.find((p) => String(p.id) === projectId);

  const submit = () =>
    run(async () => {
      if (!slot) {
        toast.error("Pick a time slot");
        return false;
      }

      const visit = await siteVisitsApi.create({
        leadId: lead.id,
        projectId: projectId ? Number(projectId) : null,
        unitId: unitId ? Number(unitId) : null,
        visitorName: lead.name,
        visitorPhone: lead.phone,
        partySize: Math.max(1, Number(partySize) || 1),
        visitType,
        status: "Scheduled",
        scheduledAt: slot.startsAt,
        durationMinutes: Number(duration),
        hostId: hostId ? Number(hostId) : null,
        transportMode: transport || null,
        pickupLocation: [pickup, meetingPoint].filter(Boolean).join(" → ") || null,
        budgetDiscussed: budget ? Number(budget) : null,
        nextAction: [attendees && `Attending: ${attendees}`, notes]
          .filter(Boolean)
          .join(". ") || null,
        branchId: lead.branchId,
        // The picker already warned; this carries the rep's decision to the
        // server, which otherwise refuses the double-booking.
        allowOverlap: slot.overlaps,
      });

      if (remind) {
        await followUpsApi.create({
          subject: `Confirm venue walkthrough ${visit.visitCode} with ${lead.name}`,
          relatedType: "Lead",
          relatedId: lead.id,
          relatedName: lead.name,
          channel: "Call",
          status: "Open",
          priority: lead.priority,
          dueAt: new Date(
            new Date(slot.startsAt).getTime() - 86_400_000
          ).toISOString(),
          slaMinutes: 480,
          branchId: lead.branchId,
          ownerId: hostId ? Number(hostId) : lead.ownerId,
        });
      }

      if (moveStage && lead.stage === "New") {
        await leadsApi.bulkStage([lead.id], "Contacted");
      }

      toast.success("Site visit scheduled", {
        description: `${visit.visitCode} · ${slot.label} · ${duration} min`,
      });
      onDone({ related: true });
    });

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogShell
        icon={CalendarClock}
        iconClass="text-rose-600 dark:text-rose-400"
        title="Schedule a venue visit"
        description={
          <>
            {lead.name}
            {lead.phone ? ` · ${lead.phone}` : ""}
            {lead.eventType ? ` · ${humanise(lead.eventType)}` : ""}
            {lead.guestCount ? ` · ${lead.guestCount.toLocaleString("en-IN")} guests` : ""}
          </>
        }
        width="sm:max-w-4xl"
        onOpenChange={onOpenChange}
        footer={
          <Button onClick={submit} disabled={busy || !slot}>
            {busy ? <Loader2 className="animate-spin" /> : null}
            {slot ? `Schedule for ${slot.label}` : "Pick a slot"}
          </Button>
        }
      >
        <div className="flex flex-col gap-4">
          <Section title="What they are seeing">
            <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
              <Field label="Project">
                <Choice
                  value={projectId}
                  onChange={(next) => {
                    setProjectId(next);
                    setUnitId("");
                  }}
                  options={[
                    { label: "Not decided yet", value: "" },
                    ...projects.map((project) => ({
                      label: `${project.name} · ${project.availableUnits} available`,
                      value: String(project.id),
                    })),
                  ]}
                />
              </Field>

              <Field
                label="Unit"
                hint={units.length > 0 ? `${units.length} sellable` : "optional"}
              >
                <Choice
                  value={unitId}
                  onChange={setUnitId}
                  options={[
                    {
                      label: projectId ? "Show a few options" : "Pick a project first",
                      value: "",
                    },
                    ...units.map((unit) => ({
                      label: `${unit.unitNumber} · ${unit.configuration} · ${formatMoney(unit.totalPrice)}`,
                      value: String(unit.id),
                    })),
                  ]}
                />
              </Field>

              <Field label="Visit type">
                <Choice
                  value={visitType}
                  onChange={setVisitType}
                  options={[
                    { label: "First visit", value: "FirstVisit" },
                    { label: "Repeat visit", value: "RepeatVisit" },
                    { label: "Closing visit", value: "ClosingVisit" },
                    { label: "Handover", value: "Handover" },
                  ]}
                />
              </Field>
            </div>

            {selectedProject ? (
              <p className="text-[11.5px] text-muted-foreground">
                {selectedProject.locality ? `${selectedProject.locality} · ` : ""}
                {selectedProject.priceMin
                  ? `${formatMoney(selectedProject.priceMin)}–${formatMoney(selectedProject.priceMax)}`
                  : ""}
                {selectedProject.possessionDate
                  ? ` · possession ${new Date(selectedProject.possessionDate).getFullYear()}`
                  : ""}
              </p>
            ) : null}
          </Section>

          <Section title="Who and how long">
            <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
              <Field label="Host">
                <Choice
                  value={hostId}
                  onChange={(next) => {
                    setHostId(next);
                    // The grid is per person, so changing host invalidates the pick.
                    setSlot(null);
                  }}
                  options={[
                    { label: "Unassigned", value: "" },
                    ...users.map((user) => ({
                      label: user.name,
                      value: String(user.id),
                    })),
                  ]}
                />
              </Field>

              <Field label="Duration">
                <Choice
                  value={duration}
                  onChange={(next) => {
                    setDuration(next);
                    setSlot(null);
                  }}
                  options={DURATIONS}
                />
              </Field>

              <Field label="Party size" hint="people">
                <Input
                  type="number"
                  min={1}
                  value={partySize}
                  onChange={(event) => setPartySize(event.target.value)}
                  className="h-8 text-[13px]"
                />
              </Field>

              <Field label="Who else is coming" hint="optional">
                <Input
                  value={attendees}
                  onChange={(event) => setAttendees(event.target.value)}
                  placeholder="Spouse, parents, architect…"
                  className="h-8 text-[13px]"
                />
              </Field>
            </div>
          </Section>

          <Section title="When">
            <div className="rounded-md border p-2.5">
              <SlotPicker
                userId={hostId ? Number(hostId) : null}
                durationMinutes={Number(duration)}
                value={slot}
                onChange={setSlot}
              />
            </div>
          </Section>

          <Section title="Logistics">
            <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
              <Field label="Transport">
                <Choice
                  value={transport}
                  onChange={setTransport}
                  options={[
                    "Own vehicle",
                    "Company cab",
                    "Pickup arranged",
                    "Cab reimbursed",
                  ]}
                />
              </Field>

              <Field label="Pickup from" hint="optional">
                <Input
                  value={pickup}
                  onChange={(event) => setPickup(event.target.value)}
                  placeholder="Home, office, station"
                  className="h-8 text-[13px]"
                />
              </Field>

              <Field label="Meeting point" hint="optional">
                <Input
                  value={meetingPoint}
                  onChange={(event) => setMeetingPoint(event.target.value)}
                  placeholder="Sales gallery, tower lobby"
                  className="h-8 text-[13px]"
                />
              </Field>

              <Field label="Budget to discuss" hint="₹">
                <Input
                  type="number"
                  min={0}
                  value={budget}
                  onChange={(event) => setBudget(event.target.value)}
                  className="h-8 text-[13px]"
                />
              </Field>
            </div>

            <Field label="Notes for the host">
              <Textarea
                value={notes}
                onChange={(event) => setNotes(event.target.value)}
                rows={2}
                placeholder="Preferences, objections raised so far, anything the host should know…"
                className="min-h-14 text-[13px]"
              />
            </Field>

            <div className="flex flex-wrap items-center gap-4">
              <Toggle checked={remind} onChange={setRemind}>
                Call to confirm a day before
              </Toggle>
              {lead.stage === "New" ? (
                <Toggle checked={moveStage} onChange={setMoveStage}>
                  Move the lead to Contacted
                </Toggle>
              ) : null}
            </div>
          </Section>
        </div>
      </DialogShell>
    </Dialog>
  );
}

/* ------------------------------------------------------------------ *
 * OBM visit
 * ------------------------------------------------------------------ */

const OBM_PURPOSES = [
  "Take the wedding brief",
  "Package and budget review",
  "Vendor shortlist",
  "Venue walkthrough planning",
  "Corporate event kickoff",
  "Destination recce",
  "Family decision meeting",
  "Recovering a quiet enquiry",
];

export function ObmVisitDialog({
  open,
  onOpenChange,
  lead,
  users,
  onDone,
}: DialogProps) {
  const { busy, run } = useSubmit(onOpenChange);

  const [partner, setPartner] = React.useState("");
  const [partnerType, setPartnerType] = React.useState("ChannelPartner");
  const [contactPerson, setContactPerson] = React.useState(lead.name);
  const [contactPhone, setContactPhone] = React.useState(lead.phone ?? "");
  const [agentId, setAgentId] = React.useState(String(lead.ownerId ?? ""));
  const [duration, setDuration] = React.useState("90");
  const [travelMode, setTravelMode] = React.useState("Own vehicle");
  const [city, setCity] = React.useState(lead.city ?? "");
  const [location, setLocation] = React.useState("");
  const [distance, setDistance] = React.useState("");
  const [expense, setExpense] = React.useState("");
  const [purpose, setPurpose] = React.useState("");
  const [expectedLeads, setExpectedLeads] = React.useState("");
  const [expectedValue, setExpectedValue] = React.useState("");
  const [notes, setNotes] = React.useState("");
  const [slot, setSlot] = React.useState<SlotSelection | null>(null);
  const [scheduleDebrief, setScheduleDebrief] = React.useState(true);

  // Distance is the input a rep actually knows; the fuel estimate follows from
  // it, so it is offered rather than demanded.
  const suggestedExpense = distance
    ? Math.round(Number(distance) * 2 * 12 + 150)
    : null;

  const submit = () =>
    run(async () => {
      if (!partner.trim()) {
        toast.error("Partner name is required");
        return false;
      }
      if (!slot) {
        toast.error("Pick a time slot");
        return false;
      }

      const visit = await obmVisitsApi.create({
        // Booked from the lead record, so it belongs to that lead — this is
        // what puts the meeting on the lead's OBM column and Related tab.
        leadId: lead.id,
        partnerName: partner.trim(),
        partnerType,
        contactPerson: contactPerson || null,
        contactPhone: contactPhone || null,
        status: "Scheduled",
        scheduledAt: slot.startsAt,
        durationMinutes: Number(duration),
        city: city || null,
        locationLabel: location || null,
        distanceKm: distance ? Number(distance) : null,
        expenseAmount: expense ? Number(expense) : null,
        purpose: purpose || `Meeting arranged through ${lead.name}`,
        meetingNotes: [
          travelMode && `Travelling by ${travelMode.toLowerCase()}`,
          expectedLeads && `Expecting ~${expectedLeads} leads`,
          notes,
        ]
          .filter(Boolean)
          .join(". ") || null,
        businessValue: expectedValue ? Number(expectedValue) : null,
        leadsGenerated: 0,
        agentId: agentId ? Number(agentId) : null,
        branchId: lead.branchId,
        allowOverlap: slot.overlaps,
      });

      if (scheduleDebrief) {
        await followUpsApi.create({
          subject: `Log outcome of ${visit.visitCode} — ${partner.trim()}`,
          relatedType: "Lead",
          relatedId: lead.id,
          relatedName: lead.name,
          channel: "Task",
          status: "Open",
          priority: "Medium",
          dueAt: new Date(
            new Date(slot.startsAt).getTime() + Number(duration) * 60_000 + 3_600_000
          ).toISOString(),
          slaMinutes: 1440,
          branchId: lead.branchId,
          ownerId: agentId ? Number(agentId) : lead.ownerId,
        });
      }

      toast.success("Field meeting scheduled", {
        description: `${visit.visitCode} · ${partner.trim()} · ${slot.label}`,
      });
      onDone({ related: true });
    });

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogShell
        icon={MapPinned}
        iconClass="text-cyan-600 dark:text-cyan-400"
        title="Schedule a field meeting"
        description="An outdoor business meeting — measured on leads generated and cost per lead, not on interest level."
        width="sm:max-w-4xl"
        onOpenChange={onOpenChange}
        footer={
          <Button onClick={submit} disabled={busy || !slot || !partner.trim()}>
            {busy ? <Loader2 className="animate-spin" /> : null}
            {slot ? `Schedule for ${slot.label}` : "Pick a slot"}
          </Button>
        }
      >
        <div className="flex flex-col gap-4">
          <Section title="Who you are meeting">
            <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
              <Field label="Partner" className="lg:col-span-2">
                <Input
                  autoFocus
                  value={partner}
                  onChange={(event) => setPartner(event.target.value)}
                  placeholder="Firm or organisation"
                  className="h-8 text-[13px]"
                />
              </Field>

              <Field label="Partner type">
                <Choice
                  value={partnerType}
                  onChange={setPartnerType}
                  options={[
                    { label: "Channel partner", value: "ChannelPartner" },
                    { label: "Builder", value: "Builder" },
                    { label: "Broker", value: "Broker" },
                    { label: "Corporate", value: "Corporate" },
                    { label: "Bank", value: "Bank" },
                    { label: "Society", value: "Society" },
                  ]}
                />
              </Field>

              <Field label="Purpose">
                <Choice
                  value={purpose}
                  onChange={setPurpose}
                  options={[
                    {
                      label: `Through ${lead.name}`,
                      value: "",
                    },
                    ...OBM_PURPOSES.map((item) => ({ label: item, value: item })),
                  ]}
                />
              </Field>

              <Field label="Contact person">
                <Input
                  value={contactPerson}
                  onChange={(event) => setContactPerson(event.target.value)}
                  className="h-8 text-[13px]"
                />
              </Field>

              <Field label="Contact phone">
                <Input
                  value={contactPhone}
                  onChange={(event) => setContactPhone(event.target.value)}
                  className="h-8 text-[13px]"
                />
              </Field>

              <Field label="Agent going">
                <Choice
                  value={agentId}
                  onChange={(next) => {
                    setAgentId(next);
                    setSlot(null);
                  }}
                  options={[
                    { label: "Unassigned", value: "" },
                    ...users.map((user) => ({
                      label: user.name,
                      value: String(user.id),
                    })),
                  ]}
                />
              </Field>

              <Field label="Duration" hint="incl. travel">
                <Choice
                  value={duration}
                  onChange={(next) => {
                    setDuration(next);
                    setSlot(null);
                  }}
                  options={DURATIONS}
                />
              </Field>
            </div>
          </Section>

          <Section title="When">
            <div className="rounded-md border p-2.5">
              <SlotPicker
                userId={agentId ? Number(agentId) : null}
                durationMinutes={Number(duration)}
                value={slot}
                onChange={setSlot}
              />
            </div>
          </Section>

          <Section title="Travel and cost">
            <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
              <Field label="City">
                <Input
                  value={city}
                  onChange={(event) => setCity(event.target.value)}
                  className="h-8 text-[13px]"
                />
              </Field>

              <Field label="Location" hint="area or landmark">
                <Input
                  value={location}
                  onChange={(event) => setLocation(event.target.value)}
                  className="h-8 text-[13px]"
                />
              </Field>

              <Field label="Travel mode">
                <Choice
                  value={travelMode}
                  onChange={setTravelMode}
                  options={["Own vehicle", "Company cab", "Public transport", "Walk"]}
                />
              </Field>

              <Field label="Round trip" hint="km">
                <Input
                  type="number"
                  min={0}
                  value={distance}
                  onChange={(event) => setDistance(event.target.value)}
                  className="h-8 text-[13px]"
                />
              </Field>
            </div>

            <div className="grid gap-3 sm:grid-cols-3">
              <Field label="Expense estimate" hint="₹">
                <div className="flex items-center gap-1.5">
                  <Input
                    type="number"
                    min={0}
                    value={expense}
                    onChange={(event) => setExpense(event.target.value)}
                    className="h-8 text-[13px]"
                  />
                  {suggestedExpense && !expense ? (
                    <button
                      type="button"
                      onClick={() => setExpense(String(suggestedExpense))}
                      className="shrink-0 rounded border px-1.5 py-1 text-[11px] whitespace-nowrap text-muted-foreground hover:bg-muted hover:text-foreground"
                    >
                      Use ₹{suggestedExpense}
                    </button>
                  ) : null}
                </div>
              </Field>

              <Field label="Leads expected">
                <Input
                  type="number"
                  min={0}
                  value={expectedLeads}
                  onChange={(event) => setExpectedLeads(event.target.value)}
                  className="h-8 text-[13px]"
                />
              </Field>

              <Field label="Business value" hint="₹, estimate">
                <Input
                  type="number"
                  min={0}
                  value={expectedValue}
                  onChange={(event) => setExpectedValue(event.target.value)}
                  className="h-8 text-[13px]"
                />
              </Field>
            </div>

            {expense && expectedLeads && Number(expectedLeads) > 0 ? (
              <p className="text-[11.5px] text-muted-foreground">
                Projected cost per lead:{" "}
                <strong className="text-foreground tabular-nums">
                  ₹{Math.round(Number(expense) / Number(expectedLeads))}
                </strong>
              </p>
            ) : null}
          </Section>

          <Section title="Agenda">
            <Textarea
              value={notes}
              onChange={(event) => setNotes(event.target.value)}
              rows={3}
              placeholder="What needs to come out of this meeting…"
              className="min-h-16 text-[13px]"
            />
            <Toggle checked={scheduleDebrief} onChange={setScheduleDebrief}>
              Remind me to log the outcome afterwards
            </Toggle>
          </Section>
        </div>
      </DialogShell>
    </Dialog>
  );
}

/* ------------------------------------------------------------------ *
 * Task
 * ------------------------------------------------------------------ */

/** Channels that occupy a calendar slot rather than just carrying a due date. */
const APPOINTMENT_CHANNELS = new Set(["Meeting", "SiteVisit"]);

const SLA_CHOICES = [
  { label: "4 hours", value: "240" },
  { label: "Same day (8h)", value: "480" },
  { label: "1 day", value: "1440" },
  { label: "2 days", value: "2880" },
  { label: "1 week", value: "10080" },
];

const REMINDERS = [
  { label: "No reminder", value: "" },
  { label: "15 minutes before", value: "15" },
  { label: "1 hour before", value: "60" },
  { label: "3 hours before", value: "180" },
  { label: "1 day before", value: "1440" },
];

const REPEATS = [
  { label: "Does not repeat", value: "0" },
  { label: "Daily for 3 days", value: "1:3" },
  { label: "Daily for 5 days", value: "1:5" },
  { label: "Weekly for 4 weeks", value: "7:4" },
  { label: "Fortnightly for 3", value: "14:3" },
];

/** Quick-fill subjects — most follow-ups are one of a handful of things. */
const TASK_PRESETS = [
  "Call back as agreed",
  "Send the qualification questionnaire",
  "Send the proposal",
  "Confirm the venue walkthrough",
  "Chase the signed contract",
  "Check on the family decision",
];

function defaultDue() {
  const when = new Date(Date.now() + 24 * 3_600_000);
  when.setMinutes(when.getMinutes() - when.getTimezoneOffset());
  return when.toISOString().slice(0, 16);
}

export function TaskDialog({ open, onOpenChange, lead, users, onDone }: DialogProps) {
  const { busy, run } = useSubmit(onOpenChange);

  const [subject, setSubject] = React.useState("");
  const [channel, setChannel] = React.useState("Call");
  const [priority, setPriority] = React.useState(lead.priority);
  const [ownerId, setOwnerId] = React.useState(String(lead.ownerId ?? ""));
  const [dueAt, setDueAt] = React.useState(defaultDue);
  const [reminder, setReminder] = React.useState("60");
  const [sla, setSla] = React.useState("1440");
  const [repeat, setRepeat] = React.useState("0");
  const [description, setDescription] = React.useState("");
  const [slot, setSlot] = React.useState<SlotSelection | null>(null);

  const needsSlot = APPOINTMENT_CHANNELS.has(channel);
  const [repeatEvery, repeatCount] = repeat.split(":").map(Number);
  const occurrences = repeat === "0" ? 1 : repeatCount;

  const submit = () =>
    run(async () => {
      if (needsSlot && !slot) {
        toast.error("Pick a time slot");
        return false;
      }

      const firstDue = needsSlot && slot ? slot.startsAt : new Date(dueAt).toISOString();
      const resolvedSubject = subject.trim() || `Follow up with ${lead.name}`;

      // A repeat creates the whole run up front rather than a rule the queue has
      // to interpret — each occurrence is then an ordinary follow-up that can be
      // rescheduled or cancelled on its own.
      for (let index = 0; index < occurrences; index += 1) {
        const due = new Date(
          new Date(firstDue).getTime() + index * repeatEvery * 86_400_000
        ).toISOString();

        await followUpsApi.create({
          subject:
            occurrences > 1
              ? `${resolvedSubject} (${index + 1}/${occurrences})`
              : resolvedSubject,
          description: description || undefined,
          relatedType: "Lead",
          relatedId: lead.id,
          relatedName: lead.name,
          channel,
          status: "Open",
          priority,
          dueAt: due,
          reminderAt: reminder
            ? new Date(new Date(due).getTime() - Number(reminder) * 60_000).toISOString()
            : undefined,
          slaMinutes: Number(sla),
          branchId: lead.branchId,
          ownerId: ownerId ? Number(ownerId) : null,
        });
      }

      toast.success(
        occurrences > 1
          ? `${occurrences} follow-ups scheduled`
          : "Follow-up scheduled"
      );
      onDone({ related: true });
    });

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogShell
        icon={CheckSquare}
        iconClass="text-violet-600 dark:text-violet-400"
        title="Schedule a follow-up"
        description={`Lands in the follow-up queue against ${lead.name}. Overdue is computed from the due date, so it can never go stale.`}
        width="sm:max-w-3xl"
        onOpenChange={onOpenChange}
        footer={
          <Button onClick={submit} disabled={busy || (needsSlot && !slot)}>
            {busy ? <Loader2 className="animate-spin" /> : null}
            {occurrences > 1 ? `Schedule ${occurrences} follow-ups` : "Schedule follow-up"}
          </Button>
        }
      >
        <div className="flex flex-col gap-4">
          <Section title="What needs doing">
            <Field label="Subject">
              <Input
                autoFocus
                value={subject}
                onChange={(event) => setSubject(event.target.value)}
                placeholder={`Follow up with ${lead.name}`}
                className="h-8 text-[13px]"
              />
            </Field>

            <div className="flex flex-wrap gap-1">
              {TASK_PRESETS.map((preset) => (
                <button
                  key={preset}
                  type="button"
                  onClick={() => setSubject(preset)}
                  className={cn(
                    "inline-flex items-center gap-1 rounded border px-1.5 py-0.5 text-[11px] transition-colors",
                    subject === preset
                      ? "border-primary bg-primary/10 text-primary"
                      : "text-muted-foreground hover:bg-muted hover:text-foreground"
                  )}
                >
                  <Plus className="size-2.5" />
                  {preset}
                </button>
              ))}
            </div>

            <div className="grid gap-3 sm:grid-cols-3">
              <Field label="Channel">
                <Choice
                  value={channel}
                  onChange={(next) => {
                    setChannel(next);
                    setSlot(null);
                  }}
                  options={["Call", "Email", "WhatsApp", "Meeting", "SiteVisit", "Task"]}
                />
              </Field>
              <Field label="Priority">
                <Choice
                  value={priority}
                  onChange={setPriority}
                  options={["Low", "Medium", "High", "Hot"]}
                />
              </Field>
              <Field label="Owner">
                <Choice
                  value={ownerId}
                  onChange={(next) => {
                    setOwnerId(next);
                    setSlot(null);
                  }}
                  options={[
                    { label: "Unassigned", value: "" },
                    ...users.map((user) => ({
                      label: user.name,
                      value: String(user.id),
                    })),
                  ]}
                />
              </Field>
            </div>
          </Section>

          <Section title="When">
            {needsSlot ? (
              <>
                <p className="text-[11.5px] text-muted-foreground">
                  A {channel === "Meeting" ? "consult" : "venue walkthrough"} blocks the
                  owner&apos;s calendar, so it takes a slot rather than a due date.
                </p>
                <div className="rounded-md border p-2.5">
                  <SlotPicker
                    userId={ownerId ? Number(ownerId) : null}
                    durationMinutes={30}
                    value={slot}
                    onChange={setSlot}
                  />
                </div>
              </>
            ) : (
              <div className="grid gap-3 sm:grid-cols-3">
                <Field label="Due">
                  <Input
                    type="datetime-local"
                    value={dueAt}
                    onChange={(event) => setDueAt(event.target.value)}
                    className="h-8 text-[13px]"
                  />
                </Field>
                <Field label="Reminder">
                  <Choice value={reminder} onChange={setReminder} options={REMINDERS} />
                </Field>
                <Field label="SLA" hint="time to complete">
                  <Choice value={sla} onChange={setSla} options={SLA_CHOICES} />
                </Field>
              </div>
            )}

            {needsSlot ? (
              <div className="grid gap-3 sm:grid-cols-2">
                <Field label="Reminder">
                  <Choice value={reminder} onChange={setReminder} options={REMINDERS} />
                </Field>
                <Field label="SLA" hint="time to complete">
                  <Choice value={sla} onChange={setSla} options={SLA_CHOICES} />
                </Field>
              </div>
            ) : null}

            <Field label="Repeat">
              <Choice value={repeat} onChange={setRepeat} options={REPEATS} />
            </Field>

            {occurrences > 1 ? (
              <p className="flex items-center gap-1.5 text-[11.5px] text-muted-foreground">
                <Repeat className="size-3" />
                Creates {occurrences} separate follow-ups, {repeatEvery} day
                {repeatEvery === 1 ? "" : "s"} apart — each can be rescheduled or
                cancelled on its own.
              </p>
            ) : null}
          </Section>

          <Section title="Detail">
            <Textarea
              value={description}
              onChange={(event) => setDescription(event.target.value)}
              rows={3}
              placeholder="What needs doing, and anything the owner should know…"
              className="min-h-16 text-[13px]"
            />
          </Section>
        </div>
      </DialogShell>
    </Dialog>
  );
}
