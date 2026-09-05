"use client";

import * as React from "react";
import {
  AlertTriangle,
  Check,
  Loader2,
  Minus,
  Receipt,
  TrendingUp,
} from "lucide-react";
import { toast } from "sonner";

import { PagePanel } from "@/components/shell/page-panel";
import { CrmLoadingState } from "@/components/shell/crm-loader";
import { useSession } from "@/components/dashboard/session-provider";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import {
  Tooltip,
  TooltipContent,
  TooltipTrigger,
} from "@/components/ui/tooltip";
import {
  ApiError,
  getSubscription,
  getUsage,
  isPlatformAdmin,
  UNLIMITED,
  updateSubscription,
  type Plan,
  type PlanLimit,
  type Subscription,
  type UsagePoint,
} from "@/lib/api";
import { cn } from "@/lib/utils";

const MODULE_LABELS: Record<string, string> = {
  leads: "Leads",
  engagement: "Engagement",
  automation: "Lead Automation",
  calls: "Calls",
  sales: "Sales",
  inventory: "Inventory",
  reports: "Reports",
  calendar: "Calendar",
  goals: "Goals",
  "post-sales": "Post Sales",
  "customer-care": "Customer Care",
  constructions: "Constructions",
  hr: "HR",
  administration: "Administration",
  "system-console": "System Console",
};

const rupees = new Intl.NumberFormat("en-IN", {
  style: "currency",
  currency: "INR",
  maximumFractionDigits: 0,
});

const count = new Intl.NumberFormat("en-IN");

export default function SubscriptionPage() {
  const { user } = useSession();
  const [subscription, setSubscription] = React.useState<Subscription | null>(null);
  const [usage, setUsage] = React.useState<UsagePoint[]>([]);

  const canEdit = isPlatformAdmin(user);

  const load = React.useCallback(() => {
    getSubscription()
      .then(setSubscription)
      .catch((error: unknown) => {
        toast.error("Could not load the subscription", {
          description:
            error instanceof ApiError ? error.message : "Network error.",
        });
      });

    getUsage(60)
      .then(setUsage)
      .catch(() => setUsage([]));
  }, []);

  React.useEffect(load, [load]);

  if (subscription === null) {
    return (
      <PagePanel icon={Receipt} title="Subscription">
        <CrmLoadingState label="Loading subscription" />
      </PagePanel>
    );
  }

  return (
    <PagePanel
      icon={Receipt}
      title="Subscription"
      hint="What this workspace is entitled to, what it is using, and what that costs. Limits are enforced on the way in — an invite past the seat count is refused by the API, not just hidden in the screen."
      actions={
        <Badge
          variant="outline"
          className={cn(
            "h-6 gap-1.5 px-2 text-[11px] font-normal",
            subscription.status === "Active"
              ? "text-emerald-600 dark:text-emerald-400"
              : subscription.status === "Trial"
                ? "text-sky-600 dark:text-sky-400"
                : "text-rose-600 dark:text-rose-400"
          )}
        >
          <span className="size-1.5 rounded-full bg-current" />
          {subscription.status}
        </Badge>
      }
    >
      <Tabs defaultValue="plan" className="min-h-0 flex-1">
        <TabsList>
          <TabsTrigger value="plan">Plan &amp; usage</TabsTrigger>
          <TabsTrigger value="compare">Compare plans</TabsTrigger>
          {canEdit ? <TabsTrigger value="terms">Terms</TabsTrigger> : null}
        </TabsList>

        <TabsContent value="plan" className="flex flex-col gap-5">
          <Headline subscription={subscription} />
          <Limits limits={subscription.limits} />
          <UsageChart points={usage} />
        </TabsContent>

        <TabsContent value="compare">
          <PlanTable plans={subscription.availablePlans} />
        </TabsContent>

        {canEdit ? (
          <TabsContent value="terms">
            <TermsForm subscription={subscription} onSaved={setSubscription} />
          </TabsContent>
        ) : null}
      </Tabs>
    </PagePanel>
  );
}

/* ------------------------------------------------------------------ *
 * Headline
 * ------------------------------------------------------------------ */

function Headline({ subscription }: { subscription: Subscription }) {
  const trial = subscription.trialDaysLeft;

  return (
    <div className="flex flex-col gap-4">
      <div className="grid gap-px overflow-hidden rounded-lg border bg-border sm:grid-cols-3">
        <Figure
          label="Plan"
          value={subscription.planName}
          note={subscription.entitlementNote ?? "Published terms"}
        />
        <Figure
          label="Seats in use"
          value={count.format(
            subscription.limits.find((l) => l.key === "seats")?.used ?? 0
          )}
          note="Inactive accounts do not count"
        />
        <Figure
          label="This month"
          value={rupees.format(subscription.monthlyCost)}
          note="Seats in use × the per-seat price"
        />
      </div>

      {trial !== null ? (
        <div
          className={cn(
            "flex items-start gap-2 rounded-lg border-l-[3px] bg-muted/50 px-4 py-3",
            trial <= 3
              ? "border-l-rose-500"
              : trial <= 10
                ? "border-l-amber-500"
                : "border-l-sky-500"
          )}
        >
          <AlertTriangle className="mt-0.5 size-4 shrink-0 text-muted-foreground" />
          <p className="text-[12.5px]">
            {trial === 0 ? (
              <>This trial has run out. The workspace will be suspended on the next pass.</>
            ) : (
              <>
                <b>
                  {trial} {trial === 1 ? "day" : "days"} left
                </b>{" "}
                on this trial. It ends on{" "}
                {new Date(subscription.trialEndsAt!).toLocaleDateString(undefined, {
                  day: "numeric",
                  month: "long",
                  year: "numeric",
                })}
                , and the workspace is suspended automatically after that — not
                deleted.
              </>
            )}
          </p>
        </div>
      ) : null}

      <div>
        <p className="text-[11px] tracking-widest text-muted-foreground uppercase">
          Modules this plan opens
        </p>
        <div className="mt-1.5 flex flex-wrap gap-1">
          {subscription.modules.map((module) => (
            <Badge
              key={module}
              variant="secondary"
              className="h-5 px-1.5 text-[11px] font-normal"
            >
              {MODULE_LABELS[module] ?? module}
            </Badge>
          ))}
        </div>
      </div>

      <div className="flex flex-wrap gap-1.5">
        <Feature on={subscription.apiAccess} label="Public API" />
        <Feature on={subscription.webhooks} label="Webhooks" />
        <Feature on={subscription.customFields} label="Custom fields" />
        <Feature on={subscription.sso} label="Single sign-on" />
      </div>
    </div>
  );
}

function Figure({
  label,
  value,
  note,
}: {
  label: string;
  value: string;
  note: string;
}) {
  return (
    <div className="flex flex-col gap-0.5 bg-card px-4 py-3">
      <span className="text-[10.5px] tracking-widest text-muted-foreground uppercase">
        {label}
      </span>
      <span className="text-[19px] font-semibold tracking-tight tabular-nums">
        {value}
      </span>
      <span className="text-[11px] text-muted-foreground">{note}</span>
    </div>
  );
}

function Feature({ on, label }: { on: boolean; label: string }) {
  return (
    <span
      className={cn(
        "flex items-center gap-1.5 rounded-md border px-2 py-1 text-[11.5px]",
        on ? "" : "text-muted-foreground"
      )}
    >
      {on ? (
        <Check className="size-3 text-emerald-600 dark:text-emerald-400" />
      ) : (
        <Minus className="size-3" />
      )}
      {label}
    </span>
  );
}

/* ------------------------------------------------------------------ *
 * Limits
 * ------------------------------------------------------------------ */

function Limits({ limits }: { limits: PlanLimit[] }) {
  return (
    <div className="flex flex-col gap-3">
      <p className="text-[11px] tracking-widest text-muted-foreground uppercase">
        Limits
      </p>

      <div className="flex flex-col gap-3">
        {limits.map((limit) => {
          const unlimited = limit.allowed === UNLIMITED;
          const fraction = unlimited
            ? 0
            : Math.min(1, limit.used / Math.max(1, limit.allowed));
          const over = !unlimited && limit.used > limit.allowed;

          return (
            <div key={limit.key} className="flex flex-col gap-1">
              <div className="flex items-baseline justify-between gap-2 text-[12.5px]">
                <span className="flex items-center gap-1.5 font-medium capitalize">
                  {limit.label}
                  {limit.overridden ? (
                    <Tooltip>
                      <TooltipTrigger asChild>
                        <Badge
                          variant="outline"
                          className="h-4 cursor-help px-1 text-[9px] font-normal"
                        >
                          Negotiated
                        </Badge>
                      </TooltipTrigger>
                      <TooltipContent className="max-w-64">
                        A contract moved this ceiling off the published plan.
                      </TooltipContent>
                    </Tooltip>
                  ) : null}
                </span>
                <span
                  className={cn(
                    "tabular-nums",
                    over
                      ? "font-medium text-rose-600 dark:text-rose-400"
                      : "text-muted-foreground"
                  )}
                >
                  {count.format(limit.used)}
                  {unlimited ? " · no cap" : ` of ${count.format(limit.allowed)}`}
                </span>
              </div>

              <div className="h-1.5 overflow-hidden rounded-full bg-muted">
                <div
                  className={cn(
                    "h-full rounded-full transition-[width]",
                    over
                      ? "bg-rose-500"
                      : fraction > 0.85
                        ? "bg-amber-500"
                        : "bg-primary"
                  )}
                  style={{ width: unlimited ? "8%" : `${Math.max(2, fraction * 100)}%` }}
                />
              </div>
            </div>
          );
        })}
      </div>
    </div>
  );
}

/* ------------------------------------------------------------------ *
 * Usage over time
 * ------------------------------------------------------------------ */

/**
 * A sparkline of leads held per day, drawn as a plain SVG.
 *
 * Deliberately not a charting library: it is one series with no axis, no
 * legend and no interaction, and the smallest chart package in the tree is
 * larger than this whole screen.
 */
function UsageChart({ points }: { points: UsagePoint[] }) {
  if (points.length < 2) {
    return (
      <div className="rounded-lg border border-dashed p-4 text-[12.5px] text-muted-foreground">
        Usage is recorded once an hour, so the first points appear shortly after
        the workspace starts being used. Nothing is lost in the meantime — the
        limits above are counted live.
      </div>
    );
  }

  const width = 640;
  const height = 90;
  const max = Math.max(...points.map((p) => p.leads), 1);

  const path = points
    .map((point, index) => {
      const x = (index / (points.length - 1)) * width;
      const y = height - (point.leads / max) * (height - 8) - 4;
      return `${index === 0 ? "M" : "L"}${x.toFixed(1)},${y.toFixed(1)}`;
    })
    .join(" ");

  const last = points[points.length - 1];
  const first = points[0];
  const change = last.leads - first.leads;

  return (
    <div className="flex flex-col gap-2">
      <div className="flex items-baseline justify-between gap-2">
        <p className="text-[11px] tracking-widest text-muted-foreground uppercase">
          Leads held
        </p>
        <span className="flex items-center gap-1.5 text-[12px] text-muted-foreground tabular-nums">
          <TrendingUp className="size-3.5" />
          {change >= 0 ? "+" : ""}
          {count.format(change)} over {points.length} days
        </span>
      </div>

      <div className="overflow-x-auto rounded-lg border p-3">
        <svg
          viewBox={`0 0 ${width} ${height}`}
          className="h-[90px] w-full min-w-[320px]"
          role="img"
          aria-label={`Leads held, ${count.format(first.leads)} to ${count.format(last.leads)} over ${points.length} days`}
        >
          <path
            d={`${path} L${width},${height} L0,${height} Z`}
            className="fill-primary/10"
          />
          <path
            d={path}
            fill="none"
            className="stroke-primary"
            strokeWidth="1.75"
            strokeLinejoin="round"
            strokeLinecap="round"
          />
        </svg>
      </div>
    </div>
  );
}

/* ------------------------------------------------------------------ *
 * Plan comparison
 * ------------------------------------------------------------------ */

function PlanTable({ plans }: { plans: Plan[] }) {
  return (
    <div className="overflow-x-auto rounded-lg border">
      <table className="w-full border-collapse text-[12.5px]">
        <thead className="bg-muted/60">
          <tr>
            <th className="border-b px-3 py-2 text-left font-medium">Plan</th>
            <th className="border-b px-3 py-2 text-right font-medium">Seats</th>
            <th className="border-b px-3 py-2 text-right font-medium">Leads</th>
            <th className="border-b px-3 py-2 text-right font-medium">Branches</th>
            <th className="border-b px-3 py-2 text-right font-medium">Per seat</th>
            <th className="border-b px-3 py-2 text-left font-medium">Includes</th>
          </tr>
        </thead>
        <tbody>
          {plans.map((plan) => (
            <tr
              key={plan.key}
              className={cn("border-b last:border-b-0", plan.isCurrent && "bg-accent/50")}
            >
              <td className="px-3 py-2">
                <p className="flex items-center gap-1.5 font-medium">
                  {plan.name}
                  {plan.isCurrent ? (
                    <Badge variant="secondary" className="h-4 px-1 text-[9px]">
                      Current
                    </Badge>
                  ) : null}
                </p>
                <p className="text-[11px] text-muted-foreground">{plan.tagline}</p>
              </td>
              <td className="px-3 py-2 text-right tabular-nums">{cap(plan.seats)}</td>
              <td className="px-3 py-2 text-right tabular-nums">{cap(plan.leads)}</td>
              <td className="px-3 py-2 text-right tabular-nums">{cap(plan.branches)}</td>
              <td className="px-3 py-2 text-right tabular-nums">
                {rupees.format(plan.pricePerSeat)}
              </td>
              <td className="px-3 py-2">
                <div className="flex flex-wrap gap-1">
                  {plan.apiAccess ? <Chip>API</Chip> : null}
                  {plan.webhooks ? <Chip>Webhooks</Chip> : null}
                  {plan.customFields ? <Chip>Custom fields</Chip> : null}
                  {plan.sso ? <Chip>SSO</Chip> : null}
                </div>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

function Chip({ children }: { children: React.ReactNode }) {
  return (
    <Badge variant="outline" className="h-4 px-1 text-[9.5px] font-normal">
      {children}
    </Badge>
  );
}

function cap(value: number) {
  return value === UNLIMITED ? "No cap" : count.format(value);
}

/* ------------------------------------------------------------------ *
 * Commercial terms — platform operators only
 * ------------------------------------------------------------------ */

function TermsForm({
  subscription,
  onSaved,
}: {
  subscription: Subscription;
  onSaved: (next: Subscription) => void;
}) {
  const [plan, setPlan] = React.useState(subscription.planKey);
  const [status, setStatus] = React.useState(subscription.status);
  const [trialEndsAt, setTrialEndsAt] = React.useState(date(subscription.trialEndsAt));
  const [renewsAt, setRenewsAt] = React.useState(date(subscription.renewsAt));
  const [seats, setSeats] = React.useState(override(subscription, "seats"));
  const [leads, setLeads] = React.useState(override(subscription, "leads"));
  const [branches, setBranches] = React.useState(override(subscription, "branches"));
  const [note, setNote] = React.useState(subscription.entitlementNote ?? "");
  const [saving, setSaving] = React.useState(false);

  async function save() {
    setSaving(true);
    try {
      const next = await updateSubscription(subscription.companyId, {
        planTier: plan,
        status,
        trialEndsAt: trialEndsAt ? new Date(trialEndsAt).toISOString() : null,
        renewsAt: renewsAt ? new Date(renewsAt).toISOString() : null,
        seatLimitOverride: seats ? Number(seats) : null,
        leadLimitOverride: leads ? Number(leads) : null,
        branchLimitOverride: branches ? Number(branches) : null,
        entitlementNote: note.trim() || null,
      });

      onSaved(next);
      toast.success("Subscription updated");
    } catch (error) {
      toast.error("Could not save the subscription", {
        description:
          error instanceof ApiError ? error.message : "Network error.",
      });
    } finally {
      setSaving(false);
    }
  }

  return (
    <div className="flex max-w-2xl flex-col gap-4">
      <p className="text-[12.5px] text-muted-foreground">
        Changing the plan takes effect on the next request. Suspending the
        workspace signs everyone in it out immediately — except you.
      </p>

      <div className="grid gap-4 sm:grid-cols-2">
        <div className="space-y-1">
          <Label>Plan</Label>
          <Select value={plan} onValueChange={setPlan}>
            <SelectTrigger className="w-full">
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              {subscription.availablePlans.map((option) => (
                <SelectItem key={option.key} value={option.key}>
                  <span className="flex flex-col">
                    <span>{option.name}</span>
                    <span className="text-[11px] text-muted-foreground">
                      {option.tagline}
                    </span>
                  </span>
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </div>

        <div className="space-y-1">
          <Label>Status</Label>
          <Select value={status} onValueChange={setStatus}>
            <SelectTrigger className="w-full">
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="Active">Active</SelectItem>
              <SelectItem value="Trial">Trial</SelectItem>
              <SelectItem value="Suspended">Suspended</SelectItem>
            </SelectContent>
          </Select>
        </div>

        <div className="space-y-1">
          <Label htmlFor="trial-ends">Trial ends</Label>
          <Input
            id="trial-ends"
            type="date"
            value={trialEndsAt}
            onChange={(event) => setTrialEndsAt(event.target.value)}
          />
          <p className="text-[11px] text-muted-foreground">
            Empty means this workspace is not on a trial.
          </p>
        </div>

        <div className="space-y-1">
          <Label htmlFor="renews">Renews</Label>
          <Input
            id="renews"
            type="date"
            value={renewsAt}
            onChange={(event) => setRenewsAt(event.target.value)}
          />
        </div>
      </div>

      <div>
        <p className="text-[11px] tracking-widest text-muted-foreground uppercase">
          Negotiated ceilings
        </p>
        <p className="mt-0.5 text-[11.5px] text-muted-foreground">
          Leave blank to use the plan&apos;s own numbers.
        </p>

        <div className="mt-2 grid gap-4 sm:grid-cols-3">
          <NumberField id="seats" label="Seats" value={seats} onChange={setSeats} />
          <NumberField id="leads" label="Leads" value={leads} onChange={setLeads} />
          <NumberField
            id="branches"
            label="Branches"
            value={branches}
            onChange={setBranches}
          />
        </div>
      </div>

      <div className="space-y-1">
        <Label htmlFor="note">Why</Label>
        <Input
          id="note"
          value={note}
          placeholder="e.g. 200 seats agreed in the FY26 renewal"
          onChange={(event) => setNote(event.target.value)}
        />
      </div>

      <div>
        <Button disabled={saving} onClick={() => void save()}>
          {saving ? <Loader2 className="animate-spin" /> : <Check />}
          Save terms
        </Button>
      </div>
    </div>
  );
}

function NumberField({
  id,
  label,
  value,
  onChange,
}: {
  id: string;
  label: string;
  value: string;
  onChange: (value: string) => void;
}) {
  return (
    <div className="space-y-1">
      <Label htmlFor={id}>{label}</Label>
      <Input
        id={id}
        type="number"
        min={1}
        value={value}
        placeholder="Plan default"
        onChange={(event) => onChange(event.target.value)}
      />
    </div>
  );
}

function date(iso: string | null) {
  return iso ? iso.slice(0, 10) : "";
}

function override(subscription: Subscription, key: string) {
  const limit = subscription.limits.find((l) => l.key === key);
  return limit?.overridden ? String(limit.allowed) : "";
}
