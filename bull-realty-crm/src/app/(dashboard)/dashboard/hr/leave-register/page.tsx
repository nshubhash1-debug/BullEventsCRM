"use client";

import * as React from "react";
import { CalendarClock, RefreshCw } from "lucide-react";
import { toast } from "sonner";

import { PagePanel } from "@/components/shell/page-panel";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import {
  Sheet,
  SheetContent,
  SheetDescription,
  SheetHeader,
  SheetTitle,
} from "@/components/ui/sheet";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { ApiError } from "@/lib/api";
import { formatMoney } from "@/lib/crm-api";
import {
  leaveApi,
  type HrCompensatoryRequest,
  type HrEncashment,
  type HrLeaveAllocation,
  type HrLeaveBlock,
  type HrLeavePeriod,
  type HrLeavePolicy,
  type HrLeaveRegister,
} from "@/lib/hr-leave-api";

function day(value: string) {
  return new Date(value).toLocaleDateString("en-IN", {
    day: "numeric",
    month: "short",
    year: "numeric",
  });
}

/** Where a day came from, in a word. */
const SOURCE_TONE: Record<string, string> = {
  Policy: "bg-blue-500/10 text-blue-700 dark:text-blue-300",
  CarryForward: "bg-violet-500/10 text-violet-700 dark:text-violet-300",
  Compensatory: "bg-emerald-500/10 text-emerald-700 dark:text-emerald-300",
  Encashment: "bg-amber-500/10 text-amber-700 dark:text-amber-300",
  Manual: "bg-zinc-500/10 text-zinc-700 dark:text-zinc-300",
  Lapsed: "bg-red-500/10 text-red-700 dark:text-red-300",
};

function Num({ value, muted = false }: { value: number; muted?: boolean }) {
  return (
    <span className={"tabular-nums" + (muted && value === 0 ? " text-muted-foreground/40" : "")}>
      {value === 0 ? "—" : value.toLocaleString("en-IN", { maximumFractionDigits: 2 })}
    </span>
  );
}

/**
 * The leave register: what everybody has, and every movement behind it.
 *
 * The columns are the leave types, the rows are people, and a click opens the
 * ledger for one of them — because the question that actually gets asked is
 * never "what is the balance" but "why is the balance that".
 */
export default function HrLeaveRegisterPage() {
  const [periods, setPeriods] = React.useState<HrLeavePeriod[]>([]);
  const [periodId, setPeriodId] = React.useState<number | null>(null);
  const [register, setRegister] = React.useState<HrLeaveRegister | null>(null);
  const [policies, setPolicies] = React.useState<HrLeavePolicy[]>([]);
  const [comp, setComp] = React.useState<HrCompensatoryRequest[]>([]);
  const [encashments, setEncashments] = React.useState<HrEncashment[]>([]);
  const [blocks, setBlocks] = React.useState<HrLeaveBlock[]>([]);
  const [busy, setBusy] = React.useState(false);

  const [ledgerFor, setLedgerFor] = React.useState<{ id: number; name: string } | null>(null);
  const [ledger, setLedger] = React.useState<HrLeaveAllocation[]>([]);

  const [search, setSearch] = React.useState("");

  const loadRegister = React.useCallback((id?: number) => {
    leaveApi
      .register(id)
      .then((r) => {
        setRegister(r);
        setPeriodId(r.periodId);
      })
      .catch(() => setRegister(null));
  }, [setRegister, setPeriodId]);

  const loadAll = React.useCallback(() => {
    leaveApi.policies().then(setPolicies).catch(() => setPolicies([]));
    leaveApi.compensatory().then(setComp).catch(() => setComp([]));
    leaveApi.encashments().then(setEncashments).catch(() => setEncashments([]));
    leaveApi.blocks().then(setBlocks).catch(() => setBlocks([]));
  }, [setPolicies, setComp, setEncashments, setBlocks]);

  React.useEffect(() => {
    leaveApi
      .periods()
      .then((rows) => {
        setPeriods(rows);
        const current = rows.find((p) => p.isCurrent) ?? rows[0];
        loadRegister(current?.id);
      })
      .catch(() => setPeriods([]));
    loadAll();
  }, [loadRegister, loadAll]);

  function openLedger(employeeId: number, name: string) {
    setLedgerFor({ id: employeeId, name });
    setLedger([]);
    leaveApi
      .allocations(employeeId, periodId ?? undefined)
      .then(setLedger)
      .catch(() => setLedger([]));
  }

  async function decideComp(id: number, approve: boolean) {
    try {
      await leaveApi.decideCompensatory(id, approve);
      toast.success(approve ? "Compensatory day granted." : "Claim rejected.");
      loadAll();
      loadRegister(periodId ?? undefined);
    } catch (error) {
      toast.error(error instanceof ApiError ? error.message : "Could not decide that.");
    }
  }

  async function decideEncashment(id: number, approve: boolean) {
    try {
      await leaveApi.decideEncashment(id, approve);
      toast.success(approve ? "Encashment approved." : "Encashment rejected.");
      loadAll();
      loadRegister(periodId ?? undefined);
    } catch (error) {
      toast.error(error instanceof ApiError ? error.message : "Could not decide that.");
    }
  }

  async function rebuild() {
    setBusy(true);
    try {
      const result = await leaveApi.rebuild();
      toast.success(
        result.balancesCorrected === 0 && result.balancesOrphaned === 0
          ? `All ${result.balancesExamined} balances agree with their allocations.`
          : `Corrected ${result.balancesCorrected}, removed ${result.balancesOrphaned} with nothing behind them.`
      );
      loadRegister(periodId ?? undefined);
    } catch (error) {
      toast.error(error instanceof ApiError ? error.message : "Could not rebuild.");
    } finally {
      setBusy(false);
    }
  }

  const rows = React.useMemo(() => {
    if (!register) return [];
    const needle = search.trim().toLowerCase();
    if (!needle) return register.rows;
    return register.rows.filter((r) => r.employeeName.toLowerCase().includes(needle));
  }, [register, search]);

  const pendingComp = comp.filter((c) => c.status === "PendingManager");
  const pendingEncash = encashments.filter((e) => e.status === "PendingHr");

  return (
    <PagePanel
      icon={CalendarClock}
      title="Leave register"
      hint="Every balance, and the allocation behind it. Compensatory days, encashments and the blocked season live here too."
      actions={
        <div className="flex items-center gap-2">
          {periods.length > 0 ? (
            <select
              className="h-8 rounded-md border bg-background px-2 text-[13px]"
              value={periodId ?? ""}
              onChange={(e) => loadRegister(Number(e.target.value))}
            >
              {periods.map((p) => (
                <option key={p.id} value={p.id}>
                  {p.name}
                  {p.isCurrent ? " · current" : ""}
                </option>
              ))}
            </select>
          ) : null}
          <Button
            size="sm"
            variant="outline"
            className="h-8"
            disabled={busy}
            onClick={() => void rebuild()}
          >
            <RefreshCw className="mr-1.5 size-3.5" />
            Reconcile
          </Button>
        </div>
      }
    >
      <Tabs defaultValue="balances">
        <TabsList>
          <TabsTrigger value="balances">Balances</TabsTrigger>
          <TabsTrigger value="compensatory">
            Compensatory
            {pendingComp.length > 0 ? (
              <Badge variant="secondary" className="ml-1.5 h-4 px-1 text-[10px]">
                {pendingComp.length}
              </Badge>
            ) : null}
          </TabsTrigger>
          <TabsTrigger value="encashment">
            Encashment
            {pendingEncash.length > 0 ? (
              <Badge variant="secondary" className="ml-1.5 h-4 px-1 text-[10px]">
                {pendingEncash.length}
              </Badge>
            ) : null}
          </TabsTrigger>
          <TabsTrigger value="policies">Policies</TabsTrigger>
          <TabsTrigger value="blocked">Blocked dates</TabsTrigger>
        </TabsList>

        {/* ---------------- balances ---------------- */}

        <TabsContent value="balances" className="pt-3">
          <div className="mb-2 flex items-center gap-2">
            <Input
              className="h-8 max-w-xs"
              placeholder="Find someone…"
              value={search}
              onChange={(e) => setSearch(e.target.value)}
            />
            <span className="text-[12px] text-muted-foreground">
              {rows.length} of {register?.rows.length ?? 0}
            </span>
          </div>

          <div className="overflow-x-auto rounded border">
            <table className="w-full text-left text-[13px]">
              <thead className="bg-muted/50 text-[11px] uppercase text-muted-foreground">
                <tr>
                  <th className="p-2 font-medium">Employee</th>
                  {register?.leaveTypes.map((t) => (
                    <th key={t.id} className="p-2 text-right font-medium" title={t.name}>
                      {t.code}
                    </th>
                  ))}
                  <th className="p-2 text-right font-medium">Total</th>
                </tr>
              </thead>
              <tbody>
                {rows.map((r) => (
                  <tr
                    key={r.employeeId}
                    className="cursor-pointer border-t hover:bg-muted/40"
                    onClick={() => openLedger(r.employeeId, r.employeeName)}
                  >
                    <td className="p-2">{r.employeeName}</td>
                    {register?.leaveTypes.map((t) => {
                      const cell = r.balances.find((b) => b.leaveTypeId === t.id);
                      return (
                        <td key={t.id} className="p-2 text-right">
                          <Num value={cell?.closing ?? 0} muted />
                        </td>
                      );
                    })}
                    <td className="p-2 text-right font-medium">
                      <Num value={r.totalClosing} />
                    </td>
                  </tr>
                ))}
                {rows.length === 0 ? (
                  <tr>
                    <td
                      colSpan={(register?.leaveTypes.length ?? 0) + 2}
                      className="p-6 text-center text-[12px] text-muted-foreground"
                    >
                      Nothing allocated for this period yet.
                    </td>
                  </tr>
                ) : null}
              </tbody>
            </table>
          </div>
        </TabsContent>

        {/* ---------------- compensatory ---------------- */}

        <TabsContent value="compensatory" className="pt-3">
          <p className="mb-2 text-[12px] text-muted-foreground">
            A day worked when it should not have been, claimed back as a day off. The date is
            checked against the holiday list and the weekly off, so a normal working day cannot
            be claimed.
          </p>
          <div className="overflow-x-auto rounded border">
            <table className="w-full text-left text-[13px]">
              <thead className="bg-muted/50 text-[11px] uppercase text-muted-foreground">
                <tr>
                  <th className="p-2 font-medium">Employee</th>
                  <th className="p-2 font-medium">Worked on</th>
                  <th className="p-2 text-right font-medium">Days</th>
                  <th className="p-2 font-medium">Reason</th>
                  <th className="p-2 font-medium">Status</th>
                  <th className="p-2" />
                </tr>
              </thead>
              <tbody>
                {comp.map((c) => (
                  <tr key={c.id} className="border-t">
                    <td className="p-2">{c.employeeName}</td>
                    <td className="p-2">{day(c.workedOn)}</td>
                    <td className="p-2 text-right tabular-nums">{c.days}</td>
                    <td className="p-2 text-muted-foreground">{c.reason ?? "—"}</td>
                    <td className="p-2">
                      <Badge variant="secondary" className="font-normal">
                        {c.status}
                      </Badge>
                    </td>
                    <td className="p-2 text-right">
                      {c.status === "PendingManager" ? (
                        <span className="flex justify-end gap-1">
                          <Button
                            size="sm"
                            variant="outline"
                            className="h-6 text-[11px]"
                            onClick={() => void decideComp(c.id, true)}
                          >
                            Approve
                          </Button>
                          <Button
                            size="sm"
                            variant="ghost"
                            className="h-6 text-[11px]"
                            onClick={() => void decideComp(c.id, false)}
                          >
                            Reject
                          </Button>
                        </span>
                      ) : null}
                    </td>
                  </tr>
                ))}
                {comp.length === 0 ? (
                  <tr>
                    <td colSpan={6} className="p-6 text-center text-[12px] text-muted-foreground">
                      Nobody has claimed a compensatory day yet.
                    </td>
                  </tr>
                ) : null}
              </tbody>
            </table>
          </div>
        </TabsContent>

        {/* ---------------- encashment ---------------- */}

        <TabsContent value="encashment" className="pt-3">
          <p className="mb-2 text-[12px] text-muted-foreground">
            Unused days paid out instead of carried. The rate is basic ÷ 30 at the time of the
            payout and is frozen on the record, so a payout reprinted after a raise still shows
            what was actually paid.
          </p>
          <div className="overflow-x-auto rounded border">
            <table className="w-full text-left text-[13px]">
              <thead className="bg-muted/50 text-[11px] uppercase text-muted-foreground">
                <tr>
                  <th className="p-2 font-medium">Employee</th>
                  <th className="p-2 font-medium">Type</th>
                  <th className="p-2 text-right font-medium">Days</th>
                  <th className="p-2 text-right font-medium">Per day</th>
                  <th className="p-2 text-right font-medium">Amount</th>
                  <th className="p-2 font-medium">Status</th>
                  <th className="p-2" />
                </tr>
              </thead>
              <tbody>
                {encashments.map((e) => (
                  <tr key={e.id} className="border-t">
                    <td className="p-2">{e.employeeName}</td>
                    <td className="p-2 text-muted-foreground">{e.leaveTypeName}</td>
                    <td className="p-2 text-right tabular-nums">{e.days}</td>
                    <td className="p-2 text-right tabular-nums">{formatMoney(e.perDayAmount)}</td>
                    <td className="p-2 text-right font-medium tabular-nums">
                      {formatMoney(e.amount)}
                    </td>
                    <td className="p-2">
                      <Badge variant="secondary" className="font-normal">
                        {e.status}
                      </Badge>
                    </td>
                    <td className="p-2 text-right">
                      {e.status === "PendingHr" ? (
                        <span className="flex justify-end gap-1">
                          <Button
                            size="sm"
                            variant="outline"
                            className="h-6 text-[11px]"
                            onClick={() => void decideEncashment(e.id, true)}
                          >
                            Approve
                          </Button>
                          <Button
                            size="sm"
                            variant="ghost"
                            className="h-6 text-[11px]"
                            onClick={() => void decideEncashment(e.id, false)}
                          >
                            Reject
                          </Button>
                        </span>
                      ) : null}
                    </td>
                  </tr>
                ))}
                {encashments.length === 0 ? (
                  <tr>
                    <td colSpan={7} className="p-6 text-center text-[12px] text-muted-foreground">
                      No encashments requested.
                    </td>
                  </tr>
                ) : null}
              </tbody>
            </table>
          </div>
        </TabsContent>

        {/* ---------------- policies ---------------- */}

        <TabsContent value="policies" className="pt-3">
          <div className="grid gap-3 md:grid-cols-2 xl:grid-cols-3">
            {policies.map((p) => (
              <div key={p.id} className="rounded border p-3">
                <div className="flex items-baseline justify-between gap-2">
                  <h3 className="text-[13px] font-medium">{p.name}</h3>
                  <span className="text-[11px] text-muted-foreground">
                    {p.employeesAssigned} assigned
                  </span>
                </div>
                {p.notes ? (
                  <p className="mt-0.5 text-[11.5px] leading-snug text-muted-foreground">
                    {p.notes}
                  </p>
                ) : null}
                <table className="mt-2 w-full text-[12px]">
                  <tbody>
                    {p.lines.map((l) => (
                      <tr key={l.id} className="border-b last:border-0">
                        <td className="py-1 text-muted-foreground">{l.leaveTypeName}</td>
                        <td className="py-1 text-right tabular-nums">{l.annualAllocation} days</td>
                      </tr>
                    ))}
                    <tr className="border-t">
                      <td className="pt-1.5 font-medium">Total a year</td>
                      <td className="pt-1.5 text-right font-medium tabular-nums">
                        {p.totalDays} days
                      </td>
                    </tr>
                  </tbody>
                </table>
              </div>
            ))}
          </div>
        </TabsContent>

        {/* ---------------- blocked ---------------- */}

        <TabsContent value="blocked" className="pt-3">
          <p className="mb-2 text-[12px] text-muted-foreground">
            An events company turns up on the day, and the day is known months ahead. A hard
            block cannot be approved through by anybody; a soft one warns the approver.
          </p>
          <div className="space-y-2">
            {blocks.map((b) => (
              <div
                key={b.id}
                className={
                  "flex flex-wrap items-baseline justify-between gap-2 rounded border p-3 " +
                  (b.allowOverride ? "" : "border-amber-500/40 bg-amber-500/5")
                }
              >
                <div>
                  <div className="text-[13px] font-medium">
                    {day(b.fromDate)} — {day(b.toDate)}
                    <span className="ml-2 text-[11px] font-normal text-muted-foreground">
                      {b.dayCount} days
                    </span>
                  </div>
                  <div className="text-[12px] text-muted-foreground">{b.reason}</div>
                </div>
                <div className="flex items-center gap-2">
                  <Badge variant="secondary" className="font-normal">
                    {b.departmentName ?? "Whole company"}
                  </Badge>
                  <Badge
                    variant={b.allowOverride ? "outline" : "default"}
                    className="font-normal"
                  >
                    {b.allowOverride ? "Override allowed" : "Hard block"}
                  </Badge>
                </div>
              </div>
            ))}
            {blocks.length === 0 ? (
              <p className="rounded border border-dashed p-6 text-center text-[12px] text-muted-foreground">
                No dates blocked.
              </p>
            ) : null}
          </div>
        </TabsContent>
      </Tabs>

      {/* ---------------- one person's ledger ---------------- */}

      <Sheet
        open={ledgerFor !== null}
        onOpenChange={(open) => {
          if (!open) setLedgerFor(null);
        }}
      >
        <SheetContent className="w-full overflow-y-auto sm:max-w-md">
          <SheetHeader>
            <SheetTitle>{ledgerFor?.name}</SheetTitle>
            <SheetDescription>
              Every movement behind the balance, newest first.
            </SheetDescription>
          </SheetHeader>
          <div className="space-y-1 px-4 pb-8">
            {ledger.map((a) => (
              <div
                key={a.id}
                className="flex items-baseline justify-between gap-3 border-b py-2 text-[13px] last:border-0"
              >
                <div className="min-w-0">
                  <div className="flex items-center gap-1.5">
                    <span
                      className={
                        "rounded px-1.5 py-0.5 text-[10px] font-medium " +
                        (SOURCE_TONE[a.source] ?? SOURCE_TONE.Manual)
                      }
                    >
                      {a.source}
                    </span>
                    <span className="truncate">{a.leaveTypeName}</span>
                  </div>
                  {a.notes ? (
                    <div className="mt-0.5 text-[11px] text-muted-foreground">{a.notes}</div>
                  ) : null}
                </div>
                <span
                  className={
                    "shrink-0 tabular-nums font-medium " +
                    (a.days < 0 ? "text-red-600 dark:text-red-400" : "")
                  }
                >
                  {a.days > 0 ? "+" : ""}
                  {a.days}
                </span>
              </div>
            ))}
            {ledger.length === 0 ? (
              <p className="py-8 text-center text-[12px] text-muted-foreground">
                No allocations in this period.
              </p>
            ) : null}
          </div>
        </SheetContent>
      </Sheet>
    </PagePanel>
  );
}
