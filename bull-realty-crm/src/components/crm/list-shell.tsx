"use client";

import * as React from "react";
import { ListFilter, RotateCw, Search, SlidersHorizontal, X } from "lucide-react";
import type { LucideIcon } from "lucide-react";
import { toast } from "sonner";

import { CrmGrid, type GridColumn } from "@/components/crm/crm-grid";
import {
  DateRangeChip,
  DateRangeFilter,
} from "@/components/crm/date-range-filter";
import {
  FilterBuilder,
  FilterChips,
  type SavedView,
} from "@/components/crm/filter-builder";
import { MetricStrip, type Metric } from "@/components/crm/metrics";
import { PagePanel } from "@/components/shell/page-panel";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import type { CrmListState } from "@/hooks/use-crm-list";
import { isRangeActive } from "@/lib/date-range";
import { usePersistedState } from "@/lib/persisted-store";
import { emptyRoot, type FilterNode } from "@/lib/query";
import { cn } from "@/lib/utils";

/** A one-click preset above the grid — the "list views" pattern. */
export interface QuickView {
  id: string;
  label: string;
  /** Built lazily so presets can reference "today" at click time, not load time. */
  build: () => FilterNode | null;
  count?: number;
}

/**
 * Module-level so the persisted-state snapshot stays referentially stable — a
 * fresh `[]` on every render would make `useSyncExternalStore` loop.
 */
const NO_SAVED_VIEWS: SavedView[] = [];

interface ListShellProps<T extends { id: number }> {
  icon: LucideIcon;
  title: string;
  hint?: string;
  /** Namespace for persisted saved views and grid preferences. */
  storageKey: string;

  state: CrmListState<T>;
  columns: GridColumn<T>[];
  metrics?: Metric[];
  /** Rendered under the metric strip — distribution bars, charts. */
  visuals?: React.ReactNode;

  quickViews?: QuickView[];
  actions?: React.ReactNode;
  /** Bulk-action bar, shown when rows are selected. */
  bulkActions?: (selected: number[]) => React.ReactNode;
  onRowClick?: (row: T) => void;
  searchPlaceholder?: string;
  emptyMessage?: string;
}

/**
 * The frame every list view in the workspace shares: metrics, quick views,
 * search, the nested filter builder, and the dense grid.
 *
 * Pages supply their columns, their metrics and their presets — the plumbing
 * for filtering, paging, selection and saved views is identical everywhere, and
 * lives here rather than being re-implemented ten times.
 */
export function ListShell<T extends { id: number }>({
  icon,
  title,
  hint,
  storageKey,
  state,
  columns,
  metrics,
  visuals,
  quickViews = [],
  actions,
  bulkActions,
  onRowClick,
  searchPlaceholder = "Search…",
  emptyMessage,
}: ListShellProps<T>) {
  const [builderOpen, setBuilderOpen] = React.useState(false);
  const [activeQuickView, setActiveQuickView] = React.useState<string | null>(null);
  const [savedViews, setSavedViews] = usePersistedState<SavedView[]>(
    `brg.views.${storageKey}`,
    NO_SAVED_VIEWS
  );

  const selectedIds = React.useMemo(() => [...state.selected], [state.selected]);

  const dateActive = isRangeActive(state.dateRange);

  const clearDateRange = React.useCallback(() => {
    state.setDateRange({
      ...state.dateRange,
      preset: "all",
      from: undefined,
      to: undefined,
    });
  }, [state]);

  function applyQuickView(view: QuickView) {
    if (activeQuickView === view.id) {
      setActiveQuickView(null);
      state.clearFilter();
      return;
    }

    setActiveQuickView(view.id);
    const built = view.build();
    state.setFilter(built ?? emptyRoot());
  }

  return (
    <PagePanel
      flush
      icon={icon}
      title={title}
      hint={hint}
      actions={
        <>
          {actions}
          <Button
            variant="ghost"
            size="icon"
            aria-label="Refresh"
            title="Refresh"
            className="size-8 text-muted-foreground"
            onClick={state.refresh}
          >
            <RotateCw className={cn("size-4", state.loading && "animate-spin")} />
          </Button>
        </>
      }
      toolbar={
        <>
          <div className="relative">
            <Search className="pointer-events-none absolute top-1/2 left-2.5 size-3.5 -translate-y-1/2 text-muted-foreground" />
            <Input
              value={state.search}
              onChange={(event) => state.setSearch(event.target.value)}
              placeholder={searchPlaceholder}
              className="h-8 w-60 pl-8 text-[13px]"
            />
            {state.search ? (
              <button
                type="button"
                aria-label="Clear search"
                onClick={() => state.setSearch("")}
                className="absolute top-1/2 right-2 -translate-y-1/2 text-muted-foreground hover:text-foreground"
              >
                <X className="size-3.5" />
              </button>
            ) : null}
          </div>

          {quickViews.length > 0 ? (
            <div className="flex flex-wrap items-center gap-1">
              {quickViews.map((view) => (
                <button
                  key={view.id}
                  type="button"
                  onClick={() => applyQuickView(view)}
                  className={cn(
                    "inline-flex h-8 items-center gap-1.5 rounded px-2.5 text-[12.5px] transition-colors",
                    activeQuickView === view.id
                      ? "bg-primary font-medium text-primary-foreground"
                      : "border text-foreground/75 hover:bg-accent"
                  )}
                >
                  {view.label}
                  {view.count !== undefined ? (
                    <span
                      className={cn(
                        "tabular-nums",
                        activeQuickView === view.id
                          ? "text-primary-foreground/75"
                          : "text-muted-foreground"
                      )}
                    >
                      {view.count.toLocaleString()}
                    </span>
                  ) : null}
                </button>
              ))}
            </div>
          ) : null}

          <div className="ml-auto flex items-center gap-1.5">
            <DateRangeFilter
              fields={state.dateFields}
              range={state.dateRange}
              onChange={state.setDateRange}
            />

            <Button
              variant={
                builderOpen || state.conditionCount > 0 ? "secondary" : "outline"
              }
              size="sm"
              className="h-8"
              onClick={() => setBuilderOpen((open) => !open)}
            >
              <SlidersHorizontal /> Advanced filter
              {state.conditionCount > 0 ? (
                <Badge
                  variant="secondary"
                  className="ml-0.5 h-4 px-1 text-[10px] tabular-nums"
                >
                  {state.conditionCount}
                </Badge>
              ) : null}
            </Button>
          </div>
        </>
      }
      subToolbar={
        builderOpen ? (
          <FilterBuilder
            fields={state.fields}
            filter={state.filter}
            onChange={(next) => {
              setActiveQuickView(null);
              state.setFilter(next);
            }}
            matchCount={state.total}
            totalCount={state.total}
            savedViews={savedViews}
            onSaveView={(name) => {
              setSavedViews([
                ...savedViews,
                { id: `v-${Date.now()}`, name, filter: state.filter },
              ]);
              toast.success(`View “${name}” saved`);
            }}
            onLoadView={(view) => {
              setActiveQuickView(null);
              state.setFilter(view.filter);
            }}
            onDeleteView={(id) =>
              setSavedViews(savedViews.filter((view) => view.id !== id))
            }
          />
        ) : state.conditionCount > 0 || dateActive ? (
          <div className="flex w-full flex-wrap items-center gap-2">
            <ListFilter className="size-3.5 shrink-0" />

            <DateRangeChip
              fields={state.dateFields}
              range={state.dateRange}
              onClear={clearDateRange}
            />

            <FilterChips
              filter={state.filter}
              fields={state.fields}
              onChange={state.setFilter}
            />

            <Button
              variant="ghost"
              size="sm"
              className="ml-auto h-6 text-[11.5px]"
              onClick={() => {
                setActiveQuickView(null);
                state.clearFilter();
                clearDateRange();
              }}
            >
              <X className="size-3" /> Clear
            </Button>
          </div>
        ) : null
      }
    >
      <div className="flex min-h-0 flex-1 flex-col">
        {metrics && metrics.length > 0 ? (
          <div className="border-b p-2.5">
            <MetricStrip metrics={metrics} loading={state.initialising} />
            {visuals ? <div className="mt-2.5">{visuals}</div> : null}
          </div>
        ) : visuals ? (
          <div className="border-b p-2.5">{visuals}</div>
        ) : null}

        <CrmGrid
          storageKey={storageKey}
          columns={columns}
          rows={state.rows}
          loading={state.loading}
          initialising={state.initialising}
          sort={state.sort}
          onToggleSort={state.toggleSort}
          page={state.page}
          pageCount={state.pageCount}
          pageSize={state.pageSize}
          total={state.total}
          onPageChange={state.setPage}
          onPageSizeChange={state.setPageSize}
          selected={bulkActions ? state.selected : undefined}
          onToggleRow={bulkActions ? state.toggleRow : undefined}
          onToggleAll={bulkActions ? state.toggleAll : undefined}
          onRowClick={onRowClick}
          emptyMessage={emptyMessage}
          banner={
            bulkActions && selectedIds.length > 0 ? (
              <div className="flex flex-wrap items-center gap-2 border-b bg-primary/5 px-2.5 py-1.5">
                <span className="text-[12px] font-medium">
                  {selectedIds.length} selected
                </span>
                {bulkActions(selectedIds)}
                <Button
                  variant="ghost"
                  size="sm"
                  className="ml-auto h-6 text-[11.5px]"
                  onClick={state.clearSelection}
                >
                  <X className="size-3" /> Clear
                </Button>
              </div>
            ) : null
          }
        />
      </div>
    </PagePanel>
  );
}
