"use client";

import * as React from "react";
import { CalendarDays, X } from "lucide-react";

import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import {
  Popover,
  PopoverContent,
  PopoverTrigger,
} from "@/components/ui/popover";
import {
  Select,
  SelectContent,
  SelectGroup,
  SelectItem,
  SelectLabel,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import type { DateFieldOption } from "@/hooks/use-crm-list";
import {
  DATE_PRESETS,
  PRESET_LABELS,
  isRangeActive,
  resolveRange,
  type DatePresetKey,
  type DateRange,
} from "@/lib/date-range";
import { cn } from "@/lib/utils";

interface DateRangeFilterProps {
  fields: DateFieldOption[];
  range: DateRange;
  onChange: (next: DateRange) => void;
}

/**
 * The date window control that sits on every list toolbar.
 *
 * Two decisions, not one: *which* date and *what window*. Most lists have more
 * than one date worth filtering on — a site visit has a scheduled date and a
 * created date, and "last 7 days" means something different against each — so
 * the column is picked explicitly rather than assumed.
 */
export function DateRangeFilter({
  fields,
  range,
  onChange,
}: DateRangeFilterProps) {
  const [open, setOpen] = React.useState(false);

  // Drafted locally so a half-typed custom range does not refetch on every
  // keystroke; it applies when the popover is committed.
  const [draftFrom, setDraftFrom] = React.useState(range.from ?? "");
  const [draftTo, setDraftTo] = React.useState(range.to ?? "");

  if (fields.length === 0) return null;

  const activeField =
    fields.find((field) => field.id === range.field) ?? fields[0];
  const active = isRangeActive(range);

  function pickPreset(preset: DatePresetKey) {
    if (preset === "custom") {
      setDraftFrom(range.from ?? "");
      setDraftTo(range.to ?? "");
      // Committed before opening, for two reasons: the Select is controlled by
      // `range.preset` and would otherwise snap back to the old label, and the
      // popover's trigger only renders in custom mode — it has to exist before
      // it can anchor the panel. With no dates yet the window resolves to null,
      // so nothing is filtered until Apply.
      onChange({ ...range, preset, field: activeField.id });
      setOpen(true);
      return;
    }

    onChange({ ...range, preset, field: activeField.id, from: undefined, to: undefined });
  }

  function applyCustom() {
    if (!draftFrom && !draftTo) return;
    onChange({
      ...range,
      preset: "custom",
      field: activeField.id,
      from: draftFrom || undefined,
      to: draftTo || undefined,
    });
    setOpen(false);
  }

  const resolved = resolveRange(range);

  return (
    <div className="flex items-center gap-1.5">
      {/* Which date column — only worth showing when there is a choice */}
      {fields.length > 1 ? (
        <Select
          value={activeField.id}
          onValueChange={(field) => onChange({ ...range, field })}
        >
          <SelectTrigger
            className="h-8 w-[124px] text-[12.5px]"
            aria-label="Date column"
          >
            <SelectValue />
          </SelectTrigger>
          <SelectContent>
            {fields.map((field) => (
              <SelectItem key={field.id} value={field.id}>
                {field.label}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
      ) : null}

      <Select
        value={range.preset}
        onValueChange={(value) => pickPreset(value as DatePresetKey)}
      >
        <SelectTrigger
          className={cn(
            "h-8 w-[136px] text-[12.5px]",
            active && "border-primary/50 bg-primary/5"
          )}
          aria-label="Date range"
        >
          <CalendarDays className="size-3.5 shrink-0 text-muted-foreground" />
          <SelectValue />
        </SelectTrigger>
        <SelectContent>
          {(["Other", "Recent", "Upcoming"] as const).map((group) => {
            const items = DATE_PRESETS.filter((preset) => preset.group === group);
            if (items.length === 0) return null;

            return (
              <SelectGroup key={group}>
                {group !== "Other" ? <SelectLabel>{group}</SelectLabel> : null}
                {items.map((preset) => (
                  <SelectItem key={preset.value} value={preset.value}>
                    {preset.label}
                  </SelectItem>
                ))}
              </SelectGroup>
            );
          })}
        </SelectContent>
      </Select>

      {/* Custom range editor */}
      <Popover open={open} onOpenChange={setOpen}>
        <PopoverTrigger asChild>
          <Button
            variant={range.preset === "custom" ? "secondary" : "outline"}
            size="sm"
            className={cn("h-8", range.preset !== "custom" && "hidden")}
          >
            {resolved && range.preset === "custom"
              ? `${short(resolved.from)} – ${short(resolved.to)}`
              : "Pick dates"}
          </Button>
        </PopoverTrigger>
        <PopoverContent align="start" className="w-72">
          <div className="flex flex-col gap-3">
            <div className="grid grid-cols-2 gap-2">
              <div className="flex flex-col gap-1.5">
                <Label htmlFor="range-from" className="text-[11.5px]">
                  From
                </Label>
                <Input
                  id="range-from"
                  type="date"
                  value={draftFrom}
                  max={draftTo || undefined}
                  onChange={(event) => setDraftFrom(event.target.value)}
                  className="h-8 text-[12.5px]"
                />
              </div>
              <div className="flex flex-col gap-1.5">
                <Label htmlFor="range-to" className="text-[11.5px]">
                  To
                </Label>
                <Input
                  id="range-to"
                  type="date"
                  value={draftTo}
                  min={draftFrom || undefined}
                  onChange={(event) => setDraftTo(event.target.value)}
                  className="h-8 text-[12.5px]"
                />
              </div>
            </div>

            <p className="text-[11px] text-muted-foreground">
              Leave one side empty for an open-ended range — everything since a
              date, or everything up to one.
            </p>

            <div className="flex items-center gap-1.5">
              <Button size="sm" className="h-8 flex-1" onClick={applyCustom}>
                Apply
              </Button>
              <Button
                variant="outline"
                size="sm"
                className="h-8"
                onClick={() => {
                  setDraftFrom("");
                  setDraftTo("");
                }}
              >
                Reset
              </Button>
            </div>
          </div>
        </PopoverContent>
      </Popover>

      {active ? (
        <Button
          variant="ghost"
          size="icon"
          aria-label="Clear date filter"
          title="Clear date filter"
          className="size-8 text-muted-foreground hover:text-foreground"
          onClick={() =>
            onChange({ ...range, preset: "all", from: undefined, to: undefined })
          }
        >
          <X className="size-3.5" />
        </Button>
      ) : null}
    </div>
  );
}

/** Compact chip for the applied window, shown under the toolbar. */
export function DateRangeChip({
  fields,
  range,
  onClear,
}: {
  fields: DateFieldOption[];
  range: DateRange;
  onClear: () => void;
}) {
  if (!isRangeActive(range)) return null;

  const field = fields.find((entry) => entry.id === range.field) ?? fields[0];
  const resolved = resolveRange(range);
  if (!field || !resolved) return null;

  return (
    <Badge
      variant="secondary"
      className="h-6 gap-1 pr-1 pl-2 text-[11.5px] font-normal"
    >
      <CalendarDays className="size-3" />
      {field.label} ·{" "}
      {range.preset === "custom"
        ? `${short(resolved.from)} – ${short(resolved.to)}`
        : PRESET_LABELS[range.preset]}
      <button
        type="button"
        aria-label="Remove date filter"
        onClick={onClear}
        className="rounded p-0.5 hover:bg-background/60"
      >
        <X className="size-3" />
      </button>
    </Badge>
  );
}

function short(value: string) {
  return new Date(`${value}T00:00:00`).toLocaleDateString("en-IN", {
    day: "numeric",
    month: "short",
  });
}
