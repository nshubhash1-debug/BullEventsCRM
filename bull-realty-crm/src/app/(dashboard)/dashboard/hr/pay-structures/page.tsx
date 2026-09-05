"use client";

import * as React from "react";
import { Calculator, Check, Lock, X } from "lucide-react";
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
  payApi,
  type HrPayAssignment,
  type HrPayStructure,
  type HrSalaryComponent,
  type HrStructurePreview,
} from "@/lib/hr-pay-api";

const BLANK_COMPONENT = {
  id: null as number | null,
  name: "",
  abbreviation: "",
  componentType: "Earning",
  calculation: "Fixed",
  formula: "",
  affectsPf: false,
  affectsEsi: true,
  isTaxable: true,
  isHra: false,
  dependsOnPaymentDays: true,
  sortOrder: 100,
  isActive: true,
  notes: "",
};

/** A flag, shown as a word rather than a tick nobody can read. */
function Flag({ on, label }: { on: boolean; label: string }) {
  if (!on) return null;
  return (
    <Badge variant="secondary" className="font-normal">
      {label}
    </Badge>
  );
}

function Row({ label, hint, children }: { label: string; hint?: string; children: React.ReactNode }) {
  return (
    <div className="grid grid-cols-[1fr_auto] items-center gap-3 border-b py-2 last:border-0">
      <div>
        <Label className="text-[13px] font-normal">{label}</Label>
        {hint ? <p className="text-[11px] leading-snug text-muted-foreground">{hint}</p> : null}
      </div>
      {children}
    </div>
  );
}

/**
 * What a salary is made of.
 *
 * The preview is the point of the page: a structure is a set of formulas, and
 * the only way to know whether they say what somebody meant is to see the
 * payslip they produce. So the base figure sits at the top and every change
 * re-runs against it.
 */
export default function HrPayStructuresPage() {
  const [components, setComponents] = React.useState<HrSalaryComponent[]>([]);
  const [structures, setStructures] = React.useState<HrPayStructure[]>([]);
  const [assignments, setAssignments] = React.useState<HrPayAssignment[]>([]);
  const [employees, setEmployees] = React.useState<{ id: number; name: string }[]>([]);

  const [editing, setEditing] = React.useState<typeof BLANK_COMPONENT | null>(null);
  const [formulaCheck, setFormulaCheck] = React.useState<{ result: number; error: string | null } | null>(null);

  const [previewFor, setPreviewFor] = React.useState<HrPayStructure | null>(null);
  const [previewBase, setPreviewBase] = React.useState("100000");
  const [preview, setPreview] = React.useState<HrStructurePreview | null>(null);

  const [assignOpen, setAssignOpen] = React.useState(false);
  const [assignForm, setAssignForm] = React.useState({
    employeeId: "",
    payStructureId: "",
    base: "",
    effectiveFrom: new Date().toISOString().slice(0, 10),
  });
  const [busy, setBusy] = React.useState(false);

  const load = React.useCallback(() => {
    payApi.components().then(setComponents).catch(() => setComponents([]));
    payApi.structures().then(setStructures).catch(() => setStructures([]));
    payApi.assignments().then(setAssignments).catch(() => setAssignments([]));
  }, [setComponents, setStructures, setAssignments]);

  React.useEffect(() => {
    load();
    // Employees come from the paged query — the only list endpoint this app has.
    apiRequest<{ items: { id: number; name: string }[] }>("/api/hr/employees/query", {
      method: "POST",
      body: JSON.stringify({ page: 1, pageSize: 300 }),
      auth: true,
    })
      .then((page) => setEmployees(page.items))
      .catch(() => setEmployees([]));
  }, [load]);

  // Typing a base fires a request per keystroke, and they do not come back in
  // the order they went out — the answer for "6000" landing after the one for
  // "60000" would leave the wrong breakdown on screen with the right number in
  // the box. Only the newest request is allowed to write.
  const previewRequest = React.useRef(0);

  function runPreview(structure: HrPayStructure, base: string) {
    const amount = Number.parseFloat(base);
    const token = ++previewRequest.current;

    if (!Number.isFinite(amount) || amount <= 0) {
      setPreview(null);
      return;
    }

    payApi
      .preview(structure.id, amount)
      .then((result) => {
        if (token === previewRequest.current) setPreview(result);
      })
      .catch(() => {
        if (token === previewRequest.current) setPreview(null);
      });
  }

  function openPreview(structure: HrPayStructure) {
    setPreviewFor(structure);
    setPreview(null);
    runPreview(structure, previewBase);
  }

  // Same race as the preview: a formula is checked as it is typed.
  const formulaRequest = React.useRef(0);

  async function checkFormula(formula: string) {
    if (!formula.trim()) {
      setFormulaCheck(null);
      return;
    }
    const token = ++formulaRequest.current;
    try {
      const result = await payApi.tryFormula(formula, 100000);
      if (token === formulaRequest.current) {
        setFormulaCheck({ result: result.result, error: result.error });
      }
    } catch {
      if (token === formulaRequest.current) setFormulaCheck(null);
    }
  }

  async function saveComponent() {
    if (!editing) return;
    setBusy(true);
    try {
      await payApi.saveComponent({ ...editing, formula: editing.formula || null });
      toast.success("Component saved.");
      setEditing(null);
      setFormulaCheck(null);
      load();
    } catch (error) {
      toast.error(error instanceof ApiError ? error.message : "Could not save that.");
    } finally {
      setBusy(false);
    }
  }

  async function assign() {
    setBusy(true);
    try {
      await payApi.assign({
        employeeId: Number(assignForm.employeeId),
        payStructureId: Number(assignForm.payStructureId),
        base: Number.parseFloat(assignForm.base),
        effectiveFrom: assignForm.effectiveFrom,
      });
      toast.success("Assigned. It applies to payroll from that date.");
      setAssignOpen(false);
      load();
    } catch (error) {
      toast.error(error instanceof ApiError ? error.message : "Could not assign that.");
    } finally {
      setBusy(false);
    }
  }

  const earnings = components.filter((c) => c.componentType === "Earning");
  const deductions = components.filter((c) => c.componentType === "Deduction");

  return (
    <PagePanel
      icon={Calculator}
      title="Pay structures"
      hint="Components, the structures built from them, and who is on which. The preview shows the payslip a structure produces before anybody is put on it."
      actions={
        <div className="flex gap-1.5">
          <Button
            size="sm"
            variant="outline"
            className="h-8"
            onClick={() => {
              setEditing({ ...BLANK_COMPONENT });
              setFormulaCheck(null);
            }}
          >
            New component
          </Button>
          <Button
            size="sm"
            className="h-8"
            onClick={() => {
              setAssignForm({
                employeeId: "",
                payStructureId: "",
                base: "",
                effectiveFrom: new Date().toISOString().slice(0, 10),
              });
              setAssignOpen(true);
            }}
          >
            Assign
          </Button>
        </div>
      }
    >
      <Tabs defaultValue="structures">
        <TabsList>
          <TabsTrigger value="structures">Structures</TabsTrigger>
          <TabsTrigger value="components">
            Components
            <span className="ml-1.5 text-[11px] text-muted-foreground">{components.length}</span>
          </TabsTrigger>
          <TabsTrigger value="assignments">
            Assignments
            <span className="ml-1.5 text-[11px] text-muted-foreground">{assignments.length}</span>
          </TabsTrigger>
        </TabsList>

        {/* ---------------- structures ---------------- */}

        <TabsContent value="structures" className="pt-3">
          <div className="grid gap-3 md:grid-cols-2 xl:grid-cols-3">
            {structures.map((s) => (
              <button
                key={s.id}
                type="button"
                className="rounded border p-3 text-left transition-colors hover:bg-muted/40"
                onClick={() => openPreview(s)}
              >
                <div className="flex items-baseline justify-between gap-2">
                  <h3 className="text-[13px] font-medium">{s.name}</h3>
                  <span className="text-[11px] text-muted-foreground">
                    {s.employeesAssigned} on it
                  </span>
                </div>
                {s.notes ? (
                  <p className="mt-0.5 text-[11.5px] leading-snug text-muted-foreground">
                    {s.notes}
                  </p>
                ) : null}
                <table className="mt-2 w-full text-[12px]">
                  <tbody>
                    {s.lines.map((l) => (
                      <tr key={l.id} className="border-b last:border-0">
                        <td className="py-1">
                          <span className="text-muted-foreground">{l.componentName}</span>
                        </td>
                        <td className="py-1 text-right font-mono text-[11px] text-muted-foreground">
                          {l.calculation === "Fixed"
                            ? formatMoney(l.amount)
                            : l.calculation === "PercentOfBase"
                              ? `${l.formula}% of base`
                              : l.formula}
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
                {s.overtimeRate > 0 ? (
                  <p className="mt-1.5 text-[11px] text-muted-foreground">
                    Overtime at {formatMoney(s.overtimeRate)} an hour
                  </p>
                ) : null}
              </button>
            ))}
            {structures.length === 0 ? (
              <p className="rounded border border-dashed p-6 text-center text-[12px] text-muted-foreground">
                No structures yet.
              </p>
            ) : null}
          </div>
        </TabsContent>

        {/* ---------------- components ---------------- */}

        <TabsContent value="components" className="pt-3">
          {[
            { title: "Earnings", rows: earnings },
            { title: "Deductions", rows: deductions },
          ].map((group) => (
            <div key={group.title} className="mb-4">
              <h2 className="mb-1 text-[12px] font-medium uppercase text-muted-foreground">
                {group.title}
              </h2>
              <div className="overflow-x-auto rounded border">
                <table className="w-full text-left text-[13px]">
                  <thead className="bg-muted/50 text-[11px] uppercase text-muted-foreground">
                    <tr>
                      <th className="p-2 font-medium">Name</th>
                      <th className="p-2 font-medium">Abbr</th>
                      <th className="p-2 font-medium">How it is worked out</th>
                      <th className="p-2 font-medium">Treatment</th>
                      <th className="p-2 text-right font-medium">Used in</th>
                    </tr>
                  </thead>
                  <tbody>
                    {group.rows.map((c) => (
                      <tr
                        key={c.id}
                        className="cursor-pointer border-t hover:bg-muted/40"
                        onClick={() => {
                          setEditing({
                            id: c.id,
                            name: c.name,
                            abbreviation: c.abbreviation,
                            componentType: c.componentType,
                            calculation: c.calculation,
                            formula: c.formula ?? "",
                            affectsPf: c.affectsPf,
                            affectsEsi: c.affectsEsi,
                            isTaxable: c.isTaxable,
                            isHra: c.isHra,
                            dependsOnPaymentDays: c.dependsOnPaymentDays,
                            sortOrder: c.sortOrder,
                            isActive: c.isActive,
                            notes: c.notes ?? "",
                          });
                          setFormulaCheck(null);
                        }}
                      >
                        <td className="p-2">
                          {c.name}
                          {c.isStatutory ? (
                            <Lock className="ml-1.5 inline size-3 text-muted-foreground" />
                          ) : null}
                        </td>
                        <td className="p-2 font-mono text-[11px] text-muted-foreground">
                          {c.abbreviation}
                        </td>
                        <td className="p-2 font-mono text-[11px] text-muted-foreground">
                          {c.isStatutory
                            ? "the payroll engine"
                            : c.calculation === "Fixed"
                              ? "a fixed amount"
                              : c.calculation === "PercentOfBase"
                                ? `${c.formula ?? "?"}% of base`
                                : (c.formula ?? "—")}
                        </td>
                        <td className="p-2">
                          <span className="flex flex-wrap gap-1">
                            <Flag on={c.affectsPf} label="PF wage" />
                            <Flag on={c.isHra} label="HRA" />
                            <Flag on={!c.isTaxable} label="not taxable" />
                            <Flag on={!c.dependsOnPaymentDays} label="not pro-rated" />
                          </span>
                        </td>
                        <td className="p-2 text-right tabular-nums text-muted-foreground">
                          {c.usedInStructures || "—"}
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            </div>
          ))}
        </TabsContent>

        {/* ---------------- assignments ---------------- */}

        <TabsContent value="assignments" className="pt-3">
          <p className="mb-2 text-[12px] text-muted-foreground">
            Effective-dated, so a payslip reprinted for an earlier month still computes on the
            structure and base that applied then. Anybody without an assignment stays on the
            older Basic/HRA/Allowances record.
          </p>
          <div className="overflow-x-auto rounded border">
            <table className="w-full text-left text-[13px]">
              <thead className="bg-muted/50 text-[11px] uppercase text-muted-foreground">
                <tr>
                  <th className="p-2 font-medium">Employee</th>
                  <th className="p-2 font-medium">Structure</th>
                  <th className="p-2 text-right font-medium">Base</th>
                  <th className="p-2 font-medium">From</th>
                </tr>
              </thead>
              <tbody>
                {assignments.map((a) => (
                  <tr key={a.id} className="border-t">
                    <td className="p-2">{a.employeeName}</td>
                    <td className="p-2 text-muted-foreground">{a.structureName}</td>
                    <td className="p-2 text-right tabular-nums">{formatMoney(a.base)}</td>
                    <td className="p-2 text-muted-foreground">
                      {new Date(a.effectiveFrom).toLocaleDateString("en-IN", {
                        day: "numeric",
                        month: "short",
                        year: "numeric",
                      })}
                    </td>
                  </tr>
                ))}
                {assignments.length === 0 ? (
                  <tr>
                    <td colSpan={4} className="p-6 text-center text-[12px] text-muted-foreground">
                      Nobody is on a component structure yet.
                    </td>
                  </tr>
                ) : null}
              </tbody>
            </table>
          </div>
        </TabsContent>
      </Tabs>

      {/* ---------------- preview ---------------- */}

      <Sheet
        open={previewFor !== null}
        onOpenChange={(open) => {
          if (!open) {
            setPreviewFor(null);
            setPreview(null);
          }
        }}
      >
        <SheetContent className="w-full overflow-y-auto sm:max-w-md">
          <SheetHeader>
            <SheetTitle>{previewFor?.name}</SheetTitle>
            <SheetDescription>
              The payslip this structure produces, on a base you choose.
            </SheetDescription>
          </SheetHeader>
          <div className="space-y-3 px-4 pb-8">
            <div className="space-y-1">
              <Label className="text-[12px]">Base figure</Label>
              <Input
                className="h-9 tabular-nums"
                inputMode="decimal"
                value={previewBase}
                onChange={(e) => {
                  setPreviewBase(e.target.value);
                  if (previewFor) runPreview(previewFor, e.target.value);
                }}
              />
            </div>

            {preview ? (
              <table className="w-full text-[13px]">
                <tbody>
                  {preview.lines.map((l) => (
                    <tr key={l.abbreviation} className="border-b last:border-0">
                      <td className="py-1.5">
                        {l.name}
                        {l.formula ? (
                          <div className="font-mono text-[10.5px] text-muted-foreground">
                            {l.formula}
                          </div>
                        ) : null}
                        {l.error ? (
                          <div className="text-[11px] text-red-600 dark:text-red-400">
                            {l.error}
                          </div>
                        ) : null}
                      </td>
                      <td className="py-1.5 text-right tabular-nums">
                        {l.componentType === "Deduction" ? "−" : ""}
                        {formatMoney(l.amount)}
                      </td>
                    </tr>
                  ))}
                  <tr className="border-t">
                    <td className="pt-2 font-medium">Gross</td>
                    <td className="pt-2 text-right font-medium tabular-nums">
                      {formatMoney(preview.gross)}
                    </td>
                  </tr>
                  {preview.deductions > 0 ? (
                    <tr>
                      <td className="py-1 text-muted-foreground">Deductions on the structure</td>
                      <td className="py-1 text-right tabular-nums">
                        −{formatMoney(preview.deductions)}
                      </td>
                    </tr>
                  ) : null}
                </tbody>
              </table>
            ) : (
              <p className="py-6 text-center text-[12px] text-muted-foreground">
                Enter a base figure to see the breakdown.
              </p>
            )}

            <p className="text-[11px] leading-snug text-muted-foreground">
              Provident fund, ESI, professional tax and income tax are not shown here — they are
              computed by the payroll engine when the month is run, from the wage this structure
              produces.
            </p>
          </div>
        </SheetContent>
      </Sheet>

      {/* ---------------- component editor ---------------- */}

      <Sheet
        open={editing !== null}
        onOpenChange={(open) => {
          if (!open) {
            setEditing(null);
            setFormulaCheck(null);
          }
        }}
      >
        <SheetContent className="w-full overflow-y-auto sm:max-w-md">
          <SheetHeader>
            <SheetTitle>{editing?.id ? "Edit component" : "New component"}</SheetTitle>
            <SheetDescription>
              The flags decide what the statute makes of it, which matters more than the
              arithmetic.
            </SheetDescription>
          </SheetHeader>
          {editing ? (
            <div className="space-y-3 px-4 pb-8">
              <div className="space-y-1">
                <Label className="text-[12px]">Name</Label>
                <Input
                  className="h-9"
                  value={editing.name}
                  onChange={(e) => setEditing({ ...editing, name: e.target.value })}
                />
              </div>
              <div className="grid grid-cols-2 gap-2">
                <div className="space-y-1">
                  <Label className="text-[12px]">Abbreviation</Label>
                  <Input
                    className="h-9 font-mono"
                    placeholder="SITE"
                    value={editing.abbreviation}
                    onChange={(e) =>
                      setEditing({ ...editing, abbreviation: e.target.value.toUpperCase() })
                    }
                  />
                  <p className="text-[11px] text-muted-foreground">Formulas use this.</p>
                </div>
                <div className="space-y-1">
                  <Label className="text-[12px]">Type</Label>
                  <select
                    className="h-9 w-full rounded-md border bg-background px-2 text-[13px]"
                    value={editing.componentType}
                    onChange={(e) => setEditing({ ...editing, componentType: e.target.value })}
                  >
                    <option value="Earning">Earning</option>
                    <option value="Deduction">Deduction</option>
                  </select>
                </div>
              </div>

              <div className="space-y-1">
                <Label className="text-[12px]">How it is worked out</Label>
                <select
                  className="h-9 w-full rounded-md border bg-background px-2 text-[13px]"
                  value={editing.calculation}
                  onChange={(e) => setEditing({ ...editing, calculation: e.target.value })}
                >
                  <option value="Fixed">A fixed amount</option>
                  <option value="PercentOfBase">A percentage of the base</option>
                  <option value="Formula">A formula</option>
                </select>
              </div>

              {editing.calculation !== "Fixed" ? (
                <div className="space-y-1">
                  <Label className="text-[12px]">
                    {editing.calculation === "PercentOfBase" ? "Percentage" : "Formula"}
                  </Label>
                  <Input
                    className="h-9 font-mono text-[12px]"
                    placeholder={
                      editing.calculation === "PercentOfBase" ? "40" : "BASIC * 0.4"
                    }
                    value={editing.formula}
                    onChange={(e) => {
                      setEditing({ ...editing, formula: e.target.value });
                      if (editing.calculation === "Formula") void checkFormula(e.target.value);
                    }}
                  />
                  {editing.calculation === "Formula" ? (
                    <p className="text-[11px] leading-snug text-muted-foreground">
                      Other components by abbreviation, <code>BASE</code> for the assignment&rsquo;s
                      base, and <code>min</code>, <code>max</code>, <code>round</code>.
                    </p>
                  ) : null}
                  {formulaCheck ? (
                    formulaCheck.error ? (
                      <p className="flex items-start gap-1 text-[11.5px] text-red-600 dark:text-red-400">
                        <X className="mt-0.5 size-3 shrink-0" />
                        {formulaCheck.error}
                      </p>
                    ) : (
                      <p className="flex items-center gap-1 text-[11.5px] text-emerald-700 dark:text-emerald-400">
                        <Check className="size-3 shrink-0" />
                        On a base of 1,00,000 with basic 50,000 this comes to{" "}
                        {formatMoney(formulaCheck.result)}.
                      </p>
                    )
                  ) : null}
                </div>
              ) : null}

              <div className="pt-1">
                <h3 className="mb-1 text-[11px] font-semibold uppercase tracking-wide text-muted-foreground">
                  What the statute makes of it
                </h3>
                <Row
                  label="Counts towards the PF wage"
                  hint="Basic and dearness allowance do. This decides the largest recurring liability an employer has."
                >
                  <Switch
                    checked={editing.affectsPf}
                    onCheckedChange={(v) => setEditing({ ...editing, affectsPf: v })}
                  />
                </Row>
                <Row label="Counts towards the ESI wage">
                  <Switch
                    checked={editing.affectsEsi}
                    onCheckedChange={(v) => setEditing({ ...editing, affectsEsi: v })}
                  />
                </Row>
                <Row label="Taxable salary" hint="Reimbursements usually are not.">
                  <Switch
                    checked={editing.isTaxable}
                    onCheckedChange={(v) => setEditing({ ...editing, isTaxable: v })}
                  />
                </Row>
                <Row
                  label="This is the house rent allowance"
                  hint="For the section 10(13A) exemption."
                >
                  <Switch
                    checked={editing.isHra}
                    onCheckedChange={(v) => setEditing({ ...editing, isHra: v })}
                  />
                </Row>
                <Row
                  label="Reduced by loss of pay"
                  hint="Salary is. A fixed reimbursement or a one-off bonus is not."
                >
                  <Switch
                    checked={editing.dependsOnPaymentDays}
                    onCheckedChange={(v) =>
                      setEditing({ ...editing, dependsOnPaymentDays: v })
                    }
                  />
                </Row>
                <Row label="Active">
                  <Switch
                    checked={editing.isActive}
                    onCheckedChange={(v) => setEditing({ ...editing, isActive: v })}
                  />
                </Row>
              </div>

              <Button className="w-full" disabled={busy} onClick={() => void saveComponent()}>
                {busy ? "Saving…" : "Save component"}
              </Button>
            </div>
          ) : null}
        </SheetContent>
      </Sheet>

      {/* ---------------- assign ---------------- */}

      <Sheet open={assignOpen} onOpenChange={setAssignOpen}>
        <SheetContent className="w-full sm:max-w-md">
          <SheetHeader>
            <SheetTitle>Put somebody on a structure</SheetTitle>
            <SheetDescription>
              It applies to payroll from the effective date. Months already run keep what they
              were computed on.
            </SheetDescription>
          </SheetHeader>
          <div className="space-y-3 px-4 pb-8">
            <div className="space-y-1">
              <Label className="text-[12px]">Employee</Label>
              <select
                className="h-9 w-full rounded-md border bg-background px-2 text-[13px]"
                value={assignForm.employeeId}
                onChange={(e) => setAssignForm({ ...assignForm, employeeId: e.target.value })}
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
              <Label className="text-[12px]">Structure</Label>
              <select
                className="h-9 w-full rounded-md border bg-background px-2 text-[13px]"
                value={assignForm.payStructureId}
                onChange={(e) =>
                  setAssignForm({ ...assignForm, payStructureId: e.target.value })
                }
              >
                <option value="">Pick a structure…</option>
                {structures.map((s) => (
                  <option key={s.id} value={s.id}>
                    {s.name}
                  </option>
                ))}
              </select>
            </div>
            <div className="grid grid-cols-2 gap-2">
              <div className="space-y-1">
                <Label className="text-[12px]">Base</Label>
                <Input
                  className="h-9 tabular-nums"
                  inputMode="decimal"
                  value={assignForm.base}
                  onChange={(e) => setAssignForm({ ...assignForm, base: e.target.value })}
                />
              </div>
              <div className="space-y-1">
                <Label className="text-[12px]">Effective from</Label>
                <Input
                  type="date"
                  className="h-9"
                  value={assignForm.effectiveFrom}
                  onChange={(e) =>
                    setAssignForm({ ...assignForm, effectiveFrom: e.target.value })
                  }
                />
              </div>
            </div>
            <Button className="w-full" disabled={busy} onClick={() => void assign()}>
              {busy ? "Assigning…" : "Assign"}
            </Button>
          </div>
        </SheetContent>
      </Sheet>
    </PagePanel>
  );
}
