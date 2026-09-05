"use client";

import * as React from "react";
import { AlertTriangle, LayoutGrid, Rows3, Search, Sparkles } from "lucide-react";

import { PropDetailSheet } from "@/components/props/prop-detail-sheet";
import { PropThumb } from "@/components/props/prop-thumb";
import { PagePanel } from "@/components/shell/page-panel";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { formatMoney } from "@/lib/crm-api";
import type { WireFilterNode } from "@/lib/query";
import {
  PROP_ITEM_TYPES,
  propsApi,
  type PropCategory,
  type PropItem,
} from "@/lib/props-api";
import { cn } from "@/lib/utils";

const PAGE_SIZE = 60;

export default function PropCataloguePage() {
  const [categories, setCategories] = React.useState<PropCategory[]>([]);
  const [items, setItems] = React.useState<PropItem[]>([]);
  const [total, setTotal] = React.useState(0);
  const [page, setPage] = React.useState(1);

  /**
   * Which query the rows on screen belong to.
   *
   * Loading is derived from this rather than stored, so the effect below never
   * has to set state synchronously just to raise a spinner — the rows are stale
   * exactly while the key they were fetched for is not the current one.
   */
  const [loadedKey, setLoadedKey] = React.useState<string | null>(null);

  const [search, setSearch] = React.useState("");
  const [categoryId, setCategoryId] = React.useState<number | null>(null);
  const [itemType, setItemType] = React.useState<string>("");
  const [view, setView] = React.useState<"grid" | "list">("grid");

  const [selected, setSelected] = React.useState<PropItem | null>(null);

  // Narrowing the results always returns to page one — done here, in the
  // handlers, rather than in an effect watching the filters, so changing a
  // filter is a single render instead of a fetch on the old page followed by a
  // second on the new one.
  function narrow(apply: () => void) {
    apply();
    setPage(1);
  }

  React.useEffect(() => {
    propsApi.categories().then(setCategories).catch(() => setCategories([]));
  }, []);

  // The search box drives a server query, so it is debounced — a 734-row
  // catalogue would otherwise fire a request per keystroke.
  const [debounced, setDebounced] = React.useState("");
  React.useEffect(() => {
    const timer = setTimeout(() => setDebounced(search), 300);
    return () => clearTimeout(timer);
  }, [search]);

  const queryKey = JSON.stringify({ debounced, categoryId, itemType, page });
  const loading = loadedKey !== queryKey;

  React.useEffect(() => {
    let cancelled = false;

    const filters: WireFilterNode[] = [];
    if (categoryId) {
      filters.push({ field: "categoryId", operator: "equals", value: String(categoryId) });
    }
    if (itemType) {
      filters.push({ field: "itemType", operator: "equals", value: itemType });
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
        if (!cancelled) {
          setItems([]);
          setTotal(0);
        }
      })
      .finally(() => {
        if (!cancelled) setLoadedKey(queryKey);
      });

    return () => {
      cancelled = true;
    };
  }, [debounced, categoryId, itemType, page, queryKey]);

  const pageCount = Math.max(1, Math.ceil(total / PAGE_SIZE));

  function applyChange(updated: PropItem) {
    setItems((rows) => rows.map((r) => (r.id === updated.id ? updated : r)));
    setSelected(updated);
  }

  return (
    <PagePanel
      icon={Sparkles}
      title="Décor & props"
      hint="The godown register: every prop, its photograph, and how many are usable right now."
      actions={
        <div className="flex items-center gap-1 rounded-md border p-0.5">
          <Button
            size="sm"
            variant={view === "grid" ? "secondary" : "ghost"}
            className="h-7 px-2"
            onClick={() => setView("grid")}
          >
            <LayoutGrid className="size-3.5" />
          </Button>
          <Button
            size="sm"
            variant={view === "list" ? "secondary" : "ghost"}
            className="h-7 px-2"
            onClick={() => setView("list")}
          >
            <Rows3 className="size-3.5" />
          </Button>
        </div>
      }
      toolbar={
        <div className="flex flex-wrap items-center gap-2">
          <div className="relative">
            <Search className="pointer-events-none absolute left-2.5 top-1/2 size-3.5 -translate-y-1/2 text-muted-foreground" />
            <Input
              className="h-9 w-64 pl-8"
              placeholder="Search props, sizes, tags…"
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
            {categories.map((c) => (
              <option key={c.id} value={c.id}>
                {c.name} ({c.itemCount})
              </option>
            ))}
          </select>

          <select
            className="h-9 rounded-md border bg-background px-2 text-sm"
            value={itemType}
            onChange={(e) => narrow(() => setItemType(e.target.value))}
          >
            <option value="">All types</option>
            {PROP_ITEM_TYPES.map((t) => (
              <option key={t} value={t}>
                {t}
              </option>
            ))}
          </select>

          {search || categoryId || itemType ? (
            <Button
              size="sm"
              variant="ghost"
              className="h-9"
              onClick={() =>
                narrow(() => {
                  setSearch("");
                  setCategoryId(null);
                  setItemType("");
                })
              }
            >
              Clear
            </Button>
          ) : null}
        </div>
      }
      subToolbar={
        <span className="text-[12px] text-muted-foreground">
          {loading ? "Loading…" : `${total.toLocaleString()} items`}
          {total > 0 && !loading
            ? ` · page ${page} of ${pageCount}`
            : ""}
        </span>
      }
    >
      <div className="min-h-0 flex-1 overflow-y-auto px-5 pb-5">
        {!loading && items.length === 0 ? (
          <p className="py-16 text-center text-[13px] text-muted-foreground">
            Nothing matches that. Try a shorter search.
          </p>
        ) : view === "grid" ? (
          <div className="grid grid-cols-2 gap-3 sm:grid-cols-3 lg:grid-cols-4 xl:grid-cols-6">
            {items.map((item) => (
              <button
                key={item.id}
                type="button"
                onClick={() => setSelected(item)}
                className="group flex flex-col overflow-hidden rounded-lg border bg-card text-left transition hover:border-primary/50 hover:shadow-sm"
              >
                <PropThumb
                  src={item.primaryThumbnailUrl}
                  alt={item.name}
                  className="aspect-square w-full"
                  rounded="rounded-none"
                />

                <div className="flex flex-1 flex-col gap-1 p-2.5">
                  <div className="line-clamp-2 text-[12.5px] font-medium leading-snug">
                    {item.name}
                  </div>

                  <div className="mt-auto flex items-center justify-between gap-1 pt-1">
                    <span className="font-mono text-[10.5px] text-muted-foreground">
                      {item.code}
                    </span>
                    <span
                      className={cn(
                        "rounded px-1.5 py-0.5 text-[11px] font-semibold",
                        item.goodQuantity === 0
                          ? "bg-muted text-muted-foreground"
                          : "bg-emerald-50 text-emerald-700 dark:bg-emerald-950/40 dark:text-emerald-400"
                      )}
                    >
                      {item.goodQuantity} {item.unit.toLowerCase()}
                    </span>
                  </div>

                  {item.size ? (
                    <div className="truncate text-[11px] text-muted-foreground">
                      {item.size}
                      {item.colour ? ` · ${item.colour}` : ""}
                    </div>
                  ) : null}
                </div>
              </button>
            ))}
          </div>
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full min-w-[720px] text-[13px]">
              <thead className="sticky top-0 bg-card">
                <tr className="border-b text-left text-[11px] uppercase tracking-wide text-muted-foreground">
                  <th className="py-2 pr-2 font-medium">Item</th>
                  <th className="py-2 pr-2 font-medium">Code</th>
                  <th className="py-2 pr-2 font-medium">Category</th>
                  <th className="py-2 pr-2 font-medium">Size</th>
                  <th className="py-2 pr-2 text-right font-medium">Usable</th>
                  <th className="py-2 pr-2 text-right font-medium">Repair</th>
                  <th className="py-2 pr-2 text-right font-medium">Damaged</th>
                  <th className="py-2 text-right font-medium">Rate/day</th>
                </tr>
              </thead>
              <tbody>
                {items.map((item) => (
                  <tr
                    key={item.id}
                    onClick={() => setSelected(item)}
                    className="cursor-pointer border-b last:border-0 hover:bg-muted/50"
                  >
                    <td className="py-1.5 pr-2">
                      <div className="flex items-center gap-2">
                        <PropThumb
                          src={item.primaryThumbnailUrl}
                          alt={item.name}
                          className="size-8 shrink-0"
                        />
                        <span className="font-medium">{item.name}</span>
                        {item.isBelowReorderLevel ? (
                          <AlertTriangle className="size-3.5 text-amber-500" />
                        ) : null}
                      </div>
                    </td>
                    <td className="py-1.5 pr-2 font-mono text-[11.5px] text-muted-foreground">
                      {item.code}
                    </td>
                    <td className="py-1.5 pr-2 text-muted-foreground">{item.categoryName}</td>
                    <td className="py-1.5 pr-2 text-muted-foreground">{item.size ?? "—"}</td>
                    <td className="py-1.5 pr-2 text-right font-semibold">{item.goodQuantity}</td>
                    <td className="py-1.5 pr-2 text-right text-muted-foreground">
                      {item.repairableQuantity || "—"}
                    </td>
                    <td className="py-1.5 pr-2 text-right text-muted-foreground">
                      {item.damagedQuantity || "—"}
                    </td>
                    <td className="py-1.5 text-right text-muted-foreground">
                      {item.rentalRatePerDay ? formatMoney(item.rentalRatePerDay) : "—"}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}

        {pageCount > 1 ? (
          <div className="mt-4 flex items-center justify-center gap-2">
            <Button
              size="sm"
              variant="outline"
              className="h-8"
              disabled={page <= 1}
              onClick={() => setPage((p) => p - 1)}
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
              onClick={() => setPage((p) => p + 1)}
            >
              Next
            </Button>
          </div>
        ) : null}
      </div>

      {/* Keyed by the item, so opening a second prop starts its sheet clean. */}
      <PropDetailSheet
        key={selected?.id ?? "none"}
        item={selected}
        open={selected !== null}
        onOpenChange={(open) => !open && setSelected(null)}
        onChanged={applyChange}
      />
    </PagePanel>
  );
}
