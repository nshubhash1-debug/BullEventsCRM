"use client";

import * as React from "react";
import { zodResolver } from "@hookform/resolvers/zod";
import { Loader2, Plus } from "lucide-react";
import { Controller, useForm } from "react-hook-form";
import { toast } from "sonner";
import { z } from "zod";

import { Button } from "@/components/ui/button";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { Checkbox } from "@/components/ui/checkbox";
import {
  ApiError,
  createLead,
  eventCategoryOf,
  EVENT_SLOTS,
  EVENT_TYPES,
  INQUIRER_ROLES,
  LEAD_PRIORITIES,
  LEAD_SOURCES,
  MEAL_PREFERENCES,
  PAYMENT_PREFERENCES,
  PLANNING_PACKAGES,
  EVENT_PORTALS,
  SALUTATIONS,
  SERVICE_CATEGORIES,
  VENUE_STATUSES,
  updateLead,
  type Branch,
  type Lead,
  type UserListItem,
} from "@/lib/api";

const leadSchema = z
  .object({
    salutation: z.string(),
    name: z.string().trim().min(2, "Name is too short"),
    partnerName: z.string().trim().optional(),
    partnerPhone: z.string().trim().optional(),
    partnerEmail: z.union([z.literal(""), z.string().trim().email("Enter a valid email")]).optional(),
    inquirerRole: z.string(),
    companyName: z.string().trim().optional(),
    phone: z.string().trim().optional(),
    phone2: z.string().trim().optional(),
    email: z.union([z.literal(""), z.string().trim().email("Enter a valid email")]),
    address: z.string().trim().optional(),
    city: z.string().trim().optional(),
    country: z.string().trim().optional(),
    source: z.string().min(1, "Select a source"),
    priority: z.string().min(1, "Select a priority"),
    branchId: z.number().min(1, "Select a branch"),
    ownerId: z.string(),
    notes: z.string().trim().optional(),

    eventType: z.string(),
    eventDate: z.string(),
    eventEndDate: z.string(),
    eventSlot: z.string(),
    isDateFlexible: z.boolean(),
    guestCount: z.string(),
    ceremonyGuestCount: z.string(),
    receptionGuestCount: z.string(),
    planningPackage: z.string(),
    venueStatus: z.string(),
    portalName: z.string(),
    servicesNeeded: z.array(z.string()),
    mealPreference: z.string(),
    preferredLocality: z.string().trim().optional(),
    paymentMode: z.string(),
    budgetMin: z.string(),
    budgetMax: z.string(),
  })
  // Cross-field rules live here rather than on the individual fields because
  // each one needs to see two values at once, and a per-field refine would fire
  // before the other half of the pair had been typed.
  .refine(
    (v) => !v.eventEndDate || !v.eventDate || v.eventEndDate >= v.eventDate,
    { path: ["eventEndDate"], message: "The event cannot end before it starts." }
  )
  .refine(
    (v) => !v.budgetMax || !v.budgetMin || Number(v.budgetMax) >= Number(v.budgetMin),
    { path: ["budgetMax"], message: "The upper budget is below the lower one." }
  );

type LeadFormValues = z.infer<typeof leadSchema>;

const NONE = "none";
const UNASSIGNED = "unassigned";

function SectionLabel({ children }: { children: React.ReactNode }) {
  return (
    <p className="text-[11px] font-semibold tracking-wide text-muted-foreground uppercase">
      {children}
    </p>
  );
}

/** An ISO instant down to the `yyyy-MM-dd` a date input wants. */
function isoDate(value: string | null | undefined) {
  return value ? value.slice(0, 10) : "";
}

function splitList(value: string | null | undefined) {
  return value ? value.split(",").map((s) => s.trim()).filter(Boolean) : [];
}

/**
 * How far off the event is, in words.
 *
 * Shown live under the date because it is the number that decides how the
 * enquiry gets worked — a wedding six weeks out is a different conversation
 * from one eighteen months out, and reading that off a calendar every time is
 * exactly the friction that makes a rep skip the field.
 */
function leadTime(date: string) {
  if (!date) return null;

  const days = Math.round(
    (new Date(`${date}T00:00:00`).getTime() - new Date().setHours(0, 0, 0, 0)) /
      86_400_000
  );

  if (days < 0) return { text: `${Math.abs(days)} days ago`, urgent: true };
  if (days === 0) return { text: "Today", urgent: true };
  if (days === 1) return { text: "Tomorrow", urgent: true };
  if (days < 45) return { text: `In ${days} days`, urgent: days < 21 };

  const months = Math.round(days / 30);
  return { text: `In about ${months} months`, urgent: false };
}

export function LeadFormDialog({
  lead,
  branches,
  users,
  onSaved,
  trigger,
}: {
  lead?: Lead;
  branches: Branch[];
  users: UserListItem[];
  onSaved: (lead: Lead) => void;
  trigger?: React.ReactNode;
}) {
  const [open, setOpen] = React.useState(false);
  const isEdit = !!lead;

  const defaults = React.useCallback(
    (): LeadFormValues => ({
      salutation: lead?.salutation ?? NONE,
      name: lead?.name ?? "",
      partnerName: lead?.partnerName ?? "",
      partnerPhone: lead?.partnerPhone ?? "",
      partnerEmail: lead?.partnerEmail ?? "",
      inquirerRole: lead?.inquirerRole ?? NONE,
      companyName: lead?.companyName ?? "",
      phone: lead?.phone ?? "",
      phone2: lead?.phone2 ?? "",
      email: lead?.email ?? "",
      address: lead?.address ?? "",
      city: lead?.city ?? "",
      country: lead?.country ?? "",
      source: lead?.source ?? "Website",
      priority: lead?.priority ?? "Medium",
      branchId: lead?.branchId ?? branches[0]?.id ?? 0,
      ownerId: lead?.ownerId ? String(lead.ownerId) : UNASSIGNED,
      notes: lead?.notes ?? "",

      eventType: lead?.eventType ?? NONE,
      eventDate: isoDate(lead?.eventDate),
      eventEndDate: isoDate(lead?.eventEndDate),
      eventSlot: lead?.eventSlot ?? NONE,
      isDateFlexible: lead?.isDateFlexible ?? false,
      guestCount: lead?.guestCount ? String(lead.guestCount) : "",
      ceremonyGuestCount: lead?.ceremonyGuestCount ? String(lead.ceremonyGuestCount) : "",
      receptionGuestCount: lead?.receptionGuestCount ? String(lead.receptionGuestCount) : "",
      planningPackage: lead?.planningPackage ?? NONE,
      venueStatus: lead?.venueStatus ?? NONE,
      portalName: lead?.portalName ?? NONE,
      servicesNeeded: splitList(lead?.servicesNeeded),
      mealPreference: lead?.mealPreference ?? NONE,
      preferredLocality: lead?.preferredLocality ?? "",
      paymentMode: lead?.paymentMode ?? NONE,
      budgetMin: lead?.budgetMin ? String(lead.budgetMin) : "",
      budgetMax: lead?.budgetMax ? String(lead.budgetMax) : "",
    }),
    [lead, branches]
  );

  const form = useForm<LeadFormValues>({
    resolver: zodResolver(leadSchema),
    defaultValues: defaults(),
  });

  React.useEffect(() => {
    if (open) form.reset(defaults());
  }, [open, form, defaults]);

  const eventLeadTime = leadTime(form.watch("eventDate"));

  async function onSubmit(values: LeadFormValues) {
    const pick = (value: string) => (value === NONE ? undefined : value);

    const input = {
      salutation: pick(values.salutation),
      name: values.name,
      partnerName: values.partnerName || undefined,
      partnerPhone: values.partnerPhone || undefined,
      partnerEmail: values.partnerEmail || undefined,
      inquirerRole: pick(values.inquirerRole),
      companyName: values.companyName || undefined,
      phone: values.phone || undefined,
      phone2: values.phone2 || undefined,
      email: values.email || undefined,
      address: values.address || undefined,
      city: values.city || undefined,
      country: values.country || undefined,
      source: values.source,
      priority: values.priority,
      branchId: values.branchId,
      ownerId: values.ownerId === UNASSIGNED ? null : Number(values.ownerId),
      notes: values.notes || undefined,

      eventType: pick(values.eventType),
      // Derived rather than asked for — the server does the same, so the two
      // can never disagree about which bucket an occasion falls in.
      eventCategory: eventCategoryOf(pick(values.eventType)) ?? undefined,
      eventDate: values.eventDate || undefined,
      eventEndDate: values.eventEndDate || undefined,
      eventSlot: pick(values.eventSlot),
      isDateFlexible: values.isDateFlexible,
      guestCount: values.guestCount ? Number(values.guestCount) : undefined,
      ceremonyGuestCount: values.ceremonyGuestCount
        ? Number(values.ceremonyGuestCount)
        : undefined,
      receptionGuestCount: values.receptionGuestCount
        ? Number(values.receptionGuestCount)
        : undefined,
      planningPackage: pick(values.planningPackage),
      venueStatus: pick(values.venueStatus),
      portalName: pick(values.portalName),
      servicesNeeded: values.servicesNeeded.join(",") || undefined,
      mealPreference: pick(values.mealPreference),
      preferredLocality: values.preferredLocality || undefined,
      paymentMode: pick(values.paymentMode),
      budgetMin: values.budgetMin ? Number(values.budgetMin) : undefined,
      budgetMax: values.budgetMax ? Number(values.budgetMax) : undefined,
    };

    try {
      const result = isEdit
        ? await updateLead(lead.id, { ...input, stage: lead.stage })
        : await createLead(input);
      toast.success(isEdit ? "Lead updated" : "Lead added");
      onSaved(result);
      setOpen(false);
    } catch (error) {
      toast.error("Could not save lead", {
        description:
          error instanceof ApiError ? error.message : "Network error.",
      });
    }
  }

  return (
    <Dialog open={open} onOpenChange={setOpen}>
      <DialogTrigger asChild>
        {trigger ?? (
          <Button size="sm" className="shadow-sm">
            <Plus /> New lead
          </Button>
        )}
      </DialogTrigger>
      <DialogContent className="max-h-[85vh] overflow-y-auto sm:max-w-xl">
        <form onSubmit={(e) => void form.handleSubmit(onSubmit)(e)}>
          <DialogHeader>
            <DialogTitle>{isEdit ? "Edit lead" : "New lead"}</DialogTitle>
            <DialogDescription>
              {isEdit
                ? "Update this enquiry's details."
                : "Capture a new enquiry and assign it to a planner."}
            </DialogDescription>
          </DialogHeader>

          <div className="flex flex-col gap-5 py-4">
            <div className="flex flex-col gap-3">
              <SectionLabel>Personal information</SectionLabel>

              <div className="grid grid-cols-[88px_1fr] gap-3">
                <div className="space-y-1">
                  <Label>Salutation</Label>
                  <Controller
                    control={form.control}
                    name="salutation"
                    render={({ field }) => (
                      <Select value={field.value} onValueChange={field.onChange}>
                        <SelectTrigger className="w-full">
                          <SelectValue />
                        </SelectTrigger>
                        <SelectContent>
                          <SelectItem value={NONE}>—</SelectItem>
                          {SALUTATIONS.map((s) => (
                            <SelectItem key={s} value={s}>
                              {s}
                            </SelectItem>
                          ))}
                        </SelectContent>
                      </Select>
                    )}
                  />
                </div>
                <div className="space-y-1">
                  <Label htmlFor="lead-name">Full name</Label>
                  <Input
                    id="lead-name"
                    placeholder="e.g. Vikram Malhotra"
                    aria-invalid={!!form.formState.errors.name}
                    {...form.register("name")}
                  />
                  {form.formState.errors.name ? (
                    <p className="text-xs text-destructive">
                      {form.formState.errors.name.message}
                    </p>
                  ) : null}
                </div>
              </div>

              <div className="grid grid-cols-2 gap-3">
                <div className="space-y-1">
                  <Label>Who is enquiring</Label>
                  <Controller
                    control={form.control}
                    name="inquirerRole"
                    render={({ field }) => (
                      <Select value={field.value} onValueChange={field.onChange}>
                        <SelectTrigger className="w-full">
                          <SelectValue placeholder="Role" />
                        </SelectTrigger>
                        <SelectContent>
                          <SelectItem value={NONE}>—</SelectItem>
                          {INQUIRER_ROLES.map((r) => (
                            <SelectItem key={r.value} value={r.value}>
                              {r.label}
                            </SelectItem>
                          ))}
                        </SelectContent>
                      </Select>
                    )}
                  />
                </div>
                <div className="space-y-1">
                  <Label htmlFor="lead-partner">Partner / guest of honour</Label>
                  <Input
                    id="lead-partner"
                    placeholder="Optional"
                    {...form.register("partnerName")}
                  />
                </div>
              </div>

              <div className="grid grid-cols-2 gap-3">
                <div className="space-y-1">
                  <Label htmlFor="lead-partner-phone">Partner mobile</Label>
                  <Input id="lead-partner-phone" {...form.register("partnerPhone")} />
                </div>
                <div className="space-y-1">
                  <Label htmlFor="lead-partner-email">Partner email</Label>
                  <Input id="lead-partner-email" type="email" {...form.register("partnerEmail")} />
                </div>
              </div>

              <div className="space-y-1">
                <Label htmlFor="lead-company">Company (corporate events)</Label>
                <Input id="lead-company" {...form.register("companyName")} />
              </div>

              <div className="grid grid-cols-2 gap-3">
                <div className="space-y-1">
                  <Label htmlFor="lead-phone">Mobile</Label>
                  <Input id="lead-phone" {...form.register("phone")} />
                </div>
                <div className="space-y-1">
                  <Label htmlFor="lead-phone2">Mobile 2 (optional)</Label>
                  <Input id="lead-phone2" {...form.register("phone2")} />
                </div>
              </div>

              <div className="space-y-1">
                <Label htmlFor="lead-email">Email</Label>
                <Input
                  id="lead-email"
                  type="email"
                  aria-invalid={!!form.formState.errors.email}
                  {...form.register("email")}
                />
                {form.formState.errors.email ? (
                  <p className="text-xs text-destructive">
                    {form.formState.errors.email.message}
                  </p>
                ) : null}
              </div>

              <div className="space-y-1">
                <Label htmlFor="lead-address">Address (optional)</Label>
                <Input id="lead-address" {...form.register("address")} />
              </div>

              <div className="grid grid-cols-2 gap-3">
                <div className="space-y-1">
                  <Label htmlFor="lead-city">City</Label>
                  <Input id="lead-city" {...form.register("city")} />
                </div>
                <div className="space-y-1">
                  <Label htmlFor="lead-country">Country</Label>
                  <Input
                    id="lead-country"
                    placeholder="India"
                    {...form.register("country")}
                  />
                </div>
              </div>
            </div>

            <div className="flex flex-col gap-3 border-t pt-4">
              <SectionLabel>Event details</SectionLabel>

              <div className="grid grid-cols-2 gap-3">
                <div className="space-y-1">
                  <Label>Planning package</Label>
                  <Controller
                    control={form.control}
                    name="planningPackage"
                    render={({ field }) => (
                      <Select value={field.value} onValueChange={field.onChange}>
                        <SelectTrigger className="w-full">
                          <SelectValue placeholder="Package" />
                        </SelectTrigger>
                        <SelectContent>
                          <SelectItem value={NONE}>—</SelectItem>
                          {PLANNING_PACKAGES.map((p) => (
                            <SelectItem key={p.value} value={p.value}>
                              {p.label}
                            </SelectItem>
                          ))}
                        </SelectContent>
                      </Select>
                    )}
                  />
                </div>
                <div className="space-y-1">
                  <Label>Venue status</Label>
                  <Controller
                    control={form.control}
                    name="venueStatus"
                    render={({ field }) => (
                      <Select value={field.value} onValueChange={field.onChange}>
                        <SelectTrigger className="w-full">
                          <SelectValue placeholder="Venue" />
                        </SelectTrigger>
                        <SelectContent>
                          <SelectItem value={NONE}>—</SelectItem>
                          {VENUE_STATUSES.map((v) => (
                            <SelectItem key={v.value} value={v.value}>
                              {v.label}
                            </SelectItem>
                          ))}
                        </SelectContent>
                      </Select>
                    )}
                  />
                </div>
              </div>

              <div className="grid grid-cols-2 gap-3">
                <div className="space-y-1">
                  <Label>Occasion</Label>
                  <Controller
                    control={form.control}
                    name="eventType"
                    render={({ field }) => (
                      <Select value={field.value} onValueChange={field.onChange}>
                        <SelectTrigger className="w-full">
                          <SelectValue placeholder="Select occasion" />
                        </SelectTrigger>
                        <SelectContent>
                          <SelectItem value={NONE}>—</SelectItem>
                          {EVENT_TYPES.map((t) => (
                            <SelectItem key={t.value} value={t.value}>
                              {t.label}
                            </SelectItem>
                          ))}
                        </SelectContent>
                      </Select>
                    )}
                  />
                </div>
                <div className="space-y-1">
                  <Label htmlFor="lead-guests">Guest count</Label>
                  <Input
                    id="lead-guests"
                    type="number"
                    min={1}
                    placeholder="e.g. 350"
                    {...form.register("guestCount")}
                  />
                </div>
              </div>

              <div className="grid grid-cols-2 gap-3">
                <div className="space-y-1">
                  <Label htmlFor="lead-ceremony-guests">Ceremony guests</Label>
                  <Input
                    id="lead-ceremony-guests"
                    type="number"
                    min={1}
                    {...form.register("ceremonyGuestCount")}
                  />
                </div>
                <div className="space-y-1">
                  <Label htmlFor="lead-reception-guests">Reception guests</Label>
                  <Input
                    id="lead-reception-guests"
                    type="number"
                    min={1}
                    {...form.register("receptionGuestCount")}
                  />
                </div>
              </div>

              <div className="grid grid-cols-2 gap-3">
                <div className="space-y-1">
                  <Label htmlFor="lead-event-date">Event date</Label>
                  <Input
                    id="lead-event-date"
                    type="date"
                    {...form.register("eventDate")}
                  />
                  {eventLeadTime ? (
                    <p
                      className={
                        eventLeadTime.urgent
                          ? "text-xs font-medium text-orange-600 dark:text-orange-400"
                          : "text-xs text-muted-foreground"
                      }
                    >
                      {eventLeadTime.text}
                    </p>
                  ) : null}
                </div>
                <div className="space-y-1">
                  <Label htmlFor="lead-event-end">Ends on (multi-day)</Label>
                  <Input
                    id="lead-event-end"
                    type="date"
                    aria-invalid={!!form.formState.errors.eventEndDate}
                    {...form.register("eventEndDate")}
                  />
                  {form.formState.errors.eventEndDate ? (
                    <p className="text-xs text-destructive">
                      {form.formState.errors.eventEndDate.message}
                    </p>
                  ) : null}
                </div>
              </div>

              <div className="grid grid-cols-2 gap-3">
                <div className="space-y-1">
                  <Label>Slot</Label>
                  <Controller
                    control={form.control}
                    name="eventSlot"
                    render={({ field }) => (
                      <Select value={field.value} onValueChange={field.onChange}>
                        <SelectTrigger className="w-full">
                          <SelectValue placeholder="Select slot" />
                        </SelectTrigger>
                        <SelectContent>
                          <SelectItem value={NONE}>—</SelectItem>
                          {EVENT_SLOTS.map((s) => (
                            <SelectItem key={s.value} value={s.value}>
                              {s.label}
                            </SelectItem>
                          ))}
                        </SelectContent>
                      </Select>
                    )}
                  />
                </div>
                <div className="flex items-end pb-2">
                  <Controller
                    control={form.control}
                    name="isDateFlexible"
                    render={({ field }) => (
                      <label className="flex items-center gap-2 text-sm">
                        <Checkbox
                          checked={field.value}
                          onCheckedChange={(v) => field.onChange(v === true)}
                        />
                        Date is flexible
                      </label>
                    )}
                  />
                </div>
              </div>

              <div className="space-y-1">
                <Label>Services needed</Label>
                <Controller
                  control={form.control}
                  name="servicesNeeded"
                  render={({ field }) => (
                    <div className="flex flex-wrap gap-1.5">
                      {SERVICE_CATEGORIES.map((s) => {
                        const on = field.value.includes(s.value);

                        return (
                          <button
                            key={s.value}
                            type="button"
                            onClick={() =>
                              field.onChange(
                                on
                                  ? field.value.filter((v) => v !== s.value)
                                  : [...field.value, s.value]
                              )
                            }
                            className={
                              on
                                ? "rounded-full border border-primary bg-primary/10 px-2.5 py-1 text-xs font-medium text-primary"
                                : "rounded-full border border-border px-2.5 py-1 text-xs text-muted-foreground hover:bg-muted"
                            }
                          >
                            {s.label}
                          </button>
                        );
                      })}
                    </div>
                  )}
                />
              </div>

              <div className="grid grid-cols-2 gap-3">
                <div className="space-y-1">
                  <Label>Meal preference</Label>
                  <Controller
                    control={form.control}
                    name="mealPreference"
                    render={({ field }) => (
                      <Select value={field.value} onValueChange={field.onChange}>
                        <SelectTrigger className="w-full">
                          <SelectValue placeholder="Select" />
                        </SelectTrigger>
                        <SelectContent>
                          <SelectItem value={NONE}>—</SelectItem>
                          {MEAL_PREFERENCES.map((m) => (
                            <SelectItem key={m.value} value={m.value}>
                              {m.label}
                            </SelectItem>
                          ))}
                        </SelectContent>
                      </Select>
                    )}
                  />
                </div>
                <div className="space-y-1">
                  <Label htmlFor="lead-locality">Preferred location</Label>
                  <Input
                    id="lead-locality"
                    placeholder="e.g. South Delhi"
                    {...form.register("preferredLocality")}
                  />
                </div>
              </div>

              <div className="grid grid-cols-[1fr_1fr_1.1fr] gap-3">
                <div className="space-y-1">
                  <Label htmlFor="lead-budget-min">Budget from</Label>
                  <Input
                    id="lead-budget-min"
                    type="number"
                    min={0}
                    {...form.register("budgetMin")}
                  />
                </div>
                <div className="space-y-1">
                  <Label htmlFor="lead-budget-max">Budget to</Label>
                  <Input
                    id="lead-budget-max"
                    type="number"
                    min={0}
                    aria-invalid={!!form.formState.errors.budgetMax}
                    {...form.register("budgetMax")}
                  />
                </div>
                <div className="space-y-1">
                  <Label>Payment mode</Label>
                  <Controller
                    control={form.control}
                    name="paymentMode"
                    render={({ field }) => (
                      <Select value={field.value} onValueChange={field.onChange}>
                        <SelectTrigger className="w-full">
                          <SelectValue placeholder="Select" />
                        </SelectTrigger>
                        <SelectContent>
                          <SelectItem value={NONE}>—</SelectItem>
                          {PAYMENT_PREFERENCES.map((p) => (
                            <SelectItem key={p.value} value={p.value}>
                              {p.label}
                            </SelectItem>
                          ))}
                        </SelectContent>
                      </Select>
                    )}
                  />
                </div>
              </div>

              {form.formState.errors.budgetMax ? (
                <p className="text-xs text-destructive">
                  {form.formState.errors.budgetMax.message}
                </p>
              ) : null}
            </div>

            <div className="flex flex-col gap-3 border-t pt-4">
              <SectionLabel>Lead information</SectionLabel>

              <div className="grid grid-cols-2 gap-3">
                <div className="space-y-1">
                  <Label>Source</Label>
                  <Controller
                    control={form.control}
                    name="source"
                    render={({ field }) => (
                      <Select value={field.value} onValueChange={field.onChange}>
                        <SelectTrigger className="w-full">
                          <SelectValue />
                        </SelectTrigger>
                        <SelectContent>
                          {LEAD_SOURCES.map((s) => (
                            <SelectItem key={s.value} value={s.value}>
                              {s.label}
                            </SelectItem>
                          ))}
                        </SelectContent>
                      </Select>
                    )}
                  />
                </div>
                <div className="space-y-1">
                  <Label>Listing portal</Label>
                  <Controller
                    control={form.control}
                    name="portalName"
                    render={({ field }) => (
                      <Select
                        value={field.value}
                        onValueChange={field.onChange}
                        disabled={form.watch("source") !== "EventPortal"}
                      >
                        <SelectTrigger className="w-full">
                          <SelectValue placeholder="If a portal" />
                        </SelectTrigger>
                        <SelectContent>
                          <SelectItem value={NONE}>—</SelectItem>
                          {EVENT_PORTALS.map((p) => (
                            <SelectItem key={p.value} value={p.value}>
                              {p.label}
                            </SelectItem>
                          ))}
                        </SelectContent>
                      </Select>
                    )}
                  />
                </div>
              </div>

              <div className="grid grid-cols-3 gap-3">
                <div className="space-y-1">
                  <Label>Priority</Label>
                  <Controller
                    control={form.control}
                    name="priority"
                    render={({ field }) => (
                      <Select value={field.value} onValueChange={field.onChange}>
                        <SelectTrigger className="w-full">
                          <SelectValue />
                        </SelectTrigger>
                        <SelectContent>
                          {LEAD_PRIORITIES.map((p) => (
                            <SelectItem key={p.value} value={p.value}>
                              {p.label}
                            </SelectItem>
                          ))}
                        </SelectContent>
                      </Select>
                    )}
                  />
                </div>
                <div className="space-y-1">
                  <Label>Branch</Label>
                  <Controller
                    control={form.control}
                    name="branchId"
                    render={({ field }) => (
                      <Select
                        value={String(field.value)}
                        onValueChange={(v) => field.onChange(Number(v))}
                      >
                        <SelectTrigger className="w-full">
                          <SelectValue placeholder="Select branch" />
                        </SelectTrigger>
                        <SelectContent>
                          {branches.map((b) => (
                            <SelectItem key={b.id} value={String(b.id)}>
                              {b.name}
                            </SelectItem>
                          ))}
                        </SelectContent>
                      </Select>
                    )}
                  />
                </div>
                <div className="space-y-1">
                  <Label>Assign to</Label>
                  <Controller
                    control={form.control}
                    name="ownerId"
                    render={({ field }) => (
                      <Select value={field.value} onValueChange={field.onChange}>
                        <SelectTrigger className="w-full">
                          <SelectValue placeholder="Unassigned" />
                        </SelectTrigger>
                        <SelectContent>
                          <SelectItem value={UNASSIGNED}>Unassigned</SelectItem>
                          {users.map((u) => (
                            <SelectItem key={u.id} value={String(u.id)}>
                              {u.name}
                            </SelectItem>
                          ))}
                        </SelectContent>
                      </Select>
                    )}
                  />
                </div>
              </div>

              <div className="space-y-1">
                <Label htmlFor="lead-notes">Remarks (optional)</Label>
                <Input id="lead-notes" {...form.register("notes")} />
              </div>
            </div>
          </div>

          <DialogFooter>
            <Button
              type="button"
              variant="outline"
              onClick={() => setOpen(false)}
            >
              Cancel
            </Button>
            <Button type="submit" disabled={form.formState.isSubmitting}>
              {form.formState.isSubmitting ? (
                <Loader2 className="animate-spin" />
              ) : null}
              {isEdit ? "Save changes" : "Create lead"}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}
