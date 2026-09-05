"use client";

import * as React from "react";
import { AlertTriangle, Download, Receipt } from "lucide-react";
import { toast } from "sonner";

import { PagePanel } from "@/components/shell/page-panel";
import { PayslipSheet } from "@/components/hr/payslip-sheet";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { ApiError } from "@/lib/api";
import { formatMoney, humanise } from "@/lib/crm-api";
import { downloadFile } from "@/lib/download";
import {
  hrApi,
  type HrPayslip,
  type HrRegisterFile,
  type HrStatutorySummary,
} from "@/lib/hrms-api";

const MONTHS = [
  "January", "February", "March", "April", "May", "June",
  "July", "August", "September", "October", "November", "December",
];

interface Run {
  id: number;
  year: number;
  month: number;
  status: string;
  slipCount: number;
  netTotal: number;
}

function periodOf(run: Run | null) {
  return run ? `${MONTHS[Math.min(Math.max(run.month, 1), 12) - 1]} ${run.year}` : "";
}

/**
 * One authority's remittance for the month.
 *
 * The detail line is the part a clerk actually needs — a PF total means nothing
 * without knowing how it split, because the challan asks for the split.
 */
function Remittance({
  title,
  amount,
  members,
  detail,
}: {
  title: string;
  amount: number;
  members?: number;
  detail?: string;
}) {
  return (
    <div className="rounded-md border p-3">
      <div className="flex items-baseline justify-between gap-2">
        <span className="text-[11px] font-semibold uppercase tracking-wide text-muted-foreground">
          {title}
        </span>
        {members !== undefined ? (
          <span className="text-[11px] text-muted-foreground">{members}</span>
        ) : null}
      </div>
      <div className="mt-0.5 text-[17px] font-semibold tabular-nums">{formatMoney(amount)}</div>
      {detail ? (
        <div className="mt-0.5 text-[11px] leading-snug text-muted-foreground">{detail}</div>
      ) : null}
    </div>
  );
}

const REGISTERS: { file: HrRegisterFile; label: string; hint: string }[] = [
  { file: "pf-ecr.txt", label: "PF ECR", hint: "EPFO upload" },
  { file: "esi.csv", label: "ESI return", hint: "ESIC upload" },
  { file: "pt.csv", label: "Professional tax", hint: "state-wise" },
  { file: "tds.csv", label: "TDS register", hint: "behind Form 24Q" },
  { file: "register.csv", label: "Salary register", hint: "every column" },
];

export default function HrPayrollPage() {
  const now = new Date();
  const [runs, setRuns] = React.useState<Run[]>([]);
  const [selected, setSelected] = React.useState<Run | null>(null);
  const [slips, setSlips] = React.useState<HrPayslip[]>([]);
  const [summary, setSummary] = React.useState<HrStatutorySummary | null>(null);
  const [openSlip, setOpenSlip] = React.useState<HrPayslip | null>(null);
  const [busy, setBusy] = React.useState(false);

  const year = now.getFullYear();
  const month = now.getMonth() + 1;

  const openRun = React.useCallback((run: Run) => {
    setSelected(run);
    setSlips([]);
    setSummary(null);
    hrApi.slips(run.id).then(setSlips).catch(() => setSlips([]));
    hrApi.statutorySummary(run.id).then(setSummary).catch(() => setSummary(null));
    // The setters are listed because they are called here rather than merely
    // passed along; they are stable, so this stays a constant callback.
  }, [setSelected, setSlips, setSummary]);

  // The newest run opens on arrival — this page is almost never wanted empty.
  // Selecting it inside the fetch rather than in a second effect watching the
  // list: an effect that reacts to its own state is a cascade, and it would
  // also fight the user the moment they picked an older month.
  React.useEffect(() => {
    hrApi
      .payrollRuns()
      .then((fetched) => {
        setRuns(fetched);
        if (fetched.length > 0) openRun(fetched[0]);
      })
      .catch(() => setRuns([]));
  }, [openRun]);

  async function process() {
    setBusy(true);
    try {
      await hrApi.processPayroll(year, month);
      toast.success("Attendance locked, contributions computed, slips generated.");
      const fresh = await hrApi.payrollRuns();
      setRuns(fresh);
      const run = fresh.find((r) => r.year === year && r.month === month);
      if (run) openRun(run);
    } catch (error) {
      toast.error(error instanceof ApiError ? error.message : "Payroll failed.");
    } finally {
      setBusy(false);
    }
  }

  async function download(file: HrRegisterFile) {
    if (!selected) return;
    try {
      await downloadFile(
        hrApi.registerUrl(selected.id, file),
        `${selected.year}-${String(selected.month).padStart(2, "0")}-${file}`
      );
    } catch (error) {
      toast.error(error instanceof ApiError ? error.message : "Could not download that file.");
    }
  }

  const gaps = summary
    ? [
        summary.missingUan > 0 ? `${summary.missingUan} without a UAN` : null,
        summary.missingEsiIp > 0 ? `${summary.missingEsiIp} without an ESI number` : null,
        summary.missingPan > 0 ? `${summary.missingPan} without a PAN` : null,
      ].filter(Boolean)
    : [];

  return (
    <PagePanel
      icon={Receipt}
      title="Payroll"
      hint="Attendance → loss of pay → PF, ESI, professional tax and TDS → net. Event overtime and incentive land on the slip."
      actions={
        <Button size="sm" className="h-8" disabled={busy} onClick={() => void process()}>
          {busy ? "Processing…" : `Process ${MONTHS[month - 1]} ${year}`}
        </Button>
      }
    >
      <div className="grid gap-4 lg:grid-cols-[260px_1fr]">
        <ul className="space-y-1.5">
          {runs.map((run) => (
            <li key={run.id}>
              <button
                type="button"
                className={
                  "w-full rounded border p-2.5 text-left text-[13px] transition-colors hover:bg-muted/40" +
                  (selected?.id === run.id ? " border-foreground/30 bg-muted/50" : "")
                }
                onClick={() => openRun(run)}
              >
                <div className="font-medium">{periodOf(run)}</div>
                <div className="text-[11.5px] text-muted-foreground">
                  {humanise(run.status)} · {run.slipCount} slips · {formatMoney(run.netTotal)}
                </div>
              </button>
            </li>
          ))}
          {runs.length === 0 ? (
            <li className="rounded border border-dashed p-4 text-center text-[12px] text-muted-foreground">
              No runs yet. Process a month to begin.
            </li>
          ) : null}
        </ul>

        <div className="space-y-4">
          {summary ? (
            <>
              <div className="grid gap-2 sm:grid-cols-2 xl:grid-cols-4">
                <Remittance
                  title="Provident fund"
                  amount={summary.pfTotal}
                  members={summary.pfMembers}
                  detail={`Employee ${formatMoney(summary.pfEmployee)} · EPS ${formatMoney(
                    summary.eps
                  )} · EPF ${formatMoney(summary.epf)} · EDLI ${formatMoney(
                    summary.edli
                  )} · admin ${formatMoney(summary.pfAdmin)}`}
                />
                <Remittance
                  title="ESI"
                  amount={summary.esiTotal}
                  members={summary.esiMembers}
                  detail={`Employee ${formatMoney(summary.esiEmployee)} · employer ${formatMoney(
                    summary.esiEmployer
                  )}`}
                />
                <Remittance
                  title="Professional tax"
                  amount={summary.ptTotal}
                  detail={
                    summary.ptByState.length > 0
                      ? summary.ptByState
                          .map((s) => `${s.state} ${formatMoney(s.amount)} (${s.employees})`)
                          .join(" · ")
                      : "Nothing payable this month"
                  }
                />
                <Remittance
                  title="Income tax"
                  amount={summary.tdsTotal}
                  members={summary.tdsMembers}
                  detail={`Deposit by the 7th of the following month`}
                />
              </div>

              <div className="flex flex-wrap items-center gap-x-5 gap-y-1 rounded-md bg-muted/50 px-3 py-2 text-[12px]">
                <span>
                  <span className="text-muted-foreground">Gross </span>
                  <span className="font-medium tabular-nums">
                    {formatMoney(summary.grossTotal)}
                  </span>
                </span>
                <span>
                  <span className="text-muted-foreground">Net paid </span>
                  <span className="font-medium tabular-nums">{formatMoney(summary.netTotal)}</span>
                </span>
                <span>
                  <span className="text-muted-foreground">Cost to company </span>
                  <span className="font-medium tabular-nums">
                    {formatMoney(summary.costToCompany)}
                  </span>
                </span>
                <span className="text-muted-foreground">
                  PF and ESI due {new Date(summary.remittanceDueOn).toLocaleDateString("en-IN", {
                    day: "numeric",
                    month: "short",
                    year: "numeric",
                  })}
                </span>
              </div>

              {gaps.length > 0 ? (
                <div className="flex items-start gap-2 rounded-md border border-amber-500/40 bg-amber-500/5 px-3 py-2 text-[12px]">
                  <AlertTriangle className="mt-0.5 size-3.5 shrink-0 text-amber-600" />
                  <span>
                    <span className="font-medium">The filings will reject these.</span>{" "}
                    {gaps.join(", ")}. Add the identifiers on the employee record before
                    uploading.
                  </span>
                </div>
              ) : null}

              <div className="flex flex-wrap gap-1.5">
                {REGISTERS.map((r) => (
                  <Button
                    key={r.file}
                    variant="outline"
                    size="sm"
                    className="h-7 text-[12px]"
                    onClick={() => void download(r.file)}
                  >
                    <Download className="mr-1.5 size-3" />
                    {r.label}
                    <span className="ml-1.5 text-[10.5px] text-muted-foreground">{r.hint}</span>
                  </Button>
                ))}
              </div>
            </>
          ) : null}

          <div className="overflow-x-auto rounded border">
            <table className="w-full text-left text-[13px]">
              <thead className="bg-muted/50 text-[11px] uppercase text-muted-foreground">
                <tr>
                  <th className="p-2 font-medium">Employee</th>
                  <th className="p-2 text-right font-medium">Gross</th>
                  <th className="p-2 text-right font-medium">LOP</th>
                  <th className="p-2 text-right font-medium">PF</th>
                  <th className="p-2 text-right font-medium">ESI</th>
                  <th className="p-2 text-right font-medium">PT</th>
                  <th className="p-2 text-right font-medium">TDS</th>
                  <th className="p-2 text-right font-medium">Net</th>
                </tr>
              </thead>
              <tbody>
                {slips.map((s) => (
                  <tr
                    key={s.id}
                    className="cursor-pointer border-t hover:bg-muted/40"
                    onClick={() => setOpenSlip(s)}
                  >
                    <td className="p-2">
                      {s.employeeName}
                      {s.taxRegime && s.tds > 0 ? (
                        <Badge variant="secondary" className="ml-1.5 font-normal">
                          {s.taxRegime}
                        </Badge>
                      ) : null}
                    </td>
                    <td className="p-2 text-right tabular-nums">{formatMoney(s.gross)}</td>
                    <td className="p-2 text-right tabular-nums text-muted-foreground">
                      {s.lopDays || "—"}
                    </td>
                    <td className="p-2 text-right tabular-nums">
                      {s.pfEmployee ? formatMoney(s.pfEmployee) : "—"}
                    </td>
                    <td className="p-2 text-right tabular-nums">
                      {s.esicEmployee ? formatMoney(s.esicEmployee) : "—"}
                    </td>
                    <td className="p-2 text-right tabular-nums">
                      {s.professionalTax ? formatMoney(s.professionalTax) : "—"}
                    </td>
                    <td className="p-2 text-right tabular-nums">
                      {s.tds ? formatMoney(s.tds) : "—"}
                    </td>
                    <td className="p-2 text-right font-medium tabular-nums">
                      {formatMoney(s.net)}
                    </td>
                  </tr>
                ))}
                {slips.length === 0 ? (
                  <tr>
                    <td colSpan={8} className="p-6 text-center text-[12px] text-muted-foreground">
                      {selected ? "This run produced no payslips." : "Pick a run to see its slips."}
                    </td>
                  </tr>
                ) : null}
              </tbody>
            </table>
          </div>
        </div>
      </div>

      <PayslipSheet
        slip={openSlip}
        period={periodOf(selected)}
        open={openSlip !== null}
        onOpenChange={(open) => {
          if (!open) setOpenSlip(null);
        }}
      />
    </PagePanel>
  );
}
