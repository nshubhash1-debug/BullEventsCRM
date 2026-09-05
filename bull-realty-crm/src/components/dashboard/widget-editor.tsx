"use client";

import * as React from "react";
import { Loader2, RotateCcw, Sparkles } from "lucide-react";

import { AdvancedFilterPanel } from "@/components/dashboard/advanced-filter-panel";
import { ChartRenderer } from "@/components/dashboard/widgets/chart-renderer";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { ScrollArea } from "@/components/ui/scroll-area";
import {
  Select,
  SelectContent,
  SelectGroup,
  SelectItem,
  SelectLabel,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import {
  Sheet,
  SheetContent,
  SheetDescription,
  SheetFooter,
  SheetHeader,
  SheetTitle,
} from "@/components/ui/sheet";
import { Switch } from "@/components/ui/switch";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import type { FilterFieldDef } from "@/lib/advanced-filter";
import {
  AGGREGATION_LABELS,
  BUCKET_LABELS,
  PERIODS,
  type Aggregation,
  type DatasetMeta,
  type DateBucketKey,
  type PeriodKey,
  type SortMode,
} from "@/lib/dashboard/analytics";
import {
  HEIGHT_LABEL,
  SPAN_LABEL,
  describeWidget,
  stylesFor,
  type ChartStyle,
  type WidgetDef,
  type WidgetHeight,
  type WidgetSpan,
} from "@/lib/dashboard/types";
import {
  useDatasets,
  useFieldValues,
  useWidgetData,
} from "@/lib/dashboard/use-analytics";

/* ------------------------------------------------------------------ *
 * Editor
 * ------------------------------------------------------------------ */

interface WidgetEditorProps {
  widget: WidgetDef | null;
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onSave: (widget: WidgetDef) => void;
  inheritedPeriod: PeriodKey;
}

/**
 * The whole widget, editable in one panel with the chart redrawing as you go.
 *
 * Every control writes to a local draft and the preview runs the real query
 * against it, so what you approve is exactly what lands on the dashboard —
 * there is no separate "apply" step where the two could disagree.
 */
export function WidgetEditor({
  widget,
  open,
  onOpenChange,
  onSave,
  inheritedPeriod,
}: WidgetEditorProps) {
  // Seeded once per mount. The caller keys this component by widget id, so
  // opening a different widget remounts it with a fresh draft — which is what
  // an effect syncing prop to state would have done, without the extra render.
  const [draft, setDraft] = React.useState<WidgetDef | null>(widget);
  const { datasets, loading } = useDatasets();

  if (!draft) return null;

  const meta = datasets.find((entry) => entry.id === draft.query.dataset);

  function patchQuery(patch: Partial<WidgetDef["query"]>) {
    setDraft((current) =>
      current ? { ...current, query: { ...current.query, ...patch } } : current
    );
  }

  function patchOptions(patch: Partial<WidgetDef["options"]>) {
    setDraft((current) =>
      current ? { ...current, options: { ...current.options, ...patch } } : current
    );
  }

  function patch(patch: Partial<WidgetDef>) {
    setDraft((current) => (current ? { ...current, ...patch } : current));
  }

  /**
   * Changing the object invalidates every field id on the widget, so the query
   * is rebuilt from the new dataset's own defaults rather than carrying over
   * names that do not exist on it.
   */
  function changeDataset(id: string) {
    const next = datasets.find((entry) => entry.id === id);
    if (!next) return;

    patchQuery({
      dataset: id,
      dimension: next.defaultDimension,
      breakdown: null,
      measure: "*",
      aggregation: "count",
      dateField: next.defaultDateField,
      filters: [],
      sort: "auto",
    });
  }

  const dimensionMeta = meta?.dimensions.find(
    (entry) => entry.id === draft.query.dimension
  );
  const breakdownMeta = meta?.dimensions.find(
    (entry) => entry.id === draft.query.breakdown
  );
  const measureMeta = meta?.measures.find(
    (entry) => entry.id === draft.query.measure
  );

  const availableStyles = stylesFor(Boolean(draft.query.breakdown));
  const generatedTitle = describeWidget(draft.query, {
    dataset: meta?.label,
    dimension: dimensionMeta?.label,
    breakdown: breakdownMeta?.label,
    measure: measureMeta?.label,
  });

  return (
    <Sheet open={open} onOpenChange={onOpenChange}>
      <SheetContent
        side="right"
        className="flex w-full flex-col gap-0 p-0 sm:max-w-xl"
      >
        <SheetHeader className="border-b p-4">
          <SheetTitle className="text-base">Configure widget</SheetTitle>
          <SheetDescription>
            Pick what to measure, then how to draw it. The preview runs the real
            query.
          </SheetDescription>
        </SheetHeader>

        <Preview draft={draft} inheritedPeriod={inheritedPeriod} />

        <Tabs defaultValue="data" className="min-h-0 flex-1">
          <TabsList className="mx-4 mt-3">
            <TabsTrigger value="data">Data</TabsTrigger>
            <TabsTrigger value="chart">Chart</TabsTrigger>
            <TabsTrigger value="filters">
              Filters
              {draft.query.filters.length > 0 ? (
                <Badge
                  variant="secondary"
                  className="ml-1.5 h-4 px-1 text-[10px] tabular-nums"
                >
                  {draft.query.filters.length}
                </Badge>
              ) : null}
            </TabsTrigger>
          </TabsList>

          <ScrollArea className="h-[calc(100vh-25rem)]">
            {/* ---------------- Data ---------------- */}
            <TabsContent value="data" className="m-0 flex flex-col gap-4 p-4">
              <Field label="Object" hint="Which records this widget reads">
                <Select
                  value={draft.query.dataset}
                  onValueChange={changeDataset}
                  disabled={loading}
                >
                  <SelectTrigger className="h-9 text-[13px]">
                    <SelectValue placeholder="Loading…" />
                  </SelectTrigger>
                  <SelectContent>
                    {datasets.map((entry) => (
                      <SelectItem key={entry.id} value={entry.id}>
                        {entry.label}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </Field>

              <Field label="Measure" hint="The number each group shows">
                <div className="flex gap-2">
                  <Select
                    value={draft.query.measure}
                    onValueChange={(value) => {
                      const next = meta?.measures.find((m) => m.id === value);
                      patchQuery({
                        measure: value,
                        aggregation:
                          value === "*"
                            ? "count"
                            : (next?.aggregations[0] as Aggregation) ?? "sum",
                      });
                    }}
                  >
                    <SelectTrigger className="h-9 flex-1 text-[13px]">
                      <SelectValue />
                    </SelectTrigger>
                    <SelectContent>
                      {meta?.measures.map((entry) => (
                        <SelectItem key={entry.id} value={entry.id}>
                          {entry.label}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>

                  {draft.query.measure !== "*" ? (
                    <Select
                      value={draft.query.aggregation}
                      onValueChange={(value) =>
                        patchQuery({ aggregation: value as Aggregation })
                      }
                    >
                      <SelectTrigger className="h-9 w-36 text-[13px]">
                        <SelectValue />
                      </SelectTrigger>
                      <SelectContent>
                        {(measureMeta?.aggregations ?? []).map((fn) => (
                          <SelectItem key={fn} value={fn}>
                            {AGGREGATION_LABELS[fn]}
                          </SelectItem>
                        ))}
                      </SelectContent>
                    </Select>
                  ) : null}
                </div>
              </Field>

              <Field label="Group by" hint="One bar, slice or row per value">
                <div className="flex gap-2">
                  <Select
                    value={draft.query.dimension}
                    onValueChange={(value) => patchQuery({ dimension: value })}
                  >
                    <SelectTrigger className="h-9 flex-1 text-[13px]">
                      <SelectValue />
                    </SelectTrigger>
                    <SelectContent>
                      {meta?.dimensions.map((entry) => (
                        <SelectItem key={entry.id} value={entry.id}>
                          {entry.label}
                          {entry.kind === "date" ? " (date)" : ""}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>

                  {dimensionMeta?.kind === "date" ? (
                    <BucketSelect
                      value={draft.query.bucket}
                      onChange={(bucket) => patchQuery({ bucket })}
                    />
                  ) : null}
                </div>
              </Field>

              <Field
                label="Split by"
                hint="Adds a second dimension as coloured series"
              >
                <div className="flex gap-2">
                  <Select
                    value={draft.query.breakdown ?? "__none__"}
                    onValueChange={(value) => {
                      const breakdown = value === "__none__" ? null : value;
                      // Some styles only read with one series; fall back rather
                      // than leaving the widget on a style it cannot draw.
                      const allowed = stylesFor(Boolean(breakdown));
                      const style = allowed.some((s) => s.value === draft!.style)
                        ? draft!.style
                        : (allowed[0]?.value ?? "bar");
                      patchQuery({ breakdown });
                      patch({ style });
                    }}
                  >
                    <SelectTrigger className="h-9 flex-1 text-[13px]">
                      <SelectValue />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value="__none__">No split</SelectItem>
                      {meta?.dimensions
                        .filter((entry) => entry.id !== draft.query.dimension)
                        .map((entry) => (
                          <SelectItem key={entry.id} value={entry.id}>
                            {entry.label}
                          </SelectItem>
                        ))}
                    </SelectContent>
                  </Select>

                  {breakdownMeta?.kind === "date" ? (
                    <BucketSelect
                      value={draft.query.breakdownBucket}
                      onChange={(bucket) => patchQuery({ breakdownBucket: bucket })}
                    />
                  ) : null}
                </div>
              </Field>

              <div className="grid grid-cols-2 gap-3">
                <Field label="Period">
                  <Select
                    value={draft.query.period}
                    onValueChange={(value) =>
                      patchQuery({ period: value as PeriodKey | "inherit" })
                    }
                  >
                    <SelectTrigger className="h-9 text-[13px]">
                      <SelectValue />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value="inherit">
                        Follow dashboard filter
                      </SelectItem>
                      {PERIODS.map((period) => (
                        <SelectItem key={period.value} value={period.value}>
                          {period.label}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                </Field>

                <Field label="Date field">
                  <Select
                    value={draft.query.dateField ?? meta?.defaultDateField ?? ""}
                    onValueChange={(value) => patchQuery({ dateField: value })}
                  >
                    <SelectTrigger className="h-9 text-[13px]">
                      <SelectValue />
                    </SelectTrigger>
                    <SelectContent>
                      {meta?.dateFields.map((entry) => (
                        <SelectItem key={entry.id} value={entry.id}>
                          {entry.label}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                </Field>
              </div>

              <div className="grid grid-cols-2 gap-3">
                <Field label="Sort">
                  <Select
                    value={draft.query.sort}
                    onValueChange={(value) =>
                      patchQuery({ sort: value as SortMode })
                    }
                  >
                    <SelectTrigger className="h-9 text-[13px]">
                      <SelectValue />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value="auto">Automatic</SelectItem>
                      {dimensionMeta?.ordered ? (
                        <SelectItem value="sequence">
                          Natural order (stage sequence)
                        </SelectItem>
                      ) : null}
                      <SelectItem value="valueDesc">Value, high to low</SelectItem>
                      <SelectItem value="valueAsc">Value, low to high</SelectItem>
                      <SelectItem value="keyAsc">Label, A to Z</SelectItem>
                      <SelectItem value="keyDesc">Label, Z to A</SelectItem>
                    </SelectContent>
                  </Select>
                </Field>

                <Field label="Show top">
                  <Select
                    value={String(draft.query.limit ?? "all")}
                    onValueChange={(value) =>
                      patchQuery({ limit: value === "all" ? null : Number(value) })
                    }
                  >
                    <SelectTrigger className="h-9 text-[13px]">
                      <SelectValue />
                    </SelectTrigger>
                    <SelectContent>
                      {[5, 6, 8, 10, 12, 20, 50].map((n) => (
                        <SelectItem key={n} value={String(n)}>
                          Top {n}
                        </SelectItem>
                      ))}
                      <SelectItem value="all">All groups</SelectItem>
                    </SelectContent>
                  </Select>
                </Field>
              </div>

              {draft.query.limit ? (
                <Toggle
                  label="Roll the rest into “Other”"
                  hint="Keeps the total honest when the top N hides a long tail"
                  checked={draft.query.groupOther}
                  onChange={(groupOther) => patchQuery({ groupOther })}
                />
              ) : null}
            </TabsContent>

            {/* ---------------- Chart ---------------- */}
            <TabsContent value="chart" className="m-0 flex flex-col gap-4 p-4">
              <Field label="Chart type">
                <Select
                  value={draft.style}
                  onValueChange={(value) => patch({ style: value as ChartStyle })}
                >
                  <SelectTrigger className="h-9 text-[13px]">
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    {["Comparison", "Trend", "Composition", "Summary"].map(
                      (group) => {
                        const items = availableStyles.filter(
                          (style) => style.group === group
                        );
                        if (items.length === 0) return null;

                        return (
                          <SelectGroup key={group}>
                            <SelectLabel>{group}</SelectLabel>
                            {items.map((style) => (
                              <SelectItem key={style.value} value={style.value}>
                                {style.label}
                              </SelectItem>
                            ))}
                          </SelectGroup>
                        );
                      }
                    )}
                  </SelectContent>
                </Select>
              </Field>

              <Field label="Title" hint="Leave blank to name it from the query">
                <Input
                  value={draft.title ?? ""}
                  onChange={(event) =>
                    patch({ title: event.target.value || undefined })
                  }
                  placeholder={generatedTitle}
                  className="h-9 text-[13px]"
                />
              </Field>

              <div className="grid grid-cols-2 gap-3">
                <Field label="Width">
                  <Select
                    value={String(draft.span)}
                    onValueChange={(value) =>
                      patch({ span: Number(value) as WidgetSpan })
                    }
                  >
                    <SelectTrigger className="h-9 text-[13px]">
                      <SelectValue />
                    </SelectTrigger>
                    <SelectContent>
                      {([1, 2, 3, 4] as WidgetSpan[]).map((span) => (
                        <SelectItem key={span} value={String(span)}>
                          {SPAN_LABEL[span]}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                </Field>

                <Field label="Height">
                  <Select
                    value={draft.height}
                    onValueChange={(value) =>
                      patch({ height: value as WidgetHeight })
                    }
                  >
                    <SelectTrigger className="h-9 text-[13px]">
                      <SelectValue />
                    </SelectTrigger>
                    <SelectContent>
                      {(["short", "medium", "tall"] as WidgetHeight[]).map((h) => (
                        <SelectItem key={h} value={h}>
                          {HEIGHT_LABEL[h]}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                </Field>
              </div>

              <div className="flex flex-col gap-2.5 rounded-md border p-3">
                <Toggle
                  label="Legend"
                  checked={draft.options.legend}
                  onChange={(legend) => patchOptions({ legend })}
                />
                <Toggle
                  label="Grid lines"
                  checked={draft.options.grid}
                  onChange={(grid) => patchOptions({ grid })}
                />
                <Toggle
                  label="Data labels"
                  hint="Prints the value on each mark"
                  checked={draft.options.values}
                  onChange={(values) => patchOptions({ values })}
                />
                {draft.query.breakdown ? (
                  <Toggle
                    label="Show as 100%"
                    hint="Compares composition rather than volume"
                    checked={draft.options.stack100}
                    onChange={(stack100) => patchOptions({ stack100 })}
                  />
                ) : null}
                {draft.style === "table" ? (
                  <Toggle
                    label="Share column"
                    checked={draft.options.showShare}
                    onChange={(showShare) => patchOptions({ showShare })}
                  />
                ) : null}
              </div>

              {draft.style === "line" ||
              draft.style === "area" ||
              draft.style === "stackedArea" ? (
                <Field label="Line shape">
                  <Select
                    value={draft.options.curve}
                    onValueChange={(value) =>
                      patchOptions({ curve: value as "monotone" | "linear" | "step" })
                    }
                  >
                    <SelectTrigger className="h-9 text-[13px]">
                      <SelectValue />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value="monotone">Smooth</SelectItem>
                      <SelectItem value="linear">Straight</SelectItem>
                      <SelectItem value="step">Stepped</SelectItem>
                    </SelectContent>
                  </Select>
                </Field>
              ) : null}
            </TabsContent>

            {/* ---------------- Filters ---------------- */}
            <TabsContent value="filters" className="m-0 p-4">
              <FilterTab
                meta={meta}
                widget={draft}
                onChange={(filters) => patchQuery({ filters })}
                onMatchModeChange={(matchMode) => patchQuery({ matchMode })}
              />
            </TabsContent>
          </ScrollArea>
        </Tabs>

        <SheetFooter className="flex-row justify-end gap-2 border-t p-4">
          <Button variant="outline" onClick={() => onOpenChange(false)}>
            Cancel
          </Button>
          <Button
            onClick={() => {
              onSave(draft);
              onOpenChange(false);
            }}
          >
            Save widget
          </Button>
        </SheetFooter>
      </SheetContent>
    </Sheet>
  );
}

/* ------------------------------------------------------------------ *
 * Preview
 * ------------------------------------------------------------------ */

function Preview({
  draft,
  inheritedPeriod,
}: {
  draft: WidgetDef;
  inheritedPeriod: PeriodKey;
}) {
  const { result, loading, error } = useWidgetData(draft.query, inheritedPeriod);

  return (
    <div className="border-b bg-muted/30 px-4 py-3">
      <div className="flex items-center justify-between gap-2 pb-1.5">
        <span className="flex items-center gap-1.5 text-[11px] font-medium tracking-wide text-muted-foreground uppercase">
          <Sparkles className="size-3" />
          Live preview
        </span>
        {result ? (
          <span className="text-[11px] text-muted-foreground tabular-nums">
            {result.matchedRecords.toLocaleString("en-IN")} records ·{" "}
            {result.rows.length} groups
          </span>
        ) : null}
      </div>

      <div className="h-[168px] overflow-hidden rounded-md border bg-card">
        {loading ? (
          <div className="flex h-full items-center justify-center">
            <Loader2 className="size-4 animate-spin text-muted-foreground" />
          </div>
        ) : error ? (
          <div className="flex h-full items-center justify-center px-4 text-center">
            <p className="text-[12px] text-destructive">{error}</p>
          </div>
        ) : result ? (
          <ChartRenderer
            result={result}
            style={draft.style}
            options={draft.options}
            className="h-full"
          />
        ) : null}
      </div>
    </div>
  );
}

/* ------------------------------------------------------------------ *
 * Filters
 * ------------------------------------------------------------------ */

function FilterTab({
  meta,
  widget,
  onChange,
  onMatchModeChange,
}: {
  meta: DatasetMeta | undefined;
  widget: WidgetDef;
  onChange: (filters: WidgetDef["query"]["filters"]) => void;
  onMatchModeChange: (mode: WidgetDef["query"]["matchMode"]) => void;
}) {
  // The field being edited drives which value list is worth fetching; fetching
  // every select field's values up front would be dozens of requests for a
  // dropdown the user may never open.
  const active = widget.query.filters.at(-1)?.field ?? null;
  const selectField = meta?.filterFields.find(
    (field) => field.id === active && field.type === "select"
  );
  const values = useFieldValues(widget.query.dataset, selectField?.id ?? null);

  const fields = React.useMemo<FilterFieldDef[]>(
    () =>
      (meta?.filterFields ?? []).map((field) => ({
        id: field.id,
        label: field.label,
        type:
          field.type === "boolean"
            ? "select"
            : (field.type as FilterFieldDef["type"]),
        options:
          field.type === "boolean"
            ? [
                { label: "Yes", value: "true" },
                { label: "No", value: "false" },
              ]
            : field.id === selectField?.id
              ? values
              : undefined,
      })),
    [meta, selectField?.id, values]
  );

  if (!meta) {
    return (
      <p className="text-[13px] text-muted-foreground">
        Loading this object&apos;s fields…
      </p>
    );
  }

  return (
    <div className="flex flex-col gap-3">
      <p className="text-[12.5px] text-muted-foreground">
        These conditions apply to this widget only, on top of the dashboard&apos;s
        own filters. They run in SQL, so the counts describe every matching
        record — not just what fits on screen.
      </p>

      <AdvancedFilterPanel
        fields={fields}
        conditions={widget.query.filters}
        matchMode={widget.query.matchMode}
        onChange={onChange}
        onMatchModeChange={onMatchModeChange}
      />

      {widget.query.filters.length > 0 ? (
        <Button
          variant="ghost"
          size="sm"
          className="h-8 self-start"
          onClick={() => onChange([])}
        >
          <RotateCcw /> Clear all conditions
        </Button>
      ) : null}
    </div>
  );
}

/* ------------------------------------------------------------------ *
 * Small pieces
 * ------------------------------------------------------------------ */

function Field({
  label,
  hint,
  children,
}: {
  label: string;
  hint?: string;
  children: React.ReactNode;
}) {
  return (
    <div className="flex flex-col gap-1.5">
      <Label className="text-[12px] font-medium">{label}</Label>
      {children}
      {hint ? (
        <span className="text-[11px] text-muted-foreground">{hint}</span>
      ) : null}
    </div>
  );
}

function Toggle({
  label,
  hint,
  checked,
  onChange,
}: {
  label: string;
  hint?: string;
  checked: boolean;
  onChange: (checked: boolean) => void;
}) {
  return (
    <label className="flex cursor-pointer items-center justify-between gap-3">
      <span className="flex flex-col">
        <span className="text-[12.5px]">{label}</span>
        {hint ? (
          <span className="text-[11px] text-muted-foreground">{hint}</span>
        ) : null}
      </span>
      <Switch checked={checked} onCheckedChange={onChange} />
    </label>
  );
}

function BucketSelect({
  value,
  onChange,
}: {
  value: DateBucketKey;
  onChange: (bucket: DateBucketKey) => void;
}) {
  return (
    <Select value={value} onValueChange={(next) => onChange(next as DateBucketKey)}>
      <SelectTrigger className="h-9 w-28 text-[13px]">
        <SelectValue />
      </SelectTrigger>
      <SelectContent>
        {(Object.keys(BUCKET_LABELS) as DateBucketKey[]).map((bucket) => (
          <SelectItem key={bucket} value={bucket}>
            {BUCKET_LABELS[bucket]}
          </SelectItem>
        ))}
      </SelectContent>
    </Select>
  );
}
