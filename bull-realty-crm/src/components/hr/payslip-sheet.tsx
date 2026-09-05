"use client";

import * as React from "react";
import { ExternalLink } from "lucide-react";
import { toast } from "sonner";

import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import {
  Sheet,
  SheetContent,
  SheetDescription,
  SheetHeader,
  SheetTitle,
} from "@/components/ui/sheet";
import { ApiError } from "@/lib/api";
import { formatMoney } from "@/lib/crm-api";
import { openFile } from "@/lib/download";
import { payApi, type HrPayslipLine } from "@/lib/hr-pay-api";
import { hrApi, type HrPayslip } from "@/lib/hrms-api";

/**
 * One line of a payslip.
 *
 * A zero is hidden rather than printed, because a slip listing every deduction
 * that did not apply reads as though they did. Totals stay whatever they are —
 * a total of nothing is still the answer to a question the reader asked.
 */
function Line({
  label,
  amount,
  hint,
  total = false,
  indent = false,
}: {
  label: string;
  amount: number;
  hint?: string;
  total?: boolean;
  indent?: boolean;
}) {
  if (amount === 0 && !total) return null;

  return (
    <div
      className={
        "flex items-baseline justify-between gap-3 py-1.5 text-[13px] " +
        (total ? "mt-1 border-t pt-2 font-medium" : "border-b last:border-0") +
        (indent ? " pl-3" : "")
      }
    >
      <span className={total ? "" : "text-muted-foreground"}>
        {label}
        {hint ? <span className="ml-1.5 text-[11px] text-muted-foreground/70">{hint}</span> : null}
      </span>
      <span className="shrink-0 tabular-nums">{formatMoney(amount)}</span>
    </div>
  );
}

function Section({ title, children }: { title: string; children: React.ReactNode }) {
  return (
    <section className="space-y-0.5">
      <h3 className="mb-1 text-[11px] font-semibold uppercase tracking-wide text-muted-foreground">
        {title}
      </h3>
      {children}
    </section>
  );
}

/**
 * The statutory breakdown behind one payslip.
 *
 * Everything the engine computed, in the order a payslip is read: what was
 * earned, what came off, what is left, and then — separately, because it is not
 * the employee's money — what the company paid on top. The tax section shows
 * the projection this month's deduction was taken from, since a monthly TDS
 * figure is meaningless without the year it was divided out of.
 */
export function PayslipSheet({
  slip,
  period,
  open,
  onOpenChange,
}: {
  slip: HrPayslip | null;
  period: string;
  open: boolean;
  onOpenChange: (open: boolean) => void;
}) {
  const slipId = slip?.id ?? null;

  // The components behind the totals. Fetched per slip rather than with the
  // list, because a run of two hundred people would otherwise carry two
  // thousand lines nobody has asked to see.
  //
  // Held with the id they belong to rather than cleared when the slip changes:
  // clearing would mean a setState in the effect body, and comparing the id on
  // render also discards a response that arrives after the reader has moved on.
  const [loaded, setLoaded] = React.useState<{ id: number; lines: HrPayslipLine[] } | null>(null);

  React.useEffect(() => {
    if (slipId === null || !open) return;
    payApi
      .payslipLines(slipId)
      .then((rows) => setLoaded({ id: slipId, lines: rows }))
      .catch(() => setLoaded({ id: slipId, lines: [] }));
  }, [slipId, open]);

  const lines = loaded?.id === slipId ? loaded.lines : [];

  if (!slip) return null;

  const earningLines = lines.filter((l) => l.componentType === "Earning" && !l.isStatutory);

  const employerTotal =
    slip.pfEmployer + slip.edli + slip.pfAdminCharges + slip.esicEmployer + slip.lwfEmployer;

  // Anything in the gross the salary structure did not itemise. The slips list
  // carries no structure, so earnings are shown as gross less what is named.
  const named = slip.incentive + slip.overtimeAmount;

  return (
    <Sheet open={open} onOpenChange={onOpenChange}>
      <SheetContent className="w-full overflow-y-auto sm:max-w-lg">
        <SheetHeader>
          <SheetTitle className="flex items-center gap-2">
            {slip.employeeName}
            {slip.taxRegime ? (
              <Badge variant="secondary" className="font-normal">
                {slip.taxRegime} regime
              </Badge>
            ) : null}
          </SheetTitle>
          <SheetDescription>{period}</SheetDescription>
        </SheetHeader>

        <div className="space-y-5 px-4 pb-8">
          <Section title="Earnings">
            {earningLines.length > 0 ? (
              earningLines.map((l) => (
                <Line key={`${l.abbreviation}-${l.sortOrder}`} label={l.name} amount={l.amount} />
              ))
            ) : (
              <>
                <Line label="Gross for the month" amount={slip.gross - named} />
                <Line label="Incentive" amount={slip.incentive} />
              </>
            )}
            <Line label="Overtime" amount={slip.overtimeAmount} />
            <Line label="Gross earnings" amount={slip.gross} total />
            {slip.lopDays > 0 ? (
              <p className="pt-2 text-[11.5px] text-muted-foreground">
                {slip.lopDays} day{slip.lopDays === 1 ? "" : "s"} of loss of pay. Contributions
                were computed on the payable gross of {formatMoney(slip.payableGross)}.
              </p>
            ) : null}
          </Section>

          <Section title="Deductions">
            <Line label="Loss of pay" amount={slip.lopAmount} />
            <Line
              label="Provident fund"
              amount={slip.pfEmployee}
              hint={slip.pfWage > 0 ? `on ${formatMoney(slip.pfWage)}` : undefined}
            />
            <Line label="ESI" amount={slip.esicEmployee} />
            <Line label="Professional tax" amount={slip.professionalTax} />
            <Line label="Labour welfare fund" amount={slip.lwfEmployee} />
            <Line label="Income tax (TDS)" amount={slip.tds} />
            {lines
              .filter((l) => l.componentType === "Deduction" && !l.isStatutory
                && l.abbreviation !== "LOP")
              .map((l) => (
                <Line key={`${l.abbreviation}-${l.sortOrder}`} label={l.name} amount={l.amount} />
              ))}
            {lines.length === 0 ? (
              <Line label="Other deductions" amount={slip.otherDeductions} />
            ) : null}
            <Line label="Total deductions" amount={slip.totalDeductions} total />
          </Section>

          <div className="flex items-baseline justify-between rounded-md bg-muted px-3 py-2.5">
            <span className="text-[13px] font-medium">Net payable</span>
            <span className="text-lg font-semibold tabular-nums">{formatMoney(slip.net)}</span>
          </div>

          {employerTotal > 0 || slip.gratuityAccrual > 0 ? (
            <Section title="Employer contributions">
              <p className="pb-1 text-[11.5px] text-muted-foreground">
                Paid on top of the gross. Not deducted from the employee.
              </p>
              <Line
                label="Provident fund"
                amount={slip.pfEmployer}
                hint={slip.pfWage > 0 ? `on ${formatMoney(slip.pfWage)}` : undefined}
              />
              <Line label="— pension fund (EPS)" amount={slip.epsEmployer} indent />
              <Line label="— provident fund (EPF)" amount={slip.epfEmployer} indent />
              <Line label="EDLI insurance" amount={slip.edli} />
              <Line label="PF administration" amount={slip.pfAdminCharges} />
              <Line label="ESI" amount={slip.esicEmployer} />
              <Line label="Labour welfare fund" amount={slip.lwfEmployer} />
              <Line label="Gratuity accrued" amount={slip.gratuityAccrual} />
              <Line label="Cost to company" amount={slip.costToCompany} total />
            </Section>
          ) : null}

          {slip.tds > 0 || slip.projectedAnnualTax > 0 || slip.hraExemption > 0 ? (
            <Section title="Income tax working">
              <p className="pb-1 text-[11.5px] text-muted-foreground">
                This month&rsquo;s deduction is the balance of the year&rsquo;s projected
                liability spread over the months remaining.
              </p>
              <Line label="House rent exempt u/s 10(13A)" amount={slip.hraExemption} />
              <Line label="Projected taxable for the year" amount={slip.projectedAnnualTaxable} />
              <Line label="Projected tax for the year" amount={slip.projectedAnnualTax} />
              <Line label="Deducted this month" amount={slip.tds} total />
            </Section>
          ) : null}

          <Button
            variant="outline"
            size="sm"
            className="w-full"
            onClick={() => {
              openFile(hrApi.payslipHtmlUrl(slip.id)).catch((error) =>
                toast.error(
                  error instanceof ApiError ? error.message : "Could not open the slip."
                )
              );
            }}
          >
            <ExternalLink className="mr-1.5 size-3.5" />
            Open the printable slip
          </Button>
        </div>
      </SheetContent>
    </Sheet>
  );
}
