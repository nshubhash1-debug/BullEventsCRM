"use client";

import * as React from "react";

import {
  analyticsApi,
  toFilterNode,
  type AggregateRequest,
  type AggregateResponse,
  type DatasetMeta,
  type PeriodKey,
} from "@/lib/dashboard/analytics";
import type { WidgetQuery } from "@/lib/dashboard/types";

/* ------------------------------------------------------------------ *
 * Dataset catalogue
 * ------------------------------------------------------------------ */

/**
 * The catalogue is the same for every widget on the page and never changes
 * within a session, so it is fetched once into a module-level promise rather
 * than once per widget — a dashboard with a dozen charts would otherwise open
 * with a dozen identical requests.
 */
let catalogue: Promise<DatasetMeta[]> | null = null;

function loadCatalogue() {
  catalogue ??= analyticsApi.datasets().catch((error) => {
    // A failed load must not be cached, or the page never recovers from a
    // transient error without a hard refresh.
    catalogue = null;
    throw error;
  });
  return catalogue;
}

export function useDatasets() {
  const [datasets, setDatasets] = React.useState<DatasetMeta[]>([]);
  const [loading, setLoading] = React.useState(true);
  const [error, setError] = React.useState<string | null>(null);

  React.useEffect(() => {
    let live = true;

    loadCatalogue()
      .then((result) => {
        if (!live) return;
        setDatasets(result);
        setError(null);
      })
      .catch((cause: Error) => {
        if (live) setError(cause.message);
      })
      .finally(() => {
        if (live) setLoading(false);
      });

    return () => {
      live = false;
    };
  }, []);

  return { datasets, loading, error };
}

/** Metadata for one dataset, or undefined while the catalogue is loading. */
export function useDataset(id: string) {
  const { datasets, loading, error } = useDatasets();
  return {
    dataset: datasets.find((entry) => entry.id === id),
    datasets,
    loading,
    error,
  };
}

/* ------------------------------------------------------------------ *
 * Widget data
 * ------------------------------------------------------------------ */

/**
 * Turns a widget's saved config into the request the server expects.
 *
 * `inherit` is resolved here rather than on the server: the dashboard's period
 * control is a client concept, and the server should only ever be told the one
 * window it is actually meant to apply.
 */
export function toRequest(
  query: WidgetQuery,
  inheritedPeriod: PeriodKey
): AggregateRequest {
  return {
    dataset: query.dataset,
    dimension: query.dimension,
    bucket: query.bucket,
    breakdown: query.breakdown,
    breakdownBucket: query.breakdownBucket,
    measure: query.measure === "*" ? null : query.measure,
    aggregation: query.aggregation,
    filter: toFilterNode(query.filters, query.matchMode),
    dateField: query.dateField ?? undefined,
    period: query.period === "inherit" ? inheritedPeriod : query.period,
    sort: query.sort,
    limit: query.limit,
    groupOther: query.groupOther,
  };
}

export interface WidgetData {
  result: AggregateResponse | null;
  loading: boolean;
  error: string | null;
}

/**
 * Runs one widget's query.
 *
 * The request is serialised into the effect's dependency rather than passed as
 * an object, because a fresh object literal every render would refetch on every
 * render. Serialising means the fetch fires when the *configuration* changes,
 * which is the only thing that should cause one.
 */
export function useWidgetData(
  query: WidgetQuery,
  inheritedPeriod: PeriodKey,
  refreshToken = 0
): WidgetData {
  const request = React.useMemo(
    () => toRequest(query, inheritedPeriod),
    [query, inheritedPeriod]
  );
  const signature = JSON.stringify(request);
  const token = `${signature}::${refreshToken}`;

  // What arrived, tagged with the request that produced it. Loading is derived
  // from comparing that tag with the current request rather than set from
  // inside the effect: a `setState` in an effect body would cascade a second
  // render on every configuration change, which the React Compiler rejects.
  const [state, setState] = React.useState<{
    token: string | null;
    result: AggregateResponse | null;
    error: string | null;
  }>({ token: null, result: null, error: null });

  React.useEffect(() => {
    let live = true;

    analyticsApi
      .aggregate(JSON.parse(signature) as AggregateRequest)
      .then((result) => {
        if (live) setState({ token, result, error: null });
      })
      .catch((cause: Error) => {
        if (live) setState({ token, result: null, error: cause.message });
      });

    return () => {
      live = false;
    };
  }, [signature, token]);

  return {
    // The previous result stays on screen while the next one is in flight, so
    // a filter change dims the chart instead of blanking the whole dashboard.
    result: state.result,
    loading: state.token !== token,
    error: state.token === token ? state.error : null,
  };
}

/* ------------------------------------------------------------------ *
 * Field values
 * ------------------------------------------------------------------ */

/**
 * Distinct values for a field, for the filter builder's value dropdown.
 *
 * Reading them off the data rather than a static option list means a filter
 * offers exactly the values that exist — no dead options, and nothing missing
 * because a new one was added to the database but not to the enum in the UI.
 */
const NO_VALUES: { label: string; value: string }[] = [];

export function useFieldValues(dataset: string, field: string | null) {
  // Keyed by the field it belongs to so a stale response for the previous
  // field is discarded on read, which also removes the need to clear state
  // from inside the effect when `field` goes null.
  const [loaded, setLoaded] = React.useState<{
    key: string;
    values: { label: string; value: string }[];
  } | null>(null);

  const key = field ? `${dataset}.${field}` : "";

  React.useEffect(() => {
    if (!field) return;

    let live = true;

    analyticsApi
      .values(dataset, field)
      .then((buckets) => {
        if (!live) return;
        setLoaded({
          key,
          values: buckets
            .filter((bucket) => bucket.value && bucket.value !== "—")
            .map((bucket) => ({
              label: `${bucket.value} (${bucket.count})`,
              value: bucket.value,
            })),
        });
      })
      .catch(() => {
        if (live) setLoaded({ key, values: [] });
      });

    return () => {
      live = false;
    };
  }, [dataset, field, key]);

  return loaded?.key === key ? loaded.values : NO_VALUES;
}
