"use client";

import * as React from "react";
import { useRouter } from "next/navigation";
import {
  DndContext,
  DragOverlay,
  PointerSensor,
  useDraggable,
  useDroppable,
  useSensor,
  useSensors,
  type DragEndEvent,
  type DragStartEvent,
} from "@dnd-kit/core";
import {
  Brain,
  CalendarClock,
  GripVertical,
  KanbanSquare,
  RotateCw,
  Target,
  TrendingUp,
  Trophy,
} from "lucide-react";
import { toast } from "sonner";

import { MetricStrip, ScoreCell, type Metric } from "@/components/crm/metrics";
import { PagePanel } from "@/components/shell/page-panel";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { ScrollArea, ScrollBar } from "@/components/ui/scroll-area";
import { Skeleton } from "@/components/ui/skeleton";
import {
  Tooltip,
  TooltipContent,
  TooltipTrigger,
} from "@/components/ui/tooltip";
import { ApiError } from "@/lib/api";
import {
  formatDate,
  formatMoney,
  humanise,
  opportunitiesApi,
  type OpportunityRow,
  type PipelineBoard,
} from "@/lib/crm-api";
import { opportunityStageTone } from "@/lib/crm-tones";
import { TONE_FILL, TONE_TEXT } from "@/components/crm/metrics";
import { cn } from "@/lib/utils";

/* ------------------------------------------------------------------ *
 * Card
 * ------------------------------------------------------------------ */

function DealCard({
  deal,
  onOpen,
  dragging = false,
}: {
  deal: OpportunityRow;
  onOpen?: () => void;
  dragging?: boolean;
}) {
  const overdue =
    !deal.actualCloseDate && new Date(deal.expectedCloseDate) < new Date();

  return (
    <div
      className={cn(
        "flex flex-col gap-1.5 rounded border bg-card p-2 text-[12px] shadow-xs",
        dragging && "rotate-1 shadow-lg"
      )}
    >
      <div className="flex items-start gap-1">
        <button
          type="button"
          onClick={onOpen}
          className="min-w-0 flex-1 text-left font-medium text-primary hover:underline"
        >
          <span className="line-clamp-2">{deal.name}</span>
        </button>
        <GripVertical className="mt-0.5 size-3 shrink-0 cursor-grab text-muted-foreground/50" />
      </div>

      {deal.contactName ? (
        <span className="truncate text-[11px] text-muted-foreground">
          {deal.contactName}
        </span>
      ) : null}

      <div className="flex items-center justify-between gap-1">
        <span className="font-semibold tabular-nums">{formatMoney(deal.amount)}</span>
        {deal.aiProbability !== null ? (
          <Tooltip>
            <TooltipTrigger asChild>
              <span>
                <ScoreCell score={deal.aiProbability} band={deal.aiBand} />
              </span>
            </TooltipTrigger>
            <TooltipContent className="text-[11px]">
              {deal.aiBand} — local win probability from stage, engagement and deal shape.
            </TooltipContent>
          </Tooltip>
        ) : null}
      </div>

      <div className="flex flex-wrap items-center gap-1 text-[10.5px] text-muted-foreground">
        <span
          className={cn(
            "inline-flex items-center gap-1",
            overdue && "text-red-600 dark:text-red-400"
          )}
        >
          <CalendarClock className="size-2.5" />
          {formatDate(deal.expectedCloseDate)}
        </span>
        <span>·</span>
        <span>{deal.daysInStage}d in stage</span>
        {deal.ownerName ? (
          <>
            <span>·</span>
            <span className="truncate">{deal.ownerName}</span>
          </>
        ) : null}
      </div>
    </div>
  );
}

/* ------------------------------------------------------------------ *
 * Column
 * ------------------------------------------------------------------ */

function DraggableCard({
  deal,
  onOpen,
}: {
  deal: OpportunityRow;
  onOpen: () => void;
}) {
  const { attributes, listeners, setNodeRef, isDragging } = useDraggable({
    id: String(deal.id),
    data: { deal },
  });

  return (
    <div
      ref={setNodeRef}
      {...attributes}
      {...listeners}
      className={cn("touch-none", isDragging && "opacity-40")}
    >
      <DealCard deal={deal} onOpen={onOpen} />
    </div>
  );
}

function StageColumn({
  stage,
  label,
  count,
  value,
  weighted,
  averageDays,
  deals,
  onOpen,
}: {
  stage: string;
  label: string;
  count: number;
  value: number;
  weighted: number;
  averageDays: number;
  deals: OpportunityRow[];
  onOpen: (deal: OpportunityRow) => void;
}) {
  const { setNodeRef, isOver } = useDroppable({ id: stage });
  const tone = opportunityStageTone(stage);

  return (
    <div
      ref={setNodeRef}
      className={cn(
        "flex w-[268px] shrink-0 flex-col rounded border bg-muted/30 transition-colors",
        isOver && "border-primary bg-primary/5"
      )}
    >
      <div className="flex flex-col gap-1 border-b px-2.5 py-2">
        <div className="flex items-center gap-1.5">
          <span className={cn("size-2 rounded-full", TONE_FILL[tone])} />
          <span className="flex-1 truncate text-[12.5px] font-semibold">{label}</span>
          <span className="rounded bg-background px-1.5 text-[11px] font-medium tabular-nums">
            {count}
          </span>
        </div>

        <div className="flex items-baseline justify-between text-[11px]">
          <span className={cn("font-semibold tabular-nums", TONE_TEXT[tone])}>
            {formatMoney(value)}
          </span>
          <Tooltip>
            <TooltipTrigger asChild>
              <span className="text-muted-foreground tabular-nums">
                {formatMoney(weighted)} wtd
              </span>
            </TooltipTrigger>
            <TooltipContent className="text-[11px]">
              Amount × probability across this column
            </TooltipContent>
          </Tooltip>
        </div>

        <span className="text-[10.5px] text-muted-foreground">
          Avg {averageDays.toFixed(0)} days in stage
        </span>
      </div>

      <ScrollArea className="max-h-[calc(100vh-25rem)] flex-1">
        <div className="flex flex-col gap-1.5 p-1.5">
          {deals.length === 0 ? (
            <p className="py-6 text-center text-[11.5px] text-muted-foreground">
              Nothing here.
            </p>
          ) : (
            deals.map((deal) => (
              <DraggableCard key={deal.id} deal={deal} onOpen={() => onOpen(deal)} />
            ))
          )}
        </div>
      </ScrollArea>
    </div>
  );
}

/* ------------------------------------------------------------------ *
 * Page
 * ------------------------------------------------------------------ */

export default function PipelinePage() {
  const router = useRouter();

  // Stored with the query that produced it, so "loading" is derived from a
  // key mismatch rather than a second piece of state set inside the effect.
  const [loaded, setLoaded] = React.useState<{
    key: string;
    board: PipelineBoard;
  } | null>(null);
  const [search, setSearch] = React.useState("");
  const [debounced, setDebounced] = React.useState("");
  const [nonce, setNonce] = React.useState(0);
  const [dragging, setDragging] = React.useState<OpportunityRow | null>(null);

  const queryKey = `${debounced}|${nonce}`;
  const board = loaded?.board ?? null;
  const loading = loaded === null || loaded.key !== queryKey;

  const sensors = useSensors(
    // A small activation distance keeps a click-to-open from being read as a
    // drag on the card's title.
    useSensor(PointerSensor, { activationConstraint: { distance: 6 } })
  );

  React.useEffect(() => {
    const handle = setTimeout(() => setDebounced(search), 250);
    return () => clearTimeout(handle);
  }, [search]);

  const reload = React.useCallback(() => setNonce((n) => n + 1), []);

  React.useEffect(() => {
    let cancelled = false;

    opportunitiesApi
      .pipeline({ search: debounced || undefined })
      .then((next) => {
        if (!cancelled) setLoaded({ key: queryKey, board: next });
      })
      .catch((error: unknown) => {
        if (cancelled) return;
        toast.error("Could not load the pipeline", {
          description: error instanceof ApiError ? error.message : "Network error.",
        });
      });

    return () => {
      cancelled = true;
    };
  }, [debounced, queryKey]);

  function handleDragStart(event: DragStartEvent) {
    setDragging((event.active.data.current?.deal as OpportunityRow) ?? null);
  }

  async function handleDragEnd(event: DragEndEvent) {
    const deal = dragging;
    setDragging(null);

    const target = event.over?.id ? String(event.over.id) : null;
    if (!deal || !target || target === deal.stage) return;

    // Optimistic: the card moves immediately and only rolls back if the API
    // rejects the move — a board that lags behind the drag feels broken.
    const previous = loaded;
    setLoaded((current) => {
      if (!current) return current;

      return {
        ...current,
        board: {
          ...current.board,
          columns: current.board.columns.map((column) => {
            if (column.stage === deal.stage) {
              return {
                ...column,
                count: column.count - 1,
                value: column.value - deal.amount,
                deals: column.deals.filter((d) => d.id !== deal.id),
              };
            }
            if (column.stage === target) {
              return {
                ...column,
                count: column.count + 1,
                value: column.value + deal.amount,
                deals: [{ ...deal, stage: target }, ...column.deals],
              };
            }
            return column;
          }),
        },
      };
    });

    try {
      await opportunitiesApi.moveStage(deal.id, {
        stage: target,
        lossReason:
          target === "ClosedLost" ? "Moved to lost from the pipeline board." : undefined,
      });
      toast.success(`${deal.name} → ${humanise(target)}`);
      reload();
    } catch (error) {
      setLoaded(previous);
      toast.error("Could not move this deal", {
        description: error instanceof ApiError ? error.message : "Network error.",
      });
    }
  }

  const metrics: Metric[] = board
    ? [
        { label: "Deals on board", value: board.totalDeals.toLocaleString(), icon: KanbanSquare },
        {
          label: "Open value",
          value: formatMoney(board.openValue),
          tone: "primary",
          icon: Target,
        },
        {
          label: "Weighted",
          value: formatMoney(board.weightedValue),
          tone: "violet",
          progress: board.openValue > 0 ? board.weightedValue / board.openValue : 0,
        },
        {
          label: "Win rate",
          value: `${board.winRate.toFixed(1)}%`,
          tone: board.winRate >= 50 ? "success" : "warning",
          icon: Trophy,
          progress: board.winRate / 100,
        },
        {
          label: "Avg deal age",
          value: `${board.averageAgeDays.toFixed(0)}d`,
          tone: "info",
          icon: TrendingUp,
        },
      ]
    : [];

  return (
    <PagePanel
      flush
      icon={KanbanSquare}
      title="Pipeline"
      hint="Drag a deal between stages to move it. Winning a deal that is attached to a unit takes that unit off the market — the safeguard against selling the same flat twice."
      actions={
        <>
          {board ? (
            <span
              className="inline-flex h-8 items-center gap-1.5 rounded border px-2 text-[12px] text-muted-foreground"
              title={`Win probability from ${board.scoringEngine}`}
            >
              <Brain className="size-3.5 text-primary" />
              {board.scoringEngine.startsWith("MLNet") ? "ML model" : "Heuristic"}
            </span>
          ) : null}
          <Button
            variant="ghost"
            size="icon"
            aria-label="Refresh"
            className="size-8 text-muted-foreground"
            onClick={reload}
          >
            <RotateCw className={cn("size-4", loading && "animate-spin")} />
          </Button>
        </>
      }
      toolbar={
        <Input
          value={search}
          onChange={(event) => setSearch(event.target.value)}
          placeholder="Search deals, contacts, competitors…"
          className="h-8 w-72 text-[13px]"
        />
      }
    >
      <div className="flex min-h-0 flex-1 flex-col">
        <div className="border-b p-2.5">
          <MetricStrip metrics={metrics} loading={!board} />
        </div>

        {!board ? (
          <div className="flex gap-2 p-2.5">
            {Array.from({ length: 5 }, (_, i) => (
              <Skeleton key={i} className="h-80 w-[268px] shrink-0" />
            ))}
          </div>
        ) : (
          <DndContext
            sensors={sensors}
            onDragStart={handleDragStart}
            onDragEnd={handleDragEnd}
          >
            <ScrollArea className="w-full flex-1">
              <div className="flex gap-2 p-2.5">
                {board.columns.map((column) => (
                  <StageColumn
                    key={column.stage}
                    stage={column.stage}
                    label={column.label}
                    count={column.count}
                    value={column.value}
                    weighted={column.weightedValue}
                    averageDays={column.averageDaysInStage}
                    deals={column.deals}
                    onOpen={(deal) =>
                      deal.leadId
                        ? router.push(`/dashboard/leads/${deal.leadId}`)
                        : router.push("/dashboard/leads/opportunities")
                    }
                  />
                ))}
              </div>
              <ScrollBar orientation="horizontal" />
            </ScrollArea>

            <DragOverlay>
              {dragging ? (
                <div className="w-[252px]">
                  <DealCard deal={dragging} dragging />
                </div>
              ) : null}
            </DragOverlay>
          </DndContext>
        )}
      </div>
    </PagePanel>
  );
}
