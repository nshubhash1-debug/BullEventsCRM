"use client";

import * as React from "react";
import {
  BadgePercent,
  Building2,
  Check,
  Download,
  FileText,
  Handshake,
  Layers,
  Link2,
  Link2Off,
  Loader2,
  Lock,
  Search,
  ShieldAlert,
  TrendingUp,
  TriangleAlert,
  User,
} from "lucide-react";
import { toast } from "sonner";

import { Button } from "@/components/ui/button";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { Badge } from "@/components/ui/badge";
import { Checkbox } from "@/components/ui/checkbox";
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
import { Switch } from "@/components/ui/switch";
import { Textarea } from "@/components/ui/textarea";
import { ApiError, apiBlob } from "@/lib/api";
import {
  formatIndian,
  formatPercent,
  formatRupees,
  quoteBuilderApi,
  type BoardUnit,
  type ChargeHead,
  type ChargeSelection,
  type PaymentPlan,
  type QuotationDetail,
  type QuoteOptions,
  type QuotePreview,
  type QuotationTemplate,
} from "@/lib/inventory-api";
import {
  contactsApi,
  formatDate,
  leadsApi,
  type ContactRow,
  type LeadRow,
} from "@/lib/crm-api";
import { cn } from "@/lib/utils";

/* ------------------------------------------------------------------ *
 * Steps
 * ------------------------------------------------------------------ */

/**
 * The quotation is built in the order the conversation happens: who it is for,
 * what it costs, what else is charged, what the money earns, and on what terms.
 *
 * Steps rather than one long scroll because a cost sheet has five unrelated
 * decisions in it and a rep working down a single column reliably misses the
 * last two. Each step reports whether it is complete, so the nav doubles as the
 * checklist a manager would otherwise ask for.
 */
const STEPS = [
  { id: "customer", label: "Client", icon: User, hint: "Who the offer is for" },
  { id: "pricing", label: "Event pricing", icon: BadgePercent, hint: "Date, guests, plan and discount" },
  { id: "charges", label: "Add-ons", icon: Layers, hint: "Catering and other heads" },
  { id: "terms", label: "Terms", icon: FileText, hint: "Validity, notes, approval" },
] as const;

/* ------------------------------------------------------------------ *
 * Builder
 * ------------------------------------------------------------------ */

/**
 * Prices a unit and raises the quotation.
 *
 * Every number on screen comes from the server's preview endpoint rather than
 * being recomputed here. That is deliberate: the pricing rules — discount
 * before PLC, tax on the discounted basic, a schedule that ties back to the
 * consideration, a return that accrues on the basic price alone — are the kind
 * of arithmetic that quietly diverges the moment it exists in two places, and
 * the copy that would be wrong is the one the customer is looking at.
 *
 * What this screen owns is the *offer*: which of the optional terms are on the
 * table at all. Discount, assured return, buy-back and rental yield are each a
 * switch, because the same unit on the same plan is sold to an investor and to
 * an end user on materially different terms, and a builder that always prints
 * all four makes the rep delete things from a PDF.
 */
export function QuoteBuilder({
  unit,
  projectId,
  quotation,
  open,
  onOpenChange,
  onCreated,
}: {
  unit: BoardUnit | null;
  /**
   * The project the unit belongs to.
   *
   * Needed because charge heads are scoped per project and a board tile does
   * not carry its project id. Without it the picker lists every head in the
   * company, including ones the server will correctly refuse to price onto
   * this unit — the total stays right and the list lies, which is worse than
   * either being wrong on its own.
   */
  projectId?: number | null;
  /**
   * Present when an existing quotation is being edited. Editing runs the same
   * pricing path as creating — a quotation has no independently editable total,
   * so "edit" means "re-price", and the only difference here is which endpoint
   * the save button calls.
   */
  quotation?: QuotationDetail | null;
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onCreated: () => void;
}) {
  const editing = quotation != null;

  // In edit mode the unit is described by the quotation's own snapshot rather
  // than by a board tile, so the header reads the same either way.
  const unitNumber = quotation?.unitNumber ?? unit?.unitNumber ?? "—";
  const unitType = quotation?.unitType ?? unit?.configuration ?? "—";
  const saleableArea = quotation?.saleableArea ?? unit?.superArea ?? 0;
  // A board tile carries no tower name; the preview supplies it once priced.
  const towerName = quotation?.towerName ?? null;
  const floor = unit?.floor ?? null;
  const unitId = quotation?.unitId ?? unit?.id ?? null;

  const [step, setStep] = React.useState(0);

  /* ---------------- reference data ---------------- */

  const [plans, setPlans] = React.useState<PaymentPlan[] | null>(null);
  const [heads, setHeads] = React.useState<ChargeHead[] | null>(null);
  const [templates, setTemplates] = React.useState<QuotationTemplate[] | null>(null);

  /* ---------------- customer ---------------- */

  const [customerName, setCustomerName] = React.useState(
    quotation?.customerName ?? unit?.customerName ?? ""
  );
  const [customerPhone, setCustomerPhone] = React.useState(
    quotation?.customerPhone ?? unit?.customerPhone ?? ""
  );
  const [customerEmail, setCustomerEmail] = React.useState(
    quotation?.customerEmail ?? ""
  );
  const [billingAddress, setBillingAddress] = React.useState(
    quotation?.billingAddress ?? ""
  );

  // The record this quotation belongs to.
  //
  // A quotation nobody can trace back to a lead is a document with no pipeline
  // behind it: issuing it moves nothing to Negotiation, accepting it books
  // nothing, and the desk's forecast never sees the money. The unit sometimes
  // knows who is holding it, so that is the seed — but only the rep can say
  // which record the offer is actually for.
  const [link, setLink] = React.useState<LinkedRecord | null>(
    quotation?.leadId
      ? { kind: "lead", id: quotation.leadId, label: `Lead #${quotation.leadId}`, detail: null }
      : quotation?.contactId
        ? {
            kind: "contact",
            id: quotation.contactId,
            label: `Contact #${quotation.contactId}`,
            detail: null,
          }
        : unit?.bookedByLeadId
          ? {
              kind: "lead",
              id: unit.bookedByLeadId,
              label: unit.customerName ?? `Lead #${unit.bookedByLeadId}`,
              detail: unit.holdReason,
            }
          : unit?.bookedByContactId
            ? {
                kind: "contact",
                id: unit.bookedByContactId,
                label: unit.customerName ?? `Contact #${unit.bookedByContactId}`,
                detail: null,
              }
            : null
  );

  /* ---------------- pricing ---------------- */

  const [planId, setPlanId] = React.useState<string>("");
  const [discountOn, setDiscountOn] = React.useState(
    quotation ? quotation.discountApplied : true
  );
  const [discountInput, setDiscountInput] = React.useState<string>(
    quotation ? String(+(quotation.discountPercent * 100).toFixed(4)) : ""
  );
  const [discountLabel, setDiscountLabel] = React.useState(
    quotation?.discountLabel ?? ""
  );
  const [rateOverride, setRateOverride] = React.useState("");
  const [bookingDate, setBookingDate] = React.useState("");

  /* ---------------- the event ---------------- */
  //
  // Held as its own state rather than read off the lead each render: a proposal
  // is an offer against one head count on one date, and the family goes on
  // arguing about both after it has gone out.
  //
  // The plate guarantee is left blank rather than pre-filled with the space's
  // own minimum. Blank means "use the venue's", which is what a planner wants
  // nine times in ten; typing a number is the deliberate act of recording a
  // guarantee that was negotiated down to win the date.

  const [eventDate, setEventDate] = React.useState(
    quotation?.eventDate?.slice(0, 10) ?? ""
  );
  const [guestCount, setGuestCount] = React.useState(
    quotation?.guestCount ? String(quotation.guestCount) : ""
  );
  const [minimumPlates, setMinimumPlates] = React.useState("");

  // The optional revenue heads the rep has ticked, keyed by head id, holding the
  // quantity. Mandatory heads are not in here at all — the server adds those
  // regardless, so tracking them client-side would only create a second place
  // for them to be wrong.
  const [picked, setPicked] = React.useState<Record<number, number>>({});

  /* ---------------- returns ---------------- */

  const [roiOn, setRoiOn] = React.useState(false);
  const [roiRate, setRoiRate] = React.useState("");
  const [roiYears, setRoiYears] = React.useState("");

  const [buyBackOn, setBuyBackOn] = React.useState(false);
  const [buyBackRate, setBuyBackRate] = React.useState("");
  const [buyBackAfter, setBuyBackAfter] = React.useState("");
  const [buyBackYears, setBuyBackYears] = React.useState("");

  const [rentOn, setRentOn] = React.useState(false);
  const [rentRate, setRentRate] = React.useState("");
  const [returnConditions, setReturnConditions] = React.useState("");

  /* ---------------- terms ---------------- */

  const [validDays, setValidDays] = React.useState("15");
  const [notes, setNotes] = React.useState(quotation?.notes ?? "");
  const [terms, setTerms] = React.useState(quotation?.termsAndConditions ?? "");
  const [approvalReason, setApprovalReason] = React.useState("");

  /* ---------------- live pricing ---------------- */

  const [preview, setPreview] = React.useState<QuotePreview | null>(null);
  const [pricing, setPricing] = React.useState(false);
  const [saving, setSaving] = React.useState(false);

  /* ---------------- load reference data once ---------------- */

  React.useEffect(() => {
    if (!open) return;

    let cancelled = false;
    const scope = projectId ?? quotation?.projectId ?? null;

    // Deliberately unscoped, unlike the heads and the templates below.
    //
    // A payment plan carries an optional project, and the ones this company
    // sells on are pinned to the project they were written for — scoping the
    // list hands a rep an empty picker on every other project and leaves the
    // builder unable to price anything at all. Charge heads are the opposite:
    // they really are per project, and offering another project's parking rate
    // would put a number on the cost sheet the server then refuses to price.
    quoteBuilderApi
      .plans()
      .then((loaded) => {
        if (cancelled) return;
        setPlans(loaded);

        const seeded = quotation?.paymentPlanId ?? loaded[0]?.id;
        if (seeded) setPlanId(String(seeded));
      })
      .catch(() => !cancelled && setPlans([]));

    quoteBuilderApi
      .chargeHeads(scope)
      .then((loaded) => {
        if (cancelled) return;
        setHeads(loaded);

        // Re-tick the optional heads the quotation already carries. Mandatory
        // ones are deliberately left out: the server adds those regardless, and
        // holding them here too would give them a second source of truth.
        if (!quotation) return;

        const optional = new Set(
          loaded.filter((h) => !h.isMandatory).map((h) => h.id)
        );

        setPicked(
          Object.fromEntries(
            quotation.charges
              .filter((c) => c.chargeHeadId !== null && optional.has(c.chargeHeadId))
              .map((c) => [c.chargeHeadId as number, c.quantity])
          )
        );
      })
      .catch(() => !cancelled && setHeads([]));

    quoteBuilderApi
      .templates(scope)
      .then((loaded) => !cancelled && setTemplates(loaded))
      .catch(() => !cancelled && setTemplates([]));

    return () => {
      cancelled = true;
    };
  }, [open, quotation, projectId]);

  // A quotation stores an id, not a name. Left as "Lead #12" the chip is a
  // foreign key wearing a label, so the real record is fetched once on open.
  React.useEffect(() => {
    if (!open || link === null) return;
    if (!link.label.startsWith("Lead #") && !link.label.startsWith("Contact #")) return;

    let cancelled = false;

    const load =
      link.kind === "lead"
        ? leadsApi.one(link.id).then((row) => ({
            label: row.name,
            detail: [row.stage, row.phone].filter(Boolean).join(" · ") || null,
          }))
        : contactsApi.one(link.id).then((row) => ({
            label: row.fullName,
            detail: [row.lifecycleStage, row.phone].filter(Boolean).join(" · ") || null,
          }));

    load
      .then((resolved) => {
        if (!cancelled) setLink((current) => (current ? { ...current, ...resolved } : current));
      })
      .catch(() => {
        // A deleted or out-of-branch record keeps its id on screen rather than
        // silently unlinking — the rep needs to see that it is still attached.
      });

    return () => {
      cancelled = true;
    };
  }, [open, link]);

  const plan = plans?.find((p) => String(p.id) === planId) ?? null;

  /* ---------------- seed the return terms ---------------- */

  // Which plan the return block was last filled from. Changing the plan re-seeds
  // it, because a different plan is a different offer — the assured return is
  // the developer paying for money received early, so carrying an up-front
  // plan's nine percent across to a construction-linked one would promise a
  // return nobody costed.
  const seededPlan = React.useRef<number | null>(null);

  React.useEffect(() => {
    if (!plan) return;
    if (seededPlan.current === plan.id) return;

    const firstPass = seededPlan.current === null;
    seededPlan.current = plan.id;

    // An existing quotation opens on the terms it was struck with, not on what
    // the plan happens to grant today.
    const saved = firstPass ? quotation?.annexure ?? null : null;

    if (saved) {
      setRoiOn(saved.hasAssuredReturn);
      setRoiRate(pct(saved.assuredReturnPercent || plan.assuredReturnPercent));
      setRoiYears(num(saved.assuredReturnYears || plan.assuredReturnYears));

      setBuyBackOn(saved.hasBuyBack);
      setBuyBackRate(pct(saved.buyBackPercentPerYear || plan.buyBackPercentPerYear));
      setBuyBackAfter(
        num(saved.buyBackEligibleAfterYears || plan.buyBackEligibleAfterYears)
      );
      setBuyBackYears(num(saved.buyBackHorizonYears));

      setRentOn(saved.hasRentalYield);
      setRentRate(
        num(
          saved.indicativeRentPerSqftPerMonth || plan.indicativeRentPerSqftPerMonth,
          2
        )
      );
      setReturnConditions(saved.conditions ?? plan.returnConditions ?? "");
      return;
    }

    setRoiOn(plan.assuredReturnPercent > 0);
    setRoiRate(pct(plan.assuredReturnPercent));
    setRoiYears(num(plan.assuredReturnYears));

    setBuyBackOn(plan.buyBackPercentPerYear > 0);
    setBuyBackRate(pct(plan.buyBackPercentPerYear));
    setBuyBackAfter(num(plan.buyBackEligibleAfterYears));
    setBuyBackYears("");

    setRentOn(plan.indicativeRentPerSqftPerMonth > 0);
    setRentRate(num(plan.indicativeRentPerSqftPerMonth, 2));
    setReturnConditions(plan.returnConditions ?? "");
  }, [plan, quotation]);

  /* ---------------- what the offer says ---------------- */

  const selection = React.useMemo<ChargeSelection[]>(
    () =>
      Object.entries(picked).map(([id, quantity]) => ({
        chargeHeadId: Number(id),
        quantity,
      })),
    [picked]
  );

  // Investor ROI / buy-back / rental yield stay off the event sales path.
  const options = React.useMemo<QuoteOptions>(
    () => ({
      applyDiscount: discountOn,
      discountLabel: discountLabel.trim() || null,

      includeAssuredReturn: false,
      assuredReturnPercent: null,
      assuredReturnYears: null,

      includeBuyBack: false,
      buyBackPercentPerYear: null,
      buyBackEligibleAfterYears: null,
      buyBackHorizonYears: null,

      includeRentalYield: false,
      rentPerSqftPerMonth: null,

      returnConditions: null,
    }),
    [discountOn, discountLabel]
  );

  // The effect below cannot depend on the object — it is rebuilt on every
  // render — so it depends on what the object says instead.
  const optionsKey = JSON.stringify(options);

  /* ---------------- re-price on every change ---------------- */

  React.useEffect(() => {
    if (!open || unitId === null || !plan) return;

    // The discount box is free text so a rep can type past the standard; an
    // empty box means "whatever the plan grants", not zero.
    const typed = discountInput.trim();
    const discount = typed === "" ? null : Number(typed) / 100;

    if (typed !== "" && (Number.isNaN(discount) || discount! < 0 || discount! > 0.9)) {
      return;
    }

    let cancelled = false;

    // Debounced: the preview is a round trip and the discount box is typed into.
    // The spinner starts with the request rather than with the keystroke, so it
    // reflects work actually in flight.
    const timer = setTimeout(() => {
      if (cancelled) return;
      setPricing(true);

      quoteBuilderApi
        .preview({
          unitId,
          paymentPlanId: plan.id,
          discount,
          rateOverride: decimalOrNull(rateOverride),
          bookingDate: bookingDate || null,
          eventDate: eventDate || null,
          guestCount: guestCount ? Number(guestCount) : null,
          minimumPlates: minimumPlates ? Number(minimumPlates) : null,
          charges: selection,
          options: JSON.parse(optionsKey) as QuoteOptions,
        })
        .then((result) => {
          if (!cancelled) setPreview(result);
        })
        .catch((error) => {
          if (cancelled) return;
          toast.error("Could not price this space", {
            description: error instanceof ApiError ? error.message : "Network error.",
          });
        })
        .finally(() => {
          if (!cancelled) setPricing(false);
        });
    }, 250);

    return () => {
      cancelled = true;
      clearTimeout(timer);
    };
  }, [
    open,
    unitId,
    plan,
    discountInput,
    rateOverride,
    bookingDate,
    eventDate,
    guestCount,
    minimumPlates,
    selection,
    optionsKey,
  ]);

  /* ---------------- templates ---------------- */

  function applyTemplate(template: QuotationTemplate) {
    if (template.paymentPlanId) setPlanId(String(template.paymentPlanId));

    if (template.applyDiscount !== null) setDiscountOn(template.applyDiscount);

    if (template.defaultDiscount !== null && template.defaultDiscount !== undefined) {
      setDiscountInput(String(+(template.defaultDiscount * 100).toFixed(4)));
    }

    if (template.defaultNotes) setNotes(template.defaultNotes);
    if (template.defaultTermsAndConditions) setTerms(template.defaultTermsAndConditions);
    if (template.validDays) setValidDays(String(template.validDays));

    if (template.charges.length > 0) {
      setPicked(
        Object.fromEntries(template.charges.map((c) => [c.chargeHeadId, c.quantity]))
      );
    }

    // The template's return switches are applied after the plan seeds itself,
    // otherwise the plan effect would run second and overwrite them.
    if (template.paymentPlanId && template.paymentPlanId !== plan?.id) {
      seededPlan.current = null;
    }

    window.setTimeout(() => {
      if (template.includeAssuredReturn !== null) setRoiOn(template.includeAssuredReturn);
      if (template.includeBuyBack !== null) setBuyBackOn(template.includeBuyBack);
      if (template.includeRentalYield !== null) setRentOn(template.includeRentalYield);
    }, 0);

    toast.success(`Template "${template.name}" applied.`);
  }

  /* ---------------- save ---------------- */

  async function save() {
    if (unitId === null || !plan) return;

    if (!customerName.trim()) {
      setStep(0);
      toast.error("A quotation needs a customer name.");
      return;
    }

    const typed = discountInput.trim();
    const discount = typed === "" ? null : Number(typed) / 100;

    setSaving(true);
    try {
      const result = quotation
        ? await quoteBuilderApi.reprice(quotation.id, {
            paymentPlanId: plan.id,
            discount,
            rateOverride: decimalOrNull(rateOverride),
            bookingDate: bookingDate || null,
            charges: selection,
            options,
            customerName: customerName.trim(),
            customerPhone: customerPhone.trim() || null,
            customerEmail: customerEmail.trim() || null,
            billingAddress: billingAddress.trim() || null,
            // Zero unlinks. Sending null would leave whatever the quotation
            // already pointed at, so unlinking would be impossible.
            leadId: link?.kind === "lead" ? link.id : 0,
            contactId: link?.kind === "contact" ? link.id : 0,
            notes: notes.trim() || null,
            termsAndConditions: terms.trim() || null,
            approvalReason: approvalReason.trim() || null,
            validDays: decimalOrNull(validDays),
            eventDate: eventDate || null,
            guestCount: guestCount ? Number(guestCount) : null,
            minimumPlates: minimumPlates ? Number(minimumPlates) : null,
          })
        : await quoteBuilderApi.createFromUnit({
            unitId,
            paymentPlanId: plan.id,
            discount,
            rateOverride: decimalOrNull(rateOverride),
            bookingDate: bookingDate || null,
            eventDate: eventDate || null,
            guestCount: guestCount ? Number(guestCount) : null,
            minimumPlates: minimumPlates ? Number(minimumPlates) : null,
            charges: selection,
            options,
            customerName: customerName.trim(),
            customerPhone: customerPhone.trim() || null,
            customerEmail: customerEmail.trim() || null,
            billingAddress: billingAddress.trim() || null,
            leadId: link?.kind === "lead" ? link.id : null,
            contactId: link?.kind === "contact" ? link.id : null,
            notes: notes.trim() || null,
            termsAndConditions: terms.trim() || null,
            approvalReason: approvalReason.trim() || null,
            validDays: decimalOrNull(validDays),
          });

      toast.success(result.message, {
        description: result.approval
          ? `${result.approval.summary} — sent to a manager.`
          : "Download the PDF from the quotations list.",
        duration: 7000,
      });

      onCreated();
      onOpenChange(false);
    } catch (error) {
      toast.error(
        quotation ? "Could not re-price this quotation" : "Could not create this quotation",
        { description: error instanceof ApiError ? error.message : "Network error." }
      );
    } finally {
      setSaving(false);
    }
  }

  if (unitId === null) return null;

  const overStandard =
    preview !== null && preview.discount > preview.standardDiscount;

  const done: Record<(typeof STEPS)[number]["id"], boolean> = {
    customer: customerName.trim().length > 0,
    pricing: plan !== null && preview !== null,
    charges: preview !== null,
    terms: notes.trim().length > 0 || terms.trim().length > 0 || Boolean(eventDate),
  };

  const last = step === STEPS.length - 1;

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      {/*
        Height is capped rather than filled. At 94vh the dialog sat a hair off
        each edge of the screen, which reads as a page that failed to fit rather
        than as a panel over one — and on a tall monitor it stretched a five-field
        step down two thousand pixels. 880px is about as tall as this form is
        ever worth being.
      */}
      <DialogContent className="flex h-[min(880px,88vh)] max-h-[88vh] flex-col gap-0 overflow-hidden p-0 sm:max-w-[1180px]">
        {/* ---------------- record header ---------------- */}

        <DialogHeader className="gap-0 border-b bg-muted/30 p-0 text-left">
          <div className="flex flex-wrap items-start justify-between gap-4 px-6 pt-5 pb-4">
            <div className="min-w-0">
              <p className="flex items-center gap-1.5 text-[10.5px] font-medium tracking-wider text-muted-foreground uppercase">
                <Building2 className="size-3" />
                {editing ? "Quotation" : "New quotation"}
              </p>

              <DialogTitle className="mt-1 truncate text-[19px] leading-tight font-semibold">
                {editing ? `${quotation.quoteNumber} · ${unitNumber}` : unitNumber}
              </DialogTitle>

              <DialogDescription className="mt-1 text-[12px]">
                {[
                  quotation?.projectName ?? preview?.projectName,
                  towerName ? `Block ${towerName}` : null,
                  unitType,
                  eventDate ? `Event ${eventDate}` : null,
                  guestCount ? `${guestCount} guests` : null,
                  saleableArea > 0 ? `${formatIndian(saleableArea)} sq ft` : null,
                ]
                  .filter(Boolean)
                  .join(" · ")}
              </DialogDescription>
            </div>

            <div className="flex shrink-0 items-center gap-2">
              {editing ? (
                <Badge variant="outline" className="h-6 text-[11px]">
                  v{quotation.version} · {quotation.status}
                </Badge>
              ) : null}

              {preview?.requiresApproval ? (
                <Badge className="h-6 gap-1 bg-amber-500/15 text-[11px] text-amber-700 hover:bg-amber-500/15 dark:text-amber-300">
                  <ShieldAlert className="size-3" />
                  Needs approval
                </Badge>
              ) : null}

              {templates && templates.length > 0 && !editing ? (
                <Select
                  onValueChange={(id) => {
                    const found = templates.find((t) => String(t.id) === id);
                    if (found) applyTemplate(found);
                  }}
                >
                  <SelectTrigger size="sm" className="w-[190px]">
                    <SelectValue placeholder="Apply a template" />
                  </SelectTrigger>
                  <SelectContent align="end">
                    {templates.map((t) => (
                      <SelectItem key={t.id} value={String(t.id)}>
                        <span className="flex flex-col items-start">
                          <span>{t.name}</span>
                          <span className="text-[11px] text-muted-foreground">
                            {t.paymentPlanName ?? "No plan"}
                            {t.defaultDiscount
                              ? ` · ${(t.defaultDiscount * 100).toFixed(0)}% off`
                              : ""}
                            {t.includeAssuredReturn ? " · ROI" : ""}
                            {t.includeBuyBack ? " · buy-back" : ""}
                          </span>
                        </span>
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              ) : null}
            </div>
          </div>

          {/* ---------------- step path ---------------- */}

          <nav className="flex items-stretch gap-px overflow-x-auto border-t bg-background px-3">
            {STEPS.map((entry, index) => {
              const Icon = entry.icon;
              const active = index === step;
              const complete = done[entry.id];

              return (
                <button
                  key={entry.id}
                  type="button"
                  onClick={() => setStep(index)}
                  className={cn(
                    "group flex min-w-0 shrink-0 items-center gap-2.5 border-b-2 px-4 py-3 text-left transition-colors",
                    active
                      ? "border-primary text-foreground"
                      : "border-transparent text-muted-foreground hover:text-foreground"
                  )}
                >
                  <span
                    className={cn(
                      "flex size-5 shrink-0 items-center justify-center rounded-full border text-[10px] font-semibold",
                      complete
                        ? "border-primary bg-primary text-primary-foreground"
                        : active
                          ? "border-primary text-primary"
                          : "border-muted-foreground/40"
                    )}
                  >
                    {complete ? <Check className="size-3" /> : index + 1}
                  </span>

                  <span className="flex min-w-0 flex-col">
                    <span className="flex items-center gap-1.5 text-[12.5px] font-medium">
                      <Icon className="size-3.5" />
                      {entry.label}
                    </span>
                    <span className="truncate text-[10.5px] text-muted-foreground">
                      {entry.hint}
                    </span>
                  </span>
                </button>
              );
            })}
          </nav>
        </DialogHeader>

        {/* ---------------- body ---------------- */}

        <div className="grid min-h-0 flex-1 lg:grid-cols-[minmax(0,1fr)_400px]">
          <ScrollArea className="min-h-0">
            <div className="p-6 pb-8">
              {step === 0 ? (
                <CustomerStep
                  link={link}
                  onLink={setLink}
                  name={customerName}
                  onName={setCustomerName}
                  phone={customerPhone}
                  onPhone={setCustomerPhone}
                  email={customerEmail}
                  onEmail={setCustomerEmail}
                  address={billingAddress}
                  onAddress={setBillingAddress}
                />
              ) : null}

              {step === 1 ? (
                <PricingStep
                  plans={plans}
                  planId={planId}
                  onPlan={setPlanId}
                  plan={plan}
                  preview={preview}
                  discountOn={discountOn}
                  onDiscountOn={setDiscountOn}
                  discountInput={discountInput}
                  onDiscountInput={setDiscountInput}
                  discountLabel={discountLabel}
                  onDiscountLabel={setDiscountLabel}
                  rateOverride={rateOverride}
                  onRateOverride={setRateOverride}
                  bookingDate={bookingDate}
                  onBookingDate={setBookingDate}
                  eventDate={eventDate}
                  onEventDate={setEventDate}
                  guestCount={guestCount}
                  onGuestCount={setGuestCount}
                  minimumPlates={minimumPlates}
                  onMinimumPlates={setMinimumPlates}
                  overStandard={overStandard}
                />
              ) : null}

              {step === 2 ? (
                <ChargeHeadPicker heads={heads} picked={picked} onChange={setPicked} />
              ) : null}

              {step === 3 ? (
                <TermsStep
                  validDays={validDays}
                  onValidDays={setValidDays}
                  notes={notes}
                  onNotes={setNotes}
                  terms={terms}
                  onTerms={setTerms}
                  approvalReason={approvalReason}
                  onApprovalReason={setApprovalReason}
                  preview={preview}
                />
              ) : null}
            </div>
          </ScrollArea>

          {/* ---------------- summary rail ---------------- */}

          <div className="hidden min-h-0 border-l bg-muted/20 lg:block">
            <ScrollArea className="h-full">
              <SummaryRail preview={preview} pricing={pricing} overStandard={overStandard} />
            </ScrollArea>
          </div>
        </div>

        {/* ---------------- footer ---------------- */}

        <DialogFooter className="flex-row items-center justify-between gap-2 border-t bg-background px-5 py-4 sm:justify-between">
          <div className="flex items-center gap-2">
            <Button
              variant="ghost"
              size="sm"
              disabled={step === 0 || saving}
              onClick={() => setStep((current) => Math.max(0, current - 1))}
            >
              Back
            </Button>

            <span className="text-[11.5px] text-muted-foreground">
              Step {step + 1} of {STEPS.length}
            </span>
          </div>

          <div className="flex items-center gap-2">
            <Button variant="outline" size="sm" onClick={() => onOpenChange(false)} disabled={saving}>
              Cancel
            </Button>

            {!editing ? (
              <Button
                variant="outline"
                size="sm"
                disabled={saving || !plan}
                onClick={async () => {
                  if (!plan) return;
                  const name = prompt("Template name:");
                  if (!name?.trim()) return;

                  try {
                    await quoteBuilderApi.createTemplate({
                      projectId: projectId ?? null,
                      name: name.trim(),
                      paymentPlanId: plan.id,
                      defaultDiscount: discountInput.trim()
                        ? Number(discountInput) / 100
                        : null,
                      defaultNotes: notes.trim() || null,
                      defaultTermsAndConditions: terms.trim() || null,
                      validDays: decimalOrNull(validDays),
                      applyDiscount: discountOn,
                      includeAssuredReturn: false,
                      includeBuyBack: false,
                      includeRentalYield: false,
                      charges:
                        selection.length > 0
                          ? selection.map((s) => ({
                              chargeHeadId: s.chargeHeadId,
                              quantity: s.quantity ?? 1,
                            }))
                          : null,
                    });
                    toast.success(`Template "${name.trim()}" saved.`);
                  } catch {
                    toast.error("Could not save the template.");
                  }
                }}
              >
                Save as template
              </Button>
            ) : null}

            {last ? (
              <Button size="sm" onClick={save} disabled={saving || preview === null}>
                {saving ? <Loader2 className="size-4 animate-spin" /> : null}
                {preview?.requiresApproval
                  ? "Save & send for approval"
                  : editing
                    ? "Save changes"
                    : "Create quotation"}
              </Button>
            ) : (
              <Button
                size="sm"
                onClick={() => setStep((current) => Math.min(STEPS.length - 1, current + 1))}
              >
                Next
              </Button>
            )}
          </div>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

/* ------------------------------------------------------------------ *
 * Step 1 — customer
 * ------------------------------------------------------------------ */

function CustomerStep({
  link,
  onLink,
  name,
  onName,
  phone,
  onPhone,
  email,
  onEmail,
  address,
  onAddress,
}: {
  link: LinkedRecord | null;
  onLink: (value: LinkedRecord | null) => void;
  name: string;
  onName: (value: string) => void;
  phone: string;
  onPhone: (value: string) => void;
  email: string;
  onEmail: (value: string) => void;
  address: string;
  onAddress: (value: string) => void;
}) {
  /** Filling a field the rep has already typed into would undo their work. */
  function prefill(record: LinkedRecord & { fields: PrefillFields }) {
    onLink({ kind: record.kind, id: record.id, label: record.label, detail: record.detail });

    if (!name.trim() && record.fields.name) onName(record.fields.name);
    if (!phone.trim() && record.fields.phone) onPhone(record.fields.phone);
    if (!email.trim() && record.fields.email) onEmail(record.fields.email);
    if (!address.trim() && record.fields.address) onAddress(record.fields.address);
  }

  return (
    <div className="flex flex-col gap-7">
      <Section
        title="Linked record"
        description="The lead or contact this offer belongs to. Issuing the quotation moves that record's stage, and accepting it books the unit against it — unlinked, none of that happens."
      >
        <RecordLinkPicker link={link} onSelect={prefill} onClear={() => onLink(null)} />
      </Section>

      <Section
      title="Who this quotation is for"
      description="The name prints on the document and on the allotment that follows it. Everything else is how the desk reaches them while it is open."
    >
      <Field label="Customer name" required>
        <Input
          value={name}
          onChange={(event) => onName(event.target.value)}
          placeholder="As it should read on the document"
        />
      </Field>

      <div className="grid gap-3 sm:grid-cols-2">
        <Field label="Phone">
          <Input value={phone} onChange={(event) => onPhone(event.target.value)} />
        </Field>
        <Field label="Email">
          <Input
            type="email"
            value={email}
            onChange={(event) => onEmail(event.target.value)}
          />
        </Field>
      </div>

      <Field
        label="Billing address"
        hint="Printed on the cost sheet. Leave it blank until the buyer confirms it."
      >
        <Textarea
          rows={3}
          value={address}
          onChange={(event) => onAddress(event.target.value)}
        />
      </Field>
      </Section>
    </div>
  );
}

/* ------------------------------------------------------------------ *
 * Record link
 * ------------------------------------------------------------------ */

export interface LinkedRecord {
  kind: "lead" | "contact";
  id: number;
  label: string;
  /** Stage and phone, so two people with the same name are tellable apart. */
  detail: string | null;
}

interface PrefillFields {
  name: string | null;
  phone: string | null;
  email: string | null;
  address: string | null;
}

/**
 * Searches leads and contacts and attaches one to the quotation.
 *
 * Searching rather than listing: a desk carries thousands of open leads and a
 * dropdown of them is unusable, while the rep raising the quotation already
 * knows the name they are looking for. The result rows carry stage and phone
 * because the two Rajeev Malhotras in the pipeline are otherwise identical.
 */
function RecordLinkPicker({
  link,
  onSelect,
  onClear,
}: {
  link: LinkedRecord | null;
  onSelect: (record: LinkedRecord & { fields: PrefillFields }) => void;
  onClear: () => void;
}) {
  const [kind, setKind] = React.useState<"lead" | "contact">(link?.kind ?? "lead");
  const [term, setTerm] = React.useState("");
  const [results, setResults] = React.useState<(LinkedRecord & { fields: PrefillFields })[] | null>(
    null
  );
  const [searching, setSearching] = React.useState(false);

  React.useEffect(() => {
    const query = term.trim();

    if (query.length < 2) {
      setResults(null);
      return;
    }

    let cancelled = false;

    const timer = setTimeout(() => {
      setSearching(true);

      const request = { search: query, page: 1, pageSize: 8 };

      const load =
        kind === "lead"
          ? leadsApi.query(request).then((page) => page.items.map(fromLead))
          : contactsApi.query(request).then((page) => page.items.map(fromContact));

      load
        .then((rows) => !cancelled && setResults(rows))
        .catch(() => !cancelled && setResults([]))
        .finally(() => !cancelled && setSearching(false));
    }, 250);

    return () => {
      cancelled = true;
      clearTimeout(timer);
    };
  }, [term, kind]);

  if (link) {
    return (
      <div className="flex items-center gap-3 rounded-lg border bg-background p-3">
        <span className="flex size-8 shrink-0 items-center justify-center rounded-full bg-primary/10 text-primary">
          <Link2 className="size-4" />
        </span>

        <span className="min-w-0 flex-1">
          <span className="flex flex-wrap items-center gap-1.5">
            <span className="truncate text-[13px] font-medium">{link.label}</span>
            <Badge variant="outline" className="h-4 px-1 text-[9.5px] font-normal capitalize">
              {link.kind}
            </Badge>
          </span>
          {link.detail ? (
            <span className="block truncate text-[11px] text-muted-foreground">
              {link.detail}
            </span>
          ) : null}
        </span>

        <Button variant="ghost" size="sm" className="shrink-0" onClick={onClear}>
          <Link2Off className="size-3.5" />
          Unlink
        </Button>
      </div>
    );
  }

  return (
    <div className="flex flex-col gap-2.5">
      <div className="flex gap-1.5">
        {(["lead", "contact"] as const).map((option) => (
          <Button
            key={option}
            type="button"
            size="sm"
            variant={kind === option ? "default" : "outline"}
            className="h-7 px-3 text-[11.5px] capitalize"
            onClick={() => setKind(option)}
          >
            {option}s
          </Button>
        ))}
      </div>

      <div className="relative">
        <Search className="absolute top-1/2 left-2.5 size-3.5 -translate-y-1/2 text-muted-foreground" />
        <Input
          value={term}
          onChange={(event) => setTerm(event.target.value)}
          placeholder={`Search ${kind}s by name, phone or email…`}
          className="pl-8"
        />
        {searching ? (
          <Loader2 className="absolute top-1/2 right-2.5 size-3.5 -translate-y-1/2 animate-spin text-muted-foreground" />
        ) : null}
      </div>

      {results === null ? (
        <p className="text-[11.5px] text-muted-foreground">
          Type at least two characters. Leaving this blank raises the quotation
          against nobody — valid, but it will not move a deal.
        </p>
      ) : results.length === 0 ? (
        <p className="text-[11.5px] text-muted-foreground">
          No {kind} matches &ldquo;{term.trim()}&rdquo;.
        </p>
      ) : (
        <ul className="flex flex-col divide-y overflow-hidden rounded-lg border">
          {results.map((row) => (
            <li key={`${row.kind}-${row.id}`}>
              <button
                type="button"
                onClick={() => onSelect(row)}
                className="flex w-full items-center gap-2.5 px-3 py-2 text-left hover:bg-muted/60"
              >
                <span className="min-w-0 flex-1">
                  <span className="block truncate text-[12.5px]">{row.label}</span>
                  {row.detail ? (
                    <span className="block truncate text-[11px] text-muted-foreground">
                      {row.detail}
                    </span>
                  ) : null}
                </span>
                <Link2 className="size-3.5 shrink-0 text-muted-foreground" />
              </button>
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}

function fromLead(row: LeadRow): LinkedRecord & { fields: PrefillFields } {
  return {
    kind: "lead",
    id: row.id,
    label: row.name,
    detail:
      [row.stage, row.phone, row.interestedProjectName].filter(Boolean).join(" · ") || null,
    fields: {
      name: row.name,
      phone: row.phone,
      email: row.email,
      address: [row.address, row.city].filter(Boolean).join(", ") || null,
    },
  };
}

function fromContact(row: ContactRow): LinkedRecord & { fields: PrefillFields } {
  return {
    kind: "contact",
    id: row.id,
    label: row.fullName,
    detail: [row.lifecycleStage, row.phone].filter(Boolean).join(" · ") || null,
    fields: {
      name: row.fullName,
      phone: row.phone,
      email: row.email,
      address: [row.address, row.city].filter(Boolean).join(", ") || null,
    },
  };
}

/* ------------------------------------------------------------------ *
 * Step 2 — pricing
 * ------------------------------------------------------------------ */

function PricingStep({
  plans,
  planId,
  onPlan,
  plan,
  preview,
  discountOn,
  onDiscountOn,
  discountInput,
  onDiscountInput,
  discountLabel,
  onDiscountLabel,
  rateOverride,
  onRateOverride,
  bookingDate,
  onBookingDate,
  eventDate,
  onEventDate,
  guestCount,
  onGuestCount,
  minimumPlates,
  onMinimumPlates,
  overStandard,
}: {
  plans: PaymentPlan[] | null;
  planId: string;
  onPlan: (value: string) => void;
  plan: PaymentPlan | null;
  preview: QuotePreview | null;
  discountOn: boolean;
  onDiscountOn: (value: boolean) => void;
  discountInput: string;
  onDiscountInput: (value: string) => void;
  discountLabel: string;
  onDiscountLabel: (value: string) => void;
  rateOverride: string;
  onRateOverride: (value: string) => void;
  bookingDate: string;
  onBookingDate: (value: string) => void;
  eventDate: string;
  onEventDate: (value: string) => void;
  guestCount: string;
  onGuestCount: (value: string) => void;
  /** Blank means "use the venue's own minimum". */
  minimumPlates: string;
  onMinimumPlates: (value: string) => void;
  overStandard: boolean;
}) {
  const ceiling = plan ? plan.standardDiscount + plan.discountTolerance : 0;

  return (
    <div className="flex flex-col gap-7">
      <Section
        title="Payment plan"
        description="The plan decides the instalments, the GST rate and the discount the desk grants as standard."
      >
        {plans === null ? (
          <Skeleton className="h-9 w-full" />
        ) : (
          <div className="grid gap-2">
            {plans.map((option) => {
              const active = String(option.id) === planId;

              return (
                <button
                  key={option.id}
                  type="button"
                  onClick={() => onPlan(String(option.id))}
                  className={cn(
                    "flex items-start gap-3 rounded-lg border p-3.5 text-left transition-colors",
                    active
                      ? "border-primary bg-primary/5"
                      : "hover:border-muted-foreground/40"
                  )}
                >
                  <span
                    className={cn(
                      "mt-0.5 flex size-4 shrink-0 items-center justify-center rounded-full border",
                      active ? "border-primary bg-primary" : "border-muted-foreground/40"
                    )}
                  >
                    {active ? (
                      <Check className="size-2.5 text-primary-foreground" />
                    ) : null}
                  </span>

                  <span className="min-w-0 flex-1">
                    <span className="flex flex-wrap items-center gap-2">
                      <span className="text-[13px] font-medium">{option.name}</span>
                      <Badge variant="outline" className="h-4 px-1 text-[9.5px] font-normal">
                        {option.code}
                      </Badge>
                      {option.assuredReturnPercent > 0 ? (
                        <Badge variant="outline" className="h-4 px-1 text-[9.5px] font-normal">
                          {formatPercent(option.assuredReturnPercent)} assured
                        </Badge>
                      ) : null}
                    </span>

                    <span className="mt-0.5 block text-[11.5px] text-muted-foreground">
                      {option.standardDiscount > 0
                        ? `${formatPercent(option.standardDiscount, 2)} standard discount`
                        : "No standard discount"}
                      {option.discountTolerance > 0
                        ? ` (+${formatPercent(option.discountTolerance, 2)} tolerance)`
                        : ""}
                      {" · "}
                      {option.milestones.length} instalments · GST{" "}
                      {formatPercent(option.taxRate)}
                    </span>
                  </span>
                </button>
              );
            })}
          </div>
        )}
      </Section>

      <Section
        title="Discount"
        description="Optional. Held off, the unit is quoted at its card rate and the document says so rather than printing a nil concession."
        action={<Switch checked={discountOn} onCheckedChange={onDiscountOn} />}
      >
        {discountOn ? (
          <>
            <div className="grid gap-3 sm:grid-cols-2">
              <Field
                label="Discount %"
                hint={
                  plan
                    ? `Standard ${formatPercent(plan.standardDiscount, 2)} · approval past ${formatPercent(ceiling, 2)}`
                    : undefined
                }
              >
                <Input
                  type="number"
                  min={0}
                  max={90}
                  step="0.25"
                  value={discountInput}
                  placeholder={plan ? String(plan.standardDiscount * 100) : "Plan standard"}
                  onChange={(event) => onDiscountInput(event.target.value)}
                />
              </Field>

              <Field
                label="Label on the document"
                hint="Optional. Defaults to the plan's name."
              >
                <Input
                  value={discountLabel}
                  onChange={(event) => onDiscountLabel(event.target.value)}
                  placeholder="Launch offer"
                />
              </Field>
            </div>

            {plan ? (
              <div className="flex flex-wrap gap-1.5">
                {[
                  { label: "Plan standard", value: plan.standardDiscount },
                  { label: "At tolerance", value: ceiling },
                  { label: "Nil", value: 0 },
                ]
                  .filter((preset, index, all) =>
                    all.findIndex((other) => other.value === preset.value) === index
                  )
                  .map((preset) => (
                    <Button
                      key={preset.label}
                      type="button"
                      size="sm"
                      variant="outline"
                      className="h-6 px-2 text-[11px]"
                      onClick={() =>
                        onDiscountInput(String(+(preset.value * 100).toFixed(4)))
                      }
                    >
                      {preset.label} · {formatPercent(preset.value, 2)}
                    </Button>
                  ))}
              </div>
            ) : null}

            {overStandard ? (
              <p className="flex items-start gap-1.5 text-[11.5px] text-amber-700 dark:text-amber-300">
                <ShieldAlert className="mt-px size-3.5 shrink-0" />
                Past the plan&rsquo;s standard. Saving will stop for a manager.
              </p>
            ) : null}

            {preview && preview.discountAmount > 0 ? (
              <p className="text-[11.5px] text-muted-foreground">
                Worth {formatRupees(preview.discountAmount)} across the unit —{" "}
                {formatRupees(preview.ratePerSqft * preview.discount)} per sq ft.
              </p>
            ) : null}
          </>
        ) : (
          <p className="text-[12px] text-muted-foreground">
            Quoted at the card rate. The derivation prints &ldquo;no discount
            applied&rdquo; so a buyer comparing two offers can see it was deliberate.
          </p>
        )}
      </Section>

      <Section
        title="Rate and date"
        description="Both are exceptions. An overridden rate needs the same approval an over-discount does."
      >
        <div className="grid gap-3 sm:grid-cols-2">
          <Field
            label="Rate override ₹ / sq ft"
            hint={
              preview
                ? `Card rate ${formatRupees(preview.ratePerSqft)}${
                    preview.rateCardLabel ? ` · ${preview.rateCardLabel}` : ""
                  }`
                : undefined
            }
          >
            <Input
              type="number"
              min={0}
              step="1"
              value={rateOverride}
              placeholder="Leave blank to use the rate card"
              onChange={(event) => onRateOverride(event.target.value)}
            />
          </Field>

          <Field
            label="Booking date"
            hint="Picks the rate card in force and dates the booking instalments."
          >
            <Input
              type="date"
              value={bookingDate}
              onChange={(event) => onBookingDate(event.target.value)}
            />
          </Field>
        </div>
      </Section>

      <Section
        title="The event"
        description="The date anchors the pre-event instalments; the head count is what the per-plate lines multiply."
      >
        <div className="grid gap-3 sm:grid-cols-3">
          <Field
            label="Event date"
            hint="Instalments due before the event are counted back from here."
          >
            <Input
              type="date"
              value={eventDate}
              onChange={(event) => onEventDate(event.target.value)}
            />
          </Field>

          <Field label="Guest count" hint="What the client expects.">
            <Input
              type="number"
              min={1}
              value={guestCount}
              placeholder="e.g. 350"
              onChange={(event) => onGuestCount(event.target.value)}
            />
          </Field>

          <Field
            label="Plate guarantee"
            hint={
              preview && preview.minimumApplied
                ? `Billing ${preview.billedHeads.toLocaleString("en-IN")} plates — the guarantee, not the guest count`
                : "Blank uses the venue's own minimum."
            }
          >
            <Input
              type="number"
              min={0}
              value={minimumPlates}
              placeholder={
                preview ? String(preview.minimumPlates) : "Venue's minimum"
              }
              onChange={(event) => onMinimumPlates(event.target.value)}
            />
          </Field>
        </div>

        {/*
          The one arithmetic surprise on an events proposal, said plainly rather
          than left for the client to find: a hall with a 400-plate minimum
          bills 400 plates for a 200-guest wedding, and a planner who cannot see
          that here will quote the guest count and have to reissue.
        */}
        {preview && preview.minimumApplied ? (
          <p className="mt-3 rounded border border-amber-500/30 bg-amber-500/10 px-3 py-2 text-[12.5px] text-amber-800 dark:text-amber-200">
            {preview.guestCount.toLocaleString("en-IN")} guests, but this space
            guarantees {preview.minimumPlates.toLocaleString("en-IN")} plates —
            every per-plate line is billed on{" "}
            {preview.billedHeads.toLocaleString("en-IN")}.
          </p>
        ) : null}
      </Section>
    </div>
  );
}

/* ------------------------------------------------------------------ *
 * Step 4 — returns
 * ------------------------------------------------------------------ */

/**
 * The investment terms, each independently switchable.
 *
 * Written as three separate offers rather than one "investor annexure" toggle
 * because they are three separate promises: an assured return is income the
 * developer pays, a buy-back is an exit the developer underwrites, and a rental
 * yield is neither — it is an estimate of what the open market would pay. A
 * quotation routinely carries one and not the others.
 */
function ReturnsStep({
  annexure,
  roiOn,
  onRoiOn,
  roiRate,
  onRoiRate,
  roiYears,
  onRoiYears,
  buyBackOn,
  onBuyBackOn,
  buyBackRate,
  onBuyBackRate,
  buyBackAfter,
  onBuyBackAfter,
  buyBackYears,
  onBuyBackYears,
  rentOn,
  onRentOn,
  rentRate,
  onRentRate,
  conditions,
  onConditions,
}: {
  annexure: QuotePreview["annexure"];
  roiOn: boolean;
  onRoiOn: (value: boolean) => void;
  roiRate: string;
  onRoiRate: (value: string) => void;
  roiYears: string;
  onRoiYears: (value: string) => void;
  buyBackOn: boolean;
  onBuyBackOn: (value: boolean) => void;
  buyBackRate: string;
  onBuyBackRate: (value: string) => void;
  buyBackAfter: string;
  onBuyBackAfter: (value: string) => void;
  buyBackYears: string;
  onBuyBackYears: (value: string) => void;
  rentOn: boolean;
  onRentOn: (value: boolean) => void;
  rentRate: string;
  onRentRate: (value: string) => void;
  conditions: string;
  onConditions: (value: string) => void;
}) {
  const anything = roiOn || buyBackOn || rentOn;

  return (
    <div className="flex flex-col gap-7">
      <div className="rounded-lg border bg-muted/30 p-3.5 text-[11.5px] text-muted-foreground">
        Everything here accrues on the <strong>basic sale price</strong>, never on
        the consideration — GST and the maintenance deposit are not the buyer&rsquo;s
        capital in the unit. Switch all three off and the quotation prints no
        annexure at all.
      </div>

      <Section
        title="Assured return"
        description="Income the developer pays for money received early, until possession."
        action={<Switch checked={roiOn} onCheckedChange={onRoiOn} />}
      >
        {roiOn ? (
          <>
            <div className="grid gap-3 sm:grid-cols-2">
              <Field label="Rate % per year">
                <Input
                  type="number"
                  min={0}
                  max={100}
                  step="0.25"
                  value={roiRate}
                  onChange={(event) => onRoiRate(event.target.value)}
                />
              </Field>
              <Field label="Years" hint="Halves are allowed — 4.5 to possession.">
                <Input
                  type="number"
                  min={0}
                  max={30}
                  step="0.5"
                  value={roiYears}
                  onChange={(event) => onRoiYears(event.target.value)}
                />
              </Field>
            </div>

            {annexure?.hasAssuredReturn ? (
              <div className="grid grid-cols-3 gap-3 rounded-md border bg-background p-3">
                <Metric label="Per month" value={formatRupees(annexure.assuredReturnPerMonth)} />
                <Metric label="Per year" value={formatRupees(annexure.assuredReturnPerYear)} />
                <Metric
                  label={`Over ${annexure.assuredReturnYears} yrs`}
                  value={formatRupees(annexure.assuredReturnAmount)}
                  strong
                />
              </div>
            ) : null}
          </>
        ) : (
          <p className="text-[12px] text-muted-foreground">
            Not offered on this quotation.
          </p>
        )}
      </Section>

      <Section
        title="Buy-back"
        description="An exit the developer underwrites — appreciation on top of the capital returned."
        action={<Switch checked={buyBackOn} onCheckedChange={onBuyBackOn} />}
      >
        {buyBackOn ? (
          <>
            <div className="grid gap-3 sm:grid-cols-3">
              <Field label="Rate % per year">
                <Input
                  type="number"
                  min={0}
                  max={100}
                  step="0.25"
                  value={buyBackRate}
                  onChange={(event) => onBuyBackRate(event.target.value)}
                />
              </Field>
              <Field label="Exercisable after (yrs)">
                <Input
                  type="number"
                  min={0}
                  max={30}
                  step="0.5"
                  value={buyBackAfter}
                  onChange={(event) => onBuyBackAfter(event.target.value)}
                />
              </Field>
              <Field
                label="Accrues over (yrs)"
                hint="Blank follows the assured term."
              >
                <Input
                  type="number"
                  min={0}
                  max={30}
                  step="0.5"
                  value={buyBackYears}
                  placeholder="Assured term"
                  onChange={(event) => onBuyBackYears(event.target.value)}
                />
              </Field>
            </div>

            {annexure?.hasBuyBack ? (
              <div className="grid grid-cols-2 gap-3 rounded-md border bg-background p-3">
                <Metric label="Appreciation" value={formatRupees(annexure.buyBackAmount)} />
                <Metric
                  label="Buy-back value"
                  value={formatRupees(annexure.buyBackValue)}
                  strong
                />
              </div>
            ) : null}
          </>
        ) : (
          <p className="text-[12px] text-muted-foreground">
            Not offered on this quotation.
          </p>
        )}
      </Section>

      <Section
        title="Indicative rental yield"
        description="What the open market would pay. An alternative to the assured return, never added to it."
        action={<Switch checked={rentOn} onCheckedChange={onRentOn} />}
      >
        {rentOn ? (
          <>
            <Field label="Rent ₹ / sq ft / month">
              <Input
                type="number"
                min={0}
                step="0.5"
                value={rentRate}
                onChange={(event) => onRentRate(event.target.value)}
              />
            </Field>

            {annexure?.hasRentalYield ? (
              <div className="grid grid-cols-3 gap-3 rounded-md border bg-background p-3">
                <Metric label="Per month" value={formatRupees(annexure.indicativeRentPerMonth)} />
                <Metric label="Per year" value={formatRupees(annexure.indicativeRentPerYear)} />
                <Metric
                  label="Gross yield"
                  value={formatPercent(annexure.grossRentalYield, 2)}
                  strong
                />
              </div>
            ) : (
              <p className="text-[11.5px] text-muted-foreground">
                No rent is configured on this plan — type one to state a yield.
              </p>
            )}
          </>
        ) : (
          <p className="text-[12px] text-muted-foreground">Not stated on this quotation.</p>
        )}
      </Section>

      {anything ? (
        <Section
          title="Conditions"
          description="Printed under the annexure. What the return is subject to."
        >
          <Textarea
            rows={4}
            value={conditions}
            onChange={(event) => onConditions(event.target.value)}
            placeholder="One condition per line."
          />
        </Section>
      ) : null}
    </div>
  );
}

/* ------------------------------------------------------------------ *
 * Step 5 — terms
 * ------------------------------------------------------------------ */

function TermsStep({
  validDays,
  onValidDays,
  notes,
  onNotes,
  terms,
  onTerms,
  approvalReason,
  onApprovalReason,
  preview,
}: {
  validDays: string;
  onValidDays: (value: string) => void;
  notes: string;
  onNotes: (value: string) => void;
  terms: string;
  onTerms: (value: string) => void;
  approvalReason: string;
  onApprovalReason: (value: string) => void;
  preview: QuotePreview | null;
}) {
  return (
    <div className="flex flex-col gap-7">
      <Section
        title="Validity"
        description="The unit stays on hold for this long, and the price is guaranteed for no longer."
      >
        <div className="flex flex-wrap items-end gap-3">
          <Field label="Valid for (days)" className="w-40">
            <Input
              type="number"
              min={1}
              max={180}
              step={1}
              value={validDays}
              onChange={(event) => onValidDays(event.target.value)}
            />
          </Field>

          <div className="flex gap-1.5 pb-1">
            {[7, 15, 30, 45].map((days) => (
              <Button
                key={days}
                type="button"
                size="sm"
                variant="outline"
                className="h-6 px-2 text-[11px]"
                onClick={() => onValidDays(String(days))}
              >
                {days}d
              </Button>
            ))}
          </div>
        </div>
      </Section>

      <Section title="Remarks" description="Printed on the quotation under the schedule.">
        <Textarea rows={3} value={notes} onChange={(event) => onNotes(event.target.value)} />
      </Section>

      <Section
        title="Terms & conditions"
        description="Left blank, the document prints the standard set."
      >
        <Textarea
          rows={6}
          value={terms}
          onChange={(event) => onTerms(event.target.value)}
          placeholder="One term per line."
        />
      </Section>

      {preview?.requiresApproval ? (
        <div className="flex flex-col gap-2 rounded-md border border-amber-500/40 bg-amber-500/10 p-3">
          <p className="flex items-start gap-1.5 text-[12px] text-amber-700 dark:text-amber-300">
            <ShieldAlert className="mt-px size-3.5 shrink-0" />
            <span>{preview.approvalReason}</span>
          </p>

          <Field label="Justification for the approver">
            <Textarea
              rows={3}
              value={approvalReason}
              placeholder="Why should this be allowed?"
              onChange={(event) => onApprovalReason(event.target.value)}
            />
          </Field>
        </div>
      ) : null}
    </div>
  );
}

/* ------------------------------------------------------------------ *
 * Summary rail
 * ------------------------------------------------------------------ */

/**
 * The offer as the customer will read it, visible from every step.
 *
 * Kept on screen throughout rather than shown at the end, because most of the
 * decisions in this form are only meaningful against their effect on the
 * consideration — a rep choosing between two plans is choosing between two
 * numbers, and making them click "next" to see it is how a quotation goes out
 * at a price nobody checked.
 */
function SummaryRail({
  preview,
  pricing,
  overStandard,
}: {
  preview: QuotePreview | null;
  pricing: boolean;
  overStandard: boolean;
}) {
  if (preview === null) {
    return (
      <div className="flex flex-col gap-3 p-5">
        <Skeleton className="h-7 w-40" />
        <Skeleton className="h-28 w-full" />
        <Skeleton className="h-44 w-full" />
      </div>
    );
  }

  return (
    <div className="flex flex-col gap-4 p-5 pb-8">
      <div className="flex items-start justify-between gap-3">
        <div className="min-w-0">
          <p className="text-[10.5px] tracking-wider text-muted-foreground uppercase">
            Total consideration
          </p>
          <p className="text-[26px] leading-tight font-semibold tabular-nums">
            {formatRupees(preview.grandTotal)}
          </p>
          <p className="text-[11.5px] text-muted-foreground italic">
            {preview.grandTotalInWords}
          </p>

          {preview.discountApplied && preview.discountAmount > 0 ? (
            <p className="mt-1 text-[11px] text-emerald-700 dark:text-emerald-400">
              After a concession of {formatRupees(preview.discountAmount)}.
            </p>
          ) : null}

          {preview.refundableTotal > 0 ? (
            <p className="text-[11px] text-muted-foreground">
              Includes {formatRupees(preview.refundableTotal)} refundable.
            </p>
          ) : null}
        </div>

        {pricing ? (
          <Loader2 className="size-4 shrink-0 animate-spin text-muted-foreground" />
        ) : null}
      </div>

      <div className="flex flex-col gap-1 border-t pt-3 text-[12px]">
        <Line
          label={`Basic rate${preview.rateCardLabel ? ` · ${preview.rateCardLabel}` : ""}`}
          value={`${formatRupees(preview.ratePerSqft)} / sq ft`}
        />

        {preview.discountApplied && preview.discount > 0 ? (
          <Line
            label={`Less ${preview.discountLabel ?? "discount"} ${formatPercent(preview.discount, 2)}`}
            value={`− ${formatRupees(preview.ratePerSqft * preview.discount)} / sq ft`}
            muted
            warn={overStandard}
          />
        ) : (
          <Line label="No discount · list price" value="—" muted />
        )}

        {preview.plcPerSqft > 0 ? (
          <Line label="Add PLC" value={`+ ${formatRupees(preview.plcPerSqft)} / sq ft`} muted />
        ) : null}

        <Line
          label="Applicable rate"
          value={`${formatRupees(preview.effectiveRatePerSqft)} / sq ft`}
          strong
        />
        <Line
          label={`Basic · ${formatIndian(preview.saleableArea)} sq ft`}
          value={formatRupees(preview.basicAmount)}
        />
        <Line
          label={`GST @ ${formatPercent(preview.taxRate)}`}
          value={formatRupees(preview.taxAmount)}
        />
        <Line label="Unit cost" value={formatRupees(preview.totalAmount)} strong />
      </div>

      {preview.charges.length > 0 ? (
        <div className="flex flex-col gap-1 border-t pt-3 text-[12px]">
          <p className="mb-0.5 text-[10.5px] tracking-wider text-muted-foreground uppercase">
            Services
          </p>

          {preview.charges.map((charge) => (
            <Line
              key={`${charge.chargeHeadId}-${charge.sortOrder}`}
              // Per-head lines always show their multiplier, even at a quantity
              // of one: the whole point of the plate count is that the client
              // can see what the catering was struck on.
              label={
                charge.basis === "PerGuest"
                  ? `${charge.name} × ${formatIndian(charge.quantity)} plates`
                  : charge.basis === "PerQuantity" && charge.quantity !== 1
                    ? `${charge.name} × ${formatIndian(charge.quantity)}`
                    : charge.name
              }
              value={formatRupees(charge.totalAmount)}
              muted
            />
          ))}

          <Line label="Services total" value={formatRupees(preview.chargesTotal)} strong />
        </div>
      ) : null}

      {preview.scheduleVariance !== 0 ? (
        <p className="flex items-start gap-1.5 rounded border border-destructive/40 bg-destructive/10 px-2.5 py-2 text-[11.5px] text-destructive">
          <TriangleAlert className="mt-px size-3.5 shrink-0" />
          <span>
            The schedule totals {formatRupees(preview.scheduleVariance)} away from the
            consideration. This plan&rsquo;s percentages do not close — tell the pricing
            desk before issuing.
          </span>
        </p>
      ) : null}

      <div className="flex flex-col gap-1 border-t pt-3">
        <p className="text-[10.5px] tracking-wider text-muted-foreground uppercase">
          {preview.paymentPlanName} · {preview.milestones.length} instalments
        </p>

        {preview.unscheduledTotal > 0 ? (
          <p className="mb-1 text-[11px] text-muted-foreground">
            Spreads {formatRupees(preview.scheduledTotal)}.{" "}
            {formatRupees(preview.unscheduledTotal)} falls due on its own terms.
          </p>
        ) : null}

        <ul className="flex flex-col">
          {preview.milestones.map((milestone) => (
            <li
              key={milestone.sortOrder}
              className="flex items-baseline justify-between gap-3 border-b py-1 text-[11.5px] last:border-b-0"
            >
              <span className="min-w-0 flex-1 truncate">
                {milestone.label}
                {milestone.dueDate ? (
                  <span className="text-muted-foreground">
                    {" "}
                    · {formatDate(milestone.dueDate)}
                  </span>
                ) : null}
              </span>
              <span className="shrink-0 tabular-nums">
                {formatRupees(milestone.totalAmount)}
              </span>
            </li>
          ))}
        </ul>
      </div>

      {preview.annexure ? (
        <div className="flex flex-col gap-2 border-t pt-3 text-[12px]">
          <p className="flex items-center gap-1.5 text-[10.5px] tracking-wider text-muted-foreground uppercase">
            <Handshake className="size-3.5" />
            Investment summary · printed as an annexure
          </p>

          <div className="grid grid-cols-2 gap-2">
            {preview.annexure.totalEarned > 0 ? (
              <Metric label="Total return" value={formatRupees(preview.annexure.totalEarned)} strong />
            ) : null}
            {preview.annexure.returnOnInvestment > 0 ? (
              <Metric
                label="ROI"
                value={formatPercent(preview.annexure.returnOnInvestment, 1)}
                strong
              />
            ) : null}
            {preview.annexure.annualisedReturn > 0 ? (
              <Metric
                label="Annualised"
                value={formatPercent(preview.annexure.annualisedReturn, 2)}
              />
            ) : null}
            {preview.annexure.hasRentalYield ? (
              <Metric
                label="Rental yield"
                value={formatPercent(preview.annexure.grossRentalYield, 2)}
              />
            ) : null}
          </div>

          {preview.annexure.hasAssuredReturn ? (
            <Line
              label={`Assured ${formatPercent(preview.annexure.assuredReturnPercent, 2)} / yr × ${preview.annexure.assuredReturnYears}`}
              value={formatRupees(preview.annexure.assuredReturnAmount)}
              muted
            />
          ) : null}

          {preview.annexure.hasBuyBack ? (
            <Line
              label={`Buy-back at ${formatPercent(preview.annexure.buyBackPercentPerYear, 2)} / yr`}
              value={formatRupees(preview.annexure.buyBackValue)}
              muted
            />
          ) : null}
        </div>
      ) : null}
    </div>
  );
}

/* ------------------------------------------------------------------ *
 * Charge heads
 * ------------------------------------------------------------------ */

/**
 * The revenue heads that go on this quotation, grouped as the cost sheet groups
 * them.
 *
 * Mandatory heads are shown but not toggleable — they are on every cost sheet
 * this project issues, and letting a rep untick the development charges would
 * quietly produce an under-quote nobody catches until registration. Optional
 * heads carry a quantity where the basis needs one: two parking slots, three
 * KVA of backup.
 */
function ChargeHeadPicker({
  heads,
  picked,
  onChange,
}: {
  heads: ChargeHead[] | null;
  picked: Record<number, number>;
  onChange: (next: Record<number, number>) => void;
}) {
  if (heads === null) return <Skeleton className="h-40 w-full" />;

  if (heads.length === 0) {
    return (
      <p className="text-[12px] text-muted-foreground">
        This project has no charge heads configured, so the unit cost is the whole
        consideration.
      </p>
    );
  }

  function toggle(head: ChargeHead, on: boolean) {
    const next = { ...picked };
    if (on) next[head.id] = head.defaultQuantity || 1;
    else delete next[head.id];
    onChange(next);
  }

  function setQuantity(head: ChargeHead, raw: string) {
    const value = Number(raw);
    if (Number.isNaN(value) || value < 0) return;
    onChange({ ...picked, [head.id]: value });
  }

  const groups = new Map<string, ChargeHead[]>();
  for (const head of heads) {
    const list = groups.get(head.group) ?? [];
    list.push(head);
    groups.set(head.group, list);
  }

  return (
    <div className="flex flex-col gap-6">
      <p className="text-[12px] text-muted-foreground">
        Heads marked with a lock are on every cost sheet this project issues and
        cannot be removed. Everything else is this quotation&rsquo;s to decide.
      </p>

      {[...groups].map(([group, list]) => (
        <div key={group} className="flex flex-col gap-1.5">
          <Label className="text-[10.5px] font-medium tracking-wider text-muted-foreground uppercase">
            {group}
          </Label>

          <div className="flex flex-col divide-y rounded-md border">
            {list.map((head) => {
              const on = head.isMandatory || head.id in picked;

              return (
                <div key={head.id} className="flex items-center gap-2.5 px-3 py-2.5">
                  {head.isMandatory ? (
                    <Lock
                      className="size-3.5 shrink-0 text-muted-foreground"
                      aria-label="Always applied"
                    />
                  ) : (
                    <Checkbox
                      checked={on}
                      onCheckedChange={(checked) => toggle(head, checked === true)}
                      aria-label={head.name}
                    />
                  )}

                  <span className="min-w-0 flex-1">
                    <span className="flex flex-wrap items-center gap-1.5">
                      <span className="truncate text-[12.5px]">{head.name}</span>
                      {head.isRefundable ? (
                        <Badge variant="outline" className="h-4 px-1 text-[9px] font-normal">
                          Refundable
                        </Badge>
                      ) : null}
                      {head.includeInSchedule ? null : (
                        <Badge variant="outline" className="h-4 px-1 text-[9px] font-normal">
                          Outside schedule
                        </Badge>
                      )}
                    </span>

                    <span className="block truncate text-[11px] text-muted-foreground">
                      {head.basis === "PerSqft"
                        ? `${formatRupees(head.rate)} / sq ft`
                        : head.basis === "PerGuest"
                          ? `${formatRupees(head.rate)} / plate`
                          : head.basis === "PercentOfUnitCost"
                            ? `${formatPercent(head.rate)} of venue rental`
                            : formatRupees(head.rate)}
                      {head.taxRate > 0
                        ? ` · GST ${formatPercent(head.taxRate)}`
                        : " · no GST"}
                      {head.dueLabel ? ` · ${head.dueLabel.toLowerCase()}` : ""}
                    </span>
                  </span>

                  {head.basis === "PerQuantity" && on && !head.isMandatory ? (
                    <Input
                      type="number"
                      min={1}
                      step={1}
                      value={picked[head.id] ?? head.defaultQuantity}
                      onChange={(event) => setQuantity(head, event.target.value)}
                      className="h-7 w-16 shrink-0 text-[12px]"
                      aria-label={`${head.name} quantity`}
                    />
                  ) : null}
                </div>
              );
            })}
          </div>
        </div>
      ))}
    </div>
  );
}

/* ------------------------------------------------------------------ *
 * PDF
 * ------------------------------------------------------------------ */

/**
 * Fetches the PDF with the bearer token attached and hands the browser a blob.
 *
 * A plain link cannot work here — the endpoint is authenticated and an anchor
 * carries no Authorization header, so it would download a 401 page.
 */
export async function downloadQuotationPdf(id: number, quoteNumber: string) {
  try {
    const blob = await apiBlob(quoteBuilderApi.pdfUrl(id));

    const url = URL.createObjectURL(blob);
    const anchor = document.createElement("a");
    anchor.href = url;
    anchor.download = `${quoteNumber}.pdf`;
    document.body.appendChild(anchor);
    anchor.click();
    anchor.remove();
    URL.revokeObjectURL(url);
  } catch (error) {
    toast.error("Could not download the PDF", {
      description: error instanceof ApiError ? error.message : "Network error.",
    });
  }
}

export function PdfButton({ id, quoteNumber }: { id: number; quoteNumber: string }) {
  const [busy, setBusy] = React.useState(false);

  return (
    <Button
      variant="outline"
      size="sm"
      disabled={busy}
      onClick={async () => {
        setBusy(true);
        await downloadQuotationPdf(id, quoteNumber);
        setBusy(false);
      }}
    >
      {busy ? <Loader2 className="size-3.5 animate-spin" /> : <Download className="size-3.5" />}
      PDF
    </Button>
  );
}

/* ------------------------------------------------------------------ *
 * Small pieces
 * ------------------------------------------------------------------ */

function Section({
  title,
  description,
  action,
  children,
}: {
  title: string;
  description?: string;
  action?: React.ReactNode;
  children: React.ReactNode;
}) {
  return (
    <section className="flex flex-col gap-3">
      <div className="flex items-start justify-between gap-4">
        <div className="min-w-0">
          <h3 className="text-[13.5px] font-semibold">{title}</h3>
          {description ? (
            <p className="mt-0.5 text-[11.5px] text-muted-foreground">{description}</p>
          ) : null}
        </div>
        {action ? <div className="shrink-0 pt-1">{action}</div> : null}
      </div>

      <div className="flex flex-col gap-3">{children}</div>
    </section>
  );
}

function Field({
  label,
  hint,
  required,
  className,
  children,
}: {
  label: string;
  hint?: string;
  required?: boolean;
  className?: string;
  children: React.ReactNode;
}) {
  return (
    <div className={cn("flex flex-col gap-1.5", className)}>
      <Label className="text-[12px]">
        {label}
        {required ? <span className="text-destructive"> *</span> : null}
      </Label>
      {children}
      {hint ? <p className="text-[10.5px] text-muted-foreground">{hint}</p> : null}
    </div>
  );
}

function Metric({
  label,
  value,
  strong,
}: {
  label: string;
  value: string;
  strong?: boolean;
}) {
  return (
    <div className="flex flex-col gap-0.5">
      <span className="text-[10px] tracking-wider text-muted-foreground uppercase">
        {label}
      </span>
      <span
        className={cn("tabular-nums", strong ? "text-[14px] font-semibold" : "text-[13px]")}
      >
        {value}
      </span>
    </div>
  );
}

function Line({
  label,
  value,
  strong,
  muted,
  warn,
}: {
  label: string;
  value: string;
  strong?: boolean;
  muted?: boolean;
  warn?: boolean;
}) {
  return (
    <div className="flex items-baseline justify-between gap-3">
      <span
        className={cn(
          muted && "text-muted-foreground",
          warn && "text-amber-600 dark:text-amber-400"
        )}
      >
        {label}
      </span>
      <span
        className={cn(
          "tabular-nums",
          strong && "font-semibold",
          muted && !warn && "text-muted-foreground",
          warn && "text-amber-600 dark:text-amber-400"
        )}
      >
        {value}
      </span>
    </div>
  );
}

/* ---------------- input coercion ---------------- */

/** A percentage box holds what a fraction means: 9 in the box is 0.09 on the wire. */
function fraction(raw: string): number | null {
  const value = Number(raw.trim());
  if (raw.trim() === "" || Number.isNaN(value) || value < 0) return null;
  return value / 100;
}

/** Blank stays blank rather than becoming zero — the two mean different things. */
function decimalOrNull(raw: string): number | null {
  const value = Number(raw.trim());
  if (raw.trim() === "" || Number.isNaN(value) || value < 0) return null;
  return value;
}

/** A stored fraction, back into the percentage box the rep types in. */
function pct(value: number): string {
  return value > 0 ? String(+(value * 100).toFixed(4)) : "";
}

function num(value: number, decimals = 2): string {
  return value > 0 ? String(+value.toFixed(decimals)) : "";
}
