"use client";

import * as React from "react";
import { toast } from "sonner";

import { ApiError } from "@/lib/api";
import type { ListResource } from "@/lib/crm-api";
import {
  toDateFilterNode,
  type DatePresetKey,
  type DateRange,
} from "@/lib/date-range";
import {
  countConditions,
  emptyRoot,
  toWire,
  type FacetBucket,
  type FilterField,
  type FilterNode,
  type PagedResult,
  type SortSpec,
} from "@/lib/query";

/** A date column the list can be narrowed by, as offered in the date filter. */
export interface DateFieldOption {
  id: string;
  label: string;
}

interface UseCrmListOptions {
  /** Facet fields to ask the server to count on every query. */
  facets?: string[];
  /** Initial sort, applied before the user touches a column header. */
  sort?: SortSpec[];
  pageSize?: number;
  /**
   * A fixed filter the user cannot see or remove — how "Customer Database" and
   * "Contacts" share one endpoint while showing different populations.
   */
  scope?: FilterNode | null;
  /**
   * Date columns the quick date filter can point at. The first is the default;
   * omitting this hides the control, for lists where no date reads naturally.
   */
  dateFields?: DateFieldOption[];
  /** Window the list opens on, e.g. "last30" for a busy activity log. */
  defaultDatePreset?: DatePresetKey;
}

export interface CrmListState<T> {
  // ---- data ----
  rows: T[];
  total: number;
  page: number;
  pageCount: number;
  pageSize: number;
  facets: Record<string, FacetBucket[]>;
  aggregates: Record<string, number>;
  loading: boolean;
  /** True only on the very first load, so the table can show skeletons once. */
  initialising: boolean;

  // ---- filter model ----
  fields: FilterField[];
  filter: FilterNode;
  conditionCount: number;
  setFilter: (next: FilterNode) => void;
  clearFilter: () => void;

  // ---- date window ----
  /** Empty when the page declared no date fields; the control hides itself. */
  dateFields: DateFieldOption[];
  dateRange: DateRange;
  setDateRange: (next: DateRange) => void;

  // ---- query controls ----
  search: string;
  setSearch: (value: string) => void;
  sort: SortSpec[];
  toggleSort: (field: string) => void;
  setPage: (page: number) => void;
  setPageSize: (size: number) => void;

  // ---- selection ----
  selected: Set<number>;
  toggleRow: (id: number) => void;
  toggleAll: () => void;
  clearSelection: () => void;

  refresh: () => void;
}

/**
 * Drives a server-backed list view.
 *
 * Everything that narrows the data — search, the filter tree, sorting, paging —
 * is sent to the API and resolved in SQL. Nothing is filtered in the browser, so
 * the counts, facets and aggregates on screen describe the entire result set
 * rather than the rows that happen to be loaded.
 */
export function useCrmList<T extends { id: number }>(
  resource: ListResource<T>,
  options: UseCrmListOptions = {}
): CrmListState<T> {
  const {
    facets: facetFields = [],
    sort: initialSort = [],
    pageSize: initialPageSize = 50,
    scope = null,
    dateFields = [],
    defaultDatePreset = "all",
  } = options;

  const [fields, setFields] = React.useState<FilterField[]>([]);
  const [filter, setFilter] = React.useState<FilterNode>(emptyRoot);
  const [search, setSearchValue] = React.useState("");
  const [debouncedSearch, setDebouncedSearch] = React.useState("");
  const [sort, setSort] = React.useState<SortSpec[]>(initialSort);
  const [page, setPage] = React.useState(1);
  const [pageSize, setPageSizeValue] = React.useState(initialPageSize);

  const [dateRange, setDateRangeValue] = React.useState<DateRange>(() => ({
    preset: defaultDatePreset,
    field: dateFields[0]?.id ?? "",
  }));

  /**
   * The page is stored with the query key that produced it.
   *
   * That makes "loading" a derived value — the displayed result simply does not
   * match the query the inputs currently describe — instead of a second piece
   * of state set synchronously inside the effect, which cascades renders.
   */
  const [result, setResult] = React.useState<{
    key: string;
    data: PagedResult<T>;
  } | null>(null);
  const [nonce, setNonce] = React.useState(0);

  const [selected, setSelected] = React.useState<Set<number>>(new Set());

  // Serialised so the effect below compares by value; these are re-created on
  // every render otherwise and would loop.
  const facetKey = React.useMemo(() => facetFields.join(","), [facetFields]);
  const scopeKey = React.useMemo(() => JSON.stringify(scope), [scope]);

  /* ---------------- field metadata ---------------- */

  React.useEffect(() => {
    let cancelled = false;

    resource
      .fields()
      .then((loaded) => {
        if (!cancelled) setFields(loaded);
      })
      .catch(() => {
        if (!cancelled) setFields([]);
      });

    return () => {
      cancelled = true;
    };
    // The resource object is stable per page; refetching metadata on every
    // render would be wasteful and is never what we want.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  /* ---------------- search debounce ---------------- */

  React.useEffect(() => {
    const handle = setTimeout(() => setDebouncedSearch(search), 250);
    return () => clearTimeout(handle);
  }, [search]);

  /* ---------------- the query ---------------- */

  const filterWire = React.useMemo(
    () => toWire(filter, fields),
    [filter, fields]
  );

  const filterKey = React.useMemo(
    () => JSON.stringify(filterWire),
    [filterWire]
  );
  const sortKey = React.useMemo(() => JSON.stringify(sort), [sort]);

  // Resolved to concrete dates here so the key changes when the *window* does.
  // Keying on the preset alone would leave a list opened before midnight still
  // showing yesterday under a label that says "Today".
  const dateKey = React.useMemo(
    () => JSON.stringify(toDateFilterNode(dateRange)),
    [dateRange]
  );

  const queryKey = React.useMemo(
    () =>
      JSON.stringify([
        debouncedSearch,
        filterKey,
        dateKey,
        sortKey,
        page,
        pageSize,
        facetKey,
        scopeKey,
        nonce,
      ]),
    [
      debouncedSearch,
      filterKey,
      dateKey,
      sortKey,
      page,
      pageSize,
      facetKey,
      scopeKey,
      nonce,
    ]
  );

  React.useEffect(() => {
    let cancelled = false;

    const parsedScope = scopeKey === "null" ? null : JSON.parse(scopeKey);
    const parsedFilter = filterKey === "null" ? null : JSON.parse(filterKey);
    const parsedDate = dateKey === "null" ? null : JSON.parse(dateKey);

    // Scope, date window and the user's own tree are ANDed together, so neither
    // the page's fixed scope nor the date filter can be widened by anything the
    // user builds in the advanced filter.
    const parts = [parsedScope, parsedDate, parsedFilter].filter(Boolean);
    const combined =
      parts.length === 0
        ? null
        : parts.length === 1
          ? parts[0]
          : { conjunction: "and", children: parts };

    resource
      .query({
        search: debouncedSearch || undefined,
        filter: combined,
        sort: JSON.parse(sortKey),
        page,
        pageSize,
        facets: facetKey ? facetKey.split(",") : undefined,
      })
      .then((next) => {
        if (cancelled) return;
        setResult({ key: queryKey, data: next });
      })
      .catch((error: unknown) => {
        if (cancelled) return;
        toast.error("Could not load this view", {
          description:
            error instanceof ApiError ? error.message : "Network error.",
        });
        setResult({
          key: queryKey,
          data: {
            items: [],
            total: 0,
            page: 1,
            pageSize,
            pageCount: 0,
            facets: {},
            aggregates: {},
          },
        });
      });

    return () => {
      cancelled = true;
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [queryKey]);

  /* ---------------- controls ---------------- */

  // Any change that alters which rows match has to reset paging — otherwise a
  // narrower filter can leave the view stranded on a page that no longer exists.
  const setSearch = React.useCallback((value: string) => {
    setSearchValue(value);
    setPage(1);
  }, []);

  const applyFilter = React.useCallback((next: FilterNode) => {
    setFilter(next);
    setPage(1);
  }, []);

  const clearFilter = React.useCallback(() => {
    setFilter(emptyRoot());
    setPage(1);
  }, []);

  const setPageSize = React.useCallback((size: number) => {
    setPageSizeValue(size);
    setPage(1);
  }, []);

  const setDateRange = React.useCallback((next: DateRange) => {
    setDateRangeValue(next);
    setPage(1);
  }, []);

  /** Cycles a column: ascending → descending → unsorted. */
  const toggleSort = React.useCallback((field: string) => {
    setSort((current) => {
      const existing = current.find((s) => s.field === field);
      if (!existing) return [{ field, descending: false }];
      if (!existing.descending) return [{ field, descending: true }];
      return [];
    });
    setPage(1);
  }, []);

  /* ---------------- selection ---------------- */

  const rows = React.useMemo(() => result?.data.items ?? [], [result]);

  const toggleRow = React.useCallback((id: number) => {
    setSelected((current) => {
      const next = new Set(current);
      if (next.has(id)) next.delete(id);
      else next.add(id);
      return next;
    });
  }, []);

  const toggleAll = React.useCallback(() => {
    setSelected((current) => {
      const pageIds = rows.map((row) => row.id);
      const allSelected = pageIds.every((id) => current.has(id));

      const next = new Set(current);
      for (const id of pageIds) {
        if (allSelected) next.delete(id);
        else next.add(id);
      }
      return next;
    });
  }, [rows]);

  const clearSelection = React.useCallback(() => setSelected(new Set()), []);

  const refresh = React.useCallback(() => setNonce((n) => n + 1), []);

  return {
    rows,
    total: result?.data.total ?? 0,
    page: result?.data.page ?? page,
    pageCount: result?.data.pageCount ?? 0,
    pageSize,
    facets: result?.data.facets ?? {},
    aggregates: result?.data.aggregates ?? {},
    loading: result === null || result.key !== queryKey,
    initialising: result === null,

    fields,
    filter,
    conditionCount: countConditions(filter, fields),
    setFilter: applyFilter,
    clearFilter,

    dateFields,
    dateRange,
    setDateRange,

    search,
    setSearch,
    sort,
    toggleSort,
    setPage,
    setPageSize,

    selected,
    toggleRow,
    toggleAll,
    clearSelection,

    refresh,
  };
}
