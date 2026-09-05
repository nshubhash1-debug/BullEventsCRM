"use client";

import * as React from "react";
import { Landmark } from "lucide-react";
import { toast } from "sonner";

import { PagePanel } from "@/components/shell/page-panel";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Switch } from "@/components/ui/switch";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { ApiError } from "@/lib/api";
import { formatMoney } from "@/lib/crm-api";
import { hrApi, type HrStatutoryConfig, type HrTaxProfile } from "@/lib/hrms-api";

/** Rates are stored as fractions. Nobody types 0.0833 into a form. */
function toPercent(fraction: number) {
  return (fraction * 100).toFixed(4).replace(/\.?0+$/, "");
}

function fromPercent(text: string) {
  const value = Number.parseFloat(text);
  return Number.isFinite(value) ? value / 100 : 0;
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

/** A rate entered as a percentage and stored as a fraction. */
function RateInput({
  value,
  onChange,
}: {
  value: number;
  onChange: (fraction: number) => void;
}) {
  const [text, setText] = React.useState(() => toPercent(value));

  return (
    <div className="flex items-center gap-1">
      <Input
        className="h-8 w-24 text-right tabular-nums"
        value={text}
        inputMode="decimal"
        onChange={(e) => {
          setText(e.target.value);
          onChange(fromPercent(e.target.value));
        }}
      />
      <span className="w-3 text-[12px] text-muted-foreground">%</span>
    </div>
  );
}

function MoneyInput({
  value,
  onChange,
}: {
  value: number;
  onChange: (amount: number) => void;
}) {
  const [text, setText] = React.useState(() => String(value));

  return (
    <Input
      className="h-8 w-28 text-right tabular-nums"
      value={text}
      inputMode="decimal"
      onChange={(e) => {
        setText(e.target.value);
        const parsed = Number.parseFloat(e.target.value);
        onChange(Number.isFinite(parsed) ? parsed : 0);
      }}
    />
  );
}

/**
 * The rates payroll runs on, and the declarations it runs them against.
 *
 * Saving writes a new effective-dated rate set rather than editing the one in
 * force, so reprocessing an earlier month still computes on the law that
 * applied then. The slab tables are shown but not edited here — they are the
 * Finance Act, and a payroll clerk correcting one is the rarer case than a
 * clerk needing to check what the run used.
 */
export default function HrStatutoryPage() {
  const [config, setConfig] = React.useState<HrStatutoryConfig | null>(null);
  const [draft, setDraft] = React.useState<HrStatutoryConfig | null>(null);
  const [profiles, setProfiles] = React.useState<HrTaxProfile[]>([]);
  const [busy, setBusy] = React.useState(false);
  const [loaded, setLoaded] = React.useState(false);

  const load = React.useCallback(() => {
    hrApi
      .statutoryConfig()
      .then((c) => {
        setConfig(c);
        setDraft(c ? { ...c } : null);
      })
      .catch(() => setConfig(null))
      .finally(() => setLoaded(true));
    hrApi.taxProfiles().then(setProfiles).catch(() => setProfiles([]));
  }, []);

  React.useEffect(() => {
    load();
  }, [load]);

  function edit(patch: Partial<HrStatutoryConfig>) {
    setDraft((d) => (d ? { ...d, ...patch } : d));
  }

  async function save() {
    if (!draft) return;
    setBusy(true);
    try {
      // Dated today, so it becomes the rate set from this month onward and
      // leaves every month already run computing on what it used.
      const { id: _id, professionalTaxSlabs, incomeTaxSlabs, regimes, financialYear, ...rest } =
        draft;
      void _id;
      void professionalTaxSlabs;
      void incomeTaxSlabs;
      void regimes;
      void financialYear;

      await hrApi.saveStatutoryConfig({
        ...rest,
        effectiveFrom: new Date().toISOString().slice(0, 10),
        label: null,
      });
      toast.success("Rates saved. They apply from today onward.");
      load();
    } catch (error) {
      toast.error(error instanceof ApiError ? error.message : "Could not save the rates.");
    } finally {
      setBusy(false);
    }
  }

  const dirty =
    config !== null && draft !== null && JSON.stringify(config) !== JSON.stringify(draft);

  if (loaded && !config) {
    return (
      <PagePanel icon={Landmark} title="Statutory setup">
        <p className="p-6 text-center text-[13px] text-muted-foreground">
          No statutory rates have been set up for this company yet. Restart the API to seed the
          defaults, or add a rate set through the API.
        </p>
      </PagePanel>
    );
  }

  const ptStates = draft
    ? [...new Set(draft.professionalTaxSlabs.map((s) => s.state))].sort()
    : [];

  return (
    <PagePanel
      icon={Landmark}
      title="Statutory setup"
      hint="PF, ESI, professional tax, income tax and gratuity. Saving writes a new dated rate set — months already run keep the rates they used."
      actions={
        <Button size="sm" className="h-8" disabled={!dirty || busy} onClick={() => void save()}>
          {busy ? "Saving…" : "Save rates"}
        </Button>
      }
    >
      {draft ? (
        <Tabs defaultValue="rates">
          <TabsList>
            <TabsTrigger value="rates">Rates</TabsTrigger>
            <TabsTrigger value="pt">Professional tax</TabsTrigger>
            <TabsTrigger value="tax">Income tax</TabsTrigger>
            <TabsTrigger value="declarations">
              Declarations
              {profiles.length > 0 ? (
                <span className="ml-1.5 text-[11px] text-muted-foreground">{profiles.length}</span>
              ) : null}
            </TabsTrigger>
          </TabsList>

          {/* ---------------- rates ---------------- */}

          <TabsContent value="rates" className="pt-3">
            <p className="mb-3 text-[12px] text-muted-foreground">
              In force since{" "}
              {new Date(draft.effectiveFrom).toLocaleDateString("en-IN", {
                day: "numeric",
                month: "long",
                year: "numeric",
              })}
              . {draft.label}
            </p>

            <div className="grid gap-6 lg:grid-cols-2">
              <section>
                <h2 className="mb-1 text-[12px] font-medium uppercase text-muted-foreground">
                  Provident fund
                </h2>
                <Row label="Deduct provident fund">
                  <Switch
                    checked={draft.pfEnabled}
                    onCheckedChange={(v) => edit({ pfEnabled: v })}
                  />
                </Row>
                <Row label="Employee contribution">
                  <RateInput
                    value={draft.pfEmployeeRate}
                    onChange={(v) => edit({ pfEmployeeRate: v })}
                  />
                </Row>
                <Row label="Employer contribution">
                  <RateInput
                    value={draft.pfEmployerRate}
                    onChange={(v) => edit({ pfEmployerRate: v })}
                  />
                </Row>
                <Row
                  label="Wage ceiling"
                  hint="Contributions stop at this monthly basic unless the restriction is turned off."
                >
                  <MoneyInput
                    value={draft.pfWageCeiling}
                    onChange={(v) => edit({ pfWageCeiling: v })}
                  />
                </Row>
                <Row label="Restrict the employee to the ceiling">
                  <Switch
                    checked={draft.pfRestrictEmployeeToCeiling}
                    onCheckedChange={(v) => edit({ pfRestrictEmployeeToCeiling: v })}
                  />
                </Row>
                <Row label="Restrict the employer to the ceiling">
                  <Switch
                    checked={draft.pfRestrictEmployerToCeiling}
                    onCheckedChange={(v) => edit({ pfRestrictEmployerToCeiling: v })}
                  />
                </Row>
                <Row
                  label="Pension (EPS) rate"
                  hint="Taken out of the employer's contribution, not added to it."
                >
                  <RateInput value={draft.epsRate} onChange={(v) => edit({ epsRate: v })} />
                </Row>
                <Row label="Pension wage ceiling">
                  <MoneyInput
                    value={draft.epsWageCeiling}
                    onChange={(v) => edit({ epsWageCeiling: v })}
                  />
                </Row>
                <Row label="EDLI insurance">
                  <RateInput value={draft.edliRate} onChange={(v) => edit({ edliRate: v })} />
                </Row>
                <Row label="PF administration charges">
                  <RateInput
                    value={draft.pfAdminRate}
                    onChange={(v) => edit({ pfAdminRate: v })}
                  />
                </Row>
              </section>

              <section>
                <h2 className="mb-1 text-[12px] font-medium uppercase text-muted-foreground">
                  Employees&rsquo; State Insurance
                </h2>
                <Row label="Deduct ESI">
                  <Switch
                    checked={draft.esiEnabled}
                    onCheckedChange={(v) => edit({ esiEnabled: v })}
                  />
                </Row>
                <Row label="Employee contribution">
                  <RateInput
                    value={draft.esiEmployeeRate}
                    onChange={(v) => edit({ esiEmployeeRate: v })}
                  />
                </Row>
                <Row label="Employer contribution">
                  <RateInput
                    value={draft.esiEmployerRate}
                    onChange={(v) => edit({ esiEmployerRate: v })}
                  />
                </Row>
                <Row
                  label="Wage threshold"
                  hint="Above this monthly gross an employee is out of the scheme."
                >
                  <MoneyInput
                    value={draft.esiWageThreshold}
                    onChange={(v) => edit({ esiWageThreshold: v })}
                  />
                </Row>

                <h2 className="mb-1 mt-5 text-[12px] font-medium uppercase text-muted-foreground">
                  Professional tax and income tax
                </h2>
                <Row label="Deduct professional tax">
                  <Switch
                    checked={draft.ptEnabled}
                    onCheckedChange={(v) => edit({ ptEnabled: v })}
                  />
                </Row>
                <Row
                  label="Default state"
                  hint="Used when an employee record carries no state of its own."
                >
                  <Input
                    className="h-8 w-40"
                    value={draft.ptDefaultState ?? ""}
                    onChange={(e) => edit({ ptDefaultState: e.target.value || null })}
                  />
                </Row>
                <Row label="Deduct income tax">
                  <Switch
                    checked={draft.tdsEnabled}
                    onCheckedChange={(v) => edit({ tdsEnabled: v })}
                  />
                </Row>
                <Row label="Health and education cess">
                  <RateInput value={draft.cessRate} onChange={(v) => edit({ cessRate: v })} />
                </Row>

                <h2 className="mb-1 mt-5 text-[12px] font-medium uppercase text-muted-foreground">
                  Gratuity
                </h2>
                <Row label="Accrue gratuity">
                  <Switch
                    checked={draft.gratuityEnabled}
                    onCheckedChange={(v) => edit({ gratuityEnabled: v })}
                  />
                </Row>
                <Row label="Days per year of service">
                  <MoneyInput
                    value={draft.gratuityDaysPerYear}
                    onChange={(v) => edit({ gratuityDaysPerYear: v })}
                  />
                </Row>
                <Row label="Days in a month" hint="26 under the Payment of Gratuity Act.">
                  <MoneyInput
                    value={draft.gratuityMonthDays}
                    onChange={(v) => edit({ gratuityMonthDays: v })}
                  />
                </Row>
                <Row label="Years before it vests">
                  <MoneyInput
                    value={draft.gratuityEligibleYears}
                    onChange={(v) => edit({ gratuityEligibleYears: v })}
                  />
                </Row>
                <Row label="Lifetime ceiling">
                  <MoneyInput
                    value={draft.gratuityCeiling}
                    onChange={(v) => edit({ gratuityCeiling: v })}
                  />
                </Row>
              </section>
            </div>
          </TabsContent>

          {/* ---------------- professional tax ---------------- */}

          <TabsContent value="pt" className="pt-3">
            <p className="mb-3 text-[12px] text-muted-foreground">
              {draft.professionalTaxSlabs.length} bands across {ptStates.length} states. A band
              marked with a month applies only in that month — which is how Maharashtra&rsquo;s
              February surcharge reaches the ₹2,500 annual cap.
            </p>
            <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-3">
              {ptStates.map((state) => (
                <div key={state} className="rounded border p-3">
                  <h3 className="mb-1.5 text-[12px] font-medium">{state}</h3>
                  <table className="w-full text-[12px]">
                    <tbody>
                      {draft.professionalTaxSlabs
                        .filter((s) => s.state === state)
                        .map((s) => (
                          <tr key={s.id} className="border-b last:border-0">
                            <td className="py-1 text-muted-foreground">
                              {formatMoney(s.fromAmount)}
                              {s.toAmount === null ? " and above" : ` – ${formatMoney(s.toAmount)}`}
                              {s.month ? (
                                <Badge variant="secondary" className="ml-1.5 font-normal">
                                  month {s.month}
                                </Badge>
                              ) : null}
                            </td>
                            <td className="py-1 text-right tabular-nums">
                              {formatMoney(s.amount)}
                            </td>
                          </tr>
                        ))}
                    </tbody>
                  </table>
                </div>
              ))}
            </div>
          </TabsContent>

          {/* ---------------- income tax ---------------- */}

          <TabsContent value="tax" className="pt-3">
            <p className="mb-3 text-[12px] text-muted-foreground">
              Financial year {draft.financialYear}&ndash;
              {String((draft.financialYear + 1) % 100).padStart(2, "0")}.
            </p>
            <div className="grid gap-4 md:grid-cols-2">
              {draft.regimes.map((regime) => (
                <div key={regime.id} className="rounded border p-3">
                  <h3 className="mb-1 text-[13px] font-medium">{regime.regime} regime</h3>
                  <p className="mb-2 text-[11.5px] leading-snug text-muted-foreground">
                    Standard deduction {formatMoney(regime.standardDeduction)}. Section 87A rebate
                    of up to {formatMoney(regime.rebateMaximum)} below{" "}
                    {formatMoney(regime.rebateIncomeCeiling)} of taxable income.{" "}
                    {regime.allowsHraExemption
                      ? "House rent exemption allowed."
                      : "No house rent exemption."}{" "}
                    {regime.allowsChapterViaDeductions
                      ? "Chapter VI-A deductions allowed."
                      : "No Chapter VI-A deductions."}
                  </p>
                  <table className="w-full text-[12px]">
                    <tbody>
                      {draft.incomeTaxSlabs
                        .filter((s) => s.regime === regime.regime)
                        .sort((a, b) => a.sortOrder - b.sortOrder)
                        .map((s) => (
                          <tr key={s.id} className="border-b last:border-0">
                            <td className="py-1 text-muted-foreground">
                              {formatMoney(s.fromAmount)}
                              {s.toAmount === null ? " and above" : ` – ${formatMoney(s.toAmount)}`}
                            </td>
                            <td className="py-1 text-right tabular-nums">
                              {toPercent(s.rate)}%
                            </td>
                          </tr>
                        ))}
                    </tbody>
                  </table>
                </div>
              ))}
            </div>
          </TabsContent>

          {/* ---------------- declarations ---------------- */}

          <TabsContent value="declarations" className="pt-3">
            <p className="mb-3 text-[12px] text-muted-foreground">
              What each employee has told payroll they will invest, which is what income tax is
              deducted on until the proofs arrive. Employees with no declaration are deducted under
              the new regime.
            </p>
            <div className="overflow-x-auto rounded border">
              <table className="w-full text-left text-[13px]">
                <thead className="bg-muted/50 text-[11px] uppercase text-muted-foreground">
                  <tr>
                    <th className="p-2 font-medium">Employee</th>
                    <th className="p-2 font-medium">Regime</th>
                    <th className="p-2 text-right font-medium">Rent paid</th>
                    <th className="p-2 text-right font-medium">Chapter VI-A</th>
                    <th className="p-2 text-right font-medium">Housing interest</th>
                    <th className="p-2 font-medium">Proofs</th>
                  </tr>
                </thead>
                <tbody>
                  {profiles.map((p) => (
                    <tr key={p.id} className="border-t">
                      <td className="p-2">{p.employeeName}</td>
                      <td className="p-2">
                        <Badge variant="secondary" className="font-normal">
                          {p.regime}
                        </Badge>
                      </td>
                      <td className="p-2 text-right tabular-nums">
                        {p.annualRentPaid ? formatMoney(p.annualRentPaid) : "—"}
                        {p.rentsInMetro ? (
                          <span className="ml-1 text-[11px] text-muted-foreground">metro</span>
                        ) : null}
                      </td>
                      <td className="p-2 text-right tabular-nums">
                        {p.totalChapterVia ? formatMoney(p.totalChapterVia) : "—"}
                      </td>
                      <td className="p-2 text-right tabular-nums">
                        {p.housingLoanInterest ? formatMoney(p.housingLoanInterest) : "—"}
                      </td>
                      <td className="p-2 text-[12px] text-muted-foreground">
                        {p.proofsSubmitted ? "Submitted" : "Awaited"}
                      </td>
                    </tr>
                  ))}
                  {profiles.length === 0 ? (
                    <tr>
                      <td colSpan={6} className="p-6 text-center text-[12px] text-muted-foreground">
                        Nobody has filed a declaration for this year yet.
                      </td>
                    </tr>
                  ) : null}
                </tbody>
              </table>
            </div>
          </TabsContent>
        </Tabs>
      ) : null}
    </PagePanel>
  );
}
