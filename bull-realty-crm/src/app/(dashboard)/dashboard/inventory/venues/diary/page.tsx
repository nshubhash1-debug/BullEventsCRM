"use client";

import * as React from "react";
import Link from "next/link";
import { CalendarDays, ChevronLeft, ChevronRight, Loader2, RefreshCw } from "lucide-react";
import { toast } from "sonner";

import { STATUS_STYLES } from "@/components/inventory/status";
import { UnitSheet } from "@/components/inventory/unit-sheet";
import { QuoteBuilder } from "@/components/inventory/quote-builder";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import {
  Dialog,
  DialogContent,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { Skeleton } from "@/components/ui/skeleton";
import { ApiError } from "@/lib/api";
import { inventoryApi, type ProjectRow } from "@/lib/crm-api";
import {
  diaryApi,
  EVENT_SLOTS,
  formatRupees,
  type BoardUnit,
  type DiaryCell,
  type DiarySpaceRow,
  type EventSlot,
  type InventoryDiary,
  type UnitStatus,
} from "@/lib/inventory-api";
import { cn } from "@/lib/utils";

function iso(d: Date) {
  const m = String(d.getMonth() + 1).padStart(2, "0");
  const day = String(d.getDate()).padStart(2, "0");
  return `${d.getFullYear()}-${m}-${day}`;
}

function addDays(isoDate: string, n: number) {
  const d = new Date(`${isoDate}T12:00:00`);
  d.setDate(d.getDate() + n);
  return iso(d);
}

function shortDay(isoDate: string) {
  const d = new Date(`${isoDate}T12:00:00`);
  return d.toLocaleDateString(undefined, { weekday: "short", day: "numeric", month: "short" });
}

/**
 * Week diary — spaces as rows, days as columns. The venue operator home screen.
 */
export default function VenueDiaryPage() {
  const [projects, setProjects] = React.useState<ProjectRow[]>([]);
  const [projectId, setProjectId] = React.useState("");
  const [from, setFrom] = React.useState(() => iso(new Date()));
  const [slot, setSlot] = React.useState<EventSlot>("Evening");
  const [diary, setDiary] = React.useState<InventoryDiary | null>(null);
  const [loading, setLoading] = React.useState(true);

  const [move, setMove] = React.useState<{
    space: DiarySpaceRow;
    cell: DiaryCell;
  } | null>(null);
  const [status, setStatus] = React.useState<UnitStatus>("Held");
  const [clientName, setClientName] = React.useState("");
  const [holdHours, setHoldHours] = React.useState("48");
  const [notes, setNotes] = React.useState("");
  const [saving, setSaving] = React.useState(false);

  const [sheetUnit, setSheetUnit] = React.useState<BoardUnit | null>(null);
  const [sheetOpen, setSheetOpen] = React.useState(false);
  const [quoteOpen, setQuoteOpen] = React.useState(false);

  const to = addDays(from, 6);

  React.useEffect(() => {
    inventoryApi
      .projects()
      .then((rows) => {
        setProjects(rows);
        const preferred = rows.find((p) => p.totalUnits > 0) ?? rows[0];
        if (preferred) setProjectId(String(preferred.id));
      })
      .catch(() => setProjects([]));
  }, []);

  const load = React.useCallback(() => {
    if (!projectId) return;
    setLoading(true);
    diaryApi
      .get(Number(projectId), from, to, slot)
      .then(setDiary)
      .catch((error) => {
        setDiary(null);
        toast.error("Could not load the diary", {
          description: error instanceof ApiError ? error.message : "Network error.",
        });
      })
      .finally(() => setLoading(false));
  }, [projectId, from, to, slot]);

  React.useEffect(() => {
    load();
  }, [load]);

  async function applyMove() {
    if (!move || !projectId) return;
    setSaving(true);
    try {
      const result = await diaryApi.move(Number(projectId), {
        unitId: move.space.unitId,
        eventDate: String(move.cell.date).slice(0, 10),
        slot,
        status,
        clientName: clientName.trim() || null,
        notes: notes.trim() || null,
        holdHours: status === "Held" ? Number(holdHours) || 48 : null,
      });
      toast.success(result.message);
      setMove(null);
      load();
    } catch (error) {
      toast.error("Could not update the calendar", {
        description: error instanceof ApiError ? error.message : "Network error.",
      });
    } finally {
      setSaving(false);
    }
  }

  async function clearCell() {
    if (!move?.cell.spaceBookingId) return;
    setSaving(true);
    try {
      await diaryApi.release(move.cell.spaceBookingId);
      toast.success("Calendar slot released");
      setMove(null);
      load();
    } catch (error) {
      toast.error("Could not release", {
        description: error instanceof ApiError ? error.message : "Network error.",
      });
    } finally {
      setSaving(false);
    }
  }

  function openSpace(space: DiarySpaceRow) {
    setSheetUnit({
      id: space.unitId,
      unitNumber: space.name,
      floor: 0,
      position: 1,
      configuration: space.spaceType,
      carpetArea: 0,
      builtUpArea: null,
      superArea: null,
      plcPerSqft: 0,
      ratePerSqft: 0,
      effectiveRate: 0,
      totalPrice: space.basePrice,
      status: (space.catalogueStatus as UnitStatus) || "Available",
      isCornerUnit: false,
      facing: null,
      heldByUserId: null,
      heldByName: null,
      heldUntil: null,
      holdReason: null,
      bookedByContactId: null,
      bookedByLeadId: null,
      customerName: null,
      customerPhone: null,
      salesPersonId: null,
      salesPersonName: null,
      bookedAt: null,
      bookedQuotationId: null,
      blockReason: null,
      pendingApprovalId: null,
    });
    setSheetOpen(true);
  }

  return (
    <div className="flex min-h-0 flex-1 flex-col gap-3 p-4">
      <header className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <h1 className="flex items-center gap-2 text-lg font-semibold">
            <CalendarDays className="size-4.5 text-primary" />
            {diary?.projectName ?? "Venue diary"}
          </h1>
          <p className="text-[12.5px] text-muted-foreground">
            Spaces × days for {slot}. Peak days are marked. Click a cell to hold,
            book, confirm or blackout.
          </p>
        </div>
        <div className="flex flex-wrap items-center gap-2">
          <Button asChild variant="outline" size="sm" className="h-8">
            <Link href="/dashboard/inventory/venues/board">Day board</Link>
          </Button>
          <Button asChild variant="outline" size="sm" className="h-8">
            <Link href="/dashboard/inventory/venues/packages">Packages</Link>
          </Button>
          <Button variant="outline" size="sm" className="h-8" onClick={load}>
            <RefreshCw className={cn("size-3.5", loading && "animate-spin")} />
            Refresh
          </Button>
        </div>
      </header>

      <div className="flex flex-wrap items-center gap-2 rounded-lg border bg-card p-2">
        <Button
          variant="outline"
          size="sm"
          className="h-8"
          onClick={() => setFrom(addDays(from, -7))}
        >
          <ChevronLeft className="size-3.5" />
        </Button>
        <Input
          type="date"
          value={from}
          onChange={(e) => setFrom(e.target.value)}
          className="h-8 w-[10.5rem] text-[12.5px]"
        />
        <Button
          variant="outline"
          size="sm"
          className="h-8"
          onClick={() => setFrom(addDays(from, 7))}
        >
          <ChevronRight className="size-3.5" />
        </Button>
        <span className="text-[12px] text-muted-foreground">
          → {to}
        </span>

        <Select value={slot} onValueChange={(v) => setSlot(v as EventSlot)}>
          <SelectTrigger size="sm" className="w-36 text-[12.5px]">
            <SelectValue />
          </SelectTrigger>
          <SelectContent>
            {EVENT_SLOTS.map((s) => (
              <SelectItem key={s} value={s}>
                {s === "FullDay" ? "Full day" : s}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>

        <Select value={projectId} onValueChange={setProjectId}>
          <SelectTrigger size="sm" className="w-52 text-[12.5px]">
            <SelectValue placeholder="Venue" />
          </SelectTrigger>
          <SelectContent>
            {projects.map((p) => (
              <SelectItem key={p.id} value={String(p.id)}>
                {p.name}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
      </div>

      {diary?.peakWindows.length ? (
        <p className="text-[12px] text-amber-700 dark:text-amber-300">
          Peak:{" "}
          {diary.peakWindows
            .map(
              (p) =>
                `${p.label} ${p.startDate}–${p.endDate} (+${Math.round(p.premiumFraction * 100)}%)`
            )
            .join(" · ")}
        </p>
      ) : null}

      <div className="min-h-0 flex-1 overflow-auto rounded-lg border bg-card">
        {loading || !diary ? (
          <div className="space-y-2 p-4">
            <Skeleton className="h-10 w-full" />
            <Skeleton className="h-64 w-full" />
          </div>
        ) : diary.spaces.length === 0 ? (
          <div className="flex h-56 items-center justify-center text-[13px] text-muted-foreground">
            No bookable spaces on this venue yet.
          </div>
        ) : (
          <table className="w-full min-w-[880px] border-collapse text-[12px]">
            <thead className="sticky top-0 z-10 bg-muted/80 backdrop-blur">
              <tr>
                <th className="sticky left-0 z-20 bg-muted/90 px-3 py-2 text-left font-medium">
                  Space
                </th>
                {diary.spaces[0]?.days.map((day) => (
                  <th
                    key={day.date}
                    className={cn(
                      "px-1.5 py-2 text-center font-medium",
                      day.isPeak && "text-amber-700 dark:text-amber-300"
                    )}
                  >
                    {shortDay(day.date)}
                    {day.isPeak ? (
                      <span className="mt-0.5 block text-[10px] font-normal">Peak</span>
                    ) : null}
                  </th>
                ))}
              </tr>
            </thead>
            <tbody>
              {diary.spaces.map((space) => (
                <tr key={space.unitId} className="border-t">
                  <td className="sticky left-0 z-10 bg-card px-3 py-2">
                    <button
                      type="button"
                      className="text-left hover:underline"
                      onClick={() => openSpace(space)}
                    >
                      <div className="font-medium">{space.name}</div>
                      <div className="text-[11px] text-muted-foreground">
                        {space.spaceType.replace(/([a-z])([A-Z])/g, "$1 $2")} ·{" "}
                        {space.seatingCapacity} seats
                        {space.pricePerPlate > 0
                          ? ` · ${formatRupees(space.pricePerPlate)}/plate`
                          : ""}
                      </div>
                    </button>
                  </td>
                  {space.days.map((cell) => {
                    const style = STATUS_STYLES[cell.status] ?? STATUS_STYLES.Available;
                    return (
                      <td key={cell.date} className="px-1 py-1">
                        <button
                          type="button"
                          title={
                            cell.clientName
                              ? `${cell.status}: ${cell.clientName}`
                              : cell.status
                          }
                          onClick={() => {
                            setMove({ space, cell });
                            setStatus(
                              cell.status === "Available" ? "Held" : cell.status
                            );
                            setClientName(cell.clientName ?? "");
                            setNotes(cell.notes ?? "");
                          }}
                          className={cn(
                            "flex h-14 w-full flex-col items-center justify-center rounded border px-1 text-center transition-colors",
                            style.tile
                          )}
                        >
                          <span className="text-[10.5px] font-medium leading-tight">
                            {style.label}
                          </span>
                          {cell.clientName ? (
                            <span className="line-clamp-1 max-w-full text-[10px] opacity-80">
                              {cell.clientName}
                            </span>
                          ) : null}
                        </button>
                      </td>
                    );
                  })}
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>

      <Dialog open={move !== null} onOpenChange={(o) => !o && setMove(null)}>
        <DialogContent className="sm:max-w-md">
          <DialogHeader>
            <DialogTitle>
              {move?.space.name} · {move ? shortDay(move.cell.date) : ""}
            </DialogTitle>
          </DialogHeader>
          <div className="flex flex-col gap-3">
            <div className="space-y-1">
              <Label>Status</Label>
              <Select value={status} onValueChange={(v) => setStatus(v as UnitStatus)}>
                <SelectTrigger>
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {(
                    ["Held", "Booked", "Sold", "Blackout", "Blocked"] as UnitStatus[]
                  ).map((s) => (
                    <SelectItem key={s} value={s}>
                      {STATUS_STYLES[s]?.label ?? s}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div className="space-y-1">
              <Label>Client name</Label>
              <Input value={clientName} onChange={(e) => setClientName(e.target.value)} />
            </div>
            {status === "Held" ? (
              <div className="space-y-1">
                <Label>Hold hours</Label>
                <Input
                  type="number"
                  min={1}
                  max={336}
                  value={holdHours}
                  onChange={(e) => setHoldHours(e.target.value)}
                />
              </div>
            ) : null}
            <div className="space-y-1">
              <Label>Notes</Label>
              <Input value={notes} onChange={(e) => setNotes(e.target.value)} />
            </div>
          </div>
          <DialogFooter className="gap-2 sm:justify-between">
            {move?.cell.spaceBookingId ? (
              <Button variant="outline" onClick={() => void clearCell()} disabled={saving}>
                Release
              </Button>
            ) : (
              <span />
            )}
            <Button onClick={() => void applyMove()} disabled={saving}>
              {saving ? <Loader2 className="size-4 animate-spin" /> : null}
              Save
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <UnitSheet
        unit={sheetUnit}
        open={sheetOpen}
        onOpenChange={setSheetOpen}
        onChanged={load}
        onQuote={(u) => {
          setSheetUnit(u);
          setSheetOpen(false);
          setQuoteOpen(true);
        }}
      />
      <QuoteBuilder
        unit={sheetUnit}
        projectId={projectId ? Number(projectId) : null}
        open={quoteOpen}
        onOpenChange={setQuoteOpen}
        onCreated={load}
      />
    </div>
  );
}
