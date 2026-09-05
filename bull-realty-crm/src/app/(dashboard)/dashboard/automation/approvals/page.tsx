"use client";

import * as React from "react";
import { ChevronRight, GitPullRequestArrow, Loader2, Plus, Trash2 } from "lucide-react";
import { toast } from "sonner";

import { PagePanel } from "@/components/shell/page-panel";
import { CrmLoadingState } from "@/components/shell/crm-loader";
import { Pill } from "@/components/crm/metrics";
import { RuleCard, RuleEmpty } from "@/components/admin/rule-list";
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
import {
  automationApi,
  type ApprovalProcess,
  type AutomationReference,
  type SaveApprovalProcess,
  type SaveApprovalStep,
} from "@/lib/automation-api";

/**
 * Approval processes.
 *
 * Modelled as ordered steps rather than a branching flow. An approval that can
 * branch is one nobody can answer "who has it now" about — and that is the only
 * question anybody ever asks of an approval.
 *
 * A step names a kind of approver, not usually a person: "the submitter's
 * manager" survives somebody changing teams, where a named individual becomes a
 * dead end the first time they go on leave.
 */
export default function ApprovalProcessesPage() {
  const [processes, setProcesses] = React.useState<ApprovalProcess[] | null>(null);
  const [reference, setReference] = React.useState<AutomationReference | null>(null);
  const [editing, setEditing] = React.useState<ApprovalProcess | null>(null);
  const [creating, setCreating] = React.useState(false);
  const [busy, setBusy] = React.useState<number | null>(null);

  const load = React.useCallback(() => {
    automationApi
      .approvalProcesses()
      .then(setProcesses)
      .catch((error: unknown) => {
        toast.error("Could not load approval processes", {
          description: error instanceof ApiError ? error.message : "Network error.",
        });
        setProcesses([]);
      });
  }, []);

  React.useEffect(() => {
    load();
    automationApi.reference().then(setReference).catch(() => setReference(null));
  }, [load]);

  async function remove(process: ApprovalProcess) {
    if (!window.confirm(`Delete "${process.name}"?`)) return;

    setBusy(process.id);
    try {
      await automationApi.deleteApprovalProcess(process.id);
      toast.success("Process deleted");
      load();
    } catch (error) {
      toast.error("Could not delete it", {
        description: error instanceof ApiError ? error.message : "Network error.",
      });
    } finally {
      setBusy(null);
    }
  }

  if (!processes || !reference) {
    return (
      <PagePanel icon={GitPullRequestArrow} title="Approval processes">
        <CrmLoadingState label="Loading approval processes" />
      </PagePanel>
    );
  }

  return (
    <PagePanel
      icon={GitPullRequestArrow}
      title="Approval processes"
      hint="The sign-offs a record has to clear, in order."
      actions={
        <Button size="sm" className="gap-1.5" onClick={() => setCreating(true)}>
          <Plus className="size-3.5" />
          New process
        </Button>
      }
    >
      <div className="flex flex-col gap-4">
        {processes.length === 0 ? (
          <RuleCard icon={GitPullRequestArrow} title="Processes" count={0}>
            <RuleEmpty
              title="No approval process yet."
              meaning="Discounts, cancellations and anything else that needs a second pair of eyes go through without one."
              action={
                <Button size="sm" variant="outline" onClick={() => setCreating(true)}>
                  Define the first process
                </Button>
              }
            />
          </RuleCard>
        ) : (
          processes.map((process) => (
            <RuleCard
              key={process.id}
              icon={GitPullRequestArrow}
              title={process.name}
              hint={process.object}
              actions={
                <>
                  {process.lockRecord ? <Pill tone="warning">Locks the record</Pill> : null}
                  <Pill tone={process.isActive ? "success" : "neutral"}>
                    {process.isActive ? "Active" : "Off"}
                  </Pill>
                  <Button
                    size="sm"
                    variant="ghost"
                    className="h-7 px-2 text-[11px]"
                    onClick={() => setEditing(process)}
                  >
                    Edit
                  </Button>
                  <Button
                    size="sm"
                    variant="ghost"
                    className="h-7 px-1.5 text-muted-foreground"
                    disabled={busy === process.id}
                    onClick={() => remove(process)}
                  >
                    <Trash2 className="size-3.5" />
                  </Button>
                </>
              }
            >
              <div className="px-4 py-3">
                <p className="text-[12.5px] text-muted-foreground">
                  {process.criteriaField
                    ? `Required when ${process.criteriaField} is "${process.criteriaValue}"`
                    : `Required on every ${process.object.toLowerCase()}`}
                </p>

                {/* The chain, left to right — the shape of the thing being
                    configured, not a list of rows that happens to be ordered. */}
                <div className="mt-2.5 flex flex-wrap items-center gap-1.5">
                  {process.steps.map((step, index) => (
                    <React.Fragment key={step.id}>
                      {index > 0 ? (
                        <ChevronRight className="size-3.5 shrink-0 text-muted-foreground" />
                      ) : null}
                      <span className="inline-flex items-center gap-1.5 rounded-md border bg-muted/40 px-2 py-1 text-[12px]">
                        <span className="grid size-4 place-items-center rounded-full bg-primary/15 text-[9.5px] font-medium text-primary tabular-nums">
                          {index + 1}
                        </span>
                        {step.name}
                        <span className="text-muted-foreground">
                          ·{" "}
                          {step.approverKind === "Manager"
                            ? "the submitter's manager"
                            : step.approverKind === "Role"
                              ? (step.approverRoleName ?? step.approverRoleKey)
                              : (step.approverUserName ?? "one person")}
                        </span>
                      </span>
                    </React.Fragment>
                  ))}
                </div>
              </div>
            </RuleCard>
          ))
        )}
      </div>

      <p className="mt-2 text-[11.5px] text-muted-foreground">
        Quotation approvals already run through their own flow on the Approvals screen. Processes
        defined here describe the chain; wiring them onto other objects is still to come.
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
        <ProcessForm
          reference={reference}
          process={editing}
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

function ProcessForm({
  reference,
  process,
  onDone,
}: {
  reference: AutomationReference;
  process: ApprovalProcess | null;
  onDone: () => void;
}) {
  const [name, setName] = React.useState(process?.name ?? "");
  const [object, setObject] = React.useState(process?.object ?? "Quotation");
  const [field, setField] = React.useState(process?.criteriaField ?? "");
  const [value, setValue] = React.useState(process?.criteriaValue ?? "");
  const [lock, setLock] = React.useState(process?.lockRecord ?? true);
  const [steps, setSteps] = React.useState<SaveApprovalStep[]>(
    process?.steps.map((s) => ({
      name: s.name,
      approverKind: s.approverKind,
      approverRoleKey: s.approverRoleKey,
      approverUserId: s.approverUserId,
    })) ?? [{ name: "Manager sign-off", approverKind: "Manager" }]
  );
  const [saving, setSaving] = React.useState(false);

  function setStep(index: number, patch: Partial<SaveApprovalStep>) {
    setSteps((current) => current.map((s, i) => (i === index ? { ...s, ...patch } : s)));
  }

  async function save() {
    const named = steps.filter((s) => s.name.trim());

    if (named.length === 0) {
      toast.error("An approval needs at least one step.");
      return;
    }

    setSaving(true);
    try {
      const body: SaveApprovalProcess = {
        name,
        object,
        criteriaField: field || null,
        criteriaOperator: field ? "equals" : null,
        criteriaValue: field ? value || null : null,
        lockRecord: lock,
        isActive: process?.isActive ?? true,
        steps: named,
      };

      if (process) await automationApi.updateApprovalProcess(process.id, body);
      else await automationApi.createApprovalProcess(body);

      toast.success(process ? "Process updated" : "Process created");
      onDone();
    } catch (error) {
      toast.error("Could not save the process", {
        description: error instanceof ApiError ? error.message : "Network error.",
      });
    } finally {
      setSaving(false);
    }
  }

  return (
    <DialogContent className="sm:max-w-lg">
      <DialogHeader>
        <DialogTitle>{process ? "Edit approval process" : "New approval process"}</DialogTitle>
        <DialogDescription>
          Steps run in order. Each one has to approve before the next is asked.
        </DialogDescription>
      </DialogHeader>

      <div className="grid max-h-[60vh] gap-3 overflow-y-auto pr-1">
        <div className="grid gap-3 sm:grid-cols-2">
          <div className="grid gap-1.5">
            <Label htmlFor="ap-name">Name</Label>
            <Input
              id="ap-name"
              value={name}
              onChange={(e) => setName(e.target.value)}
              placeholder="Discount over 10%"
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
        </div>

        <div className="grid gap-2 sm:grid-cols-2">
          <div className="grid gap-1.5">
            <Label htmlFor="ap-field">Required when</Label>
            <Input
              id="ap-field"
              value={field}
              onChange={(e) => setField(e.target.value)}
              placeholder="Field — blank means always"
            />
          </div>
          <div className="grid gap-1.5">
            <Label htmlFor="ap-value">is</Label>
            <Input
              id="ap-value"
              value={value}
              onChange={(e) => setValue(e.target.value)}
              disabled={!field}
            />
          </div>
        </div>

        <fieldset className="grid gap-2 rounded-lg border p-3">
          <legend className="px-1 text-[11px] font-medium tracking-wide text-muted-foreground uppercase">
            Steps
          </legend>

          {steps.map((step, index) => (
            <div key={index} className="grid gap-2 rounded-md border p-2.5">
              <div className="flex items-center gap-2">
                <span className="grid size-5 shrink-0 place-items-center rounded-full bg-primary/15 text-[10px] font-medium text-primary tabular-nums">
                  {index + 1}
                </span>
                <Input
                  value={step.name}
                  onChange={(e) => setStep(index, { name: e.target.value })}
                  placeholder="Step name"
                  className="h-8"
                />
                {steps.length > 1 ? (
                  <Button
                    type="button"
                    size="sm"
                    variant="ghost"
                    className="h-8 px-1.5 text-muted-foreground"
                    onClick={() => setSteps((s) => s.filter((_, i) => i !== index))}
                  >
                    <Trash2 className="size-3.5" />
                  </Button>
                ) : null}
              </div>

              <div className="flex gap-2 pl-7">
                <Select
                  value={step.approverKind}
                  onValueChange={(kind) => setStep(index, { approverKind: kind })}
                >
                  <SelectTrigger size="sm" className="w-[190px] text-[12px]">
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="Manager">The submitter&apos;s manager</SelectItem>
                    <SelectItem value="Role">Anyone holding a role</SelectItem>
                    <SelectItem value="User">One named person</SelectItem>
                  </SelectContent>
                </Select>

                {step.approverKind === "Role" ? (
                  <Select
                    value={step.approverRoleKey ?? ""}
                    onValueChange={(key) => setStep(index, { approverRoleKey: key })}
                  >
                    <SelectTrigger size="sm" className="flex-1 text-[12px]">
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
                ) : step.approverKind === "User" ? (
                  <Select
                    value={step.approverUserId?.toString() ?? ""}
                    onValueChange={(id) => setStep(index, { approverUserId: Number(id) })}
                  >
                    <SelectTrigger size="sm" className="flex-1 text-[12px]">
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
          ))}

          <Button
            type="button"
            size="sm"
            variant="outline"
            className="gap-1 text-[12px]"
            onClick={() =>
              setSteps((s) => [...s, { name: "", approverKind: "Manager" }])
            }
          >
            <Plus className="size-3.5" />
            Add a step
          </Button>
        </fieldset>

        <label className="flex items-center gap-2.5 text-[12.5px]">
          <Switch checked={lock} onCheckedChange={setLock} />
          <span>
            Lock the record while a decision is outstanding
            <span className="block text-[11.5px] text-muted-foreground">
              Stops the thing being approved changing under the approver.
            </span>
          </span>
        </label>
      </div>

      <DialogFooter>
        <Button onClick={save} disabled={saving} className="gap-1.5">
          {saving ? (
            <Loader2 className="size-4 animate-spin" />
          ) : (
            <GitPullRequestArrow className="size-4" />
          )}
          {process ? "Save process" : "Create process"}
        </Button>
      </DialogFooter>
    </DialogContent>
  );
}
