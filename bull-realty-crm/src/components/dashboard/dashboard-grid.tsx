"use client";

import * as React from "react";
import {
  DndContext,
  KeyboardSensor,
  PointerSensor,
  closestCenter,
  useSensor,
  useSensors,
  type DragEndEvent,
} from "@dnd-kit/core";
import {
  SortableContext,
  arrayMove,
  rectSortingStrategy,
  sortableKeyboardCoordinates,
  useSortable,
} from "@dnd-kit/sortable";
import { CSS } from "@dnd-kit/utilities";
import {
  AlertCircle,
  Copy,
  Download,
  GripVertical,
  Maximize2,
  MoreHorizontal,
  Pencil,
  RefreshCw,
  Trash2,
} from "lucide-react";
import { toast } from "sonner";

import { ChartRenderer } from "@/components/dashboard/widgets/chart-renderer";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { CrmLoaderMark } from "@/components/shell/crm-loader";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuSub,
  DropdownMenuSubContent,
  DropdownMenuSubTrigger,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
import { PERIODS, type AggregateResponse, type PeriodKey } from "@/lib/dashboard/analytics";
import { formatValue } from "@/lib/dashboard/format";
import {
  HEIGHT_CLASS,
  SPAN_LABEL,
  describeWidget,
  newWidgetId,
  stylesFor,
  styleLabel,
  type ChartStyle,
  type WidgetDef,
  type WidgetSpan,
} from "@/lib/dashboard/types";
import { useDatasets, useWidgetData } from "@/lib/dashboard/use-analytics";
import { cn } from "@/lib/utils";

/** Static span classes — Tailwind cannot see dynamically built class names. */
const SPAN_CLASS: Record<WidgetSpan, string> = {
  1: "sm:col-span-1 lg:col-span-1",
  2: "sm:col-span-2 lg:col-span-2",
  3: "sm:col-span-2 lg:col-span-3",
  4: "sm:col-span-2 lg:col-span-4",
};

/* ------------------------------------------------------------------ *
 * Grid
 * ------------------------------------------------------------------ */

interface DashboardGridProps {
  widgets: WidgetDef[];
  editing: boolean;
  onChange: (widgets: WidgetDef[]) => void;
  onEdit: (widget: WidgetDef) => void;
  period: PeriodKey;
  scopeLabel: string;
  refreshToken: number;
  /** Clicking a mark adds it as a filter on the dashboard. */
  onDrill?: (widget: WidgetDef, key: string, label: string) => void;
}

export function DashboardGrid({
  widgets,
  editing,
  onChange,
  onEdit,
  period,
  scopeLabel,
  refreshToken,
  onDrill,
}: DashboardGridProps) {
  const sensors = useSensors(
    useSensor(PointerSensor, { activationConstraint: { distance: 6 } }),
    useSensor(KeyboardSensor, { coordinateGetter: sortableKeyboardCoordinates })
  );

  function handleDragEnd(event: DragEndEvent) {
    const { active, over } = event;
    if (!over || active.id === over.id) return;

    const from = widgets.findIndex((widget) => widget.id === active.id);
    const to = widgets.findIndex((widget) => widget.id === over.id);
    if (from === -1 || to === -1) return;

    onChange(arrayMove(widgets, from, to));
  }

  function update(id: string, patch: Partial<WidgetDef>) {
    onChange(
      widgets.map((widget) =>
        widget.id === id ? { ...widget, ...patch } : widget
      )
    );
  }

  // useCallback marks this as an event handler rather than render code, which
  // is what lets it mint a random id — the compiler rejects impure calls it
  // cannot prove run outside render.
  const duplicate = React.useCallback(
    (widget: WidgetDef) => {
      const copy: WidgetDef = {
        ...widget,
        id: newWidgetId(),
        title: widget.title ? `${widget.title} (copy)` : undefined,
      };
      const index = widgets.findIndex((entry) => entry.id === widget.id);
      const next = [...widgets];
      next.splice(index + 1, 0, copy);
      onChange(next);
      toast.success("Widget duplicated");
    },
    [widgets, onChange]
  );

  const grid = (
    <div className="grid grid-cols-1 gap-3 sm:grid-cols-2 lg:grid-cols-4">
      {widgets.map((widget) => (
        <WidgetCard
          key={widget.id}
          widget={widget}
          editing={editing}
          period={period}
          scopeLabel={scopeLabel}
          refreshToken={refreshToken}
          onEdit={() => onEdit(widget)}
          onDuplicate={() => duplicate(widget)}
          onPatch={(patch) => update(widget.id, patch)}
          onRemove={() => onChange(widgets.filter((w) => w.id !== widget.id))}
          onDrill={onDrill ? (key, label) => onDrill(widget, key, label) : undefined}
        />
      ))}
    </div>
  );

  if (!editing) return grid;

  return (
    <DndContext
      sensors={sensors}
      collisionDetection={closestCenter}
      onDragEnd={handleDragEnd}
    >
      <SortableContext
        items={widgets.map((widget) => widget.id)}
        strategy={rectSortingStrategy}
      >
        {grid}
      </SortableContext>
    </DndContext>
  );
}

/* ------------------------------------------------------------------ *
 * Card
 * ------------------------------------------------------------------ */

interface WidgetCardProps {
  widget: WidgetDef;
  editing: boolean;
  period: PeriodKey;
  scopeLabel: string;
  refreshToken: number;
  onEdit: () => void;
  onDuplicate: () => void;
  onPatch: (patch: Partial<WidgetDef>) => void;
  onRemove: () => void;
  onDrill?: (key: string, label: string) => void;
}

function WidgetCard({
  widget,
  editing,
  period,
  scopeLabel,
  refreshToken,
  onEdit,
  onDuplicate,
  onPatch,
  onRemove,
  onDrill,
}: WidgetCardProps) {
  const [localRefresh, setLocalRefresh] = React.useState(0);
  const [expanded, setExpanded] = React.useState(false);
  const { datasets } = useDatasets();

  const { result, loading, error } = useWidgetData(
    widget.query,
    period,
    refreshToken + localRefresh
  );

  const {
    attributes,
    listeners,
    setNodeRef,
    transform,
    transition,
    isDragging,
  } = useSortable({ id: widget.id, disabled: !editing });

  const meta = datasets.find((entry) => entry.id === widget.query.dataset);
  const title =
    widget.title ??
    describeWidget(widget.query, {
      dataset: meta?.label,
      dimension: meta?.dimensions.find((d) => d.id === widget.query.dimension)?.label,
      breakdown: meta?.dimensions.find((d) => d.id === widget.query.breakdown)?.label,
      measure: meta?.measures.find((m) => m.id === widget.query.measure)?.label,
    });

  const style: React.CSSProperties = {
    transform: CSS.Translate.toString(transform),
    transition,
  };

  // A widget with its own period is not described by the dashboard's scope
  // line, so it names the window it actually used — "own period" would leave
  // the reader guessing which one.
  const context =
    widget.query.period === "inherit"
      ? scopeLabel
      : `${scopeLabel.split(" · ")[0]} · ${
          PERIODS.find((entry) => entry.value === widget.query.period)?.label ??
          "custom period"
        }`;

  const styleOptions = stylesFor(Boolean(widget.query.breakdown));

  return (
    <>
      <div
        ref={setNodeRef}
        style={style}
        className={cn(
          "flex flex-col overflow-hidden rounded-lg border bg-card shadow-xs",
          SPAN_CLASS[widget.span],
          isDragging && "z-10 opacity-80 shadow-lg ring-2 ring-primary/40",
          editing && "ring-1 ring-dashed ring-border"
        )}
      >
        <div className="flex shrink-0 items-start gap-1.5 border-b px-3 py-2">
          {editing ? (
            <button
              type="button"
              aria-label="Drag to reorder"
              className="-ml-1 mt-0.5 cursor-grab touch-none rounded p-1 text-muted-foreground hover:bg-accent active:cursor-grabbing"
              {...attributes}
              {...listeners}
            >
              <GripVertical className="size-4" />
            </button>
          ) : null}

          <div className="min-w-0 flex-1">
            <h3 className="truncate text-[13px] font-semibold" title={title}>
              {title}
            </h3>
            <p className="truncate text-[11px] text-muted-foreground">
              {context}
              {result && result.truncatedGroups > 0
                ? ` · ${result.truncatedGroups} more hidden`
                : ""}
            </p>
          </div>

          {/* The metric style already prints the total as its whole body — a
              badge repeating it just reads as a duplicated number. */}
          {result && !loading && widget.style !== "metric" ? (
            <Badge
              variant="outline"
              className="h-5 shrink-0 px-1.5 text-[10px] font-normal tabular-nums"
              title={`${result.measureLabel} across ${result.matchedRecords} records`}
            >
              {formatValue(result.total, result.format, "axis")}
            </Badge>
          ) : null}

          <DropdownMenu>
            <DropdownMenuTrigger asChild>
              <Button
                variant="ghost"
                size="icon"
                aria-label={`${title} options`}
                className="size-6 shrink-0 text-muted-foreground"
              >
                <MoreHorizontal className="size-4" />
              </Button>
            </DropdownMenuTrigger>
            <DropdownMenuContent align="end" className="w-52">
              <DropdownMenuLabel className="text-[11px] font-normal text-muted-foreground">
                {meta?.label ?? widget.query.dataset} · {styleLabel(widget.style)}
              </DropdownMenuLabel>
              <DropdownMenuSeparator />

              <DropdownMenuItem onClick={onEdit}>
                <Pencil /> Configure
              </DropdownMenuItem>

              {/* Chart type is on the card as well as in the editor: trying
                  three shapes is the fastest way to find the readable one. */}
              <DropdownMenuSub>
                <DropdownMenuSubTrigger>
                  <RefreshCw /> Chart type
                </DropdownMenuSubTrigger>
                <DropdownMenuSubContent>
                  {styleOptions.map((option) => (
                    <DropdownMenuItem
                      key={option.value}
                      onClick={() => onPatch({ style: option.value as ChartStyle })}
                      className={cn(
                        widget.style === option.value && "bg-accent font-medium"
                      )}
                    >
                      {option.label}
                    </DropdownMenuItem>
                  ))}
                </DropdownMenuSubContent>
              </DropdownMenuSub>

              {editing ? (
                <DropdownMenuSub>
                  <DropdownMenuSubTrigger>
                    <Maximize2 /> Width
                  </DropdownMenuSubTrigger>
                  <DropdownMenuSubContent>
                    {([1, 2, 3, 4] as WidgetSpan[]).map((span) => (
                      <DropdownMenuItem
                        key={span}
                        onClick={() => onPatch({ span })}
                        className={cn(widget.span === span && "bg-accent font-medium")}
                      >
                        {SPAN_LABEL[span]}
                      </DropdownMenuItem>
                    ))}
                  </DropdownMenuSubContent>
                </DropdownMenuSub>
              ) : null}

              <DropdownMenuSeparator />

              <DropdownMenuItem onClick={() => setExpanded(true)}>
                <Maximize2 /> Expand
              </DropdownMenuItem>
              <DropdownMenuItem onClick={() => setLocalRefresh((n) => n + 1)}>
                <RefreshCw /> Refresh
              </DropdownMenuItem>
              <DropdownMenuItem
                disabled={!result}
                onClick={() => result && exportCsv(result, title)}
              >
                <Download /> Export CSV
              </DropdownMenuItem>
              <DropdownMenuItem onClick={onDuplicate}>
                <Copy /> Duplicate
              </DropdownMenuItem>

              <DropdownMenuSeparator />
              <DropdownMenuItem variant="destructive" onClick={onRemove}>
                <Trash2 /> Remove
              </DropdownMenuItem>
            </DropdownMenuContent>
          </DropdownMenu>
        </div>

        <div className={cn("min-w-0", HEIGHT_CLASS[widget.height])}>
          <WidgetBody
            result={result}
            loading={loading}
            error={error}
            widget={widget}
            onDrill={onDrill}
          />
        </div>
      </div>

      <Dialog open={expanded} onOpenChange={setExpanded}>
        <DialogContent className="sm:max-w-5xl">
          <DialogHeader>
            <DialogTitle className="text-base">{title}</DialogTitle>
            <DialogDescription>{context}</DialogDescription>
          </DialogHeader>
          <div className="h-[60vh]">
            <WidgetBody
              result={result}
              loading={loading}
              error={error}
              widget={widget}
              onDrill={onDrill}
            />
          </div>
        </DialogContent>
      </Dialog>
    </>
  );
}

function WidgetBody({
  result,
  loading,
  error,
  widget,
  onDrill,
}: {
  result: AggregateResponse | null;
  loading: boolean;
  error: string | null;
  widget: WidgetDef;
  onDrill?: (key: string, label: string) => void;
}) {
  // A stale chart stays on screen while the next one loads — blanking the card
  // on every filter change makes the whole dashboard flicker.
  if (error && !result) {
    return (
      <div className="flex h-full flex-col items-center justify-center gap-1.5 px-4 text-center">
        <AlertCircle className="size-4 text-destructive" />
        <p className="text-[12px] text-destructive">{error}</p>
      </div>
    );
  }

  if (!result) {
    return (
      <div className="flex h-full items-center justify-center">
        <CrmLoaderMark size={36} />
      </div>
    );
  }

  return (
    <div className={cn("h-full", loading && "opacity-60 transition-opacity")}>
      <ChartRenderer
        result={result}
        style={widget.style}
        options={widget.options}
        onDrill={onDrill}
        className="h-full"
      />
    </div>
  );
}

/* ------------------------------------------------------------------ *
 * Export
 * ------------------------------------------------------------------ */

/** The widget's own rows, as the CSV someone can paste into a spreadsheet. */
function exportCsv(result: AggregateResponse, title: string) {
  const header = [result.dimension, ...result.series];
  const lines = [
    header.map(quote).join(","),
    ...result.rows.map((row) =>
      [row.label, ...result.series.map((s) => row.values[s] ?? 0)]
        .map(quote)
        .join(",")
    ),
  ];

  const blob = new Blob([lines.join("\n")], { type: "text/csv;charset=utf-8" });
  const url = URL.createObjectURL(blob);
  const link = document.createElement("a");
  link.href = url;
  link.download = `${title.replace(/[^a-z0-9]+/gi, "-").toLowerCase()}.csv`;
  link.click();
  URL.revokeObjectURL(url);

  toast.success("CSV exported", { description: `${result.rows.length} rows.` });
}

function quote(value: string | number) {
  const text = String(value);
  return /[",\n]/.test(text) ? `"${text.replace(/"/g, '""')}"` : text;
}
