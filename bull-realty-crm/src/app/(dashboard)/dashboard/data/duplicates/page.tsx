"use client";

import * as React from "react";
import { CopyCheck, Loader2, Plus } from "lucide-react";
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
import { Switch } from "@/components/ui/switch";
import { ApiError } from "@/lib/api";
import { automationApi, type DuplicateRule, type SaveDuplicateRule } from "@/lib/automation-api";

/** The fields a duplicate genuinely shares. Deliberately short. */
const MATCHABLE = [
  { value: "Phone", label: "Phone number" },
  { value: "Email", label: "Email" },
  { value: "Phone,Email", label: "Phone and email" },
  { value: "Name,Phone", label: "Name and phone" },
];

/**
 * Duplicate rules.
 *
 * Matched on exact field equality rather than a similarity score. A fuzzy
 * matcher demos well and then flags two brothers at one address as the same
 * person — and a duplicate rule that cries wolf is one somebody switches off
 * within the week, which leaves you worse off than having none.
 *
 * Phone numbers are compared on their last ten digits, so the same person
 * arriving as "+91 98765 43210", "9876543210" and "098765-43210" from three
 * sources is caught as one.
 */
export default function DuplicateRulesPage() {
  const [rules, setRules] = React.useState<DuplicateRule[] | null>(null);
  const [editing, setEditing] = React.useState<DuplicateRule | null>(null);
  const [creating, setCreating] = React.useState(false);
  const [busy, setBusy] = React.useState<number | null>(null);

  const load = React.useCallback(() => {
    automationApi
      .duplicateRules()
      .then(setRules)
      .catch((error: unknown) => {
        toast.error("Could not load duplicate rules", {
          description: error instanceof ApiError ? error.message : "Network error.",
        });
        setRules([]);
      });
  }, []);

  React.useEffect(load, [load]);

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

  if (!rules) {
    return (
      <PagePanel icon={CopyCheck} title="Duplicate rules">
        <CrmLoadingState label="Loading duplicate rules" />
      </PagePanel>
    );
  }

  return (
    <PagePanel
      icon={CopyCheck}
      title="Duplicate rules"
      hint="What counts as the same person arriving twice, and what to do about it."
      actions={
        <Button size="sm" className="gap-1.5" onClick={() => setCreating(true)}>
          <Plus className="size-3.5" />
          New rule
        </Button>
      }
    >
      <RuleCard icon={CopyCheck} title="Rules" count={rules.length}>
        {rules.length === 0 ? (
          <RuleEmpty
            title="No duplicate rule yet."
            meaning="The same enquiry can arrive from a portal, a call and a walk-in, and three reps will work it without knowing about each other."
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
                <Pill tone={rule.action === "Block" ? "danger" : "warning"}>
                  {rule.action === "Block" ? "Refuses the save" : "Warns"}
                </Pill>
              }
              sentence={`A ${rule.object.toLowerCase()} whose ${fieldWords(rule.matchFields)} already exists${
                rule.acrossOwners ? ", whoever owns it" : ", among this owner's records"
              }`}
              onToggle={() =>
                act(
                  rule.id,
                  () =>
                    automationApi.updateDuplicateRule(rule.id, {
                      ...toInput(rule),
                      isActive: !rule.isActive,
                    }),
                  rule.isActive ? "Rule switched off" : "Rule switched on"
                )
              }
              onEdit={() => setEditing(rule)}
              onDelete={() => {
                if (window.confirm(`Delete "${rule.name}"?`)) {
                  act(rule.id, () => automationApi.deleteDuplicateRule(rule.id), "Rule deleted");
                }
              }}
            />
          ))
        )}
      </RuleCard>

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

function fieldWords(fields: string) {
  const parts = fields.split(",").map((f) => f.trim().toLowerCase());
  if (parts.length === 1) return parts[0];
  return `${parts.slice(0, -1).join(", ")} and ${parts.at(-1)}`;
}

function toInput(rule: DuplicateRule): SaveDuplicateRule {
  return {
    name: rule.name,
    object: rule.object,
    matchFields: rule.matchFields,
    action: rule.action,
    acrossOwners: rule.acrossOwners,
    isActive: rule.isActive,
  };
}

function RuleForm({ rule, onDone }: { rule: DuplicateRule | null; onDone: () => void }) {
  const [name, setName] = React.useState(rule?.name ?? "");
  const [fields, setFields] = React.useState(rule?.matchFields ?? "Phone");
  const [action, setAction] = React.useState(rule?.action ?? "Warn");
  const [acrossOwners, setAcrossOwners] = React.useState(rule?.acrossOwners ?? true);
  const [saving, setSaving] = React.useState(false);

  async function save() {
    setSaving(true);
    try {
      const body: SaveDuplicateRule = {
        name,
        object: "Lead",
        matchFields: fields,
        action,
        acrossOwners,
        isActive: rule?.isActive ?? true,
      };

      if (rule) await automationApi.updateDuplicateRule(rule.id, body);
      else await automationApi.createDuplicateRule(body);

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
    <DialogContent className="sm:max-w-md">
      <DialogHeader>
        <DialogTitle>{rule ? "Edit duplicate rule" : "New duplicate rule"}</DialogTitle>
        <DialogDescription>
          Checked before a lead is written, so a blocking rule refuses the save rather than
          undoing it afterwards.
        </DialogDescription>
      </DialogHeader>

      <div className="grid gap-3">
        <div className="grid gap-1.5">
          <Label htmlFor="dr-name">Name</Label>
          <Input
            id="dr-name"
            value={name}
            onChange={(e) => setName(e.target.value)}
            placeholder="Same phone number"
          />
        </div>

        <div className="grid gap-1.5">
          <Label>Treat as the same person when</Label>
          <Select value={fields} onValueChange={setFields}>
            <SelectTrigger>
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              {MATCHABLE.map((m) => (
                <SelectItem key={m.value} value={m.value}>
                  {m.label} matches
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </div>

        <div className="grid gap-1.5">
          <Label>What to do</Label>
          <Select value={action} onValueChange={setAction}>
            <SelectTrigger>
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="Warn">Warn, but let it save</SelectItem>
              <SelectItem value="Block">Refuse the save</SelectItem>
            </SelectContent>
          </Select>
          <p className="text-[11.5px] text-muted-foreground">
            Blocking is right for a phone number. Blocking on a name is how you end up unable to
            add the second Sharma.
          </p>
        </div>

        <label className="flex items-center gap-2.5 text-[12.5px]">
          <Switch checked={acrossOwners} onCheckedChange={setAcrossOwners} />
          Count matches owned by other people
        </label>
      </div>

      <DialogFooter>
        <Button onClick={save} disabled={saving} className="gap-1.5">
          {saving ? <Loader2 className="size-4 animate-spin" /> : <CopyCheck className="size-4" />}
          {rule ? "Save rule" : "Create rule"}
        </Button>
      </DialogFooter>
    </DialogContent>
  );
}
