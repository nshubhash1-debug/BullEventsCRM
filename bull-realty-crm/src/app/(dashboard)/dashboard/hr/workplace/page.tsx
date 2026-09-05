"use client";

import * as React from "react";
import { AlertTriangle, HeartHandshake, ShieldAlert } from "lucide-react";
import { toast } from "sonner";

import { PagePanel } from "@/components/shell/page-panel";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import {
  Sheet,
  SheetContent,
  SheetDescription,
  SheetHeader,
  SheetTitle,
} from "@/components/ui/sheet";
import { Switch } from "@/components/ui/switch";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { ApiError, apiRequest } from "@/lib/api";
import { formatMoney } from "@/lib/crm-api";
import {
  workplaceApi,
  GRIEVANCE_CATEGORIES,
  type HrChecklistRun,
  type HrChecklistTemplate,
  type HrExpenseType,
  type HrGrievance,
  type HrSkill,
  type HrSkillSearch,
} from "@/lib/hr-workplace-api";

function day(value: string | null) {
  if (!value) return "—";
  return new Date(value).toLocaleDateString("en-IN", {
    day: "numeric",
    month: "short",
    year: "numeric",
  });
}

const STATUS_TONE: Record<string, string> = {
  Raised: "bg-amber-500/10 text-amber-700 dark:text-amber-300",
  Acknowledged: "bg-blue-500/10 text-blue-700 dark:text-blue-300",
  Investigating: "bg-blue-500/10 text-blue-700 dark:text-blue-300",
  Escalated: "bg-red-500/10 text-red-700 dark:text-red-300",
  Resolved: "bg-emerald-500/10 text-emerald-700 dark:text-emerald-300",
  Withdrawn: "bg-zinc-500/10 text-zinc-700 dark:text-zinc-300",
};

/**
 * The parts of employment that are not pay or a job title.
 *
 * The skill search is the one that earns its place daily: the question before
 * every job is who can rig at height or drive the tempo, and answering it from
 * memory is how a crew arrives without an electrician.
 */
export default function HrWorkplacePage() {
  const [checklists, setChecklists] = React.useState<HrChecklistTemplate[]>([]);
  const [grievances, setGrievances] = React.useState<HrGrievance[]>([]);
  const [skills, setSkills] = React.useState<HrSkill[]>([]);
  const [expenseTypes, setExpenseTypes] = React.useState<HrExpenseType[]>([]);
  const [employees, setEmployees] = React.useState<{ id: number; name: string }[]>([]);
  const [busy, setBusy] = React.useState(false);

  const [search, setSearch] = React.useState({ skillId: "", minimum: "3", certified: false });
  const [found, setFound] = React.useState<HrSkillSearch | null>(null);

  const [applyOpen, setApplyOpen] = React.useState(false);
  const [applyForm, setApplyForm] = React.useState({ employeeId: "", kind: "Onboarding" });
  const [run, setRun] = React.useState<HrChecklistRun | null>(null);

  const [raiseOpen, setRaiseOpen] = React.useState(false);
  const [raiseForm, setRaiseForm] = React.useState({
    employeeId: "",
    category: "Safety",
    subject: "",
    details: "",
    againstEmployeeId: "",
    isConfidential: true,
  });

  const load = React.useCallback(() => {
    workplaceApi.checklists().then(setChecklists).catch(() => setChecklists([]));
    workplaceApi.grievances().then(setGrievances).catch(() => setGrievances([]));
    workplaceApi.skills().then(setSkills).catch(() => setSkills([]));
    workplaceApi.expenseTypes().then(setExpenseTypes).catch(() => setExpenseTypes([]));
  }, [setChecklists, setGrievances, setSkills, setExpenseTypes]);

  React.useEffect(() => {
    load();
    apiRequest<{ items: { id: number; name: string }[] }>("/api/hr/employees/query", {
      method: "POST",
      body: JSON.stringify({ page: 1, pageSize: 300 }),
      auth: true,
    })
      .then((page) => setEmployees(page.items))
      .catch(() => setEmployees([]));
  }, [load]);

  // A search fires per keystroke on the proficiency box; only the newest may write.
  const searchRequest = React.useRef(0);

  function runSearch(next: typeof search) {
    setSearch(next);
    const skillId = Number(next.skillId);
    const token = ++searchRequest.current;

    if (!skillId) {
      setFound(null);
      return;
    }

    workplaceApi
      .whoCan(skillId, Number(next.minimum) || 1, next.certified)
      .then((result) => {
        if (token === searchRequest.current) setFound(result);
      })
      .catch(() => {
        if (token === searchRequest.current) setFound(null);
      });
  }

  async function applyChecklist() {
    setBusy(true);
    try {
      const result = await workplaceApi.applyChecklist({
        employeeId: Number(applyForm.employeeId),
        kind: applyForm.kind,
        checklistTemplateId: null,
        anchorDate: null,
      });
      setRun(result);
      toast.success(
        result.tasksAdded > 0
          ? `Added ${result.tasksAdded} tasks.`
          : "Everything on that list is already there."
      );
    } catch (error) {
      toast.error(error instanceof ApiError ? error.message : "Could not apply that.");
    } finally {
      setBusy(false);
    }
  }

  async function raiseGrievance() {
    setBusy(true);
    try {
      await workplaceApi.raiseGrievance({
        employeeId: Number(raiseForm.employeeId),
        category: raiseForm.category,
        subject: raiseForm.subject,
        details: raiseForm.details || null,
        againstEmployeeId: raiseForm.againstEmployeeId
          ? Number(raiseForm.againstEmployeeId)
          : null,
        isConfidential: raiseForm.isConfidential,
        attachmentUrl: null,
      });
      toast.success("Raised.");
      setRaiseOpen(false);
      load();
    } catch (error) {
      toast.error(error instanceof ApiError ? error.message : "Could not raise that.");
    } finally {
      setBusy(false);
    }
  }

  async function assign(grievance: HrGrievance) {
    const name = window.prompt("Assign to whom? (type part of a name)")?.trim();
    if (!name) return;

    const match = employees.find((e) =>
      e.name.toLowerCase().includes(name.toLowerCase())
    );
    if (!match) {
      toast.error(`No employee matches "${name}".`);
      return;
    }

    const committee = grievance.needsCommittee
      ? window.confirm(
          `${grievance.category} complaints must go to the constituted committee. ` +
            `Does ${match.name} sit on it?`
        )
      : false;

    try {
      await workplaceApi.assignGrievance(grievance.id, match.id, committee);
      toast.success(`Assigned to ${match.name}.`);
      load();
    } catch (error) {
      toast.error(error instanceof ApiError ? error.message : "Could not assign that.");
    }
  }

  async function resolve(grievance: HrGrievance) {
    const resolution = window.prompt("What was actually done?")?.trim();
    if (!resolution) return;

    try {
      await workplaceApi.resolveGrievance(grievance.id, {
        resolution,
        complainantSatisfied: null,
        withdrawn: false,
      });
      toast.success("Closed.");
      load();
    } catch (error) {
      toast.error(error instanceof ApiError ? error.message : "Could not close that.");
    }
  }

  const open = grievances.filter((g) =>
    ["Raised", "Acknowledged", "Investigating", "Escalated"].includes(g.status)
  );

  return (
    <PagePanel
      icon={HeartHandshake}
      title="Workplace"
      hint="Joining and leaving lists, complaints and what came of them, who can do what, and the rules on claiming expenses back."
      actions={
        <div className="flex gap-1.5">
          <Button
            size="sm"
            variant="outline"
            className="h-8"
            onClick={() => {
              setRun(null);
              setApplyOpen(true);
            }}
          >
            Apply a checklist
          </Button>
          <Button size="sm" className="h-8" onClick={() => setRaiseOpen(true)}>
            Raise a grievance
          </Button>
        </div>
      }
    >
      <Tabs defaultValue="skills">
        <TabsList>
          <TabsTrigger value="skills">
            Who can do what
            <span className="ml-1.5 text-[11px] text-muted-foreground">{skills.length}</span>
          </TabsTrigger>
          <TabsTrigger value="grievances">
            Grievances
            {open.length > 0 ? (
              <Badge variant="secondary" className="ml-1.5 h-4 px-1 text-[10px]">
                {open.length}
              </Badge>
            ) : null}
          </TabsTrigger>
          <TabsTrigger value="checklists">Checklists</TabsTrigger>
          <TabsTrigger value="expenses">Expense rules</TabsTrigger>
        </TabsList>

        {/* ---------------- skills ---------------- */}

        <TabsContent value="skills" className="pt-3">
          <div className="mb-3 flex flex-wrap items-end gap-2 rounded-md border p-3">
            <div className="space-y-1">
              <Label className="text-[12px]">Who can</Label>
              <select
                className="h-9 w-64 rounded-md border bg-background px-2 text-[13px]"
                value={search.skillId}
                onChange={(e) => runSearch({ ...search, skillId: e.target.value })}
              >
                <option value="">Pick a skill…</option>
                {skills.map((s) => (
                  <option key={s.id} value={s.id}>
                    {s.name}
                    {s.requiresCertification ? " (certified)" : ""}
                  </option>
                ))}
              </select>
            </div>
            <div className="space-y-1">
              <Label className="text-[12px]">At least</Label>
              <select
                className="h-9 w-36 rounded-md border bg-background px-2 text-[13px]"
                value={search.minimum}
                onChange={(e) => runSearch({ ...search, minimum: e.target.value })}
              >
                <option value="1">Aware</option>
                <option value="2">Working</option>
                <option value="3">Competent</option>
                <option value="4">Advanced</option>
                <option value="5">Expert</option>
              </select>
            </div>
            <div className="flex items-center gap-2 pb-2">
              <Switch
                checked={search.certified}
                onCheckedChange={(v) => runSearch({ ...search, certified: v })}
              />
              <Label className="text-[12px] font-normal">Live certificate only</Label>
            </div>
          </div>

          {found ? (
            <>
              <div className="mb-2 flex flex-wrap items-center gap-x-4 gap-y-1 text-[12px]">
                <span className="font-medium">
                  {found.found} can do {found.skillName}
                </span>
                {found.expiredCertificates > 0 ? (
                  <span className="flex items-center gap-1 text-amber-700 dark:text-amber-400">
                    <AlertTriangle className="size-3.5" />
                    {found.expiredCertificates} with an expired certificate
                  </span>
                ) : null}
              </div>
              <div className="overflow-x-auto rounded border">
                <table className="w-full text-left text-[13px]">
                  <thead className="bg-muted/50 text-[11px] uppercase text-muted-foreground">
                    <tr>
                      <th className="p-2 font-medium">Person</th>
                      <th className="p-2 font-medium">Level</th>
                      <th className="p-2 text-right font-medium">Years</th>
                      <th className="p-2 font-medium">Certificate</th>
                      <th className="p-2 font-medium">Assessed</th>
                    </tr>
                  </thead>
                  <tbody>
                    {found.people.map((p) => (
                      <tr key={p.id} className="border-t">
                        <td className="p-2">{p.employeeName}</td>
                        <td className="p-2">
                          {p.proficiencyLabel}
                          <span className="ml-1.5 text-[11px] text-muted-foreground">
                            {p.proficiency}/5
                          </span>
                        </td>
                        <td className="p-2 text-right tabular-nums text-muted-foreground">
                          {p.yearsOfExperience || "—"}
                        </td>
                        <td className="p-2">
                          {p.certificateNumber ? (
                            <>
                              <span className="font-mono text-[11px]">
                                {p.certificateNumber}
                              </span>
                              <div
                                className={
                                  "text-[11px] " +
                                  (p.certificateExpired
                                    ? "text-red-600 dark:text-red-400"
                                    : "text-muted-foreground")
                                }
                              >
                                {p.certificateExpired ? "expired " : "until "}
                                {day(p.certifiedUntil)}
                              </div>
                            </>
                          ) : (
                            <span className="text-muted-foreground">—</span>
                          )}
                        </td>
                        <td className="p-2 text-muted-foreground">
                          {p.assessedOn ? day(p.assessedOn) : "not assessed"}
                        </td>
                      </tr>
                    ))}
                    {found.people.length === 0 ? (
                      <tr>
                        <td
                          colSpan={5}
                          className="p-6 text-center text-[12px] text-muted-foreground"
                        >
                          Nobody on the rolls matches. That is the answer worth knowing
                          before the job is quoted.
                        </td>
                      </tr>
                    ) : null}
                  </tbody>
                </table>
              </div>
            </>
          ) : (
            <div className="grid gap-3 md:grid-cols-2 xl:grid-cols-3">
              {Object.entries(
                skills.reduce<Record<string, HrSkill[]>>((groups, skill) => {
                  const key = skill.category ?? "Other";
                  (groups[key] ??= []).push(skill);
                  return groups;
                }, {})
              ).map(([category, group]) => (
                <div key={category} className="rounded border p-3">
                  <h3 className="mb-1.5 text-[12px] font-medium uppercase text-muted-foreground">
                    {category}
                  </h3>
                  <table className="w-full text-[12px]">
                    <tbody>
                      {group.map((s) => (
                        <tr key={s.id} className="border-b last:border-0">
                          <td className="py-1">
                            {s.name}
                            {s.requiresCertification ? (
                              <ShieldAlert
                                className="ml-1 inline size-3 text-muted-foreground"
                                aria-label="needs a certificate"
                              />
                            ) : null}
                          </td>
                          <td className="w-20 py-1 text-right text-muted-foreground">
                            {s.peopleWithIt > 0
                              ? `${s.peopleWithIt} · ${s.averageProficiency}`
                              : "nobody"}
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              ))}
            </div>
          )}
        </TabsContent>

        {/* ---------------- grievances ---------------- */}

        <TabsContent value="grievances" className="pt-3">
          <div className="overflow-x-auto rounded border">
            <table className="w-full text-left text-[13px]">
              <thead className="bg-muted/50 text-[11px] uppercase text-muted-foreground">
                <tr>
                  <th className="p-2 font-medium">Raised by</th>
                  <th className="p-2 font-medium">About</th>
                  <th className="p-2 font-medium">Category</th>
                  <th className="p-2 font-medium">With</th>
                  <th className="p-2 text-right font-medium">Days</th>
                  <th className="p-2 font-medium">Status</th>
                  <th className="p-2" />
                </tr>
              </thead>
              <tbody>
                {grievances.map((g) => (
                  <tr key={g.id} className="border-t">
                    <td className="p-2">{g.employeeName}</td>
                    <td className="p-2">
                      {g.subject}
                      {g.resolution ? (
                        <div className="text-[11px] text-muted-foreground">{g.resolution}</div>
                      ) : null}
                    </td>
                    <td className="p-2">
                      {g.category}
                      {g.needsCommittee ? (
                        <Badge variant="secondary" className="ml-1.5 font-normal">
                          committee
                        </Badge>
                      ) : null}
                    </td>
                    <td className="p-2 text-muted-foreground">{g.assignedToName ?? "—"}</td>
                    <td className="p-2 text-right tabular-nums">{g.ageInDays}</td>
                    <td className="p-2">
                      <span
                        className={
                          "rounded px-1.5 py-0.5 text-[10.5px] font-medium " +
                          (STATUS_TONE[g.status] ?? STATUS_TONE.Raised)
                        }
                      >
                        {g.status}
                      </span>
                    </td>
                    <td className="p-2 text-right">
                      {g.resolvedAt === null ? (
                        <span className="flex justify-end gap-1">
                          <Button
                            size="sm"
                            variant="outline"
                            className="h-6 text-[11px]"
                            onClick={() => void assign(g)}
                          >
                            Assign
                          </Button>
                          <Button
                            size="sm"
                            variant="ghost"
                            className="h-6 text-[11px]"
                            onClick={() => void resolve(g)}
                          >
                            Close
                          </Button>
                        </span>
                      ) : null}
                    </td>
                  </tr>
                ))}
                {grievances.length === 0 ? (
                  <tr>
                    <td colSpan={7} className="p-6 text-center text-[12px] text-muted-foreground">
                      Nothing raised.
                    </td>
                  </tr>
                ) : null}
              </tbody>
            </table>
          </div>
        </TabsContent>

        {/* ---------------- checklists ---------------- */}

        <TabsContent value="checklists" className="pt-3">
          <div className="grid gap-3 md:grid-cols-2 xl:grid-cols-3">
            {checklists.map((c) => (
              <div key={c.id} className="rounded border p-3">
                <div className="flex items-baseline justify-between gap-2">
                  <h3 className="text-[13px] font-medium">{c.name}</h3>
                  <Badge variant="secondary" className="font-normal">
                    {c.kind}
                  </Badge>
                </div>
                {c.notes ? (
                  <p className="mt-0.5 text-[11.5px] leading-snug text-muted-foreground">
                    {c.notes}
                  </p>
                ) : null}
                <ul className="mt-2 space-y-1">
                  {c.tasks.map((task) => (
                    <li key={task.id} className="text-[12px]">
                      <span className={task.isBlocking ? "font-medium" : ""}>{task.title}</span>
                      <span className="ml-1.5 text-[11px] text-muted-foreground">
                        {task.owner} ·{" "}
                        {task.dueOffsetDays === 0
                          ? "on the day"
                          : task.dueOffsetDays < 0
                            ? `${Math.abs(task.dueOffsetDays)}d before`
                            : `${task.dueOffsetDays}d after`}
                        {task.isBlocking ? " · blocking" : ""}
                      </span>
                    </li>
                  ))}
                </ul>
              </div>
            ))}
          </div>
        </TabsContent>

        {/* ---------------- expense rules ---------------- */}

        <TabsContent value="expenses" className="pt-3">
          <p className="mb-2 text-[12px] text-muted-foreground">
            The limits used to live in whoever was approving that day. These are checked as a
            claim is typed, so somebody finds out while they can still split the bill.
          </p>
          <div className="overflow-x-auto rounded border">
            <table className="w-full text-left text-[13px]">
              <thead className="bg-muted/50 text-[11px] uppercase text-muted-foreground">
                <tr>
                  <th className="p-2 font-medium">Type</th>
                  <th className="p-2 text-right font-medium">Per claim</th>
                  <th className="p-2 text-right font-medium">Per month</th>
                  <th className="p-2 font-medium">Bill</th>
                  <th className="p-2 font-medium">Needs a trip</th>
                </tr>
              </thead>
              <tbody>
                {expenseTypes.map((x) => (
                  <tr key={x.id} className="border-t">
                    <td className="p-2">
                      {x.name}
                      {x.description ? (
                        <div className="text-[11px] leading-snug text-muted-foreground">
                          {x.description}
                        </div>
                      ) : null}
                    </td>
                    <td className="p-2 text-right tabular-nums">
                      {x.perClaimLimit > 0 ? formatMoney(x.perClaimLimit) : "no limit"}
                    </td>
                    <td className="p-2 text-right tabular-nums">
                      {x.monthlyLimit > 0 ? formatMoney(x.monthlyLimit) : "no limit"}
                    </td>
                    <td className="p-2 text-[12px] text-muted-foreground">
                      {!x.requiresReceipt
                        ? "not needed"
                        : x.receiptWaivedBelow > 0
                          ? `above ${formatMoney(x.receiptWaivedBelow)}`
                          : "always"}
                    </td>
                    <td className="p-2 text-[12px] text-muted-foreground">
                      {x.requiresTravelRequest ? "yes" : "—"}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </TabsContent>
      </Tabs>

      {/* ---------------- apply a checklist ---------------- */}

      <Sheet open={applyOpen} onOpenChange={setApplyOpen}>
        <SheetContent className="w-full overflow-y-auto sm:max-w-md">
          <SheetHeader>
            <SheetTitle>Apply a checklist</SheetTitle>
            <SheetDescription>
              The list that fits the person most closely is picked — a crew joiner needs boots
              and a safety briefing, a designer needs neither.
            </SheetDescription>
          </SheetHeader>
          <div className="space-y-3 px-4 pb-8">
            <div className="space-y-1">
              <Label className="text-[12px]">Employee</Label>
              <select
                className="h-9 w-full rounded-md border bg-background px-2 text-[13px]"
                value={applyForm.employeeId}
                onChange={(e) => setApplyForm({ ...applyForm, employeeId: e.target.value })}
              >
                <option value="">Pick someone…</option>
                {employees.map((e) => (
                  <option key={e.id} value={e.id}>
                    {e.name}
                  </option>
                ))}
              </select>
            </div>
            <div className="space-y-1">
              <Label className="text-[12px]">Which list</Label>
              <select
                className="h-9 w-full rounded-md border bg-background px-2 text-[13px]"
                value={applyForm.kind}
                onChange={(e) => setApplyForm({ ...applyForm, kind: e.target.value })}
              >
                <option value="Onboarding">Joining</option>
                <option value="Separation">Leaving</option>
              </select>
            </div>
            <Button className="w-full" disabled={busy} onClick={() => void applyChecklist()}>
              {busy ? "Applying…" : "Apply"}
            </Button>

            {run ? (
              <div className="space-y-2 pt-2">
                <div className="rounded-md bg-muted/50 px-3 py-2 text-[12px]">
                  <span className="font-medium">{run.employeeName}</span> ·{" "}
                  {run.done} of {run.total} done
                  {run.overdue > 0 ? ` · ${run.overdue} overdue` : ""}
                </div>
                {run.blockedBecause ? (
                  <div className="flex items-start gap-2 rounded-md border border-amber-500/40 bg-amber-500/5 px-3 py-2 text-[12px]">
                    <AlertTriangle className="mt-0.5 size-3.5 shrink-0 text-amber-600" />
                    {run.blockedBecause}
                  </div>
                ) : null}
                <ul className="space-y-1">
                  {run.items.map((i) => (
                    <li
                      key={i.id}
                      className="flex items-baseline justify-between gap-2 border-b py-1.5 text-[12px] last:border-0"
                    >
                      <span className={i.done ? "text-muted-foreground line-through" : ""}>
                        {i.title}
                        <span className="ml-1.5 text-[11px] text-muted-foreground">
                          {i.owner}
                          {i.isBlocking ? " · blocking" : ""}
                        </span>
                      </span>
                      <span
                        className={
                          "shrink-0 text-[11px] " +
                          (i.isOverdue
                            ? "text-red-600 dark:text-red-400"
                            : "text-muted-foreground")
                        }
                      >
                        {day(i.dueOn)}
                      </span>
                    </li>
                  ))}
                </ul>
              </div>
            ) : null}
          </div>
        </SheetContent>
      </Sheet>

      {/* ---------------- raise a grievance ---------------- */}

      <Sheet open={raiseOpen} onOpenChange={setRaiseOpen}>
        <SheetContent className="w-full overflow-y-auto sm:max-w-md">
          <SheetHeader>
            <SheetTitle>Raise a grievance</SheetTitle>
            <SheetDescription>
              Confidential by default. A complaint anybody in HR can browse is one nobody
              raises twice.
            </SheetDescription>
          </SheetHeader>
          <div className="space-y-3 px-4 pb-8">
            <div className="space-y-1">
              <Label className="text-[12px]">Raised by</Label>
              <select
                className="h-9 w-full rounded-md border bg-background px-2 text-[13px]"
                value={raiseForm.employeeId}
                onChange={(e) => setRaiseForm({ ...raiseForm, employeeId: e.target.value })}
              >
                <option value="">Pick someone…</option>
                {employees.map((e) => (
                  <option key={e.id} value={e.id}>
                    {e.name}
                  </option>
                ))}
              </select>
            </div>
            <div className="space-y-1">
              <Label className="text-[12px]">Category</Label>
              <select
                className="h-9 w-full rounded-md border bg-background px-2 text-[13px]"
                value={raiseForm.category}
                onChange={(e) => setRaiseForm({ ...raiseForm, category: e.target.value })}
              >
                {GRIEVANCE_CATEGORIES.map((c) => (
                  <option key={c} value={c}>
                    {c}
                  </option>
                ))}
              </select>
              {raiseForm.category === "Harassment" ||
              raiseForm.category === "Discrimination" ? (
                <p className="flex items-start gap-1.5 text-[11.5px] text-amber-700 dark:text-amber-400">
                  <AlertTriangle className="mt-0.5 size-3 shrink-0" />
                  This has to go to the constituted committee, not a line manager.
                </p>
              ) : null}
            </div>
            <div className="space-y-1">
              <Label className="text-[12px]">Subject</Label>
              <Input
                className="h-9"
                value={raiseForm.subject}
                onChange={(e) => setRaiseForm({ ...raiseForm, subject: e.target.value })}
              />
            </div>
            <div className="space-y-1">
              <Label className="text-[12px]">Details</Label>
              <Input
                className="h-9"
                value={raiseForm.details}
                onChange={(e) => setRaiseForm({ ...raiseForm, details: e.target.value })}
              />
            </div>
            <div className="space-y-1">
              <Label className="text-[12px]">About whom (optional)</Label>
              <select
                className="h-9 w-full rounded-md border bg-background px-2 text-[13px]"
                value={raiseForm.againstEmployeeId}
                onChange={(e) =>
                  setRaiseForm({ ...raiseForm, againstEmployeeId: e.target.value })
                }
              >
                <option value="">—</option>
                {employees
                  .filter((e) => String(e.id) !== raiseForm.employeeId)
                  .map((e) => (
                    <option key={e.id} value={e.id}>
                      {e.name}
                    </option>
                  ))}
              </select>
            </div>
            <div className="flex items-center justify-between gap-3 border-b py-2">
              <Label className="text-[13px] font-normal">Confidential</Label>
              <Switch
                checked={raiseForm.isConfidential}
                onCheckedChange={(v) => setRaiseForm({ ...raiseForm, isConfidential: v })}
              />
            </div>
            <Button className="w-full" disabled={busy} onClick={() => void raiseGrievance()}>
              {busy ? "Raising…" : "Raise"}
            </Button>
          </div>
        </SheetContent>
      </Sheet>
    </PagePanel>
  );
}
