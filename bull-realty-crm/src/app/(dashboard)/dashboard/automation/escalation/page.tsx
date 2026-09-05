"use client";

import * as React from "react";
import { Loader2, Play, Plus, Timer } from "lucide-react";
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
  duration,
  type AutomationReference,
  type EscalationRule,
  type SaveEscalationRule,
} from "@/lib/automation-api";

const ACTION_WORDS: Record<string, string> = {
  NotifyOwner: "raise a task for the owner",
  NotifyManager: "raise a task for the owner's manager",
  Reassign: "reassign it",
  RaisePriority: "mark it hot",
};

const STARTS: Record<string, string> = {
  Created: "created",
  LastActivity: "last touched",
};

/**
 * Escalation and SLA rules.
 *
 * The clock runs in working hours, so a four-hour target does not expire over a
 * weekend — a lead that arrives at six on Friday is not late by Saturday
 * lunchtime, and a system that says otherwise trains everyone to ignore it.
 *
 * Each rule fires once per record. An escalation that repeats on every sweep is
 * one everyone mutes, and a muted alert is worse than no alert because it looks
 * like coverage.
 */
export default function EscalationRulesPage() {
  const [rules, setRules] = React.useState<EscalationRule[] | null>(null);
  const [reference, setReference] = React.useState<AutomationReference | null>(null);
  const [editing, setEditing] = React.useState<EscalationRule | null>(null);
  const [creating, setCreating] = React.useState(false);
  const [busy, setBusy] = React.useState<number | null>(null);
  const [sweeping, setSweeping] = React.useState(false);

  const load = React.useCallback(() => {
    automationApi
      .escalationRules()
      .then(setRules)
      .catch((error: unknown) => {
        toast.error("Could not load escalation rules", {
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

  async function sweep() {
    setSweeping(true);
    try {
      const result = await automationApi.runSweep();

      toast.success(`${result.breached} past target`, {
        description:
          result.notes.length > 0
            ? result.notes.join(" ")
            : `${result.checked} records checked, ${result.acted} acted on.`,
        duration: 7000,
      });

      load();
    } catch (error) {
      toast.error("The sweep failed", {
        description: error instanceof ApiError ? error.message : "Network error.",
      });
    } finally {
      setSweeping(false);
    }
  }

  if (!rules || !reference) {
    return (
      <PagePanel icon={Timer} title="Escalation & SLA rules">
        <CrmLoadingState label="Loading escalation rules" />
      </PagePanel>
    );
  }

  return (
    <PagePanel
      icon={Timer}
      title="Escalation & SLA rules"
      hint="What happens when a record sits too long. The clock counts working hours only."
      actions={
        <>
          <Button
            variant="outline"
            size="sm"
            className="gap-1.5"
            disabled={sweeping || rules.length === 0}
            onClick={sweep}
          >
            {sweeping ? <Loader2 className="size-3.5 animate-spin" /> : <Play className="size-3.5" />}
            Run now
          </Button>
          <Button size="sm" className="gap-1.5" onClick={() => setCreating(true)}>
            <Plus className="size-3.5" />
            New rule
          </Button>
        </>
      }
    >
      <RuleCard icon={Timer} title="Rules" count={rules.length}>
        {rules.length === 0 ? (
          <RuleEmpty
            title="No escalation rule yet."
            meaning="Nothing is watching how long a lead waits. A rule sets a target and raises a task the moment it is missed."
            action={
              <Button size="sm" variant="outline" onClick={() => setCreating(true)}>
                Write the first rule
              </Button>
            }
          />
        ) : (
          rules.map((rule) => (
            <RuleRow
              key={rule.id}
              name={rule.name}
              active={rule.isActive}
              busy={busy === rule.id}
              badges={
                <>
                  <Pill tone="info">{duration(rule.targetMinutes)}</Pill>
                  {rule.firedCount > 0 ? (
                    <Pill tone="warning">fired {rule.firedCount}×</Pill>
                  ) : null}
                </>
              }
              sentence={`${
                rule.criteriaField
                  ? `A ${rule.object.toLowerCase()} where ${rule.criteriaField} is "${rule.criteriaValue}"`
                  : `Any ${rule.object.toLowerCase()}`
              } still open ${duration(rule.targetMinutes)} of working time after it was ${
                STARTS[rule.startsFrom] ?? "created"
              } → ${ACTION_WORDS[rule.action] ?? rule.action}${
                rule.reassignToUserName ? ` to ${rule.reassignToUserName}` : ""
              }`}
              note={
                rule.businessHoursName
                  ? `Measured against ${rule.businessHoursName}`
                  : "Measured against the default business hours"
              }
              onToggle={() =>
                act(
                  rule.id,
                  () =>
                    automationApi.updateEscalationRule(rule.id, {
                      ...toInput(rule),
                      isActive: !rule.isActive,
                    }),
                  rule.isActive ? "Rule switched off" : "Rule switched on"
                )
              }
              onEdit={() => setEditing(rule)}
              onDelete={() => {
                if (window.confirm(`Delete "${rule.name}"?`)) {
                  act(rule.id, () => automationApi.deleteEscalationRule(rule.id), "Rule deleted");
                }
              }}
            />
          ))
        )}
      </RuleCard>

      <p className="mt-2 text-[11.5px] text-muted-foreground">
        Escalations run on a schedule. Add an <strong>Escalation sweep</strong> job on the Scheduled
        jobs screen to have them checked automatically.
      </p>

      <Dialog
        open={creating || editing !== null}
        onOpenChange={(open) => {
          if (!open) {
            setCreating(false);
            setEditing(null);
          }
        }}
      >
        <RuleForm
          reference={reference}
          rule={editing}
          onDone={() => {
            setCreating(false);
            setEditing(null);
            load();
          }}
        />
      </Dialog>
    </PagePanel>
  );
}

function toInput(rule: EscalationRule): SaveEscalationRule {
  return {
    name: rule.name,
    object: rule.object,
    criteriaField: rule.criteriaField,
    criteriaOperator: rule.criteriaOperator,
    criteriaValue: rule.criteriaValue,
    startsFrom: rule.startsFrom,
    targetMinutes: rule.targetMinutes,
    businessHoursId: rule.businessHoursId,
    action: rule.action,
    reassignToUserId: rule.reassignToUserId,
    isActive: rule.isActive,
  };
}

function RuleForm({
  reference,
  rule,
  onDone,
}: {
  reference: AutomationReference;
  rule: EscalationRule | null;
  onDone: () => void;
}) {
  const [name, setName] = React.useState(rule?.name ?? "");
  const [field, setField] = React.useState(rule?.criteriaField ?? "");
  const [value, setValue] = React.useState(rule?.criteriaValue ?? "");
  const [startsFrom, setStartsFrom] = React.useState(rule?.startsFrom ?? "Created");
  const [amount, setAmount] = React.useState(String((rule?.targetMinutes ?? 240) / 60));
  const [unit, setUnit] = React.useState("hours");
  const [hoursId, setHoursId] = React.useState(rule?.businessHoursId?.toString() ?? "");
  const [action, setAction] = React.useState(rule?.action ?? "NotifyManager");
  const [reassign, setReassign] = React.useState(rule?.reassignToUserId?.toString() ?? "");
  const [saving, setSaving] = React.useState(false);

  async function save() {
    const minutes =
      unit === "minutes"
        ? Number(amount)
        : unit === "hours"
          ? Number(amount) * 60
          : Number(amount) * 1440;

    setSaving(true);
    try {
      const body: SaveEscalationRule = {
        name,
        object: "Lead",
        criteriaField: field || null,
        criteriaOperator: field ? "equals" : null,
        criteriaValue: field ? value || null : null,
        startsFrom,
        targetMinutes: Math.round(minutes),
        businessHoursId: Number(hoursId) || null,
        action,
        reassignToUserId: action === "Reassign" ? Number(reassign) || null : null,
        isActive: rule?.isActive ?? true,
      };

      if (rule) await automationApi.updateEscalationRule(rule.id, body);
      else await automationApi.createEscalationRule(body);

      toast.success(rule ? "Rule updated" : "Rule created");
      onDone();
    } catch (error) {
      toast.error("Could not save the rule", {
        description: error instanceof ApiError ? error.message : "Network error.",
      });
    } finally {
      setSaving(false);
    }
  }

  return (
    <DialogContent className="sm:max-w-lg">
      <DialogHeader>
        <DialogTitle>{rule ? "Edit escalation rule" : "New escalation rule"}</DialogTitle>
        <DialogDescription>
          The target is measured in working time, so a weekend does not count against it.
        </DialogDescription>
      </DialogHeader>

      <div className="grid gap-3">
        <div className="grid gap-1.5">
          <Label htmlFor="er-name">Name</Label>
          <Input
            id="er-name"
            value={name}
            onChange={(e) => setName(e.target.value)}
            placeholder="First response within four hours"
          />
        </div>

        <div className="grid gap-2 sm:grid-cols-2">
          <div className="grid gap-1.5">
            <Label>Only when</Label>
            <Select value={field} onValueChange={setField}>
              <SelectTrigger>
                <SelectValue placeholder="Any lead" />
              </SelectTrigger>
              <SelectContent>
                {["Priority", "Source", "Stage", "City"].map((f) => (
                  <SelectItem key={f} value={f}>
                    {f}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>

          <div className="grid gap-1.5">
            <Label htmlFor="er-value">is</Label>
            <Input
              id="er-value"
              value={value}
              onChange={(e) => setValue(e.target.value)}
              disabled={!field}
              placeholder="Hot"
            />
          </div>
        </div>

        <div className="grid gap-2 sm:grid-cols-3">
          <div className="grid gap-1.5">
            <Label>Clock starts</Label>
            <Select value={startsFrom} onValueChange={setStartsFrom}>
              <SelectTrigger>
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="Created">when created</SelectItem>
                <SelectItem value="LastActivity">at last activity</SelectItem>
              </SelectContent>
            </Select>
          </div>

          <div className="grid gap-1.5">
            <Label htmlFor="er-amount">Target</Label>
            <Input
              id="er-amount"
              inputMode="numeric"
              value={amount}
              onChange={(e) => setAmount(e.target.value)}
              className="tabular-nums"
            />
          </div>

          <div className="grid gap-1.5">
            <Label>&nbsp;</Label>
            <Select value={unit} onValueChange={setUnit}>
              <SelectTrigger>
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="minutes">minutes</SelectItem>
                <SelectItem value="hours">working hours</SelectItem>
                <SelectItem value="days">working days</SelectItem>
              </SelectContent>
            </Select>
          </div>
        </div>

        <div className="grid gap-1.5">
          <Label>Business hours</Label>
          <Select value={hoursId} onValueChange={setHoursId}>
            <SelectTrigger>
              <SelectValue placeholder="Use the default set" />
            </SelectTrigger>
            <SelectContent>
              {reference.businessHours.map((h) => (
                <SelectItem key={h.value} value={h.value}>
                  {h.label}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </div>

        <div className="grid gap-1.5">
          <Label>Then</Label>
          <Select value={action} onValueChange={setAction}>
            <SelectTrigger>
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="NotifyManager">Raise a task for the owner&apos;s manager</SelectItem>
              <SelectItem value="NotifyOwner">Raise a task for the owner</SelectItem>
              <SelectItem value="RaisePriority">Mark the lead hot</SelectItem>
              <SelectItem value="Reassign">Reassign it</SelectItem>
            </SelectContent>
          </Select>

          {action === "Reassign" ? (
            <Select value={reassign} onValueChange={setReassign}>
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
          ) : null}
        </div>
      </div>

      <DialogFooter>
        <Button onClick={save} disabled={saving} className="gap-1.5">
          {saving ? <Loader2 className="size-4 animate-spin" /> : <Timer className="size-4" />}
          {rule ? "Save rule" : "Create rule"}
        </Button>
      </DialogFooter>
    </DialogContent>
  );
}
