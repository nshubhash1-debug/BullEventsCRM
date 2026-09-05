"use client";

import * as React from "react";
import { Plus, Save, X } from "lucide-react";

import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import {
  OPERATORS,
  defaultOperator,
  describeCondition,
  isComplete,
  isUnary,
  newCondition,
  type FilterCondition,
  type FilterFieldDef,
  type MatchMode,
} from "@/lib/advanced-filter";
import { cn } from "@/lib/utils";

interface AdvancedFilterPanelProps {
  fields: FilterFieldDef[];
  conditions: FilterCondition[];
  matchMode: MatchMode;
  onChange: (conditions: FilterCondition[]) => void;
  onMatchModeChange: (mode: MatchMode) => void;
  onSaveView?: (name: string) => void;
  /** Rows matching right now, shown as live feedback while building. */
  matchCount?: number;
  totalCount?: number;
}

export function AdvancedFilterPanel({
  fields,
  conditions,
  matchMode,
  onChange,
  onMatchModeChange,
  onSaveView,
  matchCount,
  totalCount,
}: AdvancedFilterPanelProps) {
  const [saveName, setSaveName] = React.useState("");
  const [saving, setSaving] = React.useState(false);

  function update(id: string, patch: Partial<FilterCondition>) {
    onChange(
      conditions.map((condition) =>
        condition.id === id ? { ...condition, ...patch } : condition
      )
    );
  }

  function remove(id: string) {
    onChange(conditions.filter((condition) => condition.id !== id));
  }

  function changeField(id: string, fieldId: string) {
    const field = fields.find((f) => f.id === fieldId);
    if (!field) return;
    // Operators and value are field-specific, so both reset on a field change.
    update(id, {
      field: fieldId,
      operator: defaultOperator(field.type),
      value: "",
    });
  }

  function commitSave() {
    const name = saveName.trim();
    if (!name || !onSaveView) return;
    onSaveView(name);
    setSaveName("");
    setSaving(false);
  }

  return (
    <div className="flex w-full flex-col gap-2">
      <div className="flex flex-wrap items-center gap-2">
        <span className="text-[11px] font-bold tracking-wide text-muted-foreground uppercase">
          Match
        </span>
        <div className="flex overflow-hidden rounded border">
          {(["all", "any"] as MatchMode[]).map((mode) => (
            <button
              key={mode}
              type="button"
              onClick={() => onMatchModeChange(mode)}
              className={cn(
                "px-2.5 py-1 text-[12px] transition-colors",
                matchMode === mode
                  ? "bg-primary font-medium text-primary-foreground"
                  : "bg-card hover:bg-accent"
              )}
            >
              {mode === "all" ? "All conditions (AND)" : "Any condition (OR)"}
            </button>
          ))}
        </div>

        {matchCount !== undefined && totalCount !== undefined ? (
          <Badge variant="outline" className="h-6 font-normal tabular-nums">
            {matchCount} of {totalCount} match
          </Badge>
        ) : null}
      </div>

      {conditions.length === 0 ? (
        <p className="text-[12.5px] text-muted-foreground">
          No conditions yet — add one to narrow the list.
        </p>
      ) : (
        <div className="flex flex-col gap-1.5">
          {conditions.map((condition, index) => {
            const field =
              fields.find((f) => f.id === condition.field) ?? fields[0];
            const unary = isUnary(field.type, condition.operator);
            const complete = isComplete(condition, fields);

            return (
              <div
                key={condition.id}
                className="flex flex-wrap items-center gap-1.5"
              >
                <span className="w-10 shrink-0 text-[11px] font-medium text-muted-foreground uppercase">
                  {index === 0 ? "Where" : matchMode === "all" ? "and" : "or"}
                </span>

                <Select
                  value={condition.field}
                  onValueChange={(value) => changeField(condition.id, value)}
                >
                  <SelectTrigger className="h-8 w-44 text-[12.5px]">
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    {fields.map((option) => (
                      <SelectItem key={option.id} value={option.id}>
                        {option.label}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>

                <Select
                  value={condition.operator}
                  onValueChange={(value) =>
                    update(condition.id, { operator: value, value: "" })
                  }
                >
                  <SelectTrigger className="h-8 w-40 text-[12.5px]">
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    {OPERATORS[field.type].map((operator) => (
                      <SelectItem key={operator.value} value={operator.value}>
                        {operator.label}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>

                {unary ? (
                  <span className="w-52 text-[12px] text-muted-foreground">
                    no value needed
                  </span>
                ) : field.type === "select" && field.options ? (
                  <Select
                    value={condition.value}
                    onValueChange={(value) => update(condition.id, { value })}
                  >
                    <SelectTrigger className="h-8 w-52 text-[12.5px]">
                      <SelectValue placeholder="Choose a value…" />
                    </SelectTrigger>
                    <SelectContent>
                      {field.options.map((option) => (
                        <SelectItem key={option.value} value={option.value}>
                          {option.label}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                ) : (
                  <Input
                    value={condition.value}
                    onChange={(event) =>
                      update(condition.id, { value: event.target.value })
                    }
                    type={
                      field.type === "number"
                        ? "number"
                        : field.type === "date" &&
                            !condition.operator.includes("NDays")
                          ? "date"
                          : "text"
                    }
                    placeholder={
                      condition.operator.includes("NDays")
                        ? "number of days"
                        : "value"
                    }
                    className="h-8 w-52 text-[12.5px]"
                  />
                )}

                {!complete ? (
                  <span className="text-[11px] text-amber-600 dark:text-amber-400">
                    incomplete — ignored
                  </span>
                ) : null}

                <Button
                  variant="ghost"
                  size="icon"
                  aria-label="Remove condition"
                  className="size-7 text-muted-foreground hover:text-destructive"
                  onClick={() => remove(condition.id)}
                >
                  <X className="size-3.5" />
                </Button>
              </div>
            );
          })}
        </div>
      )}

      <div className="flex flex-wrap items-center gap-1.5">
        <Button
          variant="outline"
          size="sm"
          className="h-8"
          onClick={() => onChange([...conditions, newCondition(fields)])}
        >
          <Plus /> Add condition
        </Button>

        {conditions.length > 0 ? (
          <Button
            variant="ghost"
            size="sm"
            className="h-8"
            onClick={() => onChange([])}
          >
            Remove all
          </Button>
        ) : null}

        {onSaveView && conditions.some((c) => isComplete(c, fields)) ? (
          saving ? (
            <div className="flex items-center gap-1.5">
              <Input
                autoFocus
                value={saveName}
                onChange={(event) => setSaveName(event.target.value)}
                onKeyDown={(event) => {
                  if (event.key === "Enter") commitSave();
                  if (event.key === "Escape") setSaving(false);
                }}
                placeholder="Name this view…"
                className="h-8 w-44 text-[12.5px]"
              />
              <Button size="sm" className="h-8" onClick={commitSave}>
                Save
              </Button>
              <Button
                variant="ghost"
                size="sm"
                className="h-8"
                onClick={() => setSaving(false)}
              >
                Cancel
              </Button>
            </div>
          ) : (
            <Button
              variant="outline"
              size="sm"
              className="h-8"
              onClick={() => setSaving(true)}
            >
              <Save /> Save as view
            </Button>
          )
        ) : null}
      </div>
    </div>
  );
}

/** Compact read-only summary of the applied conditions. */
export function FilterChips({
  conditions,
  fields,
  matchMode,
  onRemove,
}: {
  conditions: FilterCondition[];
  fields: FilterFieldDef[];
  matchMode: MatchMode;
  onRemove: (id: string) => void;
}) {
  const active = conditions.filter((c) => isComplete(c, fields));
  if (active.length === 0) return null;

  return (
    <div className="flex flex-wrap items-center gap-1.5">
      {active.map((condition, index) => (
        <React.Fragment key={condition.id}>
          {index > 0 ? (
            <span className="text-[11px] font-medium text-muted-foreground uppercase">
              {matchMode === "all" ? "and" : "or"}
            </span>
          ) : null}
          <Badge
            variant="secondary"
            className="h-6 gap-1 pr-1 pl-2 text-[11.5px] font-normal"
          >
            {describeCondition(condition, fields)}
            <button
              type="button"
              aria-label="Remove filter"
              onClick={() => onRemove(condition.id)}
              className="rounded p-0.5 hover:bg-background/60"
            >
              <X className="size-3" />
            </button>
          </Badge>
        </React.Fragment>
      ))}
    </div>
  );
}
