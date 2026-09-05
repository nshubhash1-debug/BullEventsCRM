"use client";

import * as React from "react";
import { BadgeIndianRupee, Check, Save, Search, Wand2 } from "lucide-react";
import { toast } from "sonner";

import { PropThumb } from "@/components/props/prop-thumb";
import { PagePanel } from "@/components/shell/page-panel";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { ApiError } from "@/lib/api";
import { formatMoney } from "@/lib/crm-api";
import type { WireFilterNode } from "@/lib/query";
import {
  propsApi,
  type PropItem,
  type PropPricingSummary,
  type PropRateRow,
} from "@/lib/props-api";
import { cn } from "@/lib/utils";

const PAGE_SIZE = 100;

/** What a row is holding that has not been saved yet. */
type Draft = { rental?: string; replacement?: string; location?: string };

function num(value: string | undefined) {
  if (value === undefined || value.trim() === "") return undefined;
  const parsed = Number(value);
  return Number.isFinite(parsed) && parsed >= 0 ? parsed : undefined;
}

export default function PropRatesPage() {
  const [summary, setSummary] = React.useState<PropPricingSummary | null>(null);
  const [items, setItems] = React.useState<PropItem[]>([]);
  const [total, setTotal] = React.useState(0);
  const [page, setPage] = React.useState(1);

  const [search, setSearch] = React.useState("");
  const [debounced, setDebounced] = React.useState("");
  const [categoryId, setCategoryId] = React.useState<number | null>(null);
  const [unpricedOnly, setUnpricedOnly] = React.useState(true);

  const [drafts, setDrafts] = React.useState<Record<number, Draft>>({});
  const [saving, setSaving] = React.useState(false);
  const [nonce, setNonce] = React.useState(0);
  const [loadedKey, setLoadedKey] = React.useState<string | null>(null);

  /* The two bulk helpers, which are what make 734 rows an afternoon. */
  const [fillRental, setFillRental] = React.useState("");
  const [fillReplacement, setFillReplacement] = React.useState("");
  const [derivePercent, setDerivePercent] = React.useState("5");

  React.useEffect(() => {
    const timer = setTimeout(() => setDebounced(search), 300);
    return () => clearTimeout(timer);
  }, [search]);

  const reload = React.useCallback(() => setNonce((n) => n + 1), []);

  React.useEffect(() => {
    propsApi.pricingCoverage().then(setSummary).catch(() => setSummary(null));
  }, [nonce]);

  const queryKey = JSON.stringify({ debounced, categoryId, unpricedOnly, page, nonce });
  const loading = loadedKey !== queryKey;

  React.useEffect(() => {
    let cancelled = false;

    const filters: WireFilterNode[] = [];
    if (categoryId) {
      filters.push({ field: "categoryId", operator: "equals", value: String(categoryId) });
    }
    if (unpricedOnly) {
      filters.push({ field: "rentalRatePerDay", operator: "isNull" });
    }

    propsApi
      .query({
        search: debounced || undefined,
        page,
        pageSize: PAGE_SIZE,
        sort: [{ field: "name", descending: false }],
        filter: filters.length ? { conjunction: "and", children: filters } : null,
      })
      .then((result) => {
        if (cancelled) return;
        setItems(result.items);
        setTotal(result.total);
      })
      .catch(() => {
        if (cancelled) return;
        setItems([]);
        setTotal(0);
      })
      .finally(() => {
        if (!cancelled) setLoadedKey(queryKey);
      });

    return () => {
      cancelled = true;
    };
  }, [debounced, categoryId, unpricedOnly, page, nonce, queryKey]);

  function narrow(apply: () => void) {
    apply();
    setPage(1);
    setDrafts({});
  }

  function edit(id: number, field: keyof Draft, value: string) {
    setDrafts((d) => ({ ...d, [id]: { ...d[id], [field]: value } }));
  }

  /** The value a cell shows: what has been typed, else what is stored. */
  function shown(item: PropItem, field: keyof Draft) {
    const draft = drafts[item.id]?.[field];
    if (draft !== undefined) return draft;

    if (field === "rental") return item.rentalRatePerDay?.toString() ?? "";
    if (field === "replacement") return item.replacementValue?.toString() ?? "";
    return item.storageLocation ?? "";
  }

  /** Puts one number down every visible row's rate column. */
  function fillDown(field: "rental" | "replacement", value: string) {
    if (num(value) === undefined) {
      toast.error("Enter a number first.");
      return;
    }
    setDrafts((d) => {
      const next = { ...d };
      items.forEach((item) => {
        next[item.id] = { ...next[item.id], [field]: value };
      });
      return next;
    });
    toast.success(`Filled ${items.length} rows. Nothing is saved until you press Save.`);
  }

  /**
   * Derives a daily rental from the replacement value.
   *
   * The rule of thumb rental businesses actually use: a piece should pay for
   * itself in roughly twenty hires, which is a daily rate of about 5% of what
   * replacing it costs. The percentage is editable because that number is a
   * commercial decision, not a law.
   */
  function deriveFromReplacement() {
    const percent = num(derivePercent);
    if (percent === undefined || percent <= 0) {
      toast.error("Enter a percentage.");
      return;
    }

    let filled = 0;
    setDrafts((d) => {
      const next = { ...d };
      items.forEach((item) => {
        const replacement = num(shown(item, "replacement"));
        if (replacement === undefined || replacement <= 0) return;
        next[item.id] = {
          ...next[item.id],
          rental: String(Math.round((replacement * percent) / 100)),
        };
        filled++;
      });
      return next;
    });

    if (filled === 0) {
      toast.error("No visible row has a replacement value to work from yet.");
    } else {
      toast.success(`Derived ${filled} rental rates at ${percent}% of replacement value.`);
    }
  }

  const dirty = Object.keys(drafts).length;

  async function save() {
    const rows: PropRateRow[] = [];

    for (const [id, draft] of Object.entries(drafts)) {
      const row: PropRateRow = { propItemId: Number(id) };
      let touched = false;

      if (draft.rental !== undefined) {
        row.rentalRatePerDay = num(draft.rental) ?? 0;
        touched = true;
      }
      if (draft.replacement !== undefined) {
        row.replacementValue = num(draft.replacement) ?? 0;
        touched = true;
      }
      if (draft.location !== undefined) {
        row.storageLocation = draft.location;
        touched = true;
      }

      if (touched) rows.push(row);
    }

    if (rows.length === 0) {
      toast.error("Nothing has been changed.");
      return;
    }

    setSaving(true);
    try {
      const result = await propsApi.setRates(rows);
      toast.success(
        `${result.updated} items priced. ${result.stillUnpriced} of ${result.totalItems} still without a rate.`
      );
      result.warnings.forEach((w) => toast.warning(w));
      setDrafts({});
      reload();
    } catch (error) {
      toast.error(error instanceof ApiError ? error.message : "Could not save that.");
    } finally {
      setSaving(false);
    }
  }

  const pageCount = Math.max(1, Math.ceil(total / PAGE_SIZE));
  const progress = summary && summary.totalItems > 0
    ? summary.priced / summary.totalItems
    : 0;

  return (
    <PagePanel
      icon={BadgeIndianRupee}
      title="Rate card"
      hint="The register came out of a workbook with no rate column. Until a line carries a rental rate it cannot be quoted, valued or billed for when it breaks."
      actions={
        dirty > 0 ? (
          <Button size="sm" className="h-8" disabled={saving} onClick={() => void save()}>
            <Save className="mr-1 size-3.5" />
            {saving ? "Saving…" : `Save ${dirty} row${dirty === 1 ? "" : "s"}`}
          </Button>
        ) : null
      }
      toolbar={
        <div className="flex flex-wrap items-end gap-2">
          <div className="relative">
            <Search className="pointer-events-none absolute left-2.5 top-1/2 size-3.5 -translate-y-1/2 text-muted-foreground" />
            <Input
              className="h-9 w-52 pl-8"
              placeholder="Search the register…"
              value={search}
              onChange={(e) => narrow(() => setSearch(e.target.value))}
            />
          </div>

          <select
            className="h-9 rounded-md border bg-background px-2 text-sm"
            value={categoryId ?? ""}
            onChange={(e) =>
              narrow(() => setCategoryId(e.target.value ? Number(e.target.value) : null))
            }
          >
            <option value="">All categories</option>
            {(summary?.byCategory ?? []).map((c) => (
              <option key={c.categoryId} value={c.categoryId}>
                {c.categoryName} — {c.unpriced} unpriced
              </option>
            ))}
          </select>

          <Button
            size="sm"
            variant={unpricedOnly ? "secondary" : "outline"}
            className="h-9"
            onClick={() => narrow(() => setUnpricedOnly((v) => !v))}
          >
            Unpriced only
          </Button>
        </div>
      }
      subToolbar={
        <span className="text-[12px] text-muted-foreground">
          {loading
            ? "Loading…"
            : `${total.toLocaleString()} items${
                pageCount > 1 ? ` · page ${page} of ${pageCount}` : ""
              }`}
          {dirty > 0 ? ` · ${dirty} unsaved` : ""}
        </span>
      }
    >
      <div className="min-h-0 flex-1 space-y-4 overflow-y-auto px-5 pb-5">
        {/* ---------------- how far along ---------------- */}
        {summary ? (
          <div className="rounded-lg border bg-card p-3">
            <div className="flex flex-wrap items-baseline justify-between gap-2">
              <div className="text-[13px]">
                <span className="text-lg font-semibold tabular-nums">
                  {summary.priced.toLocaleString()}
                </span>
                <span className="text-muted-foreground">
                  {" "}
                  of {summary.totalItems.toLocaleString()} priced
                </span>
                {summary.averageRate ? (
                  <span className="ml-2 text-muted-foreground">
                    · averaging {formatMoney(summary.averageRate)}/day
                  </span>
                ) : null}
              </div>
              <div className="text-[13px]">
                <span className="text-muted-foreground">Catalogue value </span>
                <span className="font-semibold tabular-nums">
                  {formatMoney(summary.catalogueValue)}
                </span>
              </div>
            </div>

            <div className="mt-2 h-2 overflow-hidden rounded bg-muted">
              <div
                className={cn(
                  "h-full rounded transition-all",
                  progress === 1 ? "bg-emerald-500" : "bg-primary/70"
                )}
                style={{ width: `${Math.round(progress * 100)}%` }}
              />
            </div>

            {summary.unpriced > 0 ? (
              <div className="mt-2 flex flex-wrap gap-1.5">
                {summary.byCategory
                  .filter((c) => c.unpriced > 0)
                  .map((c) => (
                    <button
                      key={c.categoryId}
                      type="button"
                      onClick={() => narrow(() => setCategoryId(c.categoryId))}
                      className={cn(
                        "rounded border px-2 py-1 text-[11.5px] transition hover:border-primary/50",
                        categoryId === c.categoryId ? "border-primary bg-primary/5" : ""
                      )}
                    >
                      {c.categoryName}
                      <span className="ml-1 tabular-nums text-amber-600">{c.unpriced}</span>
                    </button>
                  ))}
              </div>
            ) : (
              <p className="mt-2 text-[12.5px] text-emerald-600 dark:text-emerald-400">
                <Check className="mr-1 inline size-3.5" />
                Every line is priced. Décor now costs itself on a proposal.
              </p>
            )}
          </div>
        ) : null}

        {/* ---------------- bulk helpers ---------------- */}
        <div className="rounded-lg border bg-muted/30 p-3">
          <div className="mb-2 flex items-center gap-1.5 text-[13px] font-medium">
            <Wand2 className="size-3.5 text-muted-foreground" />
            Fill the {items.length} rows on this page
          </div>

          <div className="flex flex-wrap items-end gap-2">
            <div>
              <Label className="text-[11px] text-muted-foreground">Replacement value</Label>
              <div className="mt-1 flex gap-1">
                <Input
                  className="h-9 w-28"
                  placeholder="e.g. 2000"
                  value={fillReplacement}
                  onChange={(e) => setFillReplacement(e.target.value)}
                />
                <Button
                  size="sm"
                  variant="outline"
                  className="h-9"
                  onClick={() => fillDown("replacement", fillReplacement)}
                >
                  Fill down
                </Button>
              </div>
            </div>

            <div>
              <Label className="text-[11px] text-muted-foreground">Rental per day</Label>
              <div className="mt-1 flex gap-1">
                <Input
                  className="h-9 w-28"
                  placeholder="e.g. 100"
                  value={fillRental}
                  onChange={(e) => setFillRental(e.target.value)}
                />
                <Button
                  size="sm"
                  variant="outline"
                  className="h-9"
                  onClick={() => fillDown("rental", fillRental)}
                >
                  Fill down
                </Button>
              </div>
            </div>

            <div>
              <Label className="text-[11px] text-muted-foreground">
                Or derive rental from replacement
              </Label>
              <div className="mt-1 flex gap-1">
                <Input
                  className="h-9 w-16"
                  value={derivePercent}
                  onChange={(e) => setDerivePercent(e.target.value)}
                />
                <span className="flex h-9 items-center text-[13px] text-muted-foreground">%</span>
                <Button size="sm" variant="outline" className="h-9" onClick={deriveFromReplacement}>
                  Derive
                </Button>
              </div>
            </div>
          </div>

          <p className="mt-2 text-[11.5px] leading-relaxed text-muted-foreground">
            5% is the usual rule of thumb — a piece pays for itself in about twenty
            hires. It is a starting point, not a price: change it per category, and
            change the rows that are worth more than the rule says. Nothing is written
            until you press Save.
          </p>
        </div>

        {/* ---------------- the grid ---------------- */}
        <div className="overflow-x-auto rounded-lg border">
          <table className="w-full min-w-[820px] text-[12.5px]">
            <thead className="bg-muted/40">
              <tr className="text-left text-[10.5px] uppercase tracking-wide text-muted-foreground">
                <th className="px-3 py-2 font-medium">Item</th>
                <th className="px-2 py-2 font-medium">Category</th>
                <th className="px-2 py-2 text-right font-medium">Stock</th>
                <th className="px-2 py-2 text-right font-medium">Replacement</th>
                <th className="px-2 py-2 text-right font-medium">Rental / day</th>
                <th className="px-2 py-2 font-medium">Shelf</th>
                <th className="px-2 py-2 text-right font-medium">Line value</th>
              </tr>
            </thead>
            <tbody>
              {items.map((item) => {
                const rental = num(shown(item, "rental"));
                const replacement = num(shown(item, "replacement"));
                const changed = drafts[item.id] !== undefined;

                return (
                  <tr
                    key={item.id}
                    className={cn("border-t", changed ? "bg-primary/5" : "")}
                  >
                    <td className="px-3 py-1.5">
                      <div className="flex items-center gap-2">
                        <PropThumb
                          src={item.primaryThumbnailUrl}
                          alt={item.name}
                          className="size-8 shrink-0"
                        />
                        <div className="min-w-0">
                          <div className="truncate font-medium">{item.name}</div>
                          <div className="font-mono text-[10px] text-muted-foreground">
                            {item.code}
                            {item.size ? ` · ${item.size}` : ""}
                          </div>
                        </div>
                      </div>
                    </td>
                    <td className="px-2 py-1.5 text-muted-foreground">{item.categoryName}</td>
                    <td className="px-2 py-1.5 text-right tabular-nums text-muted-foreground">
                      {item.goodQuantity}
                      <span className="ml-0.5 text-[10px]">{item.unit.toLowerCase()}</span>
                    </td>
                    <td className="px-2 py-1.5 text-right">
                      <Input
                        className="ml-auto h-7 w-24 text-right text-[12px] tabular-nums"
                        placeholder="—"
                        value={shown(item, "replacement")}
                        onChange={(e) => edit(item.id, "replacement", e.target.value)}
                      />
                    </td>
                    <td className="px-2 py-1.5 text-right">
                      <Input
                        className={cn(
                          "ml-auto h-7 w-24 text-right text-[12px] tabular-nums",
                          rental === undefined || rental === 0
                            ? "border-amber-300 dark:border-amber-900"
                            : ""
                        )}
                        placeholder="—"
                        value={shown(item, "rental")}
                        onChange={(e) => edit(item.id, "rental", e.target.value)}
                      />
                    </td>
                    <td className="px-2 py-1.5">
                      <Input
                        className="h-7 w-28 text-[12px]"
                        placeholder="Rack / shelf"
                        value={shown(item, "location")}
                        onChange={(e) => edit(item.id, "location", e.target.value)}
                      />
                    </td>
                    <td className="px-2 py-1.5 text-right tabular-nums text-muted-foreground">
                      {replacement ? formatMoney(replacement * item.goodQuantity) : "—"}
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>

        {!loading && items.length === 0 ? (
          <p className="py-12 text-center text-[13px] text-muted-foreground">
            {unpricedOnly
              ? "Nothing left unpriced here."
              : "Nothing matches that."}
          </p>
        ) : null}

        {pageCount > 1 ? (
          <div className="flex items-center justify-center gap-2">
            <Button
              size="sm"
              variant="outline"
              className="h-8"
              disabled={page <= 1}
              onClick={() => {
                setDrafts({});
                setPage((p) => p - 1);
              }}
            >
              Previous
            </Button>
            <span className="text-[12px] text-muted-foreground">
              {page} / {pageCount}
            </span>
            <Button
              size="sm"
              variant="outline"
              className="h-8"
              disabled={page >= pageCount}
              onClick={() => {
                setDrafts({});
                setPage((p) => p + 1);
              }}
            >
              Next
            </Button>
          </div>
        ) : null}

        {dirty > 0 ? (
          <div className="sticky bottom-0 flex items-center justify-between rounded-lg border bg-card px-4 py-2.5 shadow-lg">
            <span className="text-[13px]">
              <span className="font-semibold tabular-nums">{dirty}</span> row
              {dirty === 1 ? "" : "s"} changed but not saved
            </span>
            <div className="flex gap-2">
              <Button size="sm" variant="outline" className="h-8" onClick={() => setDrafts({})}>
                Discard
              </Button>
              <Button size="sm" className="h-8" disabled={saving} onClick={() => void save()}>
                <Save className="mr-1 size-3.5" />
                {saving ? "Saving…" : "Save"}
              </Button>
            </div>
          </div>
        ) : null}
      </div>
    </PagePanel>
  );
}
