"use client";

import * as React from "react";
import { HandCoins } from "lucide-react";
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
  type HrAdditionalSalary,
  type HrAdvance,
  type HrSalaryComponent,
} from "@/lib/hr-pay-api";

const MONTHS = [
  "January", "February", "March", "April", "May", "June",
  "July", "August", "September", "October", "November", "December",
];

/** Where an advance has got to, at a glance. */
const STATUS_TONE: Record<string, string> = {
  Requested: "bg-zinc-500/10 text-zinc-700 dark:text-zinc-300",
  Approved: "bg-blue-500/10 text-blue-700 dark:text-blue-300",
  Paid: "bg-violet-500/10 text-violet-700 dark:text-violet-300",
  Recovering: "bg-amber-500/10 text-amber-700 dark:text-amber-300",
  Closed: "bg-emerald-500/10 text-emerald-700 dark:text-emerald-300",
  Rejected: "bg-red-500/10 text-red-700 dark:text-red-300",
  WrittenOff: "bg-red-500/10 text-red-700 dark:text-red-300",
};

/** What can follow what — an advance only moves forward. */
const NEXT_STATUS: Record<string, string[]> = {
  Requested: ["Approved", "Rejected"],
  Approved: ["Paid", "Rejected"],
  Paid: ["WrittenOff"],
  Recovering: ["WrittenOff"],
};

/**
 * Money advanced against future salary, and one-off pay.
 *
 * The festival advance is a fixture of an Indian payroll and the reason a
 * deduction turns up on a payslip that no salary structure explains. Recovery
 * happens on the payroll run rather than here — the run is the only thing that
 * knows whether a month has actually been paid.
 */
export default function HrAdvancesPage() {
  const now = new Date();
  const [advances, setAdvances] = React.useState<HrAdvance[]>([]);
  const [additional, setAdditional] = React.useState<HrAdditionalSalary[]>([]);
  const [components, setComponents] = React.useState<HrSalaryComponent[]>([]);
  const [employees, setEmployees] = React.useState<{ id: number; name: string }[]>([]);
  const [busy, setBusy] = React.useState(false);

  const [advanceOpen, setAdvanceOpen] = React.useState(false);
  const [advanceForm, setAdvanceForm] = React.useState({
    employeeId: "",
    amount: "",
    instalments: "3",
    recoveryStartYear: String(now.getFullYear()),
    recoveryStartMonth: String(now.getMonth() + 1),
    purpose: "",
  });

  const [payOpen, setPayOpen] = React.useState(false);
  const [payForm, setPayForm] = React.useState({
    employeeId: "",
    salaryComponentId: "",
    amount: "",
    year: String(now.getFullYear()),
    month: String(now.getMonth() + 1),
    isRecurring: false,
    recurringUntil: "",
    dependsOnPaymentDays: false,
    reason: "",
  });

  const load = React.useCallback(() => {
    payApi.advances().then(setAdvances).catch(() => setAdvances([]));
    payApi.additional().then(setAdditional).catch(() => setAdditional([]));
  }, [setAdvances, setAdditional]);

  React.useEffect(() => {
    load();
    payApi.components().then(setComponents).catch(() => setComponents([]));
    apiRequest<{ items: { id: number; name: string }[] }>("/api/hr/employees/query", {
      method: "POST",
      body: JSON.stringify({ page: 1, pageSize: 300 }),
      auth: true,
    })
      .then((page) => setEmployees(page.items))
      .catch(() => setEmployees([]));
  }, [load]);

  async function requestAdvance() {
    setBusy(true);
    try {
      await payApi.requestAdvance({
        employeeId: Number(advanceForm.employeeId),
        amount: Number.parseFloat(advanceForm.amount),
        instalments: Number(advanceForm.instalments),
        recoveryStartYear: Number(advanceForm.recoveryStartYear),
        recoveryStartMonth: Number(advanceForm.recoveryStartMonth),
        purpose: advanceForm.purpose || null,
      });
      toast.success("Advance raised. Approve and mark it paid to start recovery.");
      setAdvanceOpen(false);
      load();
    } catch (error) {
      toast.error(error instanceof ApiError ? error.message : "Could not raise that.");
    } finally {
      setBusy(false);
    }
  }

  async function move(advance: HrAdvance, status: string) {
    const reason =
      status === "WrittenOff"
        ? window.prompt("Why is this being written off?")?.trim()
        : undefined;
    if (status === "WrittenOff" && !reason) return;

    try {
      await payApi.setAdvanceStatus(advance.id, status, reason);
      toast.success(`Marked ${status.toLowerCase()}.`);
      load();
    } catch (error) {
      toast.error(error instanceof ApiError ? error.message : "Could not do that.");
    }
  }

  async function addPay() {
    setBusy(true);
    try {
      await payApi.addAdditional({
        employeeId: Number(payForm.employeeId),
        salaryComponentId: Number(payForm.salaryComponentId),
        amount: Number.parseFloat(payForm.amount),
        year: Number(payForm.year),
        month: Number(payForm.month),
        isRecurring: payForm.isRecurring,
        recurringUntil: payForm.recurringUntil || null,
        dependsOnPaymentDays: payForm.dependsOnPaymentDays,
        reason: payForm.reason || null,
      });
      toast.success("It will land on that month's payslip.");
      setPayOpen(false);
      load();
    } catch (error) {
      toast.error(error instanceof ApiError ? error.message : "Could not add that.");
    } finally {
      setBusy(false);
    }
  }

  const outstanding = advances.reduce((total, a) => total + a.outstanding, 0);
  const usable = components.filter((c) => !c.isStatutory && c.isActive);

  return (
    <PagePanel
      icon={HandCoins}
      title="Advances and one-off pay"
      hint="Money advanced against future salary, recovered in instalments on the payroll run. Plus bonuses, arrears and recoveries that belong to a single month."
      actions={
        <div className="flex gap-1.5">
          <Button
            size="sm"
            variant="outline"
            className="h-8"
            onClick={() => setPayOpen(true)}
          >
            One-off pay
          </Button>
          <Button size="sm" className="h-8" onClick={() => setAdvanceOpen(true)}>
            New advance
          </Button>
        </div>
      }
    >
      <Tabs defaultValue="advances">
        <TabsList>
          <TabsTrigger value="advances">
            Advances
            <span className="ml-1.5 text-[11px] text-muted-foreground">{advances.length}</span>
          </TabsTrigger>
          <TabsTrigger value="additional">
            One-off pay
            <span className="ml-1.5 text-[11px] text-muted-foreground">{additional.length}</span>
          </TabsTrigger>
        </TabsList>

        <TabsContent value="advances" className="pt-3">
          {outstanding > 0 ? (
            <div className="mb-2 rounded-md bg-muted/50 px-3 py-2 text-[12px]">
              <span className="text-muted-foreground">Still to recover </span>
              <span className="font-medium tabular-nums">{formatMoney(outstanding)}</span>
              <span className="ml-3 text-muted-foreground">
                across {advances.filter((a) => a.outstanding > 0).length} advances
              </span>
            </div>
          ) : null}

          <div className="overflow-x-auto rounded border">
            <table className="w-full text-left text-[13px]">
              <thead className="bg-muted/50 text-[11px] uppercase text-muted-foreground">
                <tr>
                  <th className="p-2 font-medium">Employee</th>
                  <th className="p-2 font-medium">Purpose</th>
                  <th className="p-2 text-right font-medium">Amount</th>
                  <th className="p-2 text-right font-medium">Instalment</th>
                  <th className="p-2 text-right font-medium">Recovered</th>
                  <th className="p-2 text-right font-medium">Outstanding</th>
                  <th className="p-2 font-medium">Status</th>
                  <th className="p-2" />
                </tr>
              </thead>
              <tbody>
                {advances.map((a) => (
                  <tr key={a.id} className="border-t">
                    <td className="p-2">{a.employeeName}</td>
                    <td className="p-2 text-muted-foreground">{a.purpose ?? "—"}</td>
                    <td className="p-2 text-right tabular-nums">{formatMoney(a.amount)}</td>
                    <td className="p-2 text-right tabular-nums text-muted-foreground">
                      {formatMoney(a.instalmentAmount)} × {a.instalments}
                    </td>
                    <td className="p-2 text-right tabular-nums">
                      {formatMoney(a.recovered)}
                      <span className="ml-1 text-[11px] text-muted-foreground">
                        ({a.instalmentsTaken})
                      </span>
                    </td>
                    <td className="p-2 text-right font-medium tabular-nums">
                      {formatMoney(a.outstanding)}
                    </td>
                    <td className="p-2">
                      <span
                        className={
                          "rounded px-1.5 py-0.5 text-[10.5px] font-medium " +
                          (STATUS_TONE[a.status] ?? STATUS_TONE.Requested)
                        }
                      >
                        {a.status}
                      </span>
                      {a.writeOffReason ? (
                        <div className="mt-0.5 text-[11px] text-muted-foreground">
                          {a.writeOffReason}
                        </div>
                      ) : null}
                    </td>
                    <td className="p-2 text-right">
                      <span className="flex justify-end gap-1">
                        {(NEXT_STATUS[a.status] ?? []).map((next) => (
                          <Button
                            key={next}
                            size="sm"
                            variant={next === "Approved" || next === "Paid" ? "outline" : "ghost"}
                            className="h-6 text-[11px]"
                            onClick={() => void move(a, next)}
                          >
                            {next === "WrittenOff" ? "Write off" : next}
                          </Button>
                        ))}
                      </span>
                    </td>
                  </tr>
                ))}
                {advances.length === 0 ? (
                  <tr>
                    <td colSpan={8} className="p-6 text-center text-[12px] text-muted-foreground">
                      No advances.
                    </td>
                  </tr>
                ) : null}
              </tbody>
            </table>
          </div>

          <p className="mt-2 text-[11.5px] leading-snug text-muted-foreground">
            Recovery is taken on the payroll run, and recorded against that run — so
            reprocessing a month never takes a second instalment. Advances are interest-free;
            an interest-bearing employee loan is a perquisite with its own tax treatment and is
            not what this is.
          </p>
        </TabsContent>

        <TabsContent value="additional" className="pt-3">
          <div className="overflow-x-auto rounded border">
            <table className="w-full text-left text-[13px]">
              <thead className="bg-muted/50 text-[11px] uppercase text-muted-foreground">
                <tr>
                  <th className="p-2 font-medium">Employee</th>
                  <th className="p-2 font-medium">Component</th>
                  <th className="p-2 text-right font-medium">Amount</th>
                  <th className="p-2 font-medium">Month</th>
                  <th className="p-2 font-medium">Reason</th>
                  <th className="p-2 font-medium">Status</th>
                  <th className="p-2" />
                </tr>
              </thead>
              <tbody>
                {additional.map((a) => (
                  <tr key={a.id} className="border-t">
                    <td className="p-2">{a.employeeName}</td>
                    <td className="p-2">
                      {a.componentName}
                      {a.componentType === "Deduction" ? (
                        <Badge variant="secondary" className="ml-1.5 font-normal">
                          deduction
                        </Badge>
                      ) : null}
                    </td>
                    <td className="p-2 text-right tabular-nums">
                      {a.componentType === "Deduction" ? "−" : ""}
                      {formatMoney(a.amount)}
                    </td>
                    <td className="p-2 text-muted-foreground">
                      {MONTHS[Math.min(Math.max(a.month, 1), 12) - 1]} {a.year}
                      {a.isRecurring ? (
                        <Badge variant="secondary" className="ml-1.5 font-normal">
                          recurring
                        </Badge>
                      ) : null}
                    </td>
                    <td className="p-2 text-muted-foreground">{a.reason ?? "—"}</td>
                    <td className="p-2">
                      <Badge variant="secondary" className="font-normal">
                        {a.status}
                      </Badge>
                    </td>
                    <td className="p-2 text-right">
                      {a.status === "Approved" && a.paidInPayrollRunId === null ? (
                        <Button
                          size="sm"
                          variant="ghost"
                          className="h-6 text-[11px]"
                          onClick={() =>
                            void payApi
                              .cancelAdditional(a.id)
                              .then(() => {
                                toast.success("Cancelled.");
                                load();
                              })
                              .catch((error) =>
                                toast.error(
                                  error instanceof ApiError
                                    ? error.message
                                    : "Could not cancel that."
                                )
                              )
                          }
                        >
                          Cancel
                        </Button>
                      ) : null}
                    </td>
                  </tr>
                ))}
                {additional.length === 0 ? (
                  <tr>
                    <td colSpan={7} className="p-6 text-center text-[12px] text-muted-foreground">
                      Nothing added for any month.
                    </td>
                  </tr>
                ) : null}
              </tbody>
            </table>
          </div>
        </TabsContent>
      </Tabs>

      {/* ---------------- new advance ---------------- */}

      <Sheet open={advanceOpen} onOpenChange={setAdvanceOpen}>
        <SheetContent className="w-full sm:max-w-md">
          <SheetHeader>
            <SheetTitle>Advance against salary</SheetTitle>
            <SheetDescription>
              The instalment is worked out from the amount and the number of months, and is
              checked against the employee&rsquo;s gross.
            </SheetDescription>
          </SheetHeader>
          <div className="space-y-3 px-4 pb-8">
            <div className="space-y-1">
              <Label className="text-[12px]">Employee</Label>
              <select
                className="h-9 w-full rounded-md border bg-background px-2 text-[13px]"
                value={advanceForm.employeeId}
                onChange={(e) =>
                  setAdvanceForm({ ...advanceForm, employeeId: e.target.value })
                }
              >
                <option value="">Pick someone…</option>
                {employees.map((e) => (
                  <option key={e.id} value={e.id}>
                    {e.name}
                  </option>
                ))}
              </select>
            </div>
            <div className="grid grid-cols-2 gap-2">
              <div className="space-y-1">
                <Label className="text-[12px]">Amount</Label>
                <Input
                  className="h-9 tabular-nums"
                  inputMode="decimal"
                  value={advanceForm.amount}
                  onChange={(e) => setAdvanceForm({ ...advanceForm, amount: e.target.value })}
                />
              </div>
              <div className="space-y-1">
                <Label className="text-[12px]">Instalments</Label>
                <Input
                  className="h-9 tabular-nums"
                  inputMode="numeric"
                  value={advanceForm.instalments}
                  onChange={(e) =>
                    setAdvanceForm({ ...advanceForm, instalments: e.target.value })
                  }
                />
                {Number(advanceForm.instalments) > 0 && advanceForm.amount ? (
                  <p className="text-[11px] text-muted-foreground">
                    {formatMoney(
                      Number.parseFloat(advanceForm.amount) / Number(advanceForm.instalments)
                    )}{" "}
                    a month
                  </p>
                ) : null}
              </div>
            </div>
            <div className="grid grid-cols-2 gap-2">
              <div className="space-y-1">
                <Label className="text-[12px]">Recovery starts</Label>
                <select
                  className="h-9 w-full rounded-md border bg-background px-2 text-[13px]"
                  value={advanceForm.recoveryStartMonth}
                  onChange={(e) =>
                    setAdvanceForm({ ...advanceForm, recoveryStartMonth: e.target.value })
                  }
                >
                  {MONTHS.map((m, i) => (
                    <option key={m} value={i + 1}>
                      {m}
                    </option>
                  ))}
                </select>
              </div>
              <div className="space-y-1">
                <Label className="text-[12px]">Year</Label>
                <Input
                  className="h-9 tabular-nums"
                  inputMode="numeric"
                  value={advanceForm.recoveryStartYear}
                  onChange={(e) =>
                    setAdvanceForm({ ...advanceForm, recoveryStartYear: e.target.value })
                  }
                />
              </div>
            </div>
            <div className="space-y-1">
              <Label className="text-[12px]">Purpose</Label>
              <Input
                className="h-9"
                placeholder="Festival advance"
                value={advanceForm.purpose}
                onChange={(e) => setAdvanceForm({ ...advanceForm, purpose: e.target.value })}
              />
            </div>
            <Button className="w-full" disabled={busy} onClick={() => void requestAdvance()}>
              {busy ? "Raising…" : "Raise advance"}
            </Button>
          </div>
        </SheetContent>
      </Sheet>

      {/* ---------------- one-off pay ---------------- */}

      <Sheet open={payOpen} onOpenChange={setPayOpen}>
        <SheetContent className="w-full overflow-y-auto sm:max-w-md">
          <SheetHeader>
            <SheetTitle>Pay or deduct, once</SheetTitle>
            <SheetDescription>
              A bonus, arrears after a backdated raise, a fine, a recovery. It lands on one
              month&rsquo;s payslip and is gone the next.
            </SheetDescription>
          </SheetHeader>
          <div className="space-y-3 px-4 pb-8">
            <div className="space-y-1">
              <Label className="text-[12px]">Employee</Label>
              <select
                className="h-9 w-full rounded-md border bg-background px-2 text-[13px]"
                value={payForm.employeeId}
                onChange={(e) => setPayForm({ ...payForm, employeeId: e.target.value })}
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
              <Label className="text-[12px]">Component</Label>
              <select
                className="h-9 w-full rounded-md border bg-background px-2 text-[13px]"
                value={payForm.salaryComponentId}
                onChange={(e) => setPayForm({ ...payForm, salaryComponentId: e.target.value })}
              >
                <option value="">Pick a component…</option>
                <optgroup label="Earnings">
                  {usable
                    .filter((c) => c.componentType === "Earning")
                    .map((c) => (
                      <option key={c.id} value={c.id}>
                        {c.name}
                      </option>
                    ))}
                </optgroup>
                <optgroup label="Deductions">
                  {usable
                    .filter((c) => c.componentType === "Deduction")
                    .map((c) => (
                      <option key={c.id} value={c.id}>
                        {c.name}
                      </option>
                    ))}
                </optgroup>
              </select>
              <p className="text-[11px] text-muted-foreground">
                Provident fund, ESI, professional tax and income tax are not listed — the
                payroll engine computes those.
              </p>
            </div>
            <div className="grid grid-cols-3 gap-2">
              <div className="space-y-1">
                <Label className="text-[12px]">Amount</Label>
                <Input
                  className="h-9 tabular-nums"
                  inputMode="decimal"
                  value={payForm.amount}
                  onChange={(e) => setPayForm({ ...payForm, amount: e.target.value })}
                />
              </div>
              <div className="space-y-1">
                <Label className="text-[12px]">Month</Label>
                <select
                  className="h-9 w-full rounded-md border bg-background px-2 text-[13px]"
                  value={payForm.month}
                  onChange={(e) => setPayForm({ ...payForm, month: e.target.value })}
                >
                  {MONTHS.map((m, i) => (
                    <option key={m} value={i + 1}>
                      {m.slice(0, 3)}
                    </option>
                  ))}
                </select>
              </div>
              <div className="space-y-1">
                <Label className="text-[12px]">Year</Label>
                <Input
                  className="h-9 tabular-nums"
                  inputMode="numeric"
                  value={payForm.year}
                  onChange={(e) => setPayForm({ ...payForm, year: e.target.value })}
                />
              </div>
            </div>

            <div className="grid grid-cols-[1fr_auto] items-center gap-3 border-b py-2">
              <div>
                <Label className="text-[13px] font-normal">Repeat every month</Label>
                <p className="text-[11px] leading-snug text-muted-foreground">
                  For a temporary allowance — a site posting for a few months.
                </p>
              </div>
              <Switch
                checked={payForm.isRecurring}
                onCheckedChange={(v) => setPayForm({ ...payForm, isRecurring: v })}
              />
            </div>
            {payForm.isRecurring ? (
              <div className="space-y-1">
                <Label className="text-[12px]">Until</Label>
                <Input
                  type="date"
                  className="h-9"
                  value={payForm.recurringUntil}
                  onChange={(e) => setPayForm({ ...payForm, recurringUntil: e.target.value })}
                />
                <p className="text-[11px] text-muted-foreground">
                  Required — without an end date this is a raise, not a one-off.
                </p>
              </div>
            ) : null}

            <div className="grid grid-cols-[1fr_auto] items-center gap-3 border-b py-2">
              <div>
                <Label className="text-[13px] font-normal">Reduced by loss of pay</Label>
                <p className="text-[11px] leading-snug text-muted-foreground">
                  Usually not, for a one-off.
                </p>
              </div>
              <Switch
                checked={payForm.dependsOnPaymentDays}
                onCheckedChange={(v) => setPayForm({ ...payForm, dependsOnPaymentDays: v })}
              />
            </div>

            <div className="space-y-1">
              <Label className="text-[12px]">Reason</Label>
              <Input
                className="h-9"
                placeholder="Diwali bonus"
                value={payForm.reason}
                onChange={(e) => setPayForm({ ...payForm, reason: e.target.value })}
              />
            </div>

            <Button className="w-full" disabled={busy} onClick={() => void addPay()}>
              {busy ? "Adding…" : "Add to that month"}
            </Button>
          </div>
        </SheetContent>
      </Sheet>
    </PagePanel>
  );
}
