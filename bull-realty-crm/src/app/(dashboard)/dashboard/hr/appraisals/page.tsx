"use client";

import * as React from "react";
import { Goal as GoalIcon, MessageSquare } from "lucide-react";
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
import {
  talentApi,
  FEEDBACK_RELATIONSHIPS,
  type HrAppraisalDetail,
  type HrAppraisalTemplate,
  type HrFeedback,
} from "@/lib/hr-talent-api";

interface Cycle {
  id: number;
  name: string;
  fromDate: string;
  toDate: string;
  status: string;
}

interface AppraisalRow {
  id: number;
  employeeName: string;
  selfScore: number | null;
  managerScore: number | null;
  rating: string | null;
  status: string;
}

/** One to five, as buttons — a number box invites a 7. */
function ScorePicker({
  value,
  onPick,
  disabled,
}: {
  value: number | null;
  onPick: (score: number) => void;
  disabled?: boolean;
}) {
  return (
    <span className="flex gap-0.5">
      {[1, 2, 3, 4, 5].map((n) => (
        <button
          key={n}
          type="button"
          disabled={disabled}
          onClick={() => onPick(n)}
          className={
            "size-6 rounded border text-[11px] tabular-nums transition-colors " +
            (value === n
              ? "border-foreground bg-foreground text-background"
              : "hover:bg-muted disabled:opacity-40")
          }
          aria-label={`Score ${n}`}
        >
          {n}
        </button>
      ))}
    </span>
  );
}

/**
 * Appraisals built on a template, and the feedback gathered around them.
 *
 * Self and manager scores sit side by side rather than one replacing the other,
 * because the gap between them is the conversation an appraisal exists to have.
 */
export default function HrAppraisalsPage() {
  const [templates, setTemplates] = React.useState<HrAppraisalTemplate[]>([]);
  const [cycles, setCycles] = React.useState<Cycle[]>([]);
  const [rows, setRows] = React.useState<AppraisalRow[]>([]);
  const [employees, setEmployees] = React.useState<{ id: number; name: string }[]>([]);
  const [busy, setBusy] = React.useState(false);

  const [open, setOpen] = React.useState<HrAppraisalDetail | null>(null);
  const [asManager, setAsManager] = React.useState(true);

  const [startOpen, setStartOpen] = React.useState(false);
  const [startForm, setStartForm] = React.useState({
    cycleId: "",
    employeeId: "",
    appraisalTemplateId: "",
  });

  const [feedbackOpen, setFeedbackOpen] = React.useState(false);
  const [feedbackFor, setFeedbackFor] = React.useState<HrAppraisalDetail | null>(null);
  const [feedbackList, setFeedbackList] = React.useState<HrFeedback[]>([]);
  const [feedbackForm, setFeedbackForm] = React.useState({
    givenByEmployeeId: "",
    relationship: "Peer",
    rating: "4",
    whatWorksWell: "",
    whatCouldImprove: "",
    isAnonymous: true,
  });

  const load = React.useCallback(() => {
    talentApi.templates().then(setTemplates).catch(() => setTemplates([]));
    apiRequest<Cycle[]>("/api/hr/cycles", { method: "GET", auth: true })
      .then(setCycles)
      .catch(() => setCycles([]));
    apiRequest<AppraisalRow[]>("/api/hr/appraisals", { method: "GET", auth: true })
      .then(setRows)
      .catch(() => setRows([]));
  }, [setTemplates, setCycles, setRows]);

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

  function openAppraisal(id: number) {
    talentApi
      .appraisal(id)
      .then(setOpen)
      .catch((error) =>
        toast.error(error instanceof ApiError ? error.message : "Could not open that.")
      );
  }

  async function start() {
    setBusy(true);
    try {
      const created = await talentApi.startAppraisal({
        cycleId: Number(startForm.cycleId),
        employeeId: Number(startForm.employeeId),
        appraisalTemplateId: Number(startForm.appraisalTemplateId),
      });
      toast.success("Started. The template's responsibilities are on it.");
      setStartOpen(false);
      setOpen(created);
      load();
    } catch (error) {
      toast.error(error instanceof ApiError ? error.message : "Could not start that.");
    } finally {
      setBusy(false);
    }
  }

  async function score(kraId: number, value: number) {
    if (!open) return;
    try {
      const updated = await talentApi.score(open.id, asManager, {
        scores: [{ appraisalKraId: kraId, score: value, comment: null }],
        comments: null,
      });
      setOpen(updated);
      load();
    } catch (error) {
      toast.error(error instanceof ApiError ? error.message : "Could not score that.");
    }
  }

  async function close() {
    if (!open) return;
    const rating = window.prompt("Overall rating (optional)")?.trim() || undefined;
    try {
      const updated = await talentApi.closeAppraisal(open.id, rating);
      setOpen(updated);
      toast.success("Closed.");
      load();
    } catch (error) {
      toast.error(error instanceof ApiError ? error.message : "Could not close it.");
    }
  }

  function openFeedback(appraisal: HrAppraisalDetail) {
    setFeedbackFor(appraisal);
    setFeedbackList([]);
    setFeedbackOpen(true);
    talentApi
      .feedback(appraisal.employeeId, appraisal.cycleId, true)
      .then(setFeedbackList)
      .catch(() => setFeedbackList([]));
  }

  async function giveFeedback() {
    if (!feedbackFor) return;
    setBusy(true);
    try {
      await talentApi.giveFeedback({
        employeeId: feedbackFor.employeeId,
        givenByEmployeeId: Number(feedbackForm.givenByEmployeeId),
        appraisalCycleId: feedbackFor.cycleId,
        relationship: feedbackForm.relationship,
        rating: Number(feedbackForm.rating),
        whatWorksWell: feedbackForm.whatWorksWell || null,
        whatCouldImprove: feedbackForm.whatCouldImprove || null,
        isAnonymous: feedbackForm.isAnonymous,
      });
      toast.success("Recorded. It is not shared until you release it.");
      openFeedback(feedbackFor);
      openAppraisal(feedbackFor.id);
    } catch (error) {
      toast.error(error instanceof ApiError ? error.message : "Could not record that.");
    } finally {
      setBusy(false);
    }
  }

  async function share() {
    if (!feedbackFor) return;
    try {
      const count = await talentApi.shareFeedback(feedbackFor.employeeId, feedbackFor.cycleId);
      toast.success(`Released ${count} to ${feedbackFor.employeeName}.`);
      openFeedback(feedbackFor);
    } catch (error) {
      toast.error(error instanceof ApiError ? error.message : "Could not release it.");
    }
  }

  return (
    <PagePanel
      icon={GoalIcon}
      title="Appraisals"
      hint="Weighted responsibilities from a template, scored by both sides. The gap between the two is the conversation."
      actions={
        <Button size="sm" className="h-8" onClick={() => setStartOpen(true)}>
          Start an appraisal
        </Button>
      }
    >
      <Tabs defaultValue="appraisals">
        <TabsList>
          <TabsTrigger value="appraisals">
            Appraisals
            <span className="ml-1.5 text-[11px] text-muted-foreground">{rows.length}</span>
          </TabsTrigger>
          <TabsTrigger value="templates">
            Templates
            <span className="ml-1.5 text-[11px] text-muted-foreground">{templates.length}</span>
          </TabsTrigger>
        </TabsList>

        <TabsContent value="appraisals" className="pt-3">
          <div className="overflow-x-auto rounded border">
            <table className="w-full text-left text-[13px]">
              <thead className="bg-muted/50 text-[11px] uppercase text-muted-foreground">
                <tr>
                  <th className="p-2 font-medium">Employee</th>
                  <th className="p-2 text-right font-medium">Self</th>
                  <th className="p-2 text-right font-medium">Manager</th>
                  <th className="p-2 font-medium">Rating</th>
                  <th className="p-2 font-medium">Status</th>
                </tr>
              </thead>
              <tbody>
                {rows.map((r) => (
                  <tr
                    key={r.id}
                    className="cursor-pointer border-t hover:bg-muted/40"
                    onClick={() => openAppraisal(r.id)}
                  >
                    <td className="p-2">{r.employeeName}</td>
                    <td className="p-2 text-right tabular-nums">{r.selfScore ?? "—"}</td>
                    <td className="p-2 text-right font-medium tabular-nums">
                      {r.managerScore ?? "—"}
                    </td>
                    <td className="p-2 text-muted-foreground">{r.rating ?? "—"}</td>
                    <td className="p-2">
                      <Badge variant="secondary" className="font-normal">
                        {r.status}
                      </Badge>
                    </td>
                  </tr>
                ))}
                {rows.length === 0 ? (
                  <tr>
                    <td colSpan={5} className="p-6 text-center text-[12px] text-muted-foreground">
                      No appraisals yet.
                    </td>
                  </tr>
                ) : null}
              </tbody>
            </table>
          </div>
        </TabsContent>

        <TabsContent value="templates" className="pt-3">
          <div className="grid gap-3 md:grid-cols-2">
            {templates.map((t) => (
              <div key={t.id} className="rounded border p-3">
                <div className="flex items-baseline justify-between gap-2">
                  <h3 className="text-[13px] font-medium">{t.name}</h3>
                  <span className="text-[11px] text-muted-foreground">
                    {t.appraisalsUsing} in use
                  </span>
                </div>
                {t.notes ? (
                  <p className="mt-0.5 text-[11.5px] leading-snug text-muted-foreground">
                    {t.notes}
                  </p>
                ) : null}
                <table className="mt-2 w-full text-[12px]">
                  <tbody>
                    {t.kras.map((k) => (
                      <tr key={k.id} className="border-b last:border-0">
                        <td className="py-1">
                          {k.title}
                          {k.description ? (
                            <div className="text-[11px] leading-snug text-muted-foreground">
                              {k.description}
                            </div>
                          ) : null}
                        </td>
                        <td className="w-12 py-1 text-right tabular-nums">{k.weight}%</td>
                      </tr>
                    ))}
                    <tr className="border-t">
                      <td className="pt-1.5 font-medium">Total</td>
                      <td className="pt-1.5 text-right font-medium tabular-nums">
                        {t.totalWeight}%
                      </td>
                    </tr>
                  </tbody>
                </table>
              </div>
            ))}
          </div>
        </TabsContent>
      </Tabs>

      {/* ---------------- the appraisal ---------------- */}

      <Sheet
        open={open !== null}
        onOpenChange={(isOpen) => {
          if (!isOpen) setOpen(null);
        }}
      >
        <SheetContent className="w-full overflow-y-auto sm:max-w-xl">
          <SheetHeader>
            <SheetTitle className="flex items-center gap-2">
              {open?.employeeName}
              <Badge variant="secondary" className="font-normal">
                {open?.status}
              </Badge>
            </SheetTitle>
            <SheetDescription>
              {open?.cycleName}
              {open?.templateName ? ` · ${open.templateName}` : ""}
            </SheetDescription>
          </SheetHeader>

          {open ? (
            <div className="space-y-4 px-4 pb-8">
              <div className="flex flex-wrap items-center gap-x-5 gap-y-1 rounded-md bg-muted/50 px-3 py-2 text-[12px]">
                <span>
                  <span className="text-muted-foreground">Self </span>
                  <span className="font-medium tabular-nums">{open.selfScore ?? "—"}</span>
                </span>
                <span>
                  <span className="text-muted-foreground">Manager </span>
                  <span className="font-medium tabular-nums">{open.managerScore ?? "—"}</span>
                </span>
                {open.feedbackCount > 0 ? (
                  <span>
                    <span className="text-muted-foreground">Feedback </span>
                    <span className="font-medium tabular-nums">
                      {open.feedbackAverage ?? "—"}
                    </span>
                    <span className="ml-1 text-muted-foreground">
                      from {open.feedbackCount}
                    </span>
                  </span>
                ) : null}
              </div>

              <div className="flex items-center justify-between gap-3 rounded-md border px-3 py-2">
                <div>
                  <Label className="text-[13px] font-normal">Scoring as the manager</Label>
                  <p className="text-[11px] leading-snug text-muted-foreground">
                    Turn off to record the self assessment. One never overwrites the other.
                  </p>
                </div>
                <Switch
                  checked={asManager}
                  onCheckedChange={setAsManager}
                  disabled={open.status === "Closed"}
                />
              </div>

              <div className="space-y-2">
                {open.kras.map((k) => (
                  <div key={k.id} className="rounded border p-2.5">
                    <div className="flex items-baseline justify-between gap-2">
                      <span className="text-[13px] font-medium">{k.title}</span>
                      <span className="text-[11px] text-muted-foreground">{k.weight}%</span>
                    </div>
                    <div className="mt-1.5 flex flex-wrap items-center gap-x-4 gap-y-1.5">
                      <span className="flex items-center gap-1.5">
                        <span className="w-14 text-[11px] text-muted-foreground">Self</span>
                        <ScorePicker
                          value={k.selfScore}
                          disabled={asManager || open.status === "Closed"}
                          onPick={(v) => void score(k.id, v)}
                        />
                      </span>
                      <span className="flex items-center gap-1.5">
                        <span className="w-14 text-[11px] text-muted-foreground">Manager</span>
                        <ScorePicker
                          value={k.managerScore}
                          disabled={!asManager || open.status === "Closed"}
                          onPick={(v) => void score(k.id, v)}
                        />
                      </span>
                      {k.gap !== null ? (
                        <span
                          className={
                            "text-[11.5px] " +
                            (k.gap < 0
                              ? "text-amber-700 dark:text-amber-400"
                              : k.gap > 0
                                ? "text-emerald-700 dark:text-emerald-400"
                                : "text-muted-foreground")
                          }
                        >
                          {k.gap === 0
                            ? "agreed"
                            : k.gap > 0
                              ? `manager rates ${k.gap} higher`
                              : `manager rates ${Math.abs(k.gap)} lower`}
                        </span>
                      ) : null}
                    </div>
                  </div>
                ))}
              </div>

              <div className="flex gap-1.5">
                <Button
                  variant="outline"
                  size="sm"
                  className="flex-1"
                  onClick={() => openFeedback(open)}
                >
                  <MessageSquare className="mr-1.5 size-3.5" />
                  Feedback ({open.feedbackCount})
                </Button>
                <Button
                  size="sm"
                  className="flex-1"
                  disabled={open.status === "Closed"}
                  onClick={() => void close()}
                >
                  {open.status === "Closed" ? "Closed" : "Close appraisal"}
                </Button>
              </div>
            </div>
          ) : null}
        </SheetContent>
      </Sheet>

      {/* ---------------- feedback ---------------- */}

      <Sheet open={feedbackOpen} onOpenChange={setFeedbackOpen}>
        <SheetContent className="w-full overflow-y-auto sm:max-w-md">
          <SheetHeader>
            <SheetTitle>Feedback on {feedbackFor?.employeeName}</SheetTitle>
            <SheetDescription>
              Held back until somebody releases it — 360 feedback arriving unmediated is how it
              stops being given.
            </SheetDescription>
          </SheetHeader>
          <div className="space-y-4 px-4 pb-8">
            <div className="space-y-2">
              {feedbackList.map((f) => (
                <div key={f.id} className="rounded border p-2.5 text-[13px]">
                  <div className="flex items-baseline justify-between gap-2">
                    <span className="font-medium">{f.givenByName}</span>
                    <span className="flex items-center gap-1.5">
                      <Badge variant="secondary" className="font-normal">
                        {f.relationship}
                      </Badge>
                      {f.rating !== null ? (
                        <span className="tabular-nums">{f.rating}</span>
                      ) : null}
                    </span>
                  </div>
                  {f.whatWorksWell ? (
                    <p className="mt-1 text-[12px]">
                      <span className="text-muted-foreground">Works well: </span>
                      {f.whatWorksWell}
                    </p>
                  ) : null}
                  {f.whatCouldImprove ? (
                    <p className="mt-0.5 text-[12px]">
                      <span className="text-muted-foreground">Could improve: </span>
                      {f.whatCouldImprove}
                    </p>
                  ) : null}
                  {!f.sharedWithEmployee ? (
                    <p className="mt-1 text-[11px] text-amber-700 dark:text-amber-400">
                      Not yet released
                    </p>
                  ) : null}
                </div>
              ))}
              {feedbackList.length === 0 ? (
                <p className="rounded border border-dashed p-4 text-center text-[12px] text-muted-foreground">
                  Nothing gathered yet.
                </p>
              ) : null}
            </div>

            {feedbackList.some((f) => !f.sharedWithEmployee) ? (
              <Button variant="outline" size="sm" className="w-full" onClick={() => void share()}>
                Release to {feedbackFor?.employeeName}
              </Button>
            ) : null}

            <div className="space-y-2 rounded border p-3">
              <h3 className="text-[12px] font-medium uppercase text-muted-foreground">
                Add feedback
              </h3>
              <div className="grid grid-cols-2 gap-2">
                <div className="space-y-1">
                  <Label className="text-[12px]">From</Label>
                  <select
                    className="h-9 w-full rounded-md border bg-background px-2 text-[13px]"
                    value={feedbackForm.givenByEmployeeId}
                    onChange={(e) =>
                      setFeedbackForm({ ...feedbackForm, givenByEmployeeId: e.target.value })
                    }
                  >
                    <option value="">Pick someone…</option>
                    {employees
                      .filter((e) => e.id !== feedbackFor?.employeeId)
                      .map((e) => (
                        <option key={e.id} value={e.id}>
                          {e.name}
                        </option>
                      ))}
                  </select>
                </div>
                <div className="space-y-1">
                  <Label className="text-[12px]">Relationship</Label>
                  <select
                    className="h-9 w-full rounded-md border bg-background px-2 text-[13px]"
                    value={feedbackForm.relationship}
                    onChange={(e) =>
                      setFeedbackForm({ ...feedbackForm, relationship: e.target.value })
                    }
                  >
                    {FEEDBACK_RELATIONSHIPS.map((r) => (
                      <option key={r} value={r}>
                        {r}
                      </option>
                    ))}
                  </select>
                </div>
              </div>
              <div className="space-y-1">
                <Label className="text-[12px]">Rating</Label>
                <Input
                  className="h-9 tabular-nums"
                  inputMode="numeric"
                  value={feedbackForm.rating}
                  onChange={(e) => setFeedbackForm({ ...feedbackForm, rating: e.target.value })}
                />
              </div>
              <div className="space-y-1">
                <Label className="text-[12px]">What works well</Label>
                <Input
                  className="h-9"
                  value={feedbackForm.whatWorksWell}
                  onChange={(e) =>
                    setFeedbackForm({ ...feedbackForm, whatWorksWell: e.target.value })
                  }
                />
              </div>
              <div className="space-y-1">
                <Label className="text-[12px]">What could improve</Label>
                <Input
                  className="h-9"
                  value={feedbackForm.whatCouldImprove}
                  onChange={(e) =>
                    setFeedbackForm({ ...feedbackForm, whatCouldImprove: e.target.value })
                  }
                />
              </div>
              <div className="flex items-center justify-between gap-3 py-1">
                <Label className="text-[13px] font-normal">Anonymous</Label>
                <Switch
                  checked={feedbackForm.isAnonymous}
                  onCheckedChange={(v) =>
                    setFeedbackForm({ ...feedbackForm, isAnonymous: v })
                  }
                />
              </div>
              <Button
                size="sm"
                className="w-full"
                disabled={busy}
                onClick={() => void giveFeedback()}
              >
                {busy ? "Recording…" : "Record"}
              </Button>
            </div>
          </div>
        </SheetContent>
      </Sheet>

      {/* ---------------- start ---------------- */}

      <Sheet open={startOpen} onOpenChange={setStartOpen}>
        <SheetContent className="w-full sm:max-w-md">
          <SheetHeader>
            <SheetTitle>Start an appraisal</SheetTitle>
            <SheetDescription>
              The template&rsquo;s responsibilities are copied onto it, so a template edited next
              year does not restate this year&rsquo;s rating.
            </SheetDescription>
          </SheetHeader>
          <div className="space-y-3 px-4 pb-8">
            <div className="space-y-1">
              <Label className="text-[12px]">Cycle</Label>
              <select
                className="h-9 w-full rounded-md border bg-background px-2 text-[13px]"
                value={startForm.cycleId}
                onChange={(e) => setStartForm({ ...startForm, cycleId: e.target.value })}
              >
                <option value="">Pick a cycle…</option>
                {cycles.map((c) => (
                  <option key={c.id} value={c.id}>
                    {c.name}
                  </option>
                ))}
              </select>
            </div>
            <div className="space-y-1">
              <Label className="text-[12px]">Employee</Label>
              <select
                className="h-9 w-full rounded-md border bg-background px-2 text-[13px]"
                value={startForm.employeeId}
                onChange={(e) => setStartForm({ ...startForm, employeeId: e.target.value })}
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
              <Label className="text-[12px]">Template</Label>
              <select
                className="h-9 w-full rounded-md border bg-background px-2 text-[13px]"
                value={startForm.appraisalTemplateId}
                onChange={(e) =>
                  setStartForm({ ...startForm, appraisalTemplateId: e.target.value })
                }
              >
                <option value="">Pick a template…</option>
                {templates
                  .filter((t) => t.isActive)
                  .map((t) => (
                    <option key={t.id} value={t.id}>
                      {t.name}
                    </option>
                  ))}
              </select>
            </div>
            <Button className="w-full" disabled={busy} onClick={() => void start()}>
              {busy ? "Starting…" : "Start"}
            </Button>
          </div>
        </SheetContent>
      </Sheet>
    </PagePanel>
  );
}
