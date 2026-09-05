"use client";

import * as React from "react";
import {
  AlertTriangle,
  Blocks,
  Check,
  Handshake,
  HardHat,
  Lock,
  Plus,
  Search,
  Sparkles,
  Trash2,
  Unlock,
} from "lucide-react";
import { toast } from "sonner";

import { PropThumb } from "@/components/props/prop-thumb";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { ApiError } from "@/lib/api";
import { formatMoney } from "@/lib/crm-api";
import {
  CREW_ROLES,
  chargeGroupLabel,
  humaniseLabel,
  quotationResourcesApi,
  type QuotationMargin,
  type QuotationResource,
  type QuotationResourceOptions,
} from "@/lib/resources-api";
import { cn } from "@/lib/utils";

/** How a resource line's state reads on screen. */
const STATE_TONE: Record<string, string> = {
  Planned: "bg-muted text-muted-foreground",
  Held: "bg-sky-100 text-sky-800 dark:bg-sky-950/50 dark:text-sky-300",
  Converted: "bg-emerald-100 text-emerald-800 dark:bg-emerald-950/50 dark:text-emerald-300",
  Released: "bg-muted text-muted-foreground",
};

const KIND_ICON: Record<string, React.ElementType> = {
  Prop: Sparkles,
  PropKit: Blocks,
  Crew: HardHat,
  Vendor: Handshake,
};

function pct(value: number | null | undefined) {
  if (value === null || value === undefined) return "—";
  return `${Math.round(value * 100)}%`;
}

/** A margin figure, coloured by whether the deal is actually making money. */
function MarginNumber({ value, className }: { value: number; className?: string }) {
  return (
    <span
      className={cn(
        "tabular-nums font-semibold",
        value < 0 ? "text-rose-600 dark:text-rose-400" : "text-emerald-600 dark:text-emerald-400",
        className
      )}
    >
      {formatMoney(value)}
    </span>
  );
}

export function QuotationResourcePlan({
  quotationId,
  quotationStatus,
  onChanged,
}: {
  quotationId: number;
  quotationStatus: string;
  onChanged?: () => void;
}) {
  const [plan, setPlan] = React.useState<QuotationMargin | null>(null);
  const [options, setOptions] = React.useState<QuotationResourceOptions | null>(null);
  const [busy, setBusy] = React.useState(false);
  const [picker, setPicker] = React.useState<"kit" | "prop" | "crew" | "vendor" | null>(null);
  const [search, setSearch] = React.useState("");

  const [crewForm, setCrewForm] = React.useState({ role: "Bearer", headcount: "4" });

  const load = React.useCallback(() => {
    quotationResourcesApi.plan(quotationId).then(setPlan).catch(() => setPlan(null));
  }, [quotationId]);

  React.useEffect(() => load(), [load]);

  // The picker's catalogue is fetched only once something is being picked, and
  // re-fetched as the search narrows — the availability numbers on it are
  // computed against this proposal's own dates and go stale as lines are added.
  React.useEffect(() => {
    if (!picker) return;

    const timer = setTimeout(() => {
      quotationResourcesApi
        .options(quotationId, { search: search || undefined })
        .then(setOptions)
        .catch(() => setOptions(null));
    }, 250);

    return () => clearTimeout(timer);
  }, [picker, search, quotationId, plan?.resourceCount]);

  async function act<T>(work: () => Promise<T>, success: string) {
    setBusy(true);
    try {
      const result = await work();
      toast.success(success);
      onChanged?.();
      return result;
    } catch (error) {
      toast.error(error instanceof ApiError ? error.message : "That did not work.");
      return null;
    } finally {
      setBusy(false);
    }
  }

  async function mutate(work: () => Promise<QuotationMargin>, success: string) {
    const result = await act(work, success);
    if (result) setPlan(result);
  }

  if (!plan) {
    return (
      <p className="py-10 text-center text-[13px] text-muted-foreground">Loading the plan…</p>
    );
  }

  const editable = quotationStatus !== "Rejected" && quotationStatus !== "Expired";
  const canConvert = quotationStatus === "Accepted" && plan.convertedLines < plan.resourceCount;
  const canHold = editable && plan.resources.some((r) => r.propItemId && r.state !== "Converted");

  return (
    <div className="space-y-5">
      {/* ---------------- the number a manager opens this for ---------------- */}
      <div className="rounded-lg border bg-card">
        <div className="grid grid-cols-2 divide-x sm:grid-cols-4">
          <div className="p-3">
            <div className="text-[11px] uppercase tracking-wide text-muted-foreground">
              Client pays
            </div>
            <div className="mt-1 text-xl font-semibold tabular-nums">
              {formatMoney(plan.revenue)}
            </div>
          </div>
          <div className="p-3">
            <div className="text-[11px] uppercase tracking-wide text-muted-foreground">
              Planned cost
            </div>
            <div className="mt-1 text-xl font-semibold tabular-nums">
              {formatMoney(plan.plannedCost)}
            </div>
            <div className="text-[11px] text-muted-foreground">
              {plan.resourceCount} line{plan.resourceCount === 1 ? "" : "s"}
            </div>
          </div>
          <div className="p-3">
            <div className="text-[11px] uppercase tracking-wide text-muted-foreground">
              Planned margin
            </div>
            <div className="mt-1 text-xl">
              <MarginNumber value={plan.plannedMargin} />
            </div>
            <div className="text-[11px] text-muted-foreground">
              {pct(plan.plannedMarginFraction)}
            </div>
          </div>
          <div className="p-3">
            <div className="text-[11px] uppercase tracking-wide text-muted-foreground">
              Committed
            </div>
            <div className="mt-1 text-xl font-semibold tabular-nums">
              {formatMoney(plan.committedCost)}
            </div>
            {plan.costVariance !== 0 ? (
              <div
                className={cn(
                  "text-[11px] font-medium",
                  plan.costVariance > 0 ? "text-rose-600" : "text-emerald-600"
                )}
              >
                {plan.costVariance > 0 ? "+" : ""}
                {formatMoney(plan.costVariance)} vs plan
              </div>
            ) : (
              <div className="text-[11px] text-muted-foreground">on plan</div>
            )}
          </div>
        </div>

        {plan.byGroup.length > 0 ? (
          <div className="border-t px-3 py-2">
            <table className="w-full text-[12px]">
              <thead>
                <tr className="text-left text-[10.5px] uppercase tracking-wide text-muted-foreground">
                  <th className="py-1 font-medium">Group</th>
                  <th className="py-1 text-right font-medium">Billed</th>
                  <th className="py-1 text-right font-medium">Costs us</th>
                  <th className="py-1 text-right font-medium">Margin</th>
                  <th className="py-1 text-right font-medium">%</th>
                </tr>
              </thead>
              <tbody>
                {plan.byGroup.map((g) => (
                  <tr key={g.group} className="border-t">
                    <td className="py-1">{chargeGroupLabel(g.label)}</td>
                    <td className="py-1 text-right tabular-nums">{formatMoney(g.sell)}</td>
                    <td className="py-1 text-right tabular-nums text-muted-foreground">
                      {formatMoney(g.cost)}
                    </td>
                    <td className="py-1 text-right">
                      <MarginNumber value={g.margin} className="font-normal" />
                    </td>
                    <td className="py-1 text-right tabular-nums text-muted-foreground">
                      {pct(g.marginFraction)}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        ) : null}
      </div>

      {/* ---------------- actions ---------------- */}
      <div className="flex flex-wrap gap-2">
        {editable ? (
          <>
            <Button
              size="sm"
              variant={picker === "kit" ? "secondary" : "outline"}
              className="h-8"
              onClick={() => setPicker(picker === "kit" ? null : "kit")}
            >
              <Blocks className="mr-1 size-3.5" />
              Add a kit
            </Button>
            <Button
              size="sm"
              variant={picker === "prop" ? "secondary" : "outline"}
              className="h-8"
              onClick={() => setPicker(picker === "prop" ? null : "prop")}
            >
              <Sparkles className="mr-1 size-3.5" />
              Add props
            </Button>
            <Button
              size="sm"
              variant={picker === "crew" ? "secondary" : "outline"}
              className="h-8"
              onClick={() => setPicker(picker === "crew" ? null : "crew")}
            >
              <HardHat className="mr-1 size-3.5" />
              Add crew
            </Button>
            <Button
              size="sm"
              variant={picker === "vendor" ? "secondary" : "outline"}
              className="h-8"
              onClick={() => setPicker(picker === "vendor" ? null : "vendor")}
            >
              <Handshake className="mr-1 size-3.5" />
              Add a supplier
            </Button>
          </>
        ) : null}

        {canHold ? (
          plan.heldLines > 0 ? (
            <Button
              size="sm"
              variant="outline"
              className="h-8"
              disabled={busy}
              onClick={() =>
                void mutate(
                  () => quotationResourcesApi.release(quotationId),
                  "Holds released — the stock is free again."
                )
              }
            >
              <Unlock className="mr-1 size-3.5" />
              Release {plan.heldLines} hold{plan.heldLines === 1 ? "" : "s"}
            </Button>
          ) : (
            <Button
              size="sm"
              className="h-8"
              disabled={busy}
              onClick={() =>
                void act(
                  () => quotationResourcesApi.hold(quotationId),
                  "Stock held until this proposal expires."
                ).then((result) => {
                  if (!result) return;
                  setPlan(result.plan);
                  result.warnings.forEach((w) => toast.warning(w));
                })
              }
            >
              <Lock className="mr-1 size-3.5" />
              Hold the stock
            </Button>
          )
        ) : null}

        {canConvert ? (
          <Button
            size="sm"
            className="h-8"
            disabled={busy}
            onClick={() =>
              void act(
                () => quotationResourcesApi.convert(quotationId),
                "Converted."
              ).then((result) => {
                if (!result) return;
                toast.success(
                  `${result.propIssueCode ?? "No gate pass"} · ${result.crewBooked} crew · ` +
                    `${result.purchaseOrderCodes.length} supplier order(s).`
                );
                result.warnings.forEach((w) => toast.warning(w));
                load();
              })
            }
          >
            <Check className="mr-1 size-3.5" />
            Book it all
          </Button>
        ) : null}
      </div>

      {plan.heldLines > 0 ? (
        <p className="rounded-md border border-sky-200 bg-sky-50 px-3 py-2 text-[12px] text-sky-900 dark:border-sky-900 dark:bg-sky-950/40 dark:text-sky-300">
          {plan.heldLines} line{plan.heldLines === 1 ? " is" : "s are"} holding godown stock,
          expiring with this proposal. A deal that goes quiet hands the crates back on its own.
        </p>
      ) : null}

      {plan.shortLines > 0 ? (
        <p className="rounded-md border border-amber-200 bg-amber-50 px-3 py-2 text-[12px] text-amber-900 dark:border-amber-900 dark:bg-amber-950/40 dark:text-amber-300">
          <AlertTriangle className="mr-1 inline size-3.5" />
          {plan.shortLines === 1
            ? "One line promises"
            : `${plan.shortLines} lines promise`}{" "}
          more than the godown has free over these dates.
        </p>
      ) : null}

      {/* ---------------- pickers ---------------- */}
      {picker && options ? (
        <div className="rounded-lg border bg-muted/30 p-3">
          {picker === "crew" ? (
            <div className="space-y-2">
              <div className="text-[12.5px] font-medium">
                Quote a headcount — nobody is booked until the deal closes
              </div>

              <div className="flex flex-wrap gap-1.5">
                {options.crewCoverage.map((c) => (
                  <button
                    key={c.role}
                    type="button"
                    onClick={() => setCrewForm((f) => ({ ...f, role: c.role }))}
                    className={cn(
                      "rounded border px-2 py-1 text-left text-[11.5px]",
                      crewForm.role === c.role ? "border-primary bg-primary/5" : "bg-card"
                    )}
                  >
                    <div className="font-medium">{humaniseLabel(c.role)}</div>
                    <div className="tabular-nums text-muted-foreground">
                      {c.available}/{c.onRoster} free
                      {c.averageDayRate ? ` · ${formatMoney(c.averageDayRate)}/day` : ""}
                    </div>
                  </button>
                ))}
              </div>

              <div className="flex items-end gap-2">
                <div>
                  <Label className="text-[11px] text-muted-foreground">Role</Label>
                  <select
                    className="mt-1 h-9 w-44 rounded-md border bg-background px-2 text-sm"
                    value={crewForm.role}
                    onChange={(e) => setCrewForm((f) => ({ ...f, role: e.target.value }))}
                  >
                    {CREW_ROLES.map((r) => (
                      <option key={r} value={r}>
                        {humaniseLabel(r)}
                      </option>
                    ))}
                  </select>
                </div>
                <div>
                  <Label className="text-[11px] text-muted-foreground">How many</Label>
                  <Input
                    className="mt-1 h-9 w-20"
                    value={crewForm.headcount}
                    onChange={(e) => setCrewForm((f) => ({ ...f, headcount: e.target.value }))}
                  />
                </div>
                <Button
                  className="h-9"
                  disabled={busy}
                  onClick={() =>
                    void mutate(
                      () =>
                        quotationResourcesApi.addCrew(quotationId, {
                          crewRole: crewForm.role,
                          headcount: Number(crewForm.headcount) || 1,
                        }),
                      "Crew added to the plan."
                    )
                  }
                >
                  <Plus className="mr-1 size-3.5" />
                  Add
                </Button>
              </div>
            </div>
          ) : (
            <>
              {picker !== "kit" ? (
                <div className="relative mb-2">
                  <Search className="pointer-events-none absolute left-2.5 top-1/2 size-3.5 -translate-y-1/2 text-muted-foreground" />
                  <Input
                    className="h-9 pl-8"
                    placeholder={picker === "prop" ? "Search the godown…" : "Search suppliers…"}
                    value={search}
                    onChange={(e) => setSearch(e.target.value)}
                  />
                </div>
              ) : null}

              <div className="max-h-72 space-y-1 overflow-y-auto">
                {picker === "kit"
                  ? options.kits.map((kit) => (
                      <div
                        key={kit.id}
                        className="flex items-center gap-2 rounded border bg-card px-2 py-1.5"
                      >
                        <div className="min-w-0 flex-1">
                          <div className="truncate text-[12.5px] font-medium">{kit.name}</div>
                          <div className="text-[10.5px] text-muted-foreground">
                            {kit.lineCount} items · {kit.totalPieces} pieces
                            {kit.crewRequired ? ` · ${kit.crewRequired} crew` : ""}
                            {" · "}
                            <span className={cn(kit.canFulfil ? "text-emerald-600" : "text-rose-600")}>
                              {kit.canFulfil ? "can be fielded" : `${kit.shortLines} short`}
                            </span>
                          </div>
                        </div>
                        <Button
                          size="sm"
                          className="h-7 px-2"
                          disabled={busy}
                          onClick={() =>
                            void mutate(
                              () =>
                                quotationResourcesApi.addKit(quotationId, {
                                  propKitId: kit.id,
                                  multiplier: 1,
                                }),
                              `${kit.name} added — ${kit.lineCount} lines.`
                            )
                          }
                        >
                          <Plus className="size-3.5" />
                        </Button>
                      </div>
                    ))
                  : picker === "prop"
                    ? options.props.map((p) => (
                        <div
                          key={p.propItemId}
                          className="flex items-center gap-2 rounded border bg-card px-2 py-1.5"
                        >
                          <PropThumb
                            src={p.primaryThumbnailUrl}
                            alt={p.itemName}
                            className="size-8 shrink-0"
                          />
                          <div className="min-w-0 flex-1">
                            <div className="truncate text-[12.5px] font-medium">{p.itemName}</div>
                            <div className="text-[10.5px] text-muted-foreground">
                              <span className="font-mono">{p.itemCode}</span> ·{" "}
                              <span
                                className={cn(
                                  p.availableQuantity === 0 ? "text-rose-600" : "text-emerald-600"
                                )}
                              >
                                {p.availableQuantity} free
                              </span>{" "}
                              of {p.goodQuantity}
                            </div>
                          </div>
                          <Button
                            size="sm"
                            className="h-7 px-2"
                            disabled={busy || p.availableQuantity <= 0}
                            onClick={() =>
                              void mutate(
                                () =>
                                  quotationResourcesApi.add(quotationId, {
                                    resourceKind: "Prop",
                                    propItemId: p.propItemId,
                                    quantity: 1,
                                  }),
                                `${p.itemName} added.`
                              )
                            }
                          >
                            <Plus className="size-3.5" />
                          </Button>
                        </div>
                      ))
                    : options.vendors.flatMap((v) =>
                        v.rates.length === 0
                          ? []
                          : v.rates.map((rate) => (
                              <div
                                key={`${v.id}-${rate.id}`}
                                className="flex items-center gap-2 rounded border bg-card px-2 py-1.5"
                              >
                                <div className="min-w-0 flex-1">
                                  <div className="truncate text-[12.5px] font-medium">
                                    {rate.name}
                                  </div>
                                  <div className="text-[10.5px] text-muted-foreground">
                                    {v.name} · {humaniseLabel(rate.basis)} · costs{" "}
                                    {formatMoney(rate.rate)}
                                    {rate.sellRate ? ` · bills ${formatMoney(rate.sellRate)}` : ""}
                                    {v.isAvailable === false ? " · fully booked" : ""}
                                  </div>
                                </div>
                                <Button
                                  size="sm"
                                  className="h-7 px-2"
                                  disabled={busy}
                                  onClick={() =>
                                    void mutate(
                                      () =>
                                        quotationResourcesApi.add(quotationId, {
                                          resourceKind: "Vendor",
                                          vendorId: v.id,
                                          vendorRateId: rate.id,
                                        }),
                                      `${rate.name} added.`
                                    )
                                  }
                                >
                                  <Plus className="size-3.5" />
                                </Button>
                              </div>
                            ))
                      )}
              </div>
            </>
          )}
        </div>
      ) : null}

      {/* ---------------- the plan itself ---------------- */}
      {plan.resources.length === 0 ? (
        <p className="rounded-lg border border-dashed py-10 text-center text-[13px] text-muted-foreground">
          Nothing planned yet. Until something is on here, the margin is just the
          quoted price — it does not know what delivering this costs.
        </p>
      ) : (
        <div className="overflow-x-auto rounded-lg border">
          <table className="w-full min-w-[820px] text-[12.5px]">
            <thead className="bg-muted/40">
              <tr className="text-left text-[10.5px] uppercase tracking-wide text-muted-foreground">
                <th className="px-3 py-2 font-medium">Line</th>
                <th className="px-2 py-2 font-medium">Group</th>
                <th className="px-2 py-2 text-right font-medium">Qty</th>
                <th className="px-2 py-2 text-right font-medium">Days</th>
                <th className="px-2 py-2 text-right font-medium">Costs us</th>
                <th className="px-2 py-2 text-right font-medium">Bills</th>
                <th className="px-2 py-2 text-right font-medium">Margin</th>
                <th className="px-2 py-2 font-medium">State</th>
                <th className="w-8" />
              </tr>
            </thead>
            <tbody>
              {plan.resources.map((r) => (
                <ResourceRow
                  key={r.id}
                  resource={r}
                  busy={busy}
                  editable={editable}
                  onRemove={() =>
                    void mutate(
                      () => quotationResourcesApi.remove(quotationId, r.id),
                      "Line removed."
                    )
                  }
                />
              ))}
            </tbody>
            <tfoot className="border-t bg-muted/20">
              <tr className="font-medium">
                <td className="px-3 py-2" colSpan={4}>
                  {plan.resourceCount} lines
                </td>
                <td className="px-2 py-2 text-right tabular-nums">
                  {formatMoney(plan.plannedCost)}
                </td>
                <td className="px-2 py-2 text-right tabular-nums">
                  {formatMoney(plan.resources.reduce((sum, r) => sum + r.lineSell, 0))}
                </td>
                <td className="px-2 py-2 text-right">
                  <MarginNumber
                    value={plan.resources.reduce((sum, r) => sum + r.lineMargin, 0)}
                  />
                </td>
                <td colSpan={2} />
              </tr>
            </tfoot>
          </table>
        </div>
      )}
    </div>
  );
}

function ResourceRow({
  resource: r,
  busy,
  editable,
  onRemove,
}: {
  resource: QuotationResource;
  busy: boolean;
  editable: boolean;
  onRemove: () => void;
}) {
  const Icon = KIND_ICON[r.resourceKind] ?? Sparkles;
  const locked = r.state === "Converted";

  return (
    <tr className="border-t">
      <td className="px-3 py-1.5">
        <div className="flex items-center gap-2">
          {r.primaryThumbnailUrl ? (
            <PropThumb src={r.primaryThumbnailUrl} alt={r.description} className="size-7 shrink-0" />
          ) : (
            <span className="flex size-7 shrink-0 items-center justify-center rounded bg-muted">
              <Icon className="size-3.5 text-muted-foreground" />
            </span>
          )}
          <div className="min-w-0">
            <div className="truncate font-medium">{r.description}</div>
            <div className="text-[10.5px] text-muted-foreground">
              {r.vendorName ?? r.crewMemberName ?? humaniseLabel(r.resourceKind)}
              {r.isShort ? (
                <span className="ml-1 text-rose-600">
                  · only {r.availableQuantity} free
                </span>
              ) : null}
            </div>
          </div>
        </div>
      </td>
      <td className="px-2 py-1.5 text-muted-foreground">{chargeGroupLabel(r.chargeGroup)}</td>
      <td className="px-2 py-1.5 text-right tabular-nums">
        {r.quantity}
        {r.quantityUnit ? (
          <span className="ml-0.5 text-[10.5px] text-muted-foreground">{r.quantityUnit}</span>
        ) : null}
      </td>
      <td className="px-2 py-1.5 text-right tabular-nums text-muted-foreground">{r.days}</td>
      <td className="px-2 py-1.5 text-right tabular-nums">
        {r.lineCost ? formatMoney(r.lineCost) : "—"}
      </td>
      <td className="px-2 py-1.5 text-right tabular-nums">
        {r.lineSell ? formatMoney(r.lineSell) : "—"}
      </td>
      <td className="px-2 py-1.5 text-right">
        {r.lineSell || r.lineCost ? (
          <MarginNumber value={r.lineMargin} className="font-normal" />
        ) : (
          <span className="text-muted-foreground">—</span>
        )}
      </td>
      <td className="px-2 py-1.5">
        <span
          className={cn(
            "rounded px-1.5 py-0.5 text-[10.5px] font-medium",
            STATE_TONE[r.state] ?? "bg-muted"
          )}
        >
          {r.state}
        </span>
      </td>
      <td className="py-1.5 pr-2">
        {editable && !locked ? (
          <button
            type="button"
            disabled={busy}
            className="text-muted-foreground hover:text-destructive"
            onClick={onRemove}
            aria-label={`Remove ${r.description}`}
          >
            <Trash2 className="size-3.5" />
          </button>
        ) : locked ? (
          <Lock className="size-3.5 text-muted-foreground" aria-label="Already booked" />
        ) : null}
      </td>
    </tr>
  );
}
