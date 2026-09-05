"use client";

import * as React from "react";
import Link from "next/link";
import {
  Building2,
  Clock,
  ExternalLink,
  FileText,
  History,
  Loader2,
  Mail,
  Phone,
  ShieldAlert,
  User,
} from "lucide-react";
import { toast } from "sonner";

import { PdfButton } from "@/components/inventory/quote-builder";
import {
  NEEDS_APPROVAL,
  NEXT_STATUSES,
  SELLABLE,
  STATUS_STYLES,
  statusStyle,
} from "@/components/inventory/status";
import { Button } from "@/components/ui/button";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { ScrollArea } from "@/components/ui/scroll-area";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { Skeleton } from "@/components/ui/skeleton";
import { Textarea } from "@/components/ui/textarea";
import { ApiError } from "@/lib/api";
import { formatDate, formatDateTime, timeAgo } from "@/lib/crm-api";
import {
  boardApi,
  EVENT_SLOTS,
  formatPercent,
  formatRupees,
  type BoardUnit,
  type EventSlot,
  type SpaceEventDossier,
  type UnitDossier,
  type UnitParty,
  type UnitStatus,
} from "@/lib/inventory-api";
import { cn } from "@/lib/utils";

/**
 * Space dossier — capacities, policies, commercial floors, upcoming bookings.
 */
export function UnitSheet({
  unit,
  open,
  onOpenChange,
  onChanged,
  onQuote,
}: {
  unit: BoardUnit | null;
  open: boolean;
  onOpenChange: (open: boolean) => void;
  /** Refreshes the board after a move lands. */
  onChanged: () => void;
  onQuote: (unit: BoardUnit) => void;
}) {
  const [dossier, setDossier] = React.useState<UnitDossier | null>(null);
  const [eventInfo, setEventInfo] = React.useState<SpaceEventDossier | null>(null);

  // Seeded from the unit; the parent keys this component on the unit id, so a
  // different unit remounts it rather than being reset by an effect.
  const [target, setTarget] = React.useState<UnitStatus | "">("");
  const [reason, setReason] = React.useState("");
  const [customerName, setCustomerName] = React.useState(unit?.customerName ?? "");
  const [customerPhone, setCustomerPhone] = React.useState(unit?.customerPhone ?? "");
  const [holdHours, setHoldHours] = React.useState("24");
  const [eventDate, setEventDate] = React.useState(() => {
    const d = new Date();
    const m = String(d.getMonth() + 1).padStart(2, "0");
    const day = String(d.getDate()).padStart(2, "0");
    return `${d.getFullYear()}-${m}-${day}`;
  });
  const [eventSlot, setEventSlot] = React.useState<EventSlot>("Evening");
  const [saving, setSaving] = React.useState(false);

  const unitId = unit?.id;
  const [reloadToken, setReloadToken] = React.useState(0);

  React.useEffect(() => {
    if (!unitId || !open) return;

    let cancelled = false;

    boardApi
      .dossier(unitId)
      .then((loaded) => !cancelled && setDossier(loaded))
      .catch(() => !cancelled && setDossier(null));

    boardApi
      .eventDossier(unitId)
      .then((loaded) => !cancelled && setEventInfo(loaded))
      .catch(() => !cancelled && setEventInfo(null));

    return () => {
      cancelled = true;
    };
  }, [unitId, open, reloadToken]);

  if (!unit) return null;

  // The dossier carries the freshest copy; the board's tile seeds the header so
  // the window opens with content instead of a spinner.
  const current = dossier?.unit ?? unit;
  const style = statusStyle(current.status);
  const moves = NEXT_STATUSES[current.status] ?? [];
  const needsCustomer = target === "Booked";
  const willQueue = target !== "" && NEEDS_APPROVAL.includes(target);

  async function apply() {
    if (!unit || target === "") return;

    if (needsCustomer && !customerName.trim()) {
      toast.error("A booking needs the customer's name.");
      return;
    }

    setSaving(true);
    try {
      if (
        (target === "Held" || target === "Booked" || target === "Blackout") &&
        !eventDate
      ) {
        toast.error("Pick the event date this status applies to.");
        setSaving(false);
        return;
      }

      const result = await boardApi.setStatus(unit.id, {
        status: target,
        reason: reason.trim() || null,
        customerName: customerName.trim() || null,
        customerPhone: customerPhone.trim() || null,
        holdHours: target === "Held" ? Number(holdHours) || 24 : null,
        eventDate: eventDate || null,
        eventSlot,
      });

      if (result.applied) toast.success(result.message);
      else toast.warning(result.message, { duration: 6000 });

      setTarget("");
      setReason("");
      setReloadToken((token) => token + 1);
      onChanged();
    } catch (error) {
      toast.error("Could not update this space", {
        description: error instanceof ApiError ? error.message : "Network error.",
      });
    } finally {
      setSaving(false);
    }
  }

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="flex max-h-[93vh] flex-col gap-0 overflow-hidden p-0 sm:max-w-5xl">
        <DialogHeader className="border-b p-5 pb-4">
          <div className="flex flex-wrap items-start justify-between gap-3">
            <div className="min-w-0">
              <DialogTitle className="flex items-center gap-2.5 text-lg">
                {current.unitNumber}
                <span
                  className={cn(
                    "inline-flex items-center gap-1.5 rounded-full border px-2.5 py-0.5 text-[11.5px] font-medium",
                    style.chip
                  )}
                >
                  <span className={cn("size-1.5 rounded-full", style.dot)} />
                  {style.label}
                </span>
              </DialogTitle>

              <DialogDescription>
                {dossier?.projectName ?? "Venue"}
                {dossier?.towerName ? ` · ${dossier.towerName}` : ""} ·{" "}
                {current.configuration}
                {eventInfo
                  ? ` · ${eventInfo.seatingCapacity} seated / ${eventInfo.floatingCapacity} floating`
                  : ""}
              </DialogDescription>
            </div>

            {SELLABLE.includes(current.status) ? (
              <Button onClick={() => onQuote(current)}>
                <FileText className="size-4" /> Create quotation
              </Button>
            ) : null}
          </div>

          {dossier?.pendingApproval ? (
            <p className="mt-2 flex items-start gap-1.5 rounded border border-amber-500/40 bg-amber-500/10 px-2.5 py-1.5 text-[11.5px] text-amber-700 dark:text-amber-300">
              <ShieldAlert className="mt-px size-3.5 shrink-0" />
              <span>
                {dossier.pendingApproval.summary} — raised by{" "}
                {dossier.pendingApproval.requestedByName},{" "}
                {timeAgo(dossier.pendingApproval.requestedAt)}.
              </span>
            </p>
          ) : null}
        </DialogHeader>

        <ScrollArea className="flex-1">
          <div className="grid gap-5 p-5 lg:grid-cols-[minmax(0,1.15fr)_minmax(0,1fr)]">
            <div className="flex flex-col gap-4">
              <div className="grid gap-4 sm:grid-cols-2">
                <Section title="Capacity">
                  {eventInfo ? (
                    <>
                      <Row label="Seated" value={String(eventInfo.seatingCapacity)} strong />
                      <Row label="Floating" value={String(eventInfo.floatingCapacity)} />
                      {eventInfo.theatreCapacity != null ? (
                        <Row label="Theatre" value={String(eventInfo.theatreCapacity)} />
                      ) : null}
                      <Row label="Outdoor" value={eventInfo.isOutdoor ? "Yes" : "No"} />
                      <Row
                        label="Air conditioned"
                        value={eventInfo.isAirConditioned ? "Yes" : "No"}
                      />
                      <Row label="Stage / mandap" value={eventInfo.hasStage ? "Yes" : "No"} />
                      <Row
                        label="Attached kitchen"
                        value={eventInfo.hasAttachedKitchen ? "Yes" : "No"}
                      />
                    </>
                  ) : (
                    <Skeleton className="h-28 w-full" />
                  )}
                </Section>

                <Section title="Commercials">
                  {eventInfo ? (
                    <>
                      <Row
                        label="Venue rental"
                        value={formatRupees(eventInfo.basePrice)}
                        strong
                      />
                      <Row
                        label="Per plate"
                        value={
                          eventInfo.pricePerPlate > 0
                            ? formatRupees(eventInfo.pricePerPlate)
                            : "—"
                        }
                      />
                      <Row
                        label="Minimum plates"
                        value={
                          eventInfo.minimumPlates > 0
                            ? String(eventInfo.minimumPlates)
                            : "—"
                        }
                      />
                      <Row
                        label="Peak premium"
                        value={formatPercent(eventInfo.peakDatePremium)}
                      />
                      <Row
                        label="Security deposit"
                        value={formatRupees(eventInfo.securityDeposit)}
                      />
                      <Row
                        label="Turnaround"
                        value={
                          eventInfo.turnaroundHours > 0
                            ? `${eventInfo.turnaroundHours}h`
                            : "None"
                        }
                      />
                    </>
                  ) : (
                    <Skeleton className="h-28 w-full" />
                  )}
                </Section>
              </div>

              <Section title="Venue policies">
                {eventInfo ? (
                  <>
                    <Row
                      label="Outside catering"
                      value={eventInfo.allowsOutsideCatering ? "Allowed" : "In-house only"}
                    />
                    <Row
                      label="Alcohol"
                      value={eventInfo.allowsAlcohol ? "Permitted" : "Not permitted"}
                    />
                    <Row
                      label="Open flame"
                      value={eventInfo.allowsOpenFlame ? "Permitted" : "Not permitted"}
                    />
                    <Row
                      label="Noise curfew"
                      value={eventInfo.noiseCurfew ?? "—"}
                    />
                    <Row
                      label="Parking"
                      value={
                        eventInfo.parkingCapacity != null
                          ? `${eventInfo.parkingCapacity} cars`
                          : "—"
                      }
                    />
                    <Row
                      label="Guest rooms"
                      value={
                        eventInfo.guestRooms != null
                          ? String(eventInfo.guestRooms)
                          : "—"
                      }
                    />
                  </>
                ) : (
                  <Skeleton className="h-24 w-full" />
                )}
              </Section>

              <Section title="Upcoming on calendar" hint="Next holds and bookings">
                {eventInfo === null ? (
                  <Skeleton className="h-20 w-full" />
                ) : eventInfo.upcomingBookings.length === 0 ? (
                  <p className="text-[12.5px] text-muted-foreground">
                    Nothing on the calendar ahead.
                  </p>
                ) : (
                  <ul className="flex flex-col gap-1.5">
                    {eventInfo.upcomingBookings.map((b) => (
                      <li
                        key={`${b.date}-${b.spaceBookingId}`}
                        className="flex items-center justify-between rounded border px-2 py-1.5 text-[12px]"
                      >
                        <span>
                          {formatDate(String(b.date).slice(0, 10))} · {b.slot ?? "—"} ·{" "}
                          {STATUS_STYLES[b.status]?.label ?? b.status}
                        </span>
                        <span className="text-muted-foreground">
                          {b.clientName ?? "—"}
                        </span>
                      </li>
                    ))}
                  </ul>
                )}
              </Section>

              {eventInfo?.layoutImageUrl ? (
                <Section title="Layout">
                  {/* eslint-disable-next-line @next/next/no-img-element */}
                  <img
                    src={eventInfo.layoutImageUrl}
                    alt="Space layout"
                    className="max-h-64 w-full rounded border object-contain"
                  />
                </Section>
              ) : null}
            </div>

            <div className="flex flex-col gap-5">
              <div className="grid gap-4 sm:grid-cols-2">
                <Section title="Client">
                  {dossier === null ? (
                    <Skeleton className="h-16 w-full" />
                  ) : (
                    <Party
                      party={dossier.customer}
                      empty="No client attached to this space."
                      extra={
                        current.bookedAt
                          ? `Last booking ${formatDateTime(current.bookedAt)}`
                          : undefined
                      }
                    />
                  )}
                </Section>

                <Section title="Planner">
                  {dossier === null ? (
                    <Skeleton className="h-16 w-full" />
                  ) : (
                    <Party
                      party={dossier.salesPerson}
                      empty="No planner attached yet."
                      extra={
                        current.heldByName && current.heldUntil
                          ? `Holding until ${formatDateTime(current.heldUntil)}`
                          : undefined
                      }
                    />
                  )}
                </Section>
              </div>

              {current.blockReason ? (
                <Section title="Off the market because">
                  <p className="text-[12.5px] text-muted-foreground">{current.blockReason}</p>
                </Section>
              ) : null}

              {/* ---------------- quotations ---------------- */}

              <Section title="Quotations">
                {dossier === null ? (
                  <Skeleton className="h-16 w-full" />
                ) : dossier.quotations.length === 0 ? (
                  <p className="text-[12.5px] text-muted-foreground">
                    No quotation has been raised against this space.
                  </p>
                ) : (
                  <ul className="flex flex-col rounded-lg border">
                    {dossier.quotations.map((quote) => (
                      <li
                        key={quote.id}
                        className="flex flex-wrap items-center gap-2 border-b p-2.5 last:border-b-0"
                      >
                        <div className="min-w-0 flex-1">
                          <p className="flex flex-wrap items-center gap-1.5 text-[12.5px] font-medium">
                            {quote.quoteNumber}
                            {quote.version > 1 ? (
                              <span className="text-muted-foreground">v{quote.version}</span>
                            ) : null}
                            <QuoteBadge
                              status={quote.status}
                              approvalStatus={quote.approvalStatus}
                            />
                          </p>

                          <p className="truncate text-[11.5px] text-muted-foreground">
                            {quote.customerName}
                            {quote.paymentPlanName ? ` · ${quote.paymentPlanName}` : ""}
                            {quote.discountPercent > 0
                              ? ` · ${formatPercent(quote.discountPercent, 2)} off`
                              : ""}
                            {` · ${formatDate(quote.issueDate)}`}
                            {quote.ownerName ? ` · ${quote.ownerName}` : ""}
                          </p>
                        </div>

                        <span className="shrink-0 text-[12.5px] font-medium tabular-nums">
                          {formatRupees(quote.total)}
                        </span>

                        <PdfButton id={quote.id} quoteNumber={quote.quoteNumber} />
                      </li>
                    ))}
                  </ul>
                )}
              </Section>

              {/* ---------------- status move ---------------- */}

              <Section title="Change status">
                {moves.length === 0 ? (
                  <p className="text-[12.5px] text-muted-foreground">
                    Nothing to move this space to from here.
                  </p>
                ) : (
                  <div className="flex flex-col gap-3">
                    <Select
                      value={target}
                      onValueChange={(value) => setTarget(value as UnitStatus)}
                    >
                      <SelectTrigger className="w-full">
                        <SelectValue placeholder="Move to…" />
                      </SelectTrigger>
                      <SelectContent>
                        {moves.map((move) => {
                          const moveStyle = statusStyle(move);
                          return (
                            <SelectItem key={move} value={move}>
                              <span className="flex items-center gap-2">
                                <span className={cn("size-1.5 rounded-full", moveStyle.dot)} />
                                {moveStyle.label}
                                <span className="text-[11px] text-muted-foreground">
                                  {moveStyle.description}
                                </span>
                              </span>
                            </SelectItem>
                          );
                        })}
                      </SelectContent>
                    </Select>

                    {target === "Held" ? (
                      <Field label="Hold for (hours)">
                        <Input
                          type="number"
                          min={1}
                          max={168}
                          value={holdHours}
                          onChange={(event) => setHoldHours(event.target.value)}
                        />
                      </Field>
                    ) : null}

                    {(target === "Held" ||
                      target === "Booked" ||
                      target === "Blackout") ? (
                      <div className="grid gap-3 sm:grid-cols-2">
                        <Field label="Event date">
                          <Input
                            type="date"
                            value={eventDate}
                            onChange={(event) => setEventDate(event.target.value)}
                          />
                        </Field>
                        <Field label="Slot">
                          <Select
                            value={eventSlot}
                            onValueChange={(value) => setEventSlot(value as EventSlot)}
                          >
                            <SelectTrigger className="w-full">
                              <SelectValue />
                            </SelectTrigger>
                            <SelectContent>
                              {EVENT_SLOTS.map((s) => (
                                <SelectItem key={s} value={s}>
                                  {s === "FullDay" ? "Full day" : s}
                                </SelectItem>
                              ))}
                            </SelectContent>
                          </Select>
                        </Field>
                      </div>
                    ) : null}

                    {needsCustomer ? (
                      <div className="grid gap-3 sm:grid-cols-2">
                        <Field label="Customer name">
                          <Input
                            value={customerName}
                            placeholder="Who is this booked for?"
                            onChange={(event) => setCustomerName(event.target.value)}
                          />
                        </Field>
                        <Field label="Customer phone">
                          <Input
                            value={customerPhone}
                            placeholder="Optional"
                            onChange={(event) => setCustomerPhone(event.target.value)}
                          />
                        </Field>
                      </div>
                    ) : null}

                    <Field label={willQueue ? "Reason (goes to the approver)" : "Reason"}>
                      <Textarea
                        rows={2}
                        value={reason}
                        placeholder={
                          willQueue
                            ? "Why this should be approved…"
                            : "Optional note for the trail"
                        }
                        onChange={(event) => setReason(event.target.value)}
                      />
                    </Field>

                    {willQueue ? (
                      <p className="flex items-start gap-1.5 rounded border border-amber-500/40 bg-amber-500/10 px-2.5 py-2 text-[11.5px] text-amber-700 dark:text-amber-300">
                        <ShieldAlert className="mt-px size-3.5 shrink-0" />
                        <span>
                          This needs a manager. The space is held for 48 hours in the
                          meantime so nobody else can take that date.
                        </span>
                      </p>
                    ) : null}

                    <Button onClick={apply} disabled={target === "" || saving}>
                      {saving ? <Loader2 className="size-4 animate-spin" /> : null}
                      {willQueue ? "Send for approval" : "Apply"}
                    </Button>
                  </div>
                )}
              </Section>

              {/* ---------------- booking history ---------------- */}

              <Section title="Booking history">
                {dossier === null ? (
                  <Skeleton className="h-20 w-full" />
                ) : dossier.history.length === 0 ? (
                  <p className="text-[12.5px] text-muted-foreground">
                    Nothing has moved this space yet.
                  </p>
                ) : (
                  <ul className="flex flex-col gap-2.5">
                    {dossier.history.map((entry) => (
                      <li key={entry.id} className="flex gap-2 text-[12px]">
                        <History className="mt-0.5 size-3.5 shrink-0 text-muted-foreground" />
                        <div className="min-w-0">
                          <span className="flex flex-wrap items-center gap-1">
                            <span
                              className={cn(
                                "font-medium",
                                statusStyle(entry.toStatus).text
                              )}
                            >
                              {statusStyle(entry.toStatus).label}
                            </span>
                            <span className="text-muted-foreground">
                              from {statusStyle(entry.fromStatus).label}
                            </span>
                            {entry.partyName ? (
                              <span className="text-muted-foreground">
                                · for {entry.partyName}
                              </span>
                            ) : null}
                          </span>

                          {entry.reason ? (
                            <p className="text-[11.5px] text-muted-foreground">
                              {entry.reason}
                            </p>
                          ) : null}

                          <p className="flex items-center gap-2 text-[11px] text-muted-foreground">
                            <span className="inline-flex items-center gap-1">
                              <User className="size-2.5" /> {entry.actorName}
                            </span>
                            <span className="inline-flex items-center gap-1">
                              <Clock className="size-2.5" /> {timeAgo(entry.createdAt)}
                            </span>
                          </p>
                        </div>
                      </li>
                    ))}
                  </ul>
                )}
              </Section>
            </div>
          </div>
        </ScrollArea>
      </DialogContent>
    </Dialog>
  );
}

/* ------------------------------------------------------------------ *
 * Small pieces
 * ------------------------------------------------------------------ */

function Party({
  party,
  empty,
  extra,
}: {
  party: UnitParty | null;
  empty: string;
  extra?: string;
}) {
  if (!party) {
    return <p className="text-[12.5px] text-muted-foreground">{empty}</p>;
  }

  const href = party.leadId
    ? `/dashboard/leads/${party.leadId}`
    : party.contactId
      ? `/dashboard/leads/contacts?id=${party.contactId}`
      : null;

  return (
    <div className="flex flex-col gap-0.5">
      <p className="flex items-center gap-1.5 text-[13px] font-medium">
        {party.name}
        {href ? (
          <Link
            href={href}
            className="text-primary hover:underline"
            aria-label={`Open ${party.name}`}
          >
            <ExternalLink className="size-3" />
          </Link>
        ) : null}
      </p>

      {party.role ? (
        <p className="text-[11.5px] text-muted-foreground">{party.role}</p>
      ) : null}

      {party.phone ? (
        <p className="flex items-center gap-1 text-[11.5px] text-muted-foreground">
          <Phone className="size-2.5" /> {party.phone}
        </p>
      ) : null}

      {party.email ? (
        <p className="flex items-center gap-1 truncate text-[11.5px] text-muted-foreground">
          <Mail className="size-2.5 shrink-0" /> {party.email}
        </p>
      ) : null}

      {extra ? (
        <p className="flex items-center gap-1 text-[11.5px] text-muted-foreground">
          <Building2 className="size-2.5" /> {extra}
        </p>
      ) : null}
    </div>
  );
}

/**
 * A quotation's real state is two fields — where it is in its own lifecycle,
 * and whether its terms cleared approval. The approval half is shown only when
 * it is the more important of the two.
 */
function QuoteBadge({
  status,
  approvalStatus,
}: {
  status: string;
  approvalStatus: string;
}) {
  const pending = approvalStatus === "Pending";
  const rejected = approvalStatus === "Rejected";

  const label = pending ? "Awaiting approval" : rejected ? "Not approved" : status;

  const tone = pending
    ? "bg-amber-500/10 text-amber-700 border-amber-500/30 dark:text-amber-300"
    : rejected
      ? "bg-rose-500/10 text-rose-700 border-rose-500/30 dark:text-rose-300"
      : status === "Accepted"
        ? "bg-emerald-500/10 text-emerald-700 border-emerald-500/30 dark:text-emerald-300"
        : status === "Sent"
          ? "bg-sky-500/10 text-sky-700 border-sky-500/30 dark:text-sky-300"
          : "bg-muted text-muted-foreground border-border";

  return (
    <span className={cn("rounded-full border px-1.5 py-0.5 text-[10px]", tone)}>
      {label}
    </span>
  );
}

function Section({
  title,
  hint,
  children,
}: {
  title: string;
  hint?: string;
  children: React.ReactNode;
}) {
  return (
    <section className="flex flex-col gap-2">
      <h3 className="flex items-baseline justify-between gap-2">
        <span className="text-[11px] font-medium tracking-wide text-muted-foreground uppercase">
          {title}
        </span>
        {hint ? (
          <span className="text-[10.5px] text-muted-foreground">{hint}</span>
        ) : null}
      </h3>
      {children}
    </section>
  );
}

function Row({
  label,
  value,
  strong,
}: {
  label: string;
  value: string;
  strong?: boolean;
}) {
  return (
    <div className="flex items-baseline justify-between gap-3 border-b py-1.5 text-[12.5px] last:border-b-0">
      <span className="text-muted-foreground">{label}</span>
      <span className={cn("tabular-nums", strong && "font-semibold")}>{value}</span>
    </div>
  );
}

function Field({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <div className="flex flex-col gap-1.5">
      <Label className="text-[12px]">{label}</Label>
      {children}
    </div>
  );
}
