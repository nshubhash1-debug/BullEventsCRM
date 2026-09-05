"use client";

import * as React from "react";
import { AlertTriangle, Briefcase } from "lucide-react";
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
  talentApi,
  type HrInterviewRound,
  type HrJobOffer,
  type HrReferral,
  type HrRequisition,
  type HrStaffingPlan,
} from "@/lib/hr-talent-api";

function day(value: string | null) {
  if (!value) return "—";
  return new Date(value).toLocaleDateString("en-IN", {
    day: "numeric",
    month: "short",
    year: "numeric",
  });
}

const STATUS_TONE: Record<string, string> = {
  PendingApproval: "bg-amber-500/10 text-amber-700 dark:text-amber-300",
  Approved: "bg-emerald-500/10 text-emerald-700 dark:text-emerald-300",
  Rejected: "bg-red-500/10 text-red-700 dark:text-red-300",
  Filled: "bg-blue-500/10 text-blue-700 dark:text-blue-300",
  Draft: "bg-zinc-500/10 text-zinc-700 dark:text-zinc-300",
  Sent: "bg-blue-500/10 text-blue-700 dark:text-blue-300",
  Accepted: "bg-emerald-500/10 text-emerald-700 dark:text-emerald-300",
  Declined: "bg-red-500/10 text-red-700 dark:text-red-300",
  Withdrawn: "bg-zinc-500/10 text-zinc-700 dark:text-zinc-300",
  Submitted: "bg-zinc-500/10 text-zinc-700 dark:text-zinc-300",
  InProcess: "bg-blue-500/10 text-blue-700 dark:text-blue-300",
  Hired: "bg-emerald-500/10 text-emerald-700 dark:text-emerald-300",
  BonusPaid: "bg-violet-500/10 text-violet-700 dark:text-violet-300",
};

function Pill({ status }: { status: string }) {
  return (
    <span
      className={
        "rounded px-1.5 py-0.5 text-[10.5px] font-medium " +
        (STATUS_TONE[status] ?? STATUS_TONE.Draft)
      }
    >
      {status}
    </span>
  );
}

/**
 * Hiring, with the decisions kept.
 *
 * The staffing plan is what somebody agreed to pay for; a requisition is
 * checked against it and warned rather than refused, because a plan is guidance
 * and refusing outright only teaches people to stop recording plans.
 */
export default function HrHiringPage() {
  const now = new Date();
  const [plans, setPlans] = React.useState<HrStaffingPlan[]>([]);
  const [requisitions, setRequisitions] = React.useState<HrRequisition[]>([]);
  const [offers, setOffers] = React.useState<HrJobOffer[]>([]);
  const [referrals, setReferrals] = React.useState<HrReferral[]>([]);
  const [rounds, setRounds] = React.useState<HrInterviewRound[]>([]);
  const [employees, setEmployees] = React.useState<{ id: number; name: string }[]>([]);
  const [departments, setDepartments] = React.useState<{ id: number; name: string }[]>([]);
  const [busy, setBusy] = React.useState(false);

  const [raiseOpen, setRaiseOpen] = React.useState(false);
  const [raiseForm, setRaiseForm] = React.useState({
    position: "",
    departmentId: "",
    headcount: "1",
    isReplacement: false,
    replacingEmployeeId: "",
    requestedByEmployeeId: "",
    requiredBy: "",
    justification: "",
    salaryMin: "",
    salaryMax: "",
    location: "",
  });

  const [referOpen, setReferOpen] = React.useState(false);
  const [referForm, setReferForm] = React.useState({
    referrerEmployeeId: "",
    candidateName: "",
    phone: "",
    position: "",
    notes: "",
    bonusAmount: "5000",
    retentionMonths: "3",
  });

  const load = React.useCallback(() => {
    talentApi.staffingPlans().then(setPlans).catch(() => setPlans([]));
    talentApi.requisitions().then(setRequisitions).catch(() => setRequisitions([]));
    talentApi.offers().then(setOffers).catch(() => setOffers([]));
    talentApi.referrals().then(setReferrals).catch(() => setReferrals([]));
  }, [setPlans, setRequisitions, setOffers, setReferrals]);

  React.useEffect(() => {
    load();
    talentApi.rounds().then(setRounds).catch(() => setRounds([]));
    apiRequest<{ items: { id: number; name: string }[] }>("/api/hr/employees/query", {
      method: "POST",
      body: JSON.stringify({ page: 1, pageSize: 300 }),
      auth: true,
    })
      .then((page) => setEmployees(page.items))
      .catch(() => setEmployees([]));
    apiRequest<{ id: number; name: string }[]>("/api/hr/departments", {
      method: "GET",
      auth: true,
    })
      .then(setDepartments)
      .catch(() => setDepartments([]));
  }, [load]);

  async function raise() {
    setBusy(true);
    try {
      const created = await talentApi.raise({
        position: raiseForm.position,
        departmentId: raiseForm.departmentId ? Number(raiseForm.departmentId) : null,
        designationId: null,
        headcount: Number(raiseForm.headcount),
        isReplacement: raiseForm.isReplacement,
        replacingEmployeeId: raiseForm.replacingEmployeeId
          ? Number(raiseForm.replacingEmployeeId)
          : null,
        requestedByEmployeeId: raiseForm.requestedByEmployeeId
          ? Number(raiseForm.requestedByEmployeeId)
          : null,
        requiredBy: raiseForm.requiredBy || null,
        justification: raiseForm.justification || null,
        jobDescription: null,
        salaryMin: raiseForm.salaryMin ? Number(raiseForm.salaryMin) : null,
        salaryMax: raiseForm.salaryMax ? Number(raiseForm.salaryMax) : null,
        location: raiseForm.location || null,
      });

      if (created.planWarning) {
        toast.warning(created.planWarning, { duration: 8000 });
      } else {
        toast.success("Raised for approval.");
      }
      setRaiseOpen(false);
      load();
    } catch (error) {
      toast.error(error instanceof ApiError ? error.message : "Could not raise that.");
    } finally {
      setBusy(false);
    }
  }

  async function decide(requisition: HrRequisition, approve: boolean) {
    const note = approve
      ? window.prompt("A note for the record (optional)")?.trim() || undefined
      : window.prompt("Why is this being refused?")?.trim();

    if (!approve && !note) return;

    try {
      await talentApi.decideRequisition(requisition.id, approve, note);
      toast.success(approve ? "Approved — the vacancy is open." : "Refused.");
      load();
    } catch (error) {
      toast.error(error instanceof ApiError ? error.message : "Could not decide that.");
    }
  }

  async function refer() {
    setBusy(true);
    try {
      await talentApi.refer({
        referrerEmployeeId: Number(referForm.referrerEmployeeId),
        candidateName: referForm.candidateName,
        phone: referForm.phone || null,
        email: null,
        position: referForm.position || null,
        notes: referForm.notes || null,
        vacancyId: null,
        bonusAmount: Number(referForm.bonusAmount || 0),
        retentionMonths: Number(referForm.retentionMonths || 0),
      });
      toast.success("Referral recorded.");
      setReferOpen(false);
      load();
    } catch (error) {
      toast.error(error instanceof ApiError ? error.message : "Could not record that.");
    } finally {
      setBusy(false);
    }
  }

  async function referralAction(
    referral: HrReferral,
    action: "pipe" | "hired" | "bonus"
  ) {
    try {
      if (action === "pipe") await talentApi.referralToCandidate(referral.id);
      if (action === "hired") await talentApi.referralHired(referral.id);
      if (action === "bonus") {
        await talentApi.payReferralBonus(referral.id, now.getFullYear(), now.getMonth() + 1);
      }
      toast.success("Done.");
      load();
    } catch (error) {
      toast.error(error instanceof ApiError ? error.message : "Could not do that.");
    }
  }

  async function offerAction(offer: HrJobOffer, status: string) {
    const reason =
      status === "Declined" || status === "Withdrawn"
        ? window.prompt(`Why was it ${status.toLowerCase()}?`)?.trim()
        : undefined;
    if ((status === "Declined" || status === "Withdrawn") && !reason) return;

    try {
      await talentApi.setOfferStatus(offer.id, status, reason);
      toast.success(`Marked ${status.toLowerCase()}.`);
      load();
    } catch (error) {
      toast.error(error instanceof ApiError ? error.message : "Could not do that.");
    }
  }

  const pending = requisitions.filter((r) => r.status === "PendingApproval");
  const bonusDue = referrals.filter((r) => r.bonusPayable);

  return (
    <PagePanel
      icon={Briefcase}
      title="Hiring"
      hint="Agreed headcount, the requests drawn against it, what was offered and why it was turned down, and who brought the person in."
      actions={
        <div className="flex gap-1.5">
          <Button
            size="sm"
            variant="outline"
            className="h-8"
            onClick={() => setReferOpen(true)}
          >
            Refer someone
          </Button>
          <Button size="sm" className="h-8" onClick={() => setRaiseOpen(true)}>
            Raise a requisition
          </Button>
        </div>
      }
    >
      <Tabs defaultValue="requisitions">
        <TabsList>
          <TabsTrigger value="requisitions">
            Requisitions
            {pending.length > 0 ? (
              <Badge variant="secondary" className="ml-1.5 h-4 px-1 text-[10px]">
                {pending.length}
              </Badge>
            ) : null}
          </TabsTrigger>
          <TabsTrigger value="plan">Staffing plan</TabsTrigger>
          <TabsTrigger value="offers">
            Offers
            <span className="ml-1.5 text-[11px] text-muted-foreground">{offers.length}</span>
          </TabsTrigger>
          <TabsTrigger value="referrals">
            Referrals
            {bonusDue.length > 0 ? (
              <Badge variant="secondary" className="ml-1.5 h-4 px-1 text-[10px]">
                {bonusDue.length}
              </Badge>
            ) : null}
          </TabsTrigger>
          <TabsTrigger value="rounds">Interview rounds</TabsTrigger>
        </TabsList>

        {/* ---------------- requisitions ---------------- */}

        <TabsContent value="requisitions" className="pt-3">
          <div className="overflow-x-auto rounded border">
            <table className="w-full text-left text-[13px]">
              <thead className="bg-muted/50 text-[11px] uppercase text-muted-foreground">
                <tr>
                  <th className="p-2 font-medium">Role</th>
                  <th className="p-2 font-medium">Department</th>
                  <th className="p-2 text-right font-medium">Heads</th>
                  <th className="p-2 font-medium">Asked by</th>
                  <th className="p-2 font-medium">Needed by</th>
                  <th className="p-2 font-medium">Against plan</th>
                  <th className="p-2 font-medium">Status</th>
                  <th className="p-2" />
                </tr>
              </thead>
              <tbody>
                {requisitions.map((r) => (
                  <tr key={r.id} className="border-t">
                    <td className="p-2">
                      {r.position}
                      {r.isReplacement ? (
                        <Badge variant="secondary" className="ml-1.5 font-normal">
                          replacement
                        </Badge>
                      ) : null}
                      {r.justification ? (
                        <div className="text-[11px] text-muted-foreground">{r.justification}</div>
                      ) : null}
                    </td>
                    <td className="p-2 text-muted-foreground">{r.departmentName ?? "—"}</td>
                    <td className="p-2 text-right tabular-nums">{r.headcount}</td>
                    <td className="p-2 text-muted-foreground">{r.requestedByName ?? "—"}</td>
                    <td className="p-2 text-muted-foreground">{day(r.requiredBy)}</td>
                    <td className="p-2 text-[12px] text-muted-foreground">
                      {r.isReplacement ? "not counted" : (r.staffingPlanName ?? "no plan")}
                    </td>
                    <td className="p-2">
                      <Pill status={r.status} />
                      {r.decisionNote ? (
                        <div className="mt-0.5 text-[11px] text-muted-foreground">
                          {r.decisionNote}
                        </div>
                      ) : null}
                    </td>
                    <td className="p-2 text-right">
                      {r.status === "PendingApproval" ? (
                        <span className="flex justify-end gap-1">
                          <Button
                            size="sm"
                            variant="outline"
                            className="h-6 text-[11px]"
                            onClick={() => void decide(r, true)}
                          >
                            Approve
                          </Button>
                          <Button
                            size="sm"
                            variant="ghost"
                            className="h-6 text-[11px]"
                            onClick={() => void decide(r, false)}
                          >
                            Refuse
                          </Button>
                        </span>
                      ) : null}
                    </td>
                  </tr>
                ))}
                {requisitions.length === 0 ? (
                  <tr>
                    <td colSpan={8} className="p-6 text-center text-[12px] text-muted-foreground">
                      Nothing has been asked for.
                    </td>
                  </tr>
                ) : null}
              </tbody>
            </table>
          </div>
        </TabsContent>

        {/* ---------------- plan ---------------- */}

        <TabsContent value="plan" className="pt-3">
          {plans.map((p) => (
            <div key={p.id} className="mb-4">
              <div className="mb-2 flex flex-wrap items-baseline gap-x-4 gap-y-1 rounded-md bg-muted/50 px-3 py-2 text-[12px]">
                <span className="font-medium">{p.name}</span>
                <span className="text-muted-foreground">
                  {day(p.fromDate)} — {day(p.toDate)}
                </span>
                <span>
                  <span className="text-muted-foreground">Headcount </span>
                  <span className="font-medium tabular-nums">{p.totalHeadcount}</span>
                </span>
                <span>
                  <span className="text-muted-foreground">Budget </span>
                  <span className="font-medium tabular-nums">{formatMoney(p.totalBudget)}</span>
                </span>
                {p.isCurrent ? (
                  <Badge variant="secondary" className="font-normal">
                    current
                  </Badge>
                ) : null}
              </div>
              {p.notes ? (
                <p className="mb-2 text-[11.5px] text-muted-foreground">{p.notes}</p>
              ) : null}
              <div className="overflow-x-auto rounded border">
                <table className="w-full text-left text-[13px]">
                  <thead className="bg-muted/50 text-[11px] uppercase text-muted-foreground">
                    <tr>
                      <th className="p-2 font-medium">Role</th>
                      <th className="p-2 font-medium">Department</th>
                      <th className="p-2 text-right font-medium">Agreed</th>
                      <th className="p-2 text-right font-medium">Approved</th>
                      <th className="p-2 text-right font-medium">Left</th>
                      <th className="p-2 text-right font-medium">Budget each</th>
                    </tr>
                  </thead>
                  <tbody>
                    {p.lines.map((l) => (
                      <tr key={l.id} className="border-t">
                        <td className="p-2">{l.position}</td>
                        <td className="p-2 text-muted-foreground">{l.departmentName ?? "—"}</td>
                        <td className="p-2 text-right tabular-nums">{l.headcount}</td>
                        <td className="p-2 text-right tabular-nums">{l.approved || "—"}</td>
                        <td
                          className={
                            "p-2 text-right font-medium tabular-nums " +
                            (l.remaining === 0 ? "text-amber-700 dark:text-amber-400" : "")
                          }
                        >
                          {l.remaining}
                        </td>
                        <td className="p-2 text-right tabular-nums text-muted-foreground">
                          {formatMoney(l.budgetPerHead)}
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            </div>
          ))}
          {plans.length === 0 ? (
            <p className="rounded border border-dashed p-6 text-center text-[12px] text-muted-foreground">
              No staffing plan.
            </p>
          ) : null}
        </TabsContent>

        {/* ---------------- offers ---------------- */}

        <TabsContent value="offers" className="pt-3">
          <div className="overflow-x-auto rounded border">
            <table className="w-full text-left text-[13px]">
              <thead className="bg-muted/50 text-[11px] uppercase text-muted-foreground">
                <tr>
                  <th className="p-2 font-medium">Candidate</th>
                  <th className="p-2 font-medium">Role</th>
                  <th className="p-2 text-right font-medium">Annual CTC</th>
                  <th className="p-2 font-medium">Offered</th>
                  <th className="p-2 font-medium">Valid until</th>
                  <th className="p-2 font-medium">Status</th>
                  <th className="p-2" />
                </tr>
              </thead>
              <tbody>
                {offers.map((o) => (
                  <tr key={o.id} className="border-t">
                    <td className="p-2">
                      {o.candidateName}
                      {o.revision > 1 ? (
                        <Badge variant="secondary" className="ml-1.5 font-normal">
                          rev {o.revision}
                        </Badge>
                      ) : null}
                    </td>
                    <td className="p-2 text-muted-foreground">{o.position}</td>
                    <td className="p-2 text-right tabular-nums">{formatMoney(o.annualCtc)}</td>
                    <td className="p-2 text-muted-foreground">{day(o.offerDate)}</td>
                    <td className="p-2 text-muted-foreground">
                      {day(o.validUntil)}
                      {o.hasLapsed ? (
                        <span className="ml-1.5 text-[11px] text-amber-700 dark:text-amber-400">
                          lapsed
                        </span>
                      ) : null}
                    </td>
                    <td className="p-2">
                      <Pill status={o.status} />
                      {o.outcomeReason ? (
                        <div className="mt-0.5 text-[11px] text-muted-foreground">
                          {o.outcomeReason}
                        </div>
                      ) : null}
                    </td>
                    <td className="p-2 text-right">
                      {o.status === "Draft" || o.status === "Sent" ? (
                        <span className="flex justify-end gap-1">
                          {o.status === "Draft" ? (
                            <Button
                              size="sm"
                              variant="outline"
                              className="h-6 text-[11px]"
                              onClick={() => void offerAction(o, "Sent")}
                            >
                              Send
                            </Button>
                          ) : (
                            <Button
                              size="sm"
                              variant="outline"
                              className="h-6 text-[11px]"
                              onClick={() => void offerAction(o, "Accepted")}
                            >
                              Accepted
                            </Button>
                          )}
                          <Button
                            size="sm"
                            variant="ghost"
                            className="h-6 text-[11px]"
                            onClick={() => void offerAction(o, "Declined")}
                          >
                            Declined
                          </Button>
                        </span>
                      ) : null}
                    </td>
                  </tr>
                ))}
                {offers.length === 0 ? (
                  <tr>
                    <td colSpan={7} className="p-6 text-center text-[12px] text-muted-foreground">
                      No offers made.
                    </td>
                  </tr>
                ) : null}
              </tbody>
            </table>
          </div>
        </TabsContent>

        {/* ---------------- referrals ---------------- */}

        <TabsContent value="referrals" className="pt-3">
          <p className="mb-2 text-[12px] text-muted-foreground">
            The bonus is held until the referred person has stayed the retention period, and is
            then paid through payroll rather than settled outside it.
          </p>
          <div className="overflow-x-auto rounded border">
            <table className="w-full text-left text-[13px]">
              <thead className="bg-muted/50 text-[11px] uppercase text-muted-foreground">
                <tr>
                  <th className="p-2 font-medium">Referred by</th>
                  <th className="p-2 font-medium">Candidate</th>
                  <th className="p-2 font-medium">Role</th>
                  <th className="p-2 text-right font-medium">Bonus</th>
                  <th className="p-2 font-medium">Due</th>
                  <th className="p-2 font-medium">Status</th>
                  <th className="p-2" />
                </tr>
              </thead>
              <tbody>
                {referrals.map((r) => (
                  <tr key={r.id} className="border-t">
                    <td className="p-2">{r.referrerName}</td>
                    <td className="p-2">
                      {r.candidateName}
                      {r.phone ? (
                        <div className="text-[11px] text-muted-foreground">{r.phone}</div>
                      ) : null}
                    </td>
                    <td className="p-2 text-muted-foreground">{r.position ?? "—"}</td>
                    <td className="p-2 text-right tabular-nums">
                      {r.bonusAmount ? formatMoney(r.bonusAmount) : "—"}
                      {r.retentionMonths > 0 ? (
                        <div className="text-[11px] text-muted-foreground">
                          after {r.retentionMonths}m
                        </div>
                      ) : null}
                    </td>
                    <td className="p-2 text-muted-foreground">
                      {day(r.bonusDueOn)}
                      {r.bonusPayable ? (
                        <div className="text-[11px] text-emerald-700 dark:text-emerald-400">
                          payable
                        </div>
                      ) : null}
                    </td>
                    <td className="p-2">
                      <Pill status={r.status} />
                    </td>
                    <td className="p-2 text-right">
                      <span className="flex justify-end gap-1">
                        {r.candidateId === null ? (
                          <Button
                            size="sm"
                            variant="outline"
                            className="h-6 text-[11px]"
                            onClick={() => void referralAction(r, "pipe")}
                          >
                            To pipeline
                          </Button>
                        ) : null}
                        {r.hiredOn === null ? (
                          <Button
                            size="sm"
                            variant="ghost"
                            className="h-6 text-[11px]"
                            onClick={() => void referralAction(r, "hired")}
                          >
                            Hired
                          </Button>
                        ) : null}
                        {r.bonusPayable ? (
                          <Button
                            size="sm"
                            variant="outline"
                            className="h-6 text-[11px]"
                            onClick={() => void referralAction(r, "bonus")}
                          >
                            Pay bonus
                          </Button>
                        ) : null}
                      </span>
                    </td>
                  </tr>
                ))}
                {referrals.length === 0 ? (
                  <tr>
                    <td colSpan={7} className="p-6 text-center text-[12px] text-muted-foreground">
                      Nobody has referred anyone.
                    </td>
                  </tr>
                ) : null}
              </tbody>
            </table>
          </div>
        </TabsContent>

        {/* ---------------- rounds ---------------- */}

        <TabsContent value="rounds" className="pt-3">
          <p className="mb-2 text-[12px] text-muted-foreground">
            A round without a list of what it assesses is a conversation, and two panellists come
            out of it having judged different things. These skills are what the scorecard is
            built from.
          </p>
          <div className="grid gap-3 md:grid-cols-2 xl:grid-cols-3">
            {rounds.map((r) => (
              <div key={r.id} className="rounded border p-3">
                <div className="flex items-baseline justify-between gap-2">
                  <h3 className="text-[13px] font-medium">{r.name}</h3>
                  <span className="text-[11px] text-muted-foreground">
                    pass at {r.passingScore}
                  </span>
                </div>
                <ul className="mt-1.5 space-y-0.5">
                  {r.skills.map((s) => (
                    <li key={s.id} className="text-[12px] text-muted-foreground">
                      {s.name}
                    </li>
                  ))}
                </ul>
              </div>
            ))}
          </div>
        </TabsContent>
      </Tabs>

      {/* ---------------- raise ---------------- */}

      <Sheet open={raiseOpen} onOpenChange={setRaiseOpen}>
        <SheetContent className="w-full overflow-y-auto sm:max-w-md">
          <SheetHeader>
            <SheetTitle>Ask for a role</SheetTitle>
            <SheetDescription>
              Checked against the staffing plan covering the date it is needed by. A replacement
              is not counted against it — the post was already budgeted.
            </SheetDescription>
          </SheetHeader>
          <div className="space-y-3 px-4 pb-8">
            <div className="space-y-1">
              <Label className="text-[12px]">Role</Label>
              <Input
                className="h-9"
                placeholder="Helper"
                value={raiseForm.position}
                onChange={(e) => setRaiseForm({ ...raiseForm, position: e.target.value })}
              />
            </div>
            <div className="grid grid-cols-2 gap-2">
              <div className="space-y-1">
                <Label className="text-[12px]">Department</Label>
                <select
                  className="h-9 w-full rounded-md border bg-background px-2 text-[13px]"
                  value={raiseForm.departmentId}
                  onChange={(e) =>
                    setRaiseForm({ ...raiseForm, departmentId: e.target.value })
                  }
                >
                  <option value="">—</option>
                  {departments.map((d) => (
                    <option key={d.id} value={d.id}>
                      {d.name}
                    </option>
                  ))}
                </select>
              </div>
              <div className="space-y-1">
                <Label className="text-[12px]">How many</Label>
                <Input
                  className="h-9 tabular-nums"
                  inputMode="numeric"
                  value={raiseForm.headcount}
                  onChange={(e) => setRaiseForm({ ...raiseForm, headcount: e.target.value })}
                />
              </div>
            </div>

            <div className="grid grid-cols-[1fr_auto] items-center gap-3 border-b py-2">
              <div>
                <Label className="text-[13px] font-normal">Replacing somebody</Label>
                <p className="text-[11px] leading-snug text-muted-foreground">
                  Does not draw against the plan.
                </p>
              </div>
              <Switch
                checked={raiseForm.isReplacement}
                onCheckedChange={(v) => setRaiseForm({ ...raiseForm, isReplacement: v })}
              />
            </div>
            {raiseForm.isReplacement ? (
              <div className="space-y-1">
                <Label className="text-[12px]">Who is leaving</Label>
                <select
                  className="h-9 w-full rounded-md border bg-background px-2 text-[13px]"
                  value={raiseForm.replacingEmployeeId}
                  onChange={(e) =>
                    setRaiseForm({ ...raiseForm, replacingEmployeeId: e.target.value })
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
            ) : null}

            <div className="space-y-1">
              <Label className="text-[12px]">Asked by</Label>
              <select
                className="h-9 w-full rounded-md border bg-background px-2 text-[13px]"
                value={raiseForm.requestedByEmployeeId}
                onChange={(e) =>
                  setRaiseForm({ ...raiseForm, requestedByEmployeeId: e.target.value })
                }
              >
                <option value="">—</option>
                {employees.map((e) => (
                  <option key={e.id} value={e.id}>
                    {e.name}
                  </option>
                ))}
              </select>
            </div>

            <div className="grid grid-cols-3 gap-2">
              <div className="space-y-1">
                <Label className="text-[12px]">Needed by</Label>
                <Input
                  type="date"
                  className="h-9"
                  value={raiseForm.requiredBy}
                  onChange={(e) => setRaiseForm({ ...raiseForm, requiredBy: e.target.value })}
                />
              </div>
              <div className="space-y-1">
                <Label className="text-[12px]">Salary from</Label>
                <Input
                  className="h-9 tabular-nums"
                  inputMode="decimal"
                  value={raiseForm.salaryMin}
                  onChange={(e) => setRaiseForm({ ...raiseForm, salaryMin: e.target.value })}
                />
              </div>
              <div className="space-y-1">
                <Label className="text-[12px]">to</Label>
                <Input
                  className="h-9 tabular-nums"
                  inputMode="decimal"
                  value={raiseForm.salaryMax}
                  onChange={(e) => setRaiseForm({ ...raiseForm, salaryMax: e.target.value })}
                />
              </div>
            </div>

            <div className="space-y-1">
              <Label className="text-[12px]">Why</Label>
              <Input
                className="h-9"
                placeholder="Season crew"
                value={raiseForm.justification}
                onChange={(e) =>
                  setRaiseForm({ ...raiseForm, justification: e.target.value })
                }
              />
            </div>

            <Button className="w-full" disabled={busy} onClick={() => void raise()}>
              {busy ? "Raising…" : "Raise for approval"}
            </Button>
          </div>
        </SheetContent>
      </Sheet>

      {/* ---------------- refer ---------------- */}

      <Sheet open={referOpen} onOpenChange={setReferOpen}>
        <SheetContent className="w-full overflow-y-auto sm:max-w-md">
          <SheetHeader>
            <SheetTitle>Refer somebody</SheetTitle>
            <SheetDescription>
              How most crew hiring actually happens. The bonus is held until they have stayed.
            </SheetDescription>
          </SheetHeader>
          <div className="space-y-3 px-4 pb-8">
            <div className="space-y-1">
              <Label className="text-[12px]">Referred by</Label>
              <select
                className="h-9 w-full rounded-md border bg-background px-2 text-[13px]"
                value={referForm.referrerEmployeeId}
                onChange={(e) =>
                  setReferForm({ ...referForm, referrerEmployeeId: e.target.value })
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
            <div className="space-y-1">
              <Label className="text-[12px]">Who they are putting forward</Label>
              <Input
                className="h-9"
                value={referForm.candidateName}
                onChange={(e) => setReferForm({ ...referForm, candidateName: e.target.value })}
              />
            </div>
            <div className="grid grid-cols-2 gap-2">
              <div className="space-y-1">
                <Label className="text-[12px]">Phone</Label>
                <Input
                  className="h-9"
                  value={referForm.phone}
                  onChange={(e) => setReferForm({ ...referForm, phone: e.target.value })}
                />
              </div>
              <div className="space-y-1">
                <Label className="text-[12px]">For the role of</Label>
                <Input
                  className="h-9"
                  placeholder="Carpenter"
                  value={referForm.position}
                  onChange={(e) => setReferForm({ ...referForm, position: e.target.value })}
                />
              </div>
            </div>
            <div className="grid grid-cols-2 gap-2">
              <div className="space-y-1">
                <Label className="text-[12px]">Bonus</Label>
                <Input
                  className="h-9 tabular-nums"
                  inputMode="decimal"
                  value={referForm.bonusAmount}
                  onChange={(e) => setReferForm({ ...referForm, bonusAmount: e.target.value })}
                />
              </div>
              <div className="space-y-1">
                <Label className="text-[12px]">Held for (months)</Label>
                <Input
                  className="h-9 tabular-nums"
                  inputMode="numeric"
                  value={referForm.retentionMonths}
                  onChange={(e) =>
                    setReferForm({ ...referForm, retentionMonths: e.target.value })
                  }
                />
              </div>
            </div>
            <div className="space-y-1">
              <Label className="text-[12px]">Notes</Label>
              <Input
                className="h-9"
                placeholder="Worked with him at the Mehta wedding"
                value={referForm.notes}
                onChange={(e) => setReferForm({ ...referForm, notes: e.target.value })}
              />
            </div>

            {Number(referForm.retentionMonths) === 0 ? (
              <p className="flex items-start gap-1.5 rounded border border-amber-500/40 bg-amber-500/5 p-2 text-[11.5px]">
                <AlertTriangle className="mt-0.5 size-3.5 shrink-0 text-amber-600" />
                Paying on joining is what a company does when it is desperate and regrets when
                the referral leaves in six weeks.
              </p>
            ) : null}

            <Button className="w-full" disabled={busy} onClick={() => void refer()}>
              {busy ? "Recording…" : "Record referral"}
            </Button>
          </div>
        </SheetContent>
      </Sheet>
    </PagePanel>
  );
}
