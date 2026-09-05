"use client";

import * as React from "react";
import { Loader2, Plus, Share2, Trash2, Users } from "lucide-react";
import { toast } from "sonner";

import { PagePanel } from "@/components/shell/page-panel";
import { CrmLoadingState } from "@/components/shell/crm-loader";
import { Pill } from "@/components/crm/metrics";
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
import { Textarea } from "@/components/ui/textarea";
import {
  accessApi,
  ApiError,
  type SaveSharingRule,
  type SharingRule,
  type SharingRules,
} from "@/lib/api";
import { cn } from "@/lib/utils";

const TARGETS = [
  { value: "Role", label: "Everyone holding a role" },
  { value: "RoleAndSubordinates", label: "A role and everyone under it" },
  { value: "Branch", label: "Everyone in a branch" },
  { value: "User", label: "One person" },
];

const OPERATORS = [
  { value: "equals", label: "is" },
  { value: "contains", label: "contains" },
];

/**
 * Sharing rules.
 *
 * Sharing only ever widens. The role decides the baseline — own records, the
 * reporting line, or everything — and a rule adds to it. There is deliberately
 * no rule that takes access away, because the moment both directions exist the
 * answer depends on which rule ran last, and an access model nobody can predict
 * is one nobody can audit.
 *
 * Each rule is stated as one sentence on the row, in the order somebody thinks
 * about it: which records, to whom, and whether they may edit. That reads
 * faster than six labelled columns and it is what makes a wrong rule obvious.
 */
export default function SharingRulesPage() {
  const [data, setData] = React.useState<SharingRules | null>(null);
  const [editing, setEditing] = React.useState<SharingRule | null>(null);
  const [creating, setCreating] = React.useState(false);
  const [busy, setBusy] = React.useState<number | null>(null);

  const load = React.useCallback(() => {
    accessApi
      .sharingRules()
      .then(setData)
      .catch((error: unknown) => {
        toast.error("Could not load sharing rules", {
          description: error instanceof ApiError ? error.message : "Network error.",
        });
      });
  }, []);

  React.useEffect(load, [load]);

  async function remove(rule: SharingRule) {
    if (!window.confirm(`Delete "${rule.name}"? Anyone who could only see those records through it loses access.`)) {
      return;
    }

    setBusy(rule.id);
    try {
      await accessApi.deleteSharingRule(rule.id);
      toast.success("Rule deleted");
      load();
    } catch (error) {
      toast.error("Could not delete the rule", {
        description: error instanceof ApiError ? error.message : "Network error.",
      });
    } finally {
      setBusy(null);
    }
  }

  async function toggle(rule: SharingRule) {
    setBusy(rule.id);
    try {
      await accessApi.updateSharingRule(rule.id, { ...toInput(rule), isActive: !rule.isActive });
      toast.success(rule.isActive ? "Rule switched off" : "Rule switched on");
      load();
    } catch (error) {
      toast.error("Could not change the rule", {
        description: error instanceof ApiError ? error.message : "Network error.",
      });
    } finally {
      setBusy(null);
    }
  }

  if (!data) {
    return (
      <PagePanel icon={Share2} title="Sharing rules">
        <CrmLoadingState label="Loading sharing rules" />
      </PagePanel>
    );
  }

  const byObject = data.objects
    .map((object) => ({ object, rules: data.rules.filter((r) => r.object === object) }))
    .filter((group) => group.rules.length > 0);

  return (
    <PagePanel
      icon={Share2}
      title="Sharing rules"
      hint="Standing rules that open records up beyond what a role would see. They only ever add."
      actions={
        <Button size="sm" className="gap-1.5" onClick={() => setCreating(true)}>
          <Plus className="size-3.5" />
          New rule
        </Button>
      }
    >
      <div className="flex flex-col gap-4">
        {data.rules.length === 0 ? (
          <div className="py-14 text-center">
            <p className="text-[13px] text-muted-foreground">No sharing rule yet.</p>
            <p className="mx-auto mt-1 max-w-md text-[12.5px] text-muted-foreground">
              Without one, people see exactly what their role allows — their own records, or their
              reporting line. A rule widens that for a slice of records.
            </p>
          </div>
        ) : (
          byObject.map((group) => (
            <section key={group.object} className="overflow-hidden rounded-xl border bg-card shadow-xs">
              <header className="flex items-center gap-2 border-b px-4 py-2.5">
                <h2 className="text-[13.5px] font-semibold">{group.object}</h2>
                <span className="rounded bg-muted px-1.5 py-px text-[11px] text-muted-foreground tabular-nums">
                  {group.rules.length}
                </span>
              </header>

              <table className="w-full text-[12.5px]">
                <tbody>
                  {group.rules.map((rule) => (
                    <tr
                      key={rule.id}
                      className={cn("border-b last:border-0", !rule.isActive && "opacity-55")}
                    >
                      <td className="px-4 py-2.5">
                        <p className="font-medium">{rule.name}</p>
                        <p className="mt-0.5 text-[12px] text-muted-foreground">{sentence(rule)}</p>
                        {rule.description ? (
                          <p className="mt-0.5 text-[11.5px] text-muted-foreground/80">
                            {rule.description}
                          </p>
                        ) : null}
                      </td>

                      <td className="px-4 py-2.5 text-right whitespace-nowrap">
                        <Pill tone={rule.grantEdit ? "warning" : "info"}>
                          {rule.grantEdit ? "Read & edit" : "Read only"}
                        </Pill>
                      </td>

                      <td className="w-24 px-4 py-2.5 text-right whitespace-nowrap">
                        {busy === rule.id ? (
                          <Loader2 className="ml-auto size-3.5 animate-spin text-muted-foreground" />
                        ) : (
                          <div className="flex items-center justify-end gap-1.5">
                            <Switch
                              checked={rule.isActive}
                              onCheckedChange={() => toggle(rule)}
                              aria-label={rule.isActive ? "Switch off" : "Switch on"}
                            />
                            <Button
                              size="sm"
                              variant="ghost"
                              className="h-7 px-2 text-[11px]"
                              onClick={() => setEditing(rule)}
                            >
                              Edit
                            </Button>
                            <Button
                              size="sm"
                              variant="ghost"
                              className="h-7 px-1.5 text-muted-foreground"
                              onClick={() => remove(rule)}
                            >
                              <Trash2 className="size-3.5" />
                            </Button>
                          </div>
                        )}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </section>
          ))
        )}
      </div>

      <RuleDialog
        reference={data}
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

/** The rule as one readable line: which records, to whom. */
function sentence(rule: SharingRule) {
  const which = rule.ownerRoleKey
    ? `Records owned by ${rule.ownerRoleName ?? rule.ownerRoleKey}`
    : `Records where ${rule.criteriaField} ${rule.criteriaOperator === "contains" ? "contains" : "is"} "${rule.criteriaValue}"`;

  const who =
    rule.target === "Branch"
      ? `everyone in ${rule.targetBranchName ?? "the branch"}`
      : rule.target === "User"
        ? (rule.targetUserName ?? "one person")
        : rule.target === "RoleAndSubordinates"
          ? `${rule.targetRoleName ?? rule.targetRoleKey} and everyone under them`
          : `everyone holding ${rule.targetRoleName ?? rule.targetRoleKey}`;

  return `${which} → ${who}`;
}

function toInput(rule: SharingRule): SaveSharingRule {
  return {
    name: rule.name,
    description: rule.description,
    object: rule.object,
    ownerRoleKey: rule.ownerRoleKey,
    criteriaField: rule.criteriaField,
    criteriaOperator: rule.criteriaOperator,
    criteriaValue: rule.criteriaValue,
    target: rule.target,
    targetRoleKey: rule.targetRoleKey,
    targetBranchId: rule.targetBranchId,
    targetUserId: rule.targetUserId,
    grantEdit: rule.grantEdit,
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
  reference: SharingRules;
  rule: SharingRule | null;
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onSaved: () => void;
}) {
  const [name, setName] = React.useState("");
  const [description, setDescription] = React.useState("");
  const [object, setObject] = React.useState("Lead");
  const [basis, setBasis] = React.useState<"owner" | "criteria">("owner");
  const [ownerRole, setOwnerRole] = React.useState("");
  const [field, setField] = React.useState("");
  const [operator, setOperator] = React.useState("equals");
  const [value, setValue] = React.useState("");
  const [target, setTarget] = React.useState("Role");
  const [targetRole, setTargetRole] = React.useState("");
  const [targetBranch, setTargetBranch] = React.useState("");
  const [targetUser, setTargetUser] = React.useState("");
  const [grantEdit, setGrantEdit] = React.useState(false);
  const [saving, setSaving] = React.useState(false);

  const [seen, setSeen] = React.useState<SharingRule | null | undefined>(undefined);

  if (open && rule !== seen) {
    setSeen(rule);

    setName(rule?.name ?? "");
    setDescription(rule?.description ?? "");
    setObject(rule?.object ?? "Lead");
    setBasis(rule?.criteriaField ? "criteria" : "owner");
    setOwnerRole(rule?.ownerRoleKey ?? "");
    setField(rule?.criteriaField ?? "");
    setOperator(rule?.criteriaOperator ?? "equals");
    setValue(rule?.criteriaValue ?? "");
    setTarget(rule?.target ?? "Role");
    setTargetRole(rule?.targetRoleKey ?? "");
    setTargetBranch(rule?.targetBranchId?.toString() ?? "");
    setTargetUser(rule?.targetUserId?.toString() ?? "");
    setGrantEdit(rule?.grantEdit ?? false);
  }

  if (!open && seen !== undefined) setSeen(undefined);

  const fields = reference.shareableFields[object] ?? [];

  async function save() {
    const body: SaveSharingRule = {
      name,
      description: description || null,
      object,
      ownerRoleKey: basis === "owner" ? ownerRole || null : null,
      criteriaField: basis === "criteria" ? field || null : null,
      criteriaOperator: basis === "criteria" ? operator : null,
      criteriaValue: basis === "criteria" ? value || null : null,
      target,
      targetRoleKey: target === "Role" || target === "RoleAndSubordinates" ? targetRole || null : null,
      targetBranchId: target === "Branch" ? Number(targetBranch) || null : null,
      targetUserId: target === "User" ? Number(targetUser) || null : null,
      grantEdit,
      isActive: rule?.isActive ?? true,
    };

    setSaving(true);
    try {
      if (rule) await accessApi.updateSharingRule(rule.id, body);
      else await accessApi.createSharingRule(body);

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
          <DialogTitle>{rule ? "Edit sharing rule" : "New sharing rule"}</DialogTitle>
          <DialogDescription>
            Sharing only adds. Nobody loses access because of a rule written here.
          </DialogDescription>
        </DialogHeader>

        <div className="grid max-h-[60vh] gap-3 overflow-y-auto pr-1">
          <div className="grid gap-1.5">
            <Label htmlFor="rule-name">Name</Label>
            <Input
              id="rule-name"
              value={name}
              onChange={(event) => setName(event.target.value)}
              placeholder="Varanasi leads to the Varanasi branch"
            />
          </div>

          <div className="grid gap-1.5">
            <Label>Object</Label>
            <Select value={object} onValueChange={setObject}>
              <SelectTrigger>
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                {reference.objects.map((o) => (
                  <SelectItem key={o} value={o}>
                    {o}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>

          {/* ---------------- which records ---------------- */}
          <fieldset className="grid gap-2 rounded-lg border p-3">
            <legend className="px-1 text-[11px] font-medium tracking-wide text-muted-foreground uppercase">
              Which records
            </legend>

            <div className="flex gap-2">
              <Button
                type="button"
                size="sm"
                variant={basis === "owner" ? "default" : "outline"}
                className="text-[12px]"
                onClick={() => setBasis("owner")}
              >
                By owner
              </Button>
              <Button
                type="button"
                size="sm"
                variant={basis === "criteria" ? "default" : "outline"}
                className="text-[12px]"
                onClick={() => setBasis("criteria")}
                disabled={fields.length === 0}
              >
                By field
              </Button>
            </div>

            {basis === "owner" ? (
              <div className="grid gap-1.5">
                <Label>Owned by anyone holding</Label>
                <Select value={ownerRole} onValueChange={setOwnerRole}>
                  <SelectTrigger>
                    <SelectValue placeholder="Choose a role" />
                  </SelectTrigger>
                  <SelectContent>
                    {reference.roles.map((r) => (
                      <SelectItem key={r.value} value={r.value}>
                        {r.label}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
            ) : (
              <div className="grid gap-2 sm:grid-cols-3">
                <Select value={field} onValueChange={setField}>
                  <SelectTrigger>
                    <SelectValue placeholder="Field" />
                  </SelectTrigger>
                  <SelectContent>
                    {fields.map((f) => (
                      <SelectItem key={f} value={f}>
                        {f}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>

                <Select value={operator} onValueChange={setOperator}>
                  <SelectTrigger>
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    {OPERATORS.map((o) => (
                      <SelectItem key={o.value} value={o.value}>
                        {o.label}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>

                <Input
                  value={value}
                  onChange={(event) => setValue(event.target.value)}
                  placeholder="Varanasi"
                />
              </div>
            )}
          </fieldset>

          {/* ---------------- to whom ---------------- */}
          <fieldset className="grid gap-2 rounded-lg border p-3">
            <legend className="px-1 text-[11px] font-medium tracking-wide text-muted-foreground uppercase">
              Shared with
            </legend>

            <Select value={target} onValueChange={setTarget}>
              <SelectTrigger>
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                {TARGETS.map((t) => (
                  <SelectItem key={t.value} value={t.value}>
                    {t.label}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>

            {target === "Branch" ? (
              <Select value={targetBranch} onValueChange={setTargetBranch}>
                <SelectTrigger>
                  <SelectValue placeholder="Choose a branch" />
                </SelectTrigger>
                <SelectContent>
                  {reference.branches.map((b) => (
                    <SelectItem key={b.value} value={b.value}>
                      {b.label}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            ) : target === "User" ? (
              <Select value={targetUser} onValueChange={setTargetUser}>
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
              <Select value={targetRole} onValueChange={setTargetRole}>
                <SelectTrigger>
                  <SelectValue placeholder="Choose a role" />
                </SelectTrigger>
                <SelectContent>
                  {reference.roles.map((r) => (
                    <SelectItem key={r.value} value={r.value}>
                      {r.label}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            )}

            <label className="mt-1 flex items-center gap-2.5 text-[12.5px]">
              <Switch checked={grantEdit} onCheckedChange={setGrantEdit} />
              Let them edit, not only read
            </label>
          </fieldset>

          <div className="grid gap-1.5">
            <Label htmlFor="rule-note">Why this rule exists</Label>
            <Textarea
              id="rule-note"
              value={description}
              onChange={(event) => setDescription(event.target.value)}
              rows={2}
              placeholder="The question somebody will ask in six months is why this access was granted."
            />
          </div>
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
