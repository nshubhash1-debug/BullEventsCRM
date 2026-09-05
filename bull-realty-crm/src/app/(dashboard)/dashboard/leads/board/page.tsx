"use client";

import * as React from "react";
import { useRouter } from "next/navigation";
import {
  DndContext,
  PointerSensor,
  useDraggable,
  useDroppable,
  useSensor,
  useSensors,
  type DragEndEvent,
} from "@dnd-kit/core";
import { CalendarClock, KanbanSquare, RotateCw } from "lucide-react";
import { toast } from "sonner";

import { PagePanel } from "@/components/shell/page-panel";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { ScrollArea } from "@/components/ui/scroll-area";
import { Skeleton } from "@/components/ui/skeleton";
import { ApiError, LEAD_STAGES } from "@/lib/api";
import {
  formatDate,
  formatMoney,
  leadsApi,
  type LeadBoard,
  type LeadRow,
} from "@/lib/crm-api";
import { leadStageTone } from "@/lib/crm-tones";
import { TONE_FILL, TONE_TEXT } from "@/components/crm/metrics";
import { cn } from "@/lib/utils";

function Card({ lead, onOpen }: { lead: LeadRow; onOpen: () => void }) {
  const { attributes, listeners, setNodeRef, isDragging } = useDraggable({
    id: String(lead.id),
    data: { lead },
  });

  return (
    <div
      ref={setNodeRef}
      {...listeners}
      {...attributes}
      className={cn(
        "flex cursor-grab flex-col gap-1 rounded border bg-card p-2 text-[12px]",
        isDragging && "opacity-50"
      )}
    >
      <button
        type="button"
        className="text-left font-medium text-primary hover:underline"
        onClick={onOpen}
      >
        {lead.name}
      </button>
      <span className="text-muted-foreground">
        {lead.eventType ?? "Occasion tbd"} · {lead.guestCount ?? "—"} guests
      </span>
      <span className="tabular-nums text-muted-foreground">
        {lead.eventDate ? formatDate(lead.eventDate) : "Date not fixed"}
      </span>
      <span className="tabular-nums">{formatMoney(lead.budgetMax ?? 0)}</span>
      {lead.slaState === "Breached" ? (
        <span className="text-[10px] text-destructive">SLA breached</span>
      ) : null}
    </div>
  );
}

function Column({
  stage,
  label,
  count,
  value,
  averageDays,
  leads,
  onOpen,
}: {
  stage: string;
  label: string;
  count: number;
  value: number;
  averageDays: number;
  leads: LeadRow[];
  onOpen: (lead: LeadRow) => void;
}) {
  const { setNodeRef, isOver } = useDroppable({ id: stage });
  const tone = leadStageTone(stage);

  return (
    <div
      ref={setNodeRef}
      className={cn(
        "flex w-[260px] shrink-0 flex-col rounded border bg-muted/30",
        isOver && "ring-2 ring-primary/40"
      )}
    >
      <div className="flex items-center justify-between gap-2 border-b px-2.5 py-2">
        <span className={cn("text-[12px] font-semibold", TONE_TEXT[tone])}>
          {label}
        </span>
        <span className="text-[11px] text-muted-foreground tabular-nums">
          {count}
        </span>
      </div>
      <div className="flex items-center justify-between px-2.5 py-1 text-[10.5px] text-muted-foreground">
        <span>{formatMoney(value)}</span>
        <span className="inline-flex items-center gap-1">
          <CalendarClock className="size-3" />
          {averageDays.toFixed(0)}d
        </span>
      </div>
      <ScrollArea className="max-h-[calc(100vh-24rem)] flex-1">
        <div className="flex flex-col gap-1.5 p-1.5">
          {leads.length === 0 ? (
            <p className="py-6 text-center text-[11.5px] text-muted-foreground">
              Nothing here.
            </p>
          ) : (
            leads.map((lead) => (
              <Card key={lead.id} lead={lead} onOpen={() => onOpen(lead)} />
            ))
          )}
        </div>
      </ScrollArea>
      <div className={cn("h-0.5", TONE_FILL[tone])} />
    </div>
  );
}

export default function EnquiryBoardPage() {
  const router = useRouter();
  const [board, setBoard] = React.useState<LeadBoard | null>(null);
  const [search, setSearch] = React.useState("");
  const [month, setMonth] = React.useState("");
  const [loading, setLoading] = React.useState(false);

  const sensors = useSensors(
    useSensor(PointerSensor, { activationConstraint: { distance: 6 } })
  );

  const load = React.useCallback(() => {
    setLoading(true);
    leadsApi
      .board({ search: search || undefined, month: month || undefined })
      .then(setBoard)
      .catch((error: unknown) => {
        toast.error("Could not load the enquiry board", {
          description: error instanceof ApiError ? error.message : "Network error.",
        });
      })
      .finally(() => setLoading(false));
  }, [search, month]);

  React.useEffect(() => {
    const handle = setTimeout(load, 200);
    return () => clearTimeout(handle);
  }, [load]);

  async function handleDragEnd(event: DragEndEvent) {
    const lead = event.active.data.current?.lead as LeadRow | undefined;
    const target = event.over?.id ? String(event.over.id) : null;
    if (!lead || !target || target === lead.stage) return;

    try {
      await leadsApi.bulkStage([lead.id], target);
      toast.success(`${lead.name} → ${LEAD_STAGES.find((s) => s.value === target)?.label ?? target}`);
      load();
    } catch (error) {
      toast.error("Could not move this enquiry", {
        description: error instanceof ApiError ? error.message : "Network error.",
      });
    }
  }

  return (
    <PagePanel
      flush
      icon={KanbanSquare}
      title="Enquiry board"
      hint="Drag an enquiry between stages. Time on each column is days since last update."
      actions={
        <Button
          variant="ghost"
          size="icon"
          aria-label="Refresh"
          className="size-8 text-muted-foreground"
          onClick={load}
        >
          <RotateCw className={cn("size-4", loading && "animate-spin")} />
        </Button>
      }
      toolbar={
        <div className="flex gap-2">
          <Input
            value={search}
            onChange={(event) => setSearch(event.target.value)}
            placeholder="Search couples…"
            className="h-8 w-64 text-[13px]"
          />
          <Input
            type="month"
            value={month}
            onChange={(event) => setMonth(event.target.value)}
            className="h-8 w-40 text-[13px]"
          />
        </div>
      }
    >
      {!board ? (
        <div className="flex gap-2 p-2.5">
          {Array.from({ length: 7 }, (_, i) => (
            <Skeleton key={i} className="h-80 w-[260px] shrink-0" />
          ))}
        </div>
      ) : (
        <DndContext sensors={sensors} onDragEnd={handleDragEnd}>
          <ScrollArea className="w-full flex-1">
            <div className="flex gap-2 p-2.5">
              {board.columns.map((column) => (
                <Column
                  key={column.stage}
                  stage={column.stage}
                  label={column.label}
                  count={column.count}
                  value={column.value}
                  averageDays={column.averageDaysInStage}
                  leads={column.leads}
                  onOpen={(lead) => router.push(`/dashboard/leads/${lead.id}`)}
                />
              ))}
            </div>
          </ScrollArea>
        </DndContext>
      )}
    </PagePanel>
  );
}
