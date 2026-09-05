"use client";

import * as React from "react";
import { Check, ChevronDown, Plus, Save, Trash2, X } from "lucide-react";

import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import {
  Command,
  CommandEmpty,
  CommandGroup,
  CommandInput,
  CommandItem,
  CommandList,
} from "@/components/ui/command";
import { Input } from "@/components/ui/input";
import {
  Popover,
  PopoverContent,
  PopoverTrigger,
} from "@/components/ui/popover";
import { Separator } from "@/components/ui/separator";
import {
  addToGroup,
  describe,
  groupFields,
  isComplete,
  isGroup,
  newGroup,
  newLeaf,
  operatorDef,
  operatorsFor,
  removeNode,
  updateNode,
  type FilterField,
  type FilterNode,
} from "@/lib/query";
import { cn } from "@/lib/utils";

/* ------------------------------------------------------------------ *
 * Field picker
 * ------------------------------------------------------------------ */

function FieldPicker({
  fields,
  value,
  onChange,
}: {
  fields: FilterField[];
  value?: string;
  onChange: (field: FilterField) => void;
}) {
  const [open, setOpen] = React.useState(false);
  const selected = fields.find((f) => f.id === value);
  const groups = React.useMemo(() => groupFields(fields), [fields]);

  return (
    <Popover open={open} onOpenChange={setOpen}>
      <PopoverTrigger asChild>
        <Button
          variant="outline"
          size="sm"
          role="combobox"
          aria-expanded={open}
          className="h-7 w-44 justify-between px-2 text-[12.5px] font-normal"
        >
          <span className="truncate">{selected?.label ?? "Select field"}</span>
          <ChevronDown className="size-3 shrink-0 opacity-50" />
        </Button>
      </PopoverTrigger>
      <PopoverContent align="start" className="w-64 p-0">
        <Command>
          <CommandInput placeholder="Search fields…" className="h-8 text-[13px]" />
          <CommandList className="max-h-72">
            <CommandEmpty className="py-4 text-center text-[12.5px]">
              No field matches.
            </CommandEmpty>
            {groups.map((group) => (
              <CommandGroup key={group.name} heading={group.name}>
                {group.items.map((field) => (
                  <CommandItem
                    key={field.id}
                    value={`${group.name} ${field.label}`}
                    onSelect={() => {
                      onChange(field);
                      setOpen(false);
                    }}
                    className="text-[12.5px]"
                  >
                    <Check
                      className={cn(
                        "size-3.5",
                        field.id === value ? "opacity-100" : "opacity-0"
                      )}
                    />
                    <span className="flex-1 truncate">{field.label}</span>
                    <span className="text-[10px] text-muted-foreground uppercase">
                      {field.type}
                    </span>
                  </CommandItem>
                ))}
              </CommandGroup>
            ))}
          </CommandList>
        </Command>
      </PopoverContent>
    </Popover>
  );
}

/* ------------------------------------------------------------------ *
 * Value inputs
 * ------------------------------------------------------------------ */

function MultiValuePicker({
  options,
  values,
  onChange,
}: {
  options: { label: string; value: string }[];
  values: string[];
  onChange: (next: string[]) => void;
}) {
  const [open, setOpen] = React.useState(false);
  const selected = new Set(values);

  return (
    <Popover open={open} onOpenChange={setOpen}>
      <PopoverTrigger asChild>
        <Button
          variant="outline"
          size="sm"
          className="h-7 min-w-40 justify-between px-2 text-[12.5px] font-normal"
        >
          <span className="truncate">
            {values.length === 0
              ? "Select values"
              : values.length <= 2
                ? values
                    .map((v) => options.find((o) => o.value === v)?.label ?? v)
                    .join(", ")
                : `${values.length} selected`}
          </span>
          <ChevronDown className="size-3 shrink-0 opacity-50" />
        </Button>
      </PopoverTrigger>
      <PopoverContent align="start" className="w-56 p-0">
        <Command>
          <CommandInput placeholder="Search…" className="h-8 text-[13px]" />
          <CommandList className="max-h-64">
            <CommandEmpty className="py-4 text-center text-[12.5px]">
              Nothing matches.
            </CommandEmpty>
            <CommandGroup>
              {options.map((option) => (
                <CommandItem
                  key={option.value}
                  value={option.label}
                  onSelect={() =>
                    onChange(
                      selected.has(option.value)
                        ? values.filter((v) => v !== option.value)
                        : [...values, option.value]
                    )
                  }
                  className="text-[12.5px]"
                >
                  <span
                    className={cn(
                      "flex size-3.5 items-center justify-center rounded-[3px] border",
                      selected.has(option.value)
                        ? "border-primary bg-primary text-primary-foreground"
                        : "border-input"
                    )}
                  >
                    {selected.has(option.value) ? (
                      <Check className="size-2.5" />
                    ) : null}
                  </span>
                  <span className="flex-1 truncate">{option.label}</span>
                </CommandItem>
              ))}
            </CommandGroup>
          </CommandList>
        </Command>
      </PopoverContent>
    </Popover>
  );
}

function SingleValuePicker({
  options,
  value,
  onChange,
}: {
  options: { label: string; value: string }[];
  value: string;
  onChange: (next: string) => void;
}) {
  const [open, setOpen] = React.useState(false);
  const selected = options.find((o) => o.value === value);

  return (
    <Popover open={open} onOpenChange={setOpen}>
      <PopoverTrigger asChild>
        <Button
          variant="outline"
          size="sm"
          className="h-7 min-w-36 justify-between px-2 text-[12.5px] font-normal"
        >
          <span className="truncate">{selected?.label ?? "Select value"}</span>
          <ChevronDown className="size-3 shrink-0 opacity-50" />
        </Button>
      </PopoverTrigger>
      <PopoverContent align="start" className="w-52 p-0">
        <Command>
          <CommandInput placeholder="Search…" className="h-8 text-[13px]" />
          <CommandList className="max-h-64">
            <CommandEmpty className="py-4 text-center text-[12.5px]">
              Nothing matches.
            </CommandEmpty>
            <CommandGroup>
              {options.map((option) => (
                <CommandItem
                  key={option.value}
                  value={option.label}
                  onSelect={() => {
                    onChange(option.value);
                    setOpen(false);
                  }}
                  className="text-[12.5px]"
                >
                  <Check
                    className={cn(
                      "size-3.5",
                      option.value === value ? "opacity-100" : "opacity-0"
                    )}
                  />
                  <span className="flex-1 truncate">{option.label}</span>
                </CommandItem>
              ))}
            </CommandGroup>
          </CommandList>
        </Command>
      </PopoverContent>
    </Popover>
  );
}

/** Chooses the right input for the field type and the operator's arity. */
function ValueInput({
  field,
  node,
  onPatch,
}: {
  field: FilterField;
  node: FilterNode;
  onPatch: (patch: Partial<FilterNode>) => void;
}) {
  const def = operatorDef(field.type, node.operator ?? "");
  if (!def || def.unary) return null;

  if (def.multi) {
    return (
      <MultiValuePicker
        options={field.options ?? []}
        values={node.values ?? []}
        onChange={(values) => onPatch({ values })}
      />
    );
  }

  if (def.range) {
    const type = field.type === "date" ? "date" : "number";
    return (
      <span className="flex items-center gap-1">
        <Input
          type={type}
          value={node.value ?? ""}
          onChange={(e) => onPatch({ value: e.target.value })}
          className="h-7 w-32 px-2 text-[12.5px]"
        />
        <span className="text-[11px] text-muted-foreground">and</span>
        <Input
          type={type}
          value={node.value2 ?? ""}
          onChange={(e) => onPatch({ value2: e.target.value })}
          className="h-7 w-32 px-2 text-[12.5px]"
        />
      </span>
    );
  }

  if (field.type === "select" && field.options?.length) {
    return (
      <SingleValuePicker
        options={field.options}
        value={node.value ?? ""}
        onChange={(value) => onPatch({ value })}
      />
    );
  }

  // "in the last N days" wants a plain number even though the field is a date.
  const inputType =
    field.type === "number" ||
    node.operator === "lastNDays" ||
    node.operator === "nextNDays" ||
    node.operator === "olderThanNDays"
      ? "number"
      : field.type === "date"
        ? "date"
        : "text";

  return (
    <Input
      type={inputType}
      value={node.value ?? ""}
      onChange={(e) => onPatch({ value: e.target.value })}
      placeholder="Value"
      className="h-7 w-40 px-2 text-[12.5px]"
    />
  );
}

/* ------------------------------------------------------------------ *
 * Rows and groups
 * ------------------------------------------------------------------ */

function ConditionRow({
  node,
  fields,
  onPatch,
  onRemove,
}: {
  node: FilterNode;
  fields: FilterField[];
  onPatch: (patch: Partial<FilterNode>) => void;
  onRemove: () => void;
}) {
  const field = fields.find((f) => f.id === node.field);

  return (
    <div className="flex flex-wrap items-center gap-1.5">
      <FieldPicker
        fields={fields}
        value={node.field}
        onChange={(next) =>
          // Operators are per-type, so changing the field has to reset the
          // operator and clear any value that no longer makes sense.
          onPatch({
            field: next.id,
            operator: operatorsFor(next.type)[0].value,
            value: "",
            value2: undefined,
            values: undefined,
          })
        }
      />

      {field ? (
        <select
          value={node.operator ?? ""}
          onChange={(e) => onPatch({ operator: e.target.value, value: "", values: [] })}
          aria-label="Operator"
          className="h-7 rounded border bg-background px-1.5 text-[12.5px] outline-none focus-visible:ring-[3px] focus-visible:ring-ring/50"
        >
          {operatorsFor(field.type).map((op) => (
            <option key={op.value} value={op.value}>
              {op.label}
            </option>
          ))}
        </select>
      ) : null}

      {field ? <ValueInput field={field} node={node} onPatch={onPatch} /> : null}

      <Button
        variant="ghost"
        size="icon"
        aria-label="Remove condition"
        onClick={onRemove}
        className="size-6 text-muted-foreground hover:text-destructive"
      >
        <X className="size-3.5" />
      </Button>
    </div>
  );
}

function GroupNode({
  node,
  fields,
  depth,
  onChange,
  onRemove,
}: {
  node: FilterNode;
  fields: FilterField[];
  depth: number;
  onChange: (patch: Partial<FilterNode>) => void;
  onRemove?: () => void;
}) {
  const conjunction = node.conjunction ?? "and";

  return (
    <div
      className={cn(
        "flex min-w-0 flex-col gap-1.5",
        depth > 0 && "rounded border border-dashed bg-muted/30 p-2"
      )}
    >
      {(node.children ?? []).map((child, index) => (
        <div key={child.key} className="flex min-w-0 items-start gap-1.5">
          {/*
            The conjunction is editable on the second row only and read-only
            after — a group joins its children one way, and letting each row
            pick would make `A AND B OR C` ambiguous.
          */}
          <div className="w-14 shrink-0 pt-0.5">
            {index === 0 ? (
              <span className="text-[11px] text-muted-foreground">Where</span>
            ) : index === 1 ? (
              <select
                value={conjunction}
                onChange={(e) =>
                  onChange({ conjunction: e.target.value as "and" | "or" })
                }
                aria-label="Combine conditions with"
                className="h-6 w-full rounded border bg-background px-1 text-[11.5px] font-medium outline-none"
              >
                <option value="and">AND</option>
                <option value="or">OR</option>
              </select>
            ) : (
              <span className="pl-1 text-[11.5px] font-medium text-muted-foreground uppercase">
                {conjunction}
              </span>
            )}
          </div>

          <div className="min-w-0 flex-1">
            {isGroup(child) ? (
              <GroupNode
                node={child}
                fields={fields}
                depth={depth + 1}
                onChange={(patch) =>
                  onChange({
                    children: (node.children ?? []).map((c) =>
                      c.key === child.key ? { ...c, ...patch } : c
                    ),
                  })
                }
                onRemove={() =>
                  onChange({
                    children: (node.children ?? []).filter(
                      (c) => c.key !== child.key
                    ),
                  })
                }
              />
            ) : (
              <ConditionRow
                node={child}
                fields={fields}
                onPatch={(patch) =>
                  onChange({
                    children: (node.children ?? []).map((c) =>
                      c.key === child.key ? { ...c, ...patch } : c
                    ),
                  })
                }
                onRemove={() =>
                  onChange({
                    children: (node.children ?? []).filter(
                      (c) => c.key !== child.key
                    ),
                  })
                }
              />
            )}
          </div>
        </div>
      ))}

      <div className="flex items-center gap-1 pl-14">
        <Button
          variant="ghost"
          size="sm"
          className="h-6 px-1.5 text-[11.5px]"
          onClick={() =>
            onChange({ children: [...(node.children ?? []), newLeaf(fields)] })
          }
        >
          <Plus className="size-3" /> Condition
        </Button>

        {depth < 2 ? (
          <Button
            variant="ghost"
            size="sm"
            className="h-6 px-1.5 text-[11.5px]"
            onClick={() =>
              onChange({
                children: [
                  ...(node.children ?? []),
                  newGroup(fields, conjunction === "and" ? "or" : "and"),
                ],
              })
            }
          >
            <Plus className="size-3" /> Group
          </Button>
        ) : null}

        {onRemove ? (
          <Button
            variant="ghost"
            size="sm"
            className="h-6 px-1.5 text-[11.5px] text-muted-foreground hover:text-destructive"
            onClick={onRemove}
          >
            <Trash2 className="size-3" /> Remove group
          </Button>
        ) : null}
      </div>
    </div>
  );
}

/* ------------------------------------------------------------------ *
 * Panel
 * ------------------------------------------------------------------ */

export interface SavedView {
  id: string;
  name: string;
  filter: FilterNode;
}

interface FilterBuilderProps {
  fields: FilterField[];
  filter: FilterNode;
  onChange: (next: FilterNode) => void;
  matchCount: number;
  totalCount: number;
  onSaveView?: (name: string) => void;
  savedViews?: SavedView[];
  onLoadView?: (view: SavedView) => void;
  onDeleteView?: (id: string) => void;
}

/**
 * The full condition builder.
 *
 * Nesting is the point: a flat "match all / match any" list cannot express
 * "hot leads in Mumbai OR any lead over two crore, but only ones assigned this
 * quarter", which is the kind of question a sales manager actually asks.
 */
export function FilterBuilder({
  fields,
  filter,
  onChange,
  matchCount,
  totalCount,
  onSaveView,
  savedViews = [],
  onLoadView,
  onDeleteView,
}: FilterBuilderProps) {
  const [viewName, setViewName] = React.useState("");

  if (fields.length === 0) {
    return (
      <p className="py-2 text-[12.5px] text-muted-foreground">
        Loading filterable fields…
      </p>
    );
  }

  return (
    <div className="flex w-full min-w-0 flex-col gap-2 py-1">
      <GroupNode
        node={filter}
        fields={fields}
        depth={0}
        onChange={(patch) => onChange({ ...filter, ...patch })}
      />

      <Separator />

      <div className="flex flex-wrap items-center gap-2">
        <span className="text-[12px] tabular-nums">
          <strong className="font-semibold text-foreground">
            {matchCount.toLocaleString()}
          </strong>{" "}
          of {totalCount.toLocaleString()} match
        </span>

        <Button
          variant="ghost"
          size="sm"
          className="h-7 text-[12px]"
          onClick={() =>
            onChange({ ...filter, children: [], conjunction: "and" })
          }
        >
          <X className="size-3.5" /> Clear all
        </Button>

        {onSaveView ? (
          <div className="ml-auto flex items-center gap-1.5">
            <Input
              value={viewName}
              onChange={(e) => setViewName(e.target.value)}
              placeholder="Name this view…"
              className="h-7 w-44 text-[12.5px]"
            />
            <Button
              variant="outline"
              size="sm"
              className="h-7 text-[12px]"
              disabled={!viewName.trim()}
              onClick={() => {
                onSaveView(viewName.trim());
                setViewName("");
              }}
            >
              <Save className="size-3.5" /> Save view
            </Button>
          </div>
        ) : null}
      </div>

      {savedViews.length > 0 && onLoadView ? (
        <div className="flex flex-wrap items-center gap-1.5">
          <span className="text-[11px] tracking-wide text-muted-foreground uppercase">
            Saved
          </span>
          {savedViews.map((view) => (
            <span
              key={view.id}
              className="inline-flex items-center gap-1 rounded border bg-background pl-2 text-[12px]"
            >
              <button
                type="button"
                onClick={() => onLoadView(view)}
                className="py-1 hover:text-primary"
              >
                {view.name}
              </button>
              {onDeleteView ? (
                <button
                  type="button"
                  aria-label={`Delete view ${view.name}`}
                  onClick={() => onDeleteView(view.id)}
                  className="px-1.5 py-1 text-muted-foreground hover:text-destructive"
                >
                  <X className="size-3" />
                </button>
              ) : null}
            </span>
          ))}
        </div>
      ) : null}
    </div>
  );
}

/* ------------------------------------------------------------------ *
 * Chips
 * ------------------------------------------------------------------ */

/** Flattens the tree into removable chips for when the builder is collapsed. */
export function FilterChips({
  filter,
  fields,
  onChange,
}: {
  filter: FilterNode;
  fields: FilterField[];
  onChange: (next: FilterNode) => void;
}) {
  const leaves: { node: FilterNode; conjunction: string }[] = [];

  const walk = (node: FilterNode, conjunction: string) => {
    if (isGroup(node)) {
      for (const child of node.children ?? []) {
        walk(child, node.conjunction ?? "and");
      }
      return;
    }
    if (isComplete(node, fields)) leaves.push({ node, conjunction });
  };

  walk(filter, "and");
  if (leaves.length === 0) return null;

  return (
    <div className="flex flex-wrap items-center gap-1">
      {leaves.map(({ node, conjunction }, index) => (
        <React.Fragment key={node.key}>
          {index > 0 ? (
            <span className="text-[10.5px] font-medium text-muted-foreground uppercase">
              {conjunction}
            </span>
          ) : null}
          <Badge
            variant="secondary"
            className="h-6 gap-1 pr-1 pl-2 text-[11.5px] font-normal"
          >
            {describe(node, fields)}
            <button
              type="button"
              aria-label="Remove filter"
              onClick={() => onChange(removeNode(filter, node.key))}
              className="rounded-sm px-0.5 hover:text-destructive"
            >
              <X className="size-3" />
            </button>
          </Badge>
        </React.Fragment>
      ))}
    </div>
  );
}

/**
 * One-click narrowing from a facet count. Appends a condition to the root
 * rather than replacing the tree, so it composes with whatever is already set.
 */
export function appendCondition(
  filter: FilterNode,
  field: string,
  operator: string,
  value: string
): FilterNode {
  const leaf = { ...newLeaf([]), field, operator, value };
  return addToGroup(filter, filter.key, leaf);
}

export { updateNode, removeNode };
