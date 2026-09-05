"use client";

import * as React from "react";
import { Loader2 } from "lucide-react";
import { toast } from "sonner";

import { AdvancedFilterPanel } from "@/components/dashboard/advanced-filter-panel";
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
import { Label } from "@/components/ui/label";
import { ScrollArea } from "@/components/ui/scroll-area";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { Switch } from "@/components/ui/switch";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { Textarea } from "@/components/ui/textarea";
import {
  isComplete,
  newCondition,
  type FilterCondition,
  type FilterFieldDef,
  type MatchMode,
} from "@/lib/advanced-filter";
import { getBranches, getUsers, type Branch, type UserListItem } from "@/lib/api";
import { toFilterNode, type MeasureFormat } from "@/lib/dashboard/analytics";
import { useDatasets } from "@/lib/dashboard/use-analytics";
import {
  PERIOD_LABELS,
  SCOPE_LABELS,
  goalsApi,
  type Goal,
  type GoalInput,
  type GoalPeriod,
  type GoalScope,
} from "@/lib/goals/api";
import { toIsoDate } from "@/lib/calendar/dates";

/* ------------------------------------------------------------------ *
 * Draft
 * ------------------------------------------------------------------ */

interface Draft {
  name: string;
  description: string;
  dataset: string;
  measure: string;
  aggregation: string;
  dateField: string;
  filters: FilterCondition[];
  matchMode: MatchMode;

  isRatio: boolean;
  ratioMeasure: string;
  ratioAggregation: string;
  ratioFilters: FilterCondition[];
  ratioMatchMode: MatchMode;

  format: MeasureFormat;
  periodType: GoalPeriod;
  startDate: string;
  endDate: string;
  scopeType: GoalScope;
  ownerId: string;
  branchId: string;
  targetValue: string;
}

function blankDraft(): Draft {
  return {
    name: "",
    description: "",
    dataset: "opportunities",
    measure: "amount",
    aggregation: "sum",
    dateField: "actualCloseDate",
    filters: [],
    matchMode: "all",
    isRatio: false,
    ratioMeasure: "*",
    ratioAggregation: "count",
    ratioFilters: [],
    ratioMatchMode: "all",
    format: "currency",
    periodType: "Quarter",
    startDate: toIsoDate(new Date()),
    endDate: toIsoDate(new Date()),
    scopeType: "Company",
    ownerId: "",
    branchId: "",
    targetValue: "",
  };
}

/**
 * A saved goal, back in editable form.
 *
 * Filters are the one thing that does not survive the round trip: the server
 * stores the compiled tree, and the builder works on a flat condition list. The
 * form reopens with filters cleared and says so, rather than silently dropping
 * conditions on the next save.
 */
function draftFrom(goal: Goal): Draft {
  return {
    ...blankDraft(),
    name: goal.name,
    description: goal.description ?? "",
    dataset: goal.dataset,
    measure: goal.measure ?? "*",
    aggregation: goal.aggregation,
    dateField: goal.dateField,
    isRatio: goal.isRatio,
    format: goal.format,
    periodType: goal.periodType,
    startDate: goal.startDate.slice(0, 10),
    endDate: goal.endDate.slice(0, 10),
    scopeType: goal.scopeType,
    ownerId: goal.ownerId ? String(goal.ownerId) : "",
    branchId: goal.branchId ? String(goal.branchId) : "",
    targetValue: String(goal.targetValue),
  };
}

/**
 * Server field metadata in the shape the filter builder wants.
 *
 * Booleans become a two-option select, because "is true" reads worse in a
 * dropdown than "Yes".
 */
function toFilterFields(
  fields: { id: string; label: string; type: string }[] | undefined
): FilterFieldDef[] {
  return (fields ?? []).map((field) => ({
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
        : undefined,
  }));
}

/* ------------------------------------------------------------------ *
 * Dialog
 * ------------------------------------------------------------------ */

export function GoalFormDialog({
  open,
  onOpenChange,
  goal,
  onSaved,
}: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  /** Null creates a new goal. */
  goal: Goal | null;
  onSaved: () => void;
}) {
  // Seeded once per mount. The page keys this dialog by goal id, so editing a
  // different goal remounts it rather than needing a prop-to-state sync.
  const [draft, setDraft] = React.useState<Draft>(() =>
    goal ? draftFrom(goal) : blankDraft()
  );
  const [saving, setSaving] = React.useState(false);
  const [owners, setOwners] = React.useState<UserListItem[]>([]);
  const [branches, setBranches] = React.useState<Branch[]>([]);
  const { datasets } = useDatasets();

  React.useEffect(() => {
    let live = true;
    Promise.all([getUsers(), getBranches()])
      .then(([users, branchList]) => {
        if (!live) return;
        setOwners(users.filter((user) => user.isActive));
        setBranches(branchList);
      })
      .catch(() => {
        /* the scope pickers stay empty and the form still saves a company goal */
      });
    return () => {
      live = false;
    };
  }, []);

  const meta = datasets.find((entry) => entry.id === draft.dataset);
  const measureMeta = meta?.measures.find((entry) => entry.id === draft.measure);

  function patch(next: Partial<Draft>) {
    setDraft((current) => ({ ...current, ...next }));
  }

  function changeDataset(id: string) {
    const next = datasets.find((entry) => entry.id === id);
    if (!next) return;
    patch({
      dataset: id,
      measure: "*",
      aggregation: "count",
      dateField: next.defaultDateField,
      filters: [],
      ratioFilters: [],
      format: "number",
    });
  }

  // Plain derivation, not a `useMemo`: the React Compiler memoizes this for us,
  // and a hand-written memo here is what it refuses to compile around.
  const filterFields = toFilterFields(meta?.filterFields);

  async function save() {
    const target = Number(draft.targetValue);

    if (!draft.name.trim()) {
      toast.error("Give the goal a name");
      return;
    }
    if (!Number.isFinite(target) || target <= 0) {
      toast.error("Set a target greater than zero");
      return;
    }
    if (draft.scopeType === "User" && !draft.ownerId) {
      toast.error("Pick the person this goal belongs to");
      return;
    }
    if (draft.scopeType === "Branch" && !draft.branchId) {
      toast.error("Pick the branch this goal belongs to");
      return;
    }

    const input: GoalInput = {
      name: draft.name.trim(),
      description: draft.description.trim() || undefined,
      dataset: draft.dataset,
      measure: draft.measure === "*" ? null : draft.measure,
      aggregation: draft.measure === "*" ? "count" : draft.aggregation,
      filter: toFilterNode(draft.filters, draft.matchMode),
      dateField: draft.dateField,
      isRatio: draft.isRatio,
      ratioDataset: draft.isRatio ? draft.dataset : null,
      ratioMeasure: draft.isRatio
        ? draft.ratioMeasure === "*"
          ? null
          : draft.ratioMeasure
        : null,
      ratioAggregation: draft.isRatio ? draft.ratioAggregation : null,
      ratioFilter: draft.isRatio
        ? toFilterNode(draft.ratioFilters, draft.ratioMatchMode)
        : null,
      ratioDateField: draft.isRatio ? draft.dateField : null,
      format: draft.isRatio ? "percent" : draft.format,
      periodType: draft.periodType,
      startDate: draft.periodType === "Custom" ? draft.startDate : null,
      endDate: draft.periodType === "Custom" ? draft.endDate : null,
      scopeType: draft.scopeType,
      ownerId: draft.scopeType === "User" ? Number(draft.ownerId) : null,
      branchId: draft.scopeType === "Branch" ? Number(draft.branchId) : null,
      targetValue: target,
    };

    setSaving(true);
    try {
      if (goal) {
        await goalsApi.update(goal.id, input);
        toast.success("Goal updated");
      } else {
        await goalsApi.create(input);
        toast.success("Goal created");
      }
      onSaved();
      onOpenChange(false);
    } catch (cause) {
      toast.error((cause as Error).message);
    } finally {
      setSaving(false);
    }
  }

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-h-[90vh] gap-0 overflow-hidden p-0 sm:max-w-2xl">
        <DialogHeader className="border-b p-5 pb-4 text-left">
          <DialogTitle className="text-base">
            {goal ? "Edit goal" : "New goal"}
          </DialogTitle>
          <DialogDescription>
            A goal measures the same records the dashboards do, so its progress
            can never disagree with the charts.
          </DialogDescription>
        </DialogHeader>

        <Tabs defaultValue="basics">
          <TabsList className="mx-5 mt-3">
            <TabsTrigger value="basics">Basics</TabsTrigger>
            <TabsTrigger value="measure">What to measure</TabsTrigger>
            <TabsTrigger value="filters">Filters</TabsTrigger>
          </TabsList>

          <ScrollArea className="max-h-[52vh]">
            {/* ---------------- Basics ---------------- */}
            <TabsContent value="basics" className="m-0 flex flex-col gap-4 p-5">
              <Field label="Name">
                <Input
                  autoFocus
                  value={draft.name}
                  onChange={(event) => patch({ name: event.target.value })}
                  placeholder="e.g. Q3 booked revenue"
                  className="h-9 text-[13px]"
                />
              </Field>

              <Field label="Description" hint="Optional — what this target is for">
                <Textarea
                  value={draft.description}
                  onChange={(event) => patch({ description: event.target.value })}
                  placeholder="Why does this number matter?"
                  className="min-h-16 resize-none text-[13px]"
                />
              </Field>

              <div className="grid grid-cols-2 gap-3">
                <Field label="Target">
                  <Input
                    type="number"
                    inputMode="decimal"
                    value={draft.targetValue}
                    onChange={(event) => patch({ targetValue: event.target.value })}
                    placeholder={draft.isRatio ? "e.g. 25 (%)" : "e.g. 150000000"}
                    className="h-9 text-[13px]"
                  />
                </Field>

                <Field label="Shown as">
                  <Select
                    value={draft.isRatio ? "percent" : draft.format}
                    onValueChange={(value) => patch({ format: value as MeasureFormat })}
                    disabled={draft.isRatio}
                  >
                    <SelectTrigger className="h-9 text-[13px]">
                      <SelectValue />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value="currency">Currency (₹)</SelectItem>
                      <SelectItem value="number">Plain number</SelectItem>
                      <SelectItem value="percent">Percentage</SelectItem>
                    </SelectContent>
                  </Select>
                </Field>
              </div>

              <Field label="Period">
                <Select
                  value={draft.periodType}
                  onValueChange={(value) => patch({ periodType: value as GoalPeriod })}
                >
                  <SelectTrigger className="h-9 text-[13px]">
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    {(Object.keys(PERIOD_LABELS) as GoalPeriod[]).map((period) => (
                      <SelectItem key={period} value={period}>
                        {PERIOD_LABELS[period]}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </Field>

              {draft.periodType === "Custom" ? (
                <div className="grid grid-cols-2 gap-3">
                  <Field label="From">
                    <Input
                      type="date"
                      value={draft.startDate}
                      onChange={(event) => patch({ startDate: event.target.value })}
                      className="h-9 text-[13px]"
                    />
                  </Field>
                  <Field label="To">
                    <Input
                      type="date"
                      value={draft.endDate}
                      onChange={(event) => patch({ endDate: event.target.value })}
                      className="h-9 text-[13px]"
                    />
                  </Field>
                </div>
              ) : null}

              <Field label="Belongs to">
                <Select
                  value={draft.scopeType}
                  onValueChange={(value) => patch({ scopeType: value as GoalScope })}
                >
                  <SelectTrigger className="h-9 text-[13px]">
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    {(Object.keys(SCOPE_LABELS) as GoalScope[]).map((scope) => (
                      <SelectItem key={scope} value={scope}>
                        {SCOPE_LABELS[scope]}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </Field>

              {draft.scopeType === "User" ? (
                <Field label="Person">
                  <Select
                    value={draft.ownerId}
                    onValueChange={(value) => patch({ ownerId: value })}
                  >
                    <SelectTrigger className="h-9 text-[13px]">
                      <SelectValue placeholder="Choose a person…" />
                    </SelectTrigger>
                    <SelectContent>
                      {owners.map((owner) => (
                        <SelectItem key={owner.id} value={String(owner.id)}>
                          {owner.name}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                </Field>
              ) : null}

              {draft.scopeType === "Branch" ? (
                <Field label="Branch">
                  <Select
                    value={draft.branchId}
                    onValueChange={(value) => patch({ branchId: value })}
                  >
                    <SelectTrigger className="h-9 text-[13px]">
                      <SelectValue placeholder="Choose a branch…" />
                    </SelectTrigger>
                    <SelectContent>
                      {branches.map((branch) => (
                        <SelectItem key={branch.id} value={String(branch.id)}>
                          {branch.name}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                </Field>
              ) : null}
            </TabsContent>

            {/* ---------------- Measure ---------------- */}
            <TabsContent value="measure" className="m-0 flex flex-col gap-4 p-5">
              <Field label="Object" hint="Which records count towards this goal">
                <Select value={draft.dataset} onValueChange={changeDataset}>
                  <SelectTrigger className="h-9 text-[13px]">
                    <SelectValue />
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

              <Field label="Measure">
                <div className="flex gap-2">
                  <Select
                    value={draft.measure}
                    onValueChange={(value) => {
                      const next = meta?.measures.find((m) => m.id === value);
                      patch({
                        measure: value,
                        aggregation: value === "*" ? "count" : next?.aggregations[0] ?? "sum",
                        format: value === "*" ? "number" : next?.format ?? "number",
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

                  {draft.measure !== "*" ? (
                    <Select
                      value={draft.aggregation}
                      onValueChange={(value) => patch({ aggregation: value })}
                    >
                      <SelectTrigger className="h-9 w-32 text-[13px]">
                        <SelectValue />
                      </SelectTrigger>
                      <SelectContent>
                        {(measureMeta?.aggregations ?? []).map((fn) => (
                          <SelectItem key={fn} value={fn}>
                            {fn}
                          </SelectItem>
                        ))}
                      </SelectContent>
                    </Select>
                  ) : null}
                </div>
              </Field>

              <Field
                label="Date field"
                hint="Which date decides whether a record falls inside the period"
              >
                <Select
                  value={draft.dateField}
                  onValueChange={(value) => patch({ dateField: value })}
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

              <div className="flex flex-col gap-2.5 rounded-md border p-3">
                <label className="flex cursor-pointer items-center justify-between gap-3">
                  <span className="flex flex-col">
                    <span className="text-[12.5px]">Measure a rate, not a total</span>
                    <span className="text-[11px] text-muted-foreground">
                      Divides the measure above by a second one — a conversion
                      rate rather than a count
                    </span>
                  </span>
                  <Switch
                    checked={draft.isRatio}
                    onCheckedChange={(isRatio) => patch({ isRatio })}
                  />
                </label>

                {draft.isRatio ? (
                  <Field label="Divide by">
                    <Select
                      value={draft.ratioMeasure}
                      onValueChange={(value) =>
                        patch({
                          ratioMeasure: value,
                          ratioAggregation: value === "*" ? "count" : "sum",
                        })
                      }
                    >
                      <SelectTrigger className="h-9 text-[13px]">
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
                  </Field>
                ) : null}
              </div>
            </TabsContent>

            {/* ---------------- Filters ---------------- */}
            <TabsContent value="filters" className="m-0 flex flex-col gap-4 p-5">
              {goal ? (
                <p className="rounded-md border border-dashed px-3 py-2 text-[12px] text-muted-foreground">
                  Saved conditions are not shown here yet — the server stores the
                  compiled tree. Anything you add below replaces them on save.
                </p>
              ) : null}

              <div>
                <Label className="text-[12px] font-medium">
                  Which records count
                </Label>
                <p className="pb-2 text-[11.5px] text-muted-foreground">
                  e.g. only deals whose stage is Closed Won.
                </p>
                <AdvancedFilterPanel
                  fields={filterFields}
                  conditions={draft.filters}
                  matchMode={draft.matchMode}
                  onChange={(filters) => patch({ filters })}
                  onMatchModeChange={(matchMode) => patch({ matchMode })}
                />
              </div>

              {draft.isRatio ? (
                <div className="border-t pt-4">
                  <Label className="text-[12px] font-medium">
                    Which records form the denominator
                  </Label>
                  <p className="pb-2 text-[11.5px] text-muted-foreground">
                    Leave empty to divide by every record in the period.
                  </p>
                  <AdvancedFilterPanel
                    fields={filterFields}
                    conditions={draft.ratioFilters}
                    matchMode={draft.ratioMatchMode}
                    onChange={(ratioFilters) => patch({ ratioFilters })}
                    onMatchModeChange={(ratioMatchMode) => patch({ ratioMatchMode })}
                  />
                </div>
              ) : null}
            </TabsContent>
          </ScrollArea>
        </Tabs>

        <DialogFooter className="border-t p-4">
          <Button variant="outline" onClick={() => onOpenChange(false)}>
            Cancel
          </Button>
          <Button onClick={save} disabled={saving}>
            {saving ? <Loader2 className="animate-spin" /> : null}
            {goal ? "Save changes" : "Create goal"}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
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

/** Exported so the page can show how many conditions a draft carries. */
export function activeConditionCount(
  conditions: FilterCondition[],
  fields: FilterFieldDef[]
) {
  return conditions.filter((condition) => isComplete(condition, fields)).length;
}

export { newCondition };
