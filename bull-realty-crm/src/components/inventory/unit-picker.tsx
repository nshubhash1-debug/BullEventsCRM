"use client";

import * as React from "react";
import { Building2, Check, CheckSquare, Loader2, Plus, Search, Sparkles, Square } from "lucide-react";
import { toast } from "sonner";

import { QuoteBuilder } from "@/components/inventory/quote-builder";
import { MultiQuoteBuilder } from "@/components/inventory/multi-quote-builder";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { ScrollArea } from "@/components/ui/scroll-area";
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
  boardApi,
  formatIndian,
  formatRupees,
  type BoardUnit,
} from "@/lib/inventory-api";
import { cn } from "@/lib/utils";

/**
 * Starts a quotation from the quotations list rather than from the board.
 * Supports both single unit quotes and multi-unit combo quotes.
 */
export function NewQuotationButton({ onCreated }: { onCreated: () => void }) {
  const [picking, setPicking] = React.useState(false);
  const [picked, setPicked] = React.useState<{
    unit: BoardUnit;
    projectId: number;
  } | null>(null);
  const [multiPicked, setMultiPicked] = React.useState<{
    units: BoardUnit[];
    projectId: number;
  } | null>(null);

  return (
    <>
      <Button size="sm" onClick={() => setPicking(true)}>
        <Plus /> New quotation
      </Button>

      <UnitPickerDialog
        open={picking}
        onOpenChange={setPicking}
        onPicked={(unit, projectId) => {
          setPicking(false);
          setPicked({ unit, projectId });
        }}
        onMultiPicked={(units, projectId) => {
          setPicking(false);
          setMultiPicked({ units, projectId });
        }}
      />

      {picked ? (
        <QuoteBuilder
          key={picked.unit.id}
          unit={picked.unit}
          projectId={picked.projectId}
          open
          onOpenChange={(next) => {
            if (!next) setPicked(null);
          }}
          onCreated={onCreated}
        />
      ) : null}

      {multiPicked ? (
        <MultiQuoteBuilder
          key={multiPicked.units.map((u) => u.id).join("-")}
          units={multiPicked.units}
          projectId={multiPicked.projectId}
          open
          onOpenChange={(next) => {
            if (!next) setMultiPicked(null);
          }}
          onCreated={onCreated}
        />
      ) : null}
    </>
  );
}

function UnitPickerDialog({
  open,
  onOpenChange,
  onPicked,
  onMultiPicked,
}: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onPicked: (unit: BoardUnit, projectId: number) => void;
  onMultiPicked?: (units: BoardUnit[], projectId: number) => void;
}) {
  const [projects, setProjects] = React.useState<ProjectRow[] | null>(null);
  const [projectId, setProjectId] = React.useState<string>("");
  const [units, setUnits] = React.useState<BoardUnit[] | null>(null);
  const [search, setSearch] = React.useState("");
  const [isMultiMode, setIsMultiMode] = React.useState(false);
  const [selectedUnitIds, setSelectedUnitIds] = React.useState<number[]>([]);

  React.useEffect(() => {
    if (!open) {
      setSelectedUnitIds([]);
      setIsMultiMode(false);
      return;
    }

    let cancelled = false;

    inventoryApi
      .projects()
      .then((loaded) => {
        if (cancelled) return;
        setProjects(loaded);
        if (loaded.length > 0) setProjectId(String(loaded[0].id));
      })
      .catch((error) => {
        if (cancelled) return;
        setProjects([]);
        toast.error("Could not load projects", {
          description: error instanceof ApiError ? error.message : "Network error.",
        });
      });

    return () => {
      cancelled = true;
    };
  }, [open]);

  React.useEffect(() => {
    if (!open || projectId === "") return;

    let cancelled = false;
    setSelectedUnitIds([]);

    const today = new Date();
    const eventDate = [
      today.getFullYear(),
      String(today.getMonth() + 1).padStart(2, "0"),
      String(today.getDate()).padStart(2, "0"),
    ].join("-");

    boardApi
      .board(Number(projectId), { eventDate, slot: "Evening" })
      .then((board) => {
        if (cancelled) return;

        const flat = board.floors
          .flatMap((floor) => floor.units)
          .filter((u) => u.status === "Available" || u.status === "Held");

        setUnits(flat);
      })
      .catch((error) => {
        if (cancelled) return;
        setUnits([]);
        toast.error("Could not load inventory", {
          description: error instanceof ApiError ? error.message : "Network error.",
        });
      });

    return () => {
      cancelled = true;
    };
  }, [open, projectId]);

  const needle = search.trim().toLowerCase();
  const visible = (units ?? []).filter(
    (u) =>
      !needle ||
      `${u.unitNumber} ${u.configuration} ${u.facing ?? ""}`
        .toLowerCase()
        .includes(needle)
  );

  function toggleUnitSelect(unitId: number) {
    setSelectedUnitIds((prev) =>
      prev.includes(unitId) ? prev.filter((id) => id !== unitId) : [...prev, unitId]
    );
  }

  function handleCreateCombo() {
    if (!units || selectedUnitIds.length === 0) return;
    const selectedUnits = units.filter((u) => selectedUnitIds.includes(u.id));
    if (selectedUnits.length === 1) {
      onPicked(selectedUnits[0], Number(projectId));
    } else if (onMultiPicked) {
      onMultiPicked(selectedUnits, Number(projectId));
    }
  }

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="flex max-h-[85vh] flex-col gap-0 overflow-hidden p-0 sm:max-w-2xl">
        <DialogHeader className="space-y-3 border-b p-5 pb-4 text-left">
          <div className="flex items-center justify-between">
            <div>
              <DialogTitle>Choose Unit{isMultiMode ? "s" : ""}</DialogTitle>
              <DialogDescription>
                {isMultiMode
                  ? "Select multiple units to create a combined proposal."
                  : "Only sellable stock is listed."}
              </DialogDescription>
            </div>
            <Button
              variant={isMultiMode ? "default" : "outline"}
              size="sm"
              onClick={() => {
                setIsMultiMode(!isMultiMode);
                setSelectedUnitIds([]);
              }}
              className="gap-1.5"
            >
              <Sparkles className="size-3.5" />
              {isMultiMode ? "Single Unit Mode" : "Combo Mode"}
            </Button>
          </div>

          <div className="flex flex-wrap gap-2">
            {projects === null ? (
              <Skeleton className="h-9 w-56" />
            ) : (
              <Select value={projectId} onValueChange={setProjectId}>
                <SelectTrigger className="w-56">
                  <SelectValue placeholder="Project" />
                </SelectTrigger>
                <SelectContent>
                  {projects.map((project) => (
                    <SelectItem key={project.id} value={String(project.id)}>
                      {project.name}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            )}

            <div className="relative min-w-48 flex-1">
              <Search className="pointer-events-none absolute top-1/2 left-2.5 size-4 -translate-y-1/2 text-muted-foreground" />
              <Input
                value={search}
                onChange={(event) => setSearch(event.target.value)}
                placeholder="Unit number, configuration, facing…"
                className="h-9 pl-8"
              />
            </div>
          </div>
        </DialogHeader>

        <ScrollArea className="max-h-[55vh] flex-1">
          <div className="p-3" key={projectId}>
            {units === null ? (
              <div className="flex flex-col gap-2">
                {[0, 1, 2, 3, 4].map((i) => (
                  <Skeleton key={i} className="h-14 w-full" />
                ))}
              </div>
            ) : visible.length === 0 ? (
              <p className="p-6 text-center text-sm text-muted-foreground">
                {(units?.length ?? 0) === 0
                  ? "Nothing sellable in this project right now."
                  : `No unit matches “${search}”.`}
              </p>
            ) : (
              <div className="flex flex-col divide-y">
                {visible.map((unit) => {
                  const isSelected = selectedUnitIds.includes(unit.id);
                  return (
                    <button
                      key={unit.id}
                      type="button"
                      onClick={() => {
                        if (isMultiMode) {
                          toggleUnitSelect(unit.id);
                        } else {
                          onPicked(unit, Number(projectId));
                        }
                      }}
                      aria-label={`Quote ${unit.unitNumber}`}
                      className={cn(
                        "flex items-center gap-3 px-2.5 py-2.5 text-left transition-colors hover:bg-accent",
                        isSelected && "bg-primary/5"
                      )}
                    >
                      {isMultiMode ? (
                        <span className="flex size-6 shrink-0 items-center justify-center text-primary">
                          {isSelected ? (
                            <CheckSquare className="size-4.5" />
                          ) : (
                            <Square className="size-4.5 text-muted-foreground" />
                          )}
                        </span>
                      ) : (
                        <span className="flex size-8 shrink-0 items-center justify-center rounded bg-primary/10 text-primary">
                          <Building2 className="size-4" />
                        </span>
                      )}

                      <span className="min-w-0 flex-1">
                        <span className="flex flex-wrap items-center gap-1.5">
                          <span className="text-[13px] font-medium">
                            {unit.unitNumber}
                          </span>
                          <Badge
                            variant="outline"
                            className={cn(
                              "h-4 px-1 text-[9px] font-normal",
                              unit.status === "Held" &&
                                "text-amber-600 dark:text-amber-400"
                            )}
                          >
                            {unit.status}
                          </Badge>
                          {unit.plcPerSqft > 0 ? (
                            <Badge
                              variant="outline"
                              className="h-4 px-1 text-[9px] font-normal"
                            >
                              PLC
                            </Badge>
                          ) : null}
                        </span>
                        <span className="block truncate text-[11px] text-muted-foreground">
                          Floor {unit.floor} · {unit.configuration} ·{" "}
                          {formatIndian(unit.superArea ?? 0)} sq ft
                          {unit.facing ? ` · ${unit.facing} facing` : ""}
                        </span>
                      </span>

                      <span className="shrink-0 text-right">
                        <span className="block text-[13px] font-medium tabular-nums">
                          {formatRupees(unit.totalPrice)}
                        </span>
                        <span className="block text-[11px] text-muted-foreground tabular-nums">
                          {formatRupees(unit.effectiveRate)} / sq ft
                        </span>
                      </span>
                    </button>
                  );
                })}
              </div>
            )}
          </div>
        </ScrollArea>

        {isMultiMode && (
          <DialogFooter className="border-t p-3 bg-muted/20 flex justify-between items-center">
            <span className="text-xs text-muted-foreground">
              {selectedUnitIds.length} unit{selectedUnitIds.length === 1 ? "" : "s"} selected
            </span>
            <Button
              size="sm"
              disabled={selectedUnitIds.length === 0}
              onClick={handleCreateCombo}
            >
              Continue with {selectedUnitIds.length} unit{selectedUnitIds.length === 1 ? "" : "s"}
            </Button>
          </DialogFooter>
        )}
      </DialogContent>
    </Dialog>
  );
}

/** Shown while the picker resolves, so the button never looks inert. */
export function PickerSpinner() {
  return <Loader2 className="size-4 animate-spin text-muted-foreground" />;
}
