"use client";

import * as React from "react";
import {
  ArrowDown,
  ArrowUp,
  ChevronLeft,
  ChevronRight,
  ChevronsLeft,
  ChevronsRight,
  ChevronsUpDown,
  Columns3,
  Rows2,
  Rows3,
  Rows4,
} from "lucide-react";

import { Button } from "@/components/ui/button";
import { Checkbox } from "@/components/ui/checkbox";
import {
  DropdownMenu,
  DropdownMenuCheckboxItem,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
import { ScrollArea, ScrollBar } from "@/components/ui/scroll-area";
import { CrmLoadingState, useMinimumLoading } from "@/components/shell/crm-loader";
import { usePersistedState } from "@/lib/persisted-store";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { cn } from "@/lib/utils";

export type Density = "compact" | "cosy" | "roomy";

export interface GridColumn<T> {
  id: string;
  label: string;
  /** Field id the server sorts by. Omit for computed columns. */
  sortField?: string;
  align?: "left" | "right" | "center";
  /** Starting width in pixels. The user's own width overrides it. */
  width?: number;
  /** Pinned to the left and kept visible while the grid scrolls sideways. */
  sticky?: boolean;
  /** Hidden until the user turns it on in the column menu. */
  defaultHidden?: boolean;
  /** Opt a column out of resizing — the trailing actions column, mostly. */
  fixedWidth?: boolean;
  render: (row: T) => React.ReactNode;
}

interface GridPrefs {
  density?: Density;
  hidden?: string[];
  /** Column id → width in pixels, for columns the user has dragged. */
  widths?: Record<string, number>;
}

/** Module-level so the persisted-state snapshot stays referentially stable. */
const NO_PREFS: GridPrefs = {};

const DEFAULT_WIDTH = 150;
const MIN_WIDTH = 56;
const MAX_WIDTH = 720;
const SELECT_WIDTH = 32;

const DENSITY: Record<Density, { row: string; cell: string; icon: typeof Rows2 }> = {
  compact: { row: "h-[30px]", cell: "px-2 py-0 text-[12px]", icon: Rows4 },
  cosy: { row: "h-9", cell: "px-2.5 py-1 text-[12.5px]", icon: Rows3 },
  roomy: { row: "h-12", cell: "px-3 py-2 text-[13px]", icon: Rows2 },
};

interface CrmGridProps<T extends { id: number }> {
  columns: GridColumn<T>[];
  rows: T[];
  loading?: boolean;
  initialising?: boolean;

  // ---- server sorting ----
  sort?: { field: string; descending: boolean }[];
  onToggleSort?: (field: string) => void;

  // ---- server paging ----
  page: number;
  pageCount: number;
  pageSize: number;
  total: number;
  onPageChange: (page: number) => void;
  onPageSizeChange: (size: number) => void;

  // ---- selection ----
  selected?: Set<number>;
  onToggleRow?: (id: number) => void;
  onToggleAll?: () => void;

  onRowClick?: (row: T) => void;
  emptyMessage?: string;
  /** Rendered between the toolbar and the table — bulk action bars, etc. */
  banner?: React.ReactNode;
  storageKey?: string;
}

/**
 * The dense list grid.
 *
 * Everything expensive happens on the server — this renders exactly the page it
 * is given and never sorts or filters locally, so a 50-row page behaves the same
 * whether the result set is 50 rows or 50,000.
 *
 * Density, column visibility and column widths live here because they are
 * per-person preferences, not per-query state.
 */
export function CrmGrid<T extends { id: number }>({
  columns,
  rows,
  loading = false,
  initialising: initialisingProp = false,
  sort = [],
  onToggleSort,
  page,
  pageCount,
  pageSize,
  total,
  onPageChange,
  onPageSizeChange,
  selected,
  onToggleRow,
  onToggleAll,
  onRowClick,
  emptyMessage = "No records match this view.",
  banner,
  storageKey,
}: CrmGridProps<T>) {
  // Held to a floor. A view that resolves from cache in forty milliseconds
  // otherwise flashes its loading state for two frames, which reads as a glitch
  // rather than as work; a slow fetch is never cut short by it.
  const initialising = useMinimumLoading(initialisingProp);

  // Read straight from storage through useSyncExternalStore rather than
  // hydrating in an effect: the server render and the first client render agree,
  // and there is no cascading setState.
  const [prefs, setPrefs] = usePersistedState<GridPrefs>(
    `brg.grid.${storageKey ?? "default"}`,
    NO_PREFS
  );

  const density = prefs.density ?? "compact";

  // A column the user has never touched falls back to its declared default, so
  // adding a hidden-by-default column later does not surprise existing users.
  const hidden = React.useMemo(
    () =>
      new Set(
        prefs.hidden ?? columns.filter((c) => c.defaultHidden).map((c) => c.id)
      ),
    [prefs.hidden, columns]
  );

  const setDensity = (next: Density) => setPrefs({ ...prefs, density: next });

  const setHidden = (next: Set<string>) =>
    setPrefs({ ...prefs, hidden: [...next] });

  const visible = columns.filter((column) => !hidden.has(column.id));
  const style = DENSITY[density];
  const DensityIcon = style.icon;

  const selectable = !!selected && !!onToggleRow;
  const allOnPageSelected =
    selectable && rows.length > 0 && rows.every((row) => selected!.has(row.id));

  const sortState = (field?: string) =>
    field ? sort.find((s) => s.field === field) : undefined;

  /* ------------------------------------------------------------------ *
   * Column resizing
   *
   * The table is laid out with `table-layout: fixed` and a <colgroup>, so a
   * column's width is one number in one place rather than something the
   * browser negotiates from cell content.
   *
   * While the pointer is down the <col> and the table are written to directly
   * through refs. Putting the drag in React state instead would re-render every
   * row on every mouse move, which visibly stutters at fifty rows; state is
   * touched once, on release.
   * ------------------------------------------------------------------ */

  const tableRef = React.useRef<HTMLTableElement>(null);
  const colRefs = React.useRef(new Map<string, HTMLTableColElement>());
  const [resizing, setResizing] = React.useState<string | null>(null);

  const widthOf = React.useCallback(
    (column: GridColumn<T>) =>
      prefs.widths?.[column.id] ?? column.width ?? DEFAULT_WIDTH,
    [prefs.widths]
  );

  const totalWidth =
    visible.reduce((sum, column) => sum + widthOf(column), 0) +
    (selectable ? SELECT_WIDTH : 0);

  function startResize(event: React.PointerEvent<HTMLElement>, column: GridColumn<T>) {
    // The handle sits inside the header button's row; without this a drag would
    // also register as a click and re-sort the column.
    event.preventDefault();
    event.stopPropagation();

    const handle = event.currentTarget;
    handle.setPointerCapture(event.pointerId);

    const startX = event.clientX;
    const startWidth = widthOf(column);
    let latest = startWidth;

    setResizing(column.id);

    const onMove = (move: PointerEvent) => {
      latest = Math.round(
        Math.min(MAX_WIDTH, Math.max(MIN_WIDTH, startWidth + (move.clientX - startX)))
      );

      const col = colRefs.current.get(column.id);
      if (col) col.style.width = `${latest}px`;
      if (tableRef.current) {
        tableRef.current.style.minWidth = `${totalWidth - startWidth + latest}px`;
      }
    };

    const onUp = () => {
      handle.removeEventListener("pointermove", onMove);
      handle.removeEventListener("pointerup", onUp);
      handle.removeEventListener("pointercancel", onUp);

      setResizing(null);
      setPrefs({
        ...prefs,
        widths: { ...prefs.widths, [column.id]: latest },
      });
    };

    handle.addEventListener("pointermove", onMove);
    handle.addEventListener("pointerup", onUp);
    handle.addEventListener("pointercancel", onUp);
  }

  /** Double-clicking the handle drops back to the column's declared width. */
  function resetWidth(column: GridColumn<T>) {
    if (!prefs.widths?.[column.id]) return;

    const next = { ...prefs.widths };
    delete next[column.id];
    setPrefs({ ...prefs, widths: next });
  }

  const hasCustomWidths = Object.keys(prefs.widths ?? {}).length > 0;

  return (
    <div className="flex min-w-0 flex-1 flex-col">
      <div className="flex items-center justify-between gap-2 border-b px-2.5 py-1">
        <div className="flex items-center gap-1 text-[11.5px] text-muted-foreground tabular-nums">
          {loading ? (
            <span className="inline-flex items-center gap-1.5">
              <span className="size-1.5 animate-pulse rounded-full bg-primary" />
              Loading…
            </span>
          ) : (
            <span>
              {total.toLocaleString()} record{total === 1 ? "" : "s"}
              {selectable && selected!.size > 0
                ? ` · ${selected!.size} selected`
                : ""}
            </span>
          )}
        </div>

        <div className="flex items-center gap-1">
          <Button
            variant="ghost"
            size="sm"
            className="h-6 gap-1 px-1.5 text-[11.5px]"
            title={`Row density: ${density}`}
            onClick={() =>
              setDensity(
                density === "compact" ? "cosy" : density === "cosy" ? "roomy" : "compact"
              )
            }
          >
            <DensityIcon className="size-3.5" />
            <span className="hidden capitalize sm:inline">{density}</span>
          </Button>

          <DropdownMenu>
            <DropdownMenuTrigger asChild>
              <Button variant="ghost" size="sm" className="h-6 gap-1 px-1.5 text-[11.5px]">
                <Columns3 className="size-3.5" />
                <span className="hidden sm:inline">
                  Columns
                  {hidden.size > 0 ? ` (${visible.length}/${columns.length})` : ""}
                </span>
              </Button>
            </DropdownMenuTrigger>
            <DropdownMenuContent align="end" className="max-h-80 w-52 overflow-y-auto">
              <DropdownMenuLabel className="text-[11px] tracking-wide text-muted-foreground uppercase">
                Visible columns
              </DropdownMenuLabel>
              <DropdownMenuSeparator />
              {columns.map((column) => (
                <DropdownMenuCheckboxItem
                  key={column.id}
                  checked={!hidden.has(column.id)}
                  onSelect={(event) => event.preventDefault()}
                  onCheckedChange={(checked) => {
                    const next = new Set(hidden);
                    if (checked) next.delete(column.id);
                    else next.add(column.id);
                    setHidden(next);
                  }}
                  className="text-[12.5px]"
                >
                  {column.label}
                </DropdownMenuCheckboxItem>
              ))}

              {hasCustomWidths ? (
                <>
                  <DropdownMenuSeparator />
                  <DropdownMenuItem
                    className="text-[12.5px]"
                    onClick={() => setPrefs({ ...prefs, widths: {} })}
                  >
                    Reset column widths
                  </DropdownMenuItem>
                </>
              ) : null}
            </DropdownMenuContent>
          </DropdownMenu>
        </div>
      </div>

      {banner}

      <ScrollArea className="w-full flex-1">
        <table
          ref={tableRef}
          style={{ width: "100%", minWidth: totalWidth, tableLayout: "fixed" }}
          className={cn(
            "caption-bottom border-separate border-spacing-0",
            // Suppresses text selection across the page while a column is being
            // dragged; without it the drag paints a selection over every row.
            resizing && "select-none"
          )}
        >
          <colgroup>
            {selectable ? <col style={{ width: SELECT_WIDTH }} /> : null}
            {visible.map((column) => (
              <col
                key={column.id}
                ref={(element) => {
                  if (element) colRefs.current.set(column.id, element);
                  else colRefs.current.delete(column.id);
                }}
                style={{ width: widthOf(column) }}
              />
            ))}
            {/* Width-less on purpose: under `table-layout: fixed` this is the
                only auto column, so it soaks up whatever space the declared
                widths leave over instead of the grid ending mid-panel. It
                collapses to nothing once the columns overflow. */}
            <col />
          </colgroup>

          <thead className="sticky top-0 z-20 bg-muted/70 backdrop-blur">
            <tr>
              {selectable ? (
                <th
                  className={cn(
                    "sticky left-0 z-30 border-r border-b bg-muted/70 px-2 backdrop-blur",
                    style.row
                  )}
                >
                  <Checkbox
                    aria-label="Select all on this page"
                    checked={allOnPageSelected}
                    onCheckedChange={() => onToggleAll?.()}
                  />
                </th>
              ) : null}

              {visible.map((column, index) => {
                const state = sortState(column.sortField);
                const sortable = !!column.sortField && !!onToggleSort;
                const resizable = !column.fixedWidth;

                return (
                  <th
                    key={column.id}
                    className={cn(
                      "group/th relative border-r border-b bg-muted/70 text-[11.5px] font-medium whitespace-nowrap backdrop-blur last:border-r-0",
                      style.row,
                      style.cell,
                      column.align === "right" && "text-right",
                      column.align === "center" && "text-center",
                      column.sticky &&
                        cn(
                          "sticky z-30",
                          selectable ? "left-8" : "left-0",
                          index > 0 && "shadow-[1px_0_0_0_var(--border)]"
                        )
                    )}
                  >
                    {sortable ? (
                      <button
                        type="button"
                        onClick={() => onToggleSort(column.sortField!)}
                        className={cn(
                          "-mx-1 inline-flex h-full max-w-full items-center gap-1 rounded px-1 hover:text-foreground",
                          state ? "text-foreground" : "text-muted-foreground",
                          column.align === "right" && "flex-row-reverse"
                        )}
                      >
                        <span className="truncate">{column.label}</span>
                        {!state ? (
                          <ChevronsUpDown className="size-3 shrink-0 opacity-40" />
                        ) : state.descending ? (
                          <ArrowDown className="size-3 shrink-0" />
                        ) : (
                          <ArrowUp className="size-3 shrink-0" />
                        )}
                      </button>
                    ) : (
                      <span className="block truncate text-muted-foreground">
                        {column.label}
                      </span>
                    )}

                    {resizable ? (
                      <span
                        role="separator"
                        aria-orientation="vertical"
                        aria-label={`Resize ${column.label} column`}
                        title="Drag to resize · double-click to reset"
                        onPointerDown={(event) => startResize(event, column)}
                        onDoubleClick={() => resetWidth(column)}
                        onClick={(event) => event.stopPropagation()}
                        className={cn(
                          "absolute top-0 right-0 z-10 h-full w-1.5 translate-x-1/2 cursor-col-resize touch-none",
                          // A hairline that only appears on hover or while
                          // dragging — visible enough to find, quiet enough not
                          // to add a second set of gridlines.
                          "after:absolute after:inset-y-0 after:left-1/2 after:w-px after:-translate-x-1/2 after:bg-primary after:opacity-0 after:transition-opacity",
                          "hover:after:opacity-100",
                          resizing === column.id && "after:w-0.5 after:opacity-100"
                        )}
                      />
                    ) : null}
                  </th>
                );
              })}

              <th
                aria-hidden
                className={cn(
                  "border-b bg-muted/70 backdrop-blur",
                  style.row
                )}
              />
            </tr>
          </thead>

          <tbody>
            {initialising ? (
              <tr>
                <td colSpan={visible.length + (selectable ? 1 : 0) + 1}>
                  <CrmLoadingState label="Loading records" />
                </td>
              </tr>
            ) : rows.length === 0 ? (
              <tr>
                <td
                  colSpan={visible.length + (selectable ? 1 : 0) + 1}
                  className="h-40 text-center text-[13px] text-muted-foreground"
                >
                  {emptyMessage}
                </td>
              </tr>
            ) : (
              rows.map((row) => {
                const isSelected = selectable && selected!.has(row.id);

                return (
                  <tr
                    key={row.id}
                    onClick={() => onRowClick?.(row)}
                    className={cn(
                      "group transition-colors",
                      isSelected ? "bg-primary/5" : "hover:bg-muted/40",
                      onRowClick && "cursor-pointer"
                    )}
                  >
                    {selectable ? (
                      <td
                        onClick={(event) => event.stopPropagation()}
                        className={cn(
                          "sticky left-0 z-10 border-r border-b px-2",
                          isSelected ? "bg-primary/5" : "bg-card group-hover:bg-muted/40",
                          style.row
                        )}
                      >
                        <Checkbox
                          aria-label={`Select row ${row.id}`}
                          checked={isSelected}
                          onCheckedChange={() => onToggleRow!(row.id)}
                        />
                      </td>
                    ) : null}

                    {visible.map((column, index) => (
                      <td
                        key={column.id}
                        className={cn(
                          "overflow-hidden border-r border-b last:border-r-0",
                          style.row,
                          style.cell,
                          column.align === "right" && "text-right tabular-nums",
                          column.align === "center" && "text-center",
                          column.sticky &&
                            cn(
                              "sticky z-10",
                              selectable ? "left-8" : "left-0",
                              isSelected
                                ? "bg-primary/5"
                                : "bg-card group-hover:bg-muted/40",
                              index > 0 && "shadow-[1px_0_0_0_var(--border)]"
                            )
                        )}
                      >
                        {column.render(row)}
                      </td>
                    ))}

                    <td aria-hidden className={cn("border-b", style.row)} />
                  </tr>
                );
              })
            )}
          </tbody>
        </table>
        <ScrollBar orientation="horizontal" />
      </ScrollArea>

      <div className="flex flex-wrap items-center justify-between gap-2 border-t px-2.5 py-1.5 text-[12px]">
        <div className="text-muted-foreground tabular-nums">
          {total === 0
            ? "No records"
            : `${((page - 1) * pageSize + 1).toLocaleString()}–${Math.min(
                page * pageSize,
                total
              ).toLocaleString()} of ${total.toLocaleString()}`}
        </div>

        <div className="flex items-center gap-2">
          <Select
            value={String(pageSize)}
            onValueChange={(value) => onPageSizeChange(Number(value))}
          >
            <SelectTrigger size="sm" className="h-7 w-[4.5rem] text-[12px]">
              <SelectValue />
            </SelectTrigger>
            <SelectContent side="top">
              {[25, 50, 100, 200].map((size) => (
                <SelectItem key={size} value={String(size)} className="text-[12.5px]">
                  {size}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>

          <span className="tabular-nums whitespace-nowrap">
            Page {page} of {Math.max(pageCount, 1)}
          </span>

          <div className="flex items-center gap-0.5">
            <Button
              variant="outline"
              size="icon"
              aria-label="First page"
              className="size-7"
              disabled={page <= 1}
              onClick={() => onPageChange(1)}
            >
              <ChevronsLeft className="size-3.5" />
            </Button>
            <Button
              variant="outline"
              size="icon"
              aria-label="Previous page"
              className="size-7"
              disabled={page <= 1}
              onClick={() => onPageChange(page - 1)}
            >
              <ChevronLeft className="size-3.5" />
            </Button>
            <Button
              variant="outline"
              size="icon"
              aria-label="Next page"
              className="size-7"
              disabled={page >= pageCount}
              onClick={() => onPageChange(page + 1)}
            >
              <ChevronRight className="size-3.5" />
            </Button>
            <Button
              variant="outline"
              size="icon"
              aria-label="Last page"
              className="size-7"
              disabled={page >= pageCount}
              onClick={() => onPageChange(pageCount)}
            >
              <ChevronsRight className="size-3.5" />
            </Button>
          </div>
        </div>
      </div>
    </div>
  );
}
