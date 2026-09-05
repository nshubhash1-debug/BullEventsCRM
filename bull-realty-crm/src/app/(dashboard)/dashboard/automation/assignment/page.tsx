"use client";

import * as React from "react";
import { ArrowDown, ArrowUp, Loader2, Plus, Route, Users } from "lucide-react";
import { toast } from "sonner";

import { PagePanel } from "@/components/shell/page-panel";
import { CrmLoadingState } from "@/components/shell/crm-loader";
import { Pill } from "@/components/crm/metrics";
import { RuleCard, RuleEmpty, RuleRow } from "@/components/admin/rule-list";
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
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { ApiError } from "@/lib/api";
import {
  automationApi,
  type AssignmentRule,
  type AutomationReference,
  type SaveAssignmentRule,
} from "@/lib/automation-api";

const STRATEGY_LABELS: Record<string, string> = {
  RoundRobin: "in turn",
  LeastLoaded: "to whoever has the fewest open leads",
  Fixed: "always to one person",
};

const LEAD_FIELDS = [
  "Source", "City", "State", "Priority", "EventType", "EventCategory",
  "Zone", "Campaign",
];

/**
 * Assignment rules.
 *
 * Rules are tried top to bottom and the first match wins. That order is the
 * rule, so the screen shows it and lets it be changed — a rule that never fires
 * because a broader one sits above it is the commonest way lead routing goes
 * wrong, and it is only diagnosable when the order is visible.
 *
 * A lead that matches nothing is left unassigned rather than handed to somebody
 * arbitrary: an unassigned lead is visible to the whole desk and gets picked
 * up, while a lead quietly given to the wrong person does not.
 */
export default function AssignmentRulesPage() {
  const [rules, setRules] = React.useState<AssignmentRule[] | null>(null);
  const [reference, setReference] = React.useState<AutomationReference | null>(null);
  const [editing, setEditing] = React.useState<AssignmentRule | null>(null);
  const [creating, setCreating] = React.useState(false);
  const [busy, setBusy] = React.useState<number | null>(null);

  const load = React.useCallback(() => {
    automationApi
      .assignmentRules()
      .then(setRules)
      .catch((error: unknown) => {
        toast.error("Could not load assignment rules", {
          description: error instanceof ApiError ? error.message : "Network error.",
        });
        setRules([]);
      });
  }, []);

  React.useEffect(() => {
    load();
    automationApi.reference().then(setReference).catch(() => setReference(null));
  }, [load]);

  async function act(id: number, work: () => Promise<unknown>, done: string) {
    setBusy(id);
    try {
      await work();
      toast.success(done);
      load();
    } catch (error) {
      toast.error("Could not change the rule", {
        description: error instanceof ApiError ? error.message : "Network error.",
      });
    } finally {
      setBusy(null);
    }
  }

  async function move(rule: AssignmentRule, by: -1 | 1) {
    if (!rules) return;

    const index = rules.findIndex((r) => r.id === rule.id);
    const swap = index + by;
    if (swap < 0 || swap >= rules.length) return;

    const order = rules.map((r) => r.id);
    [order[index], order[swap]] = [order[swap], order[index]];

    await act(rule.id, () => automationApi.reorderAssignmentRules(order), "Order changed");
  }

  if (!rules || !reference) {
    return (
      <PagePanel icon={Route} title="Assignment rules">
        <CrmLoadingState label="Loading assignment rules" />
      </PagePanel>
    );
  }

  return (
    <PagePanel
      icon={Route}
      title="Assignment rules"
      hint="Who a new lead goes to. Tried top to bottom — the first rule that matches wins."
      actions={
        <Button size="sm" className="gap-1.5" onClick={() => setCreating(true)}>
          <Plus className="size-3.5" />
          New rule
        </Button>
      }
    >
      <RuleCard icon={Route} title="Order of play" count={rules.length}>
        {rules.length === 0 ? (
          <RuleEmpty
            title="No assignment rule yet."
            meaning="Every new lead arrives unassigned and waits for somebody to pick it up. A rule routes it the moment it lands."
            action={
              <Button size="sm" variant="outline" onClick={() => setCreating(true)}>
                Write the first rule
              </Button>
            }
          />
        ) : (
          rules.map((rule, index) => (
            <RuleRow
              key={rule.id}
              order={index + 1}
              name={rule.name}
              active={rule.isActive}
              busy={busy === rule.id}
              badges={
                <>
                  <Pill tone={rule.poolSize > 0 ? "info" : "danger"}>
                    {rule.poolSize > 0
                      ? `${rule.poolSize} in pool`
                      : "nobody in pool"}
                  </Pill>
                  {index === rules.length - 1 && !rule.criteriaField ? (
                    <Pill tone="neutral">catch-all</Pill>
                  ) : null}
                </>
              }
              sentence={sentence(rule)}
              note={rule.description}
              onToggle={() =>
                act(
                  rule.id,
                  () =>
                    automationApi.updateAssignmentRule(rule.id, {
                      ...toInput(rule),
                      isActive: !rule.isActive,
                    }),
                  rule.isActive ? "Rule switched off" : "Rule switched on"
                )
              }
              onEdit={() => setEditing(rule)}
              onDelete={() => {
                if (window.confirm(`Delete "${rule.name}"?`)) {
                  act(rule.id, () => automationApi.deleteAssignmentRule(rule.id), "Rule deleted");
                }
              }}
            />
          ))
        )}
      </RuleCard>

      {rules.length > 1 ? (
        <p className="mt-2 text-[11.5px] text-muted-foreground">
          A rule with no criteria matches everything, so it belongs last — anything below it never
          runs.
        </p>
      ) : null}

      {/* Reordering sits outside the row so the arrows do not compete with the
          switch and the edit button for the same corner. */}
      {rules.length > 1 ? (
        <div className="mt-3 flex flex-wrap gap-1.5">
          {rules.map((rule, index) => (
            <span key={rule.id} className="inline-flex items-center rounded border bg-card">
              <Button
                size="sm"
                variant="ghost"
                className="h-7 px-1.5"
                disabled={index === 0 || busy !== null}
                onClick={() => move(rule, -1)}
              >
                <ArrowUp className="size-3.5" />
              </Button>
              <span className="max-w-[160px] truncate px-1 text-[11.5px]">{rule.name}</span>
              <Button
                size="sm"
                variant="ghost"
                className="h-7 px-1.5"
                disabled={index === rules.length - 1 || busy !== null}
                onClick={() => move(rule, 1)}
              >
                <ArrowDown className="size-3.5" />
              </Button>
            </span>
          ))}
        </div>
      ) : null}

      <RuleDialog
        reference={reference}
        rule={editing}
        open={creating || editing !== null}
        onOpenChange={(open) => {
          if (!open) {
            setCreating(false);
            setEditing(null);
          }
        }}
        onSaved={load}
      />
    </PagePanel>
  );
}

function sentence(rule: AssignmentRule) {
  const when = rule.criteriaField
    ? `When ${rule.criteriaField} ${rule.criteriaOperator === "contains" ? "contains" : "is"} "${rule.criteriaValue}"`
    : "Every lead";

  const who =
    rule.strategy === "Fixed"
      ? (rule.fixedUserName ?? "one person")
      : rule.poolRoleName
        ? `${rule.poolRoleName} ${STRATEGY_LABELS[rule.strategy] ?? ""}`
        : `the chosen pool ${STRATEGY_LABELS[rule.strategy] ?? ""}`;

  return `${when} → ${who}`;
}

function toInput(rule: AssignmentRule): SaveAssignmentRule {
  return {
    name: rule.name,
    description: rule.description,
    object: rule.object,
    criteriaField: rule.criteriaField,
    criteriaOperator: rule.criteriaOperator,
    criteriaValue: rule.criteriaValue,
    strategy: rule.strategy,
    poolUserIds: rule.poolUserIds,
    poolRoleKey: rule.poolRoleKey,
    fixedUserId: rule.fixedUserId,
    isActive: rule.isActive,
  };
}

function RuleDialog({
  reference,
  rule,
  open,
  onOpenChange,
  onSaved,
}: {
  reference: AutomationReference;
  rule: AssignmentRule | null;
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onSaved: () => void;
}) {
  const [name, setName] = React.useState("");
  const [field, setField] = React.useState("");
  const [operator, setOperator] = React.useState("equals");
  const [value, setValue] = React.useState("");
  const [strategy, setStrategy] = React.useState("RoundRobin");
  const [poolRole, setPoolRole] = React.useState("");
  const [fixedUser, setFixedUser] = React.useState("");
  const [saving, setSaving] = React.useState(false);

  const [seen, setSeen] = React.useState<AssignmentRule | null | undefined>(undefined);

  if (open && rule !== seen) {
    setSeen(rule);
    setName(rule?.name ?? "");
    setField(rule?.criteriaField ?? "");
    setOperator(rule?.criteriaOperator ?? "equals");
    setValue(rule?.criteriaValue ?? "");
    setStrategy(rule?.strategy ?? "RoundRobin");
    setPoolRole(rule?.poolRoleKey ?? "");
    setFixedUser(rule?.fixedUserId?.toString() ?? "");
  }

  if (!open && seen !== undefined) setSeen(undefined);

  async function save() {
    const body: SaveAssignmentRule = {
      name,
      object: "Lead",
      criteriaField: field || null,
      criteriaOperator: field ? operator : null,
      criteriaValue: field ? value || null : null,
      strategy,
      poolRoleKey: strategy === "Fixed" ? null : poolRole || null,
      fixedUserId: strategy === "Fixed" ? Number(fixedUser) || null : null,
      isActive: rule?.isActive ?? true,
    };

    setSaving(true);
    try {
      if (rule) await automationApi.updateAssignmentRule(rule.id, body);
      else await automationApi.createAssignmentRule(body);

      toast.success(rule ? "Rule updated" : "Rule created");
      onOpenChange(false);
      onSaved();
    } catch (error) {
      toast.error("Could not save the rule", {
        description: error instanceof ApiError ? error.message : "Network error.",
      });
    } finally {
      setSaving(false);
    }
  }

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-lg">
        <DialogHeader>
          <DialogTitle>{rule ? "Edit assignment rule" : "New assignment rule"}</DialogTitle>
          <DialogDescription>
            Leads that already have an owner are left alone — this only routes the ones arriving
            unassigned.
          </DialogDescription>
        </DialogHeader>

        <div className="grid gap-3">
          <div className="grid gap-1.5">
            <Label htmlFor="ar-name">Name</Label>
            <Input
              id="ar-name"
              value={name}
              onChange={(e) => setName(e.target.value)}
              placeholder="Portal leads to the tele desk"
            />
          </div>

          <fieldset className="grid gap-2 rounded-lg border p-3">
            <legend className="px-1 text-[11px] font-medium tracking-wide text-muted-foreground uppercase">
              When
            </legend>

            <div className="grid gap-2 sm:grid-cols-3">
              <Select value={field} onValueChange={setField}>
                <SelectTrigger>
                  <SelectValue placeholder="Every lead" />
                </SelectTrigger>
                <SelectContent>
                  {LEAD_FIELDS.map((f) => (
                    <SelectItem key={f} value={f}>
                      {f}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>

              <Select value={operator} onValueChange={setOperator} disabled={!field}>
                <SelectTrigger>
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="equals">is</SelectItem>
                  <SelectItem value="contains">contains</SelectItem>
                  <SelectItem value="notequals">is not</SelectItem>
                </SelectContent>
              </Select>

              <Input
                value={value}
                onChange={(e) => setValue(e.target.value)}
                placeholder="Value"
                disabled={!field}
              />
            </div>

            {!field ? (
              <p className="text-[11.5px] text-muted-foreground">
                No condition means this matches every lead — the catch-all, which belongs last.
              </p>
            ) : null}
          </fieldset>

          <fieldset className="grid gap-2 rounded-lg border p-3">
            <legend className="px-1 text-[11px] font-medium tracking-wide text-muted-foreground uppercase">
              Route to
            </legend>

            <Select value={strategy} onValueChange={setStrategy}>
              <SelectTrigger>
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="RoundRobin">Round robin — everyone in turn</SelectItem>
                <SelectItem value="LeastLoaded">Least loaded — fewest open leads</SelectItem>
                <SelectItem value="Fixed">One named person</SelectItem>
              </SelectContent>
            </Select>

            {strategy === "Fixed" ? (
              <Select value={fixedUser} onValueChange={setFixedUser}>
                <SelectTrigger>
                  <SelectValue placeholder="Choose a person" />
                </SelectTrigger>
                <SelectContent>
                  {reference.users.map((u) => (
                    <SelectItem key={u.value} value={u.value}>
                      {u.label}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            ) : (
              <>
                <Select value={poolRole} onValueChange={setPoolRole}>
                  <SelectTrigger>
                    <SelectValue placeholder="Everyone holding a role" />
                  </SelectTrigger>
                  <SelectContent>
                    {reference.roles.map((r) => (
                      <SelectItem key={r.value} value={r.value}>
                        {r.label}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
                <p className="text-[11.5px] text-muted-foreground">
                  The pool is everyone active who holds that role, so it follows hiring without
                  being edited.
                </p>
              </>
            )}
          </fieldset>
        </div>

        <DialogFooter>
          <Button variant="outline" onClick={() => onOpenChange(false)} disabled={saving}>
            Cancel
          </Button>
          <Button onClick={save} disabled={saving} className="gap-1.5">
            {saving ? <Loader2 className="size-4 animate-spin" /> : <Users className="size-4" />}
            {rule ? "Save rule" : "Create rule"}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
