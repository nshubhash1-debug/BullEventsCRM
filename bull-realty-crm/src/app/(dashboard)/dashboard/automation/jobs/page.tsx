"use client";

import * as React from "react";
import { CircleCheck, CircleX, Loader2, Play, Plus, Repeat } from "lucide-react";
import { toast } from "sonner";

import { PagePanel } from "@/components/shell/page-panel";
import { CrmLoadingState } from "@/components/shell/crm-loader";
import { MetricStrip, Pill, TONE_TEXT, type Metric } from "@/components/crm/metrics";
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
import { automationApi, type SaveScheduledJob, type ScheduledJob } from "@/lib/automation-api";
import { cn } from "@/lib/utils";

const KIND_LABELS: Record<string, string> = {
  EscalationSweep: "Escalation sweep",
  InterestAccrual: "Penal interest accrual",
  UsageSnapshot: "Usage snapshot",
  DemandReminder: "Demand reminders",
  DataExport: "Scheduled export",
};

const PRESETS = [
  { cron: "*/15 * * * *", label: "Every 15 minutes" },
  { cron: "0 * * * *", label: "Hourly" },
  { cron: "0 */4 * * *", label: "Every 4 hours" },
  { cron: "0 2 * * *", label: "Nightly at 02:00" },
  { cron: "0 9 * * 1", label: "Mondays at 09:00" },
];

/**
 * Scheduled jobs.
 *
 * The outcome of the last run is shown on the row rather than left in a log,
 * because the question somebody opens this screen with is "did it run, and did
 * it work" — and an answer that requires reading a log file on a server is not
 * an answer.
 *
 * Jobs are picked from a fixed list of things this system knows how to do. A
 * scheduler that can run anything is a remote execution endpoint with a
 * friendly name on it.
 */
export default function ScheduledJobsPage() {
  const [jobs, setJobs] = React.useState<ScheduledJob[] | null>(null);
  const [editing, setEditing] = React.useState<ScheduledJob | null>(null);
  const [creating, setCreating] = React.useState(false);
  const [busy, setBusy] = React.useState<number | null>(null);

  const load = React.useCallback(() => {
    automationApi
      .jobs()
      .then(setJobs)
      .catch((error: unknown) => {
        toast.error("Could not load scheduled jobs", {
          description: error instanceof ApiError ? error.message : "Network error.",
        });
        setJobs([]);
      });
  }, []);

  React.useEffect(load, [load]);

  async function act(id: number, work: () => Promise<unknown>, done: string) {
    setBusy(id);
    try {
      await work();
      toast.success(done);
      load();
    } catch (error) {
      toast.error("Could not run the job", {
        description: error instanceof ApiError ? error.message : "Network error.",
      });
    } finally {
      setBusy(null);
    }
  }

  const metrics: Metric[] = React.useMemo(() => {
    const list = jobs ?? [];
    const failing = list.filter((j) => j.lastOutcome === "Failed").length;

    return [
      { label: "Jobs", value: list.length, icon: Repeat },
      { label: "Running", value: list.filter((j) => j.isActive).length, tone: "success" },
      {
        label: "Failing",
        value: failing,
        tone: failing > 0 ? "danger" : "success",
        icon: CircleX,
      },
      {
        label: "Runs",
        value: list.reduce((sum, j) => sum + j.runCount, 0),
        hint: "Since each job was created",
      },
    ];
  }, [jobs]);

  if (!jobs) {
    return (
      <PagePanel icon={Repeat} title="Scheduled jobs">
        <CrmLoadingState label="Loading scheduled jobs" />
      </PagePanel>
    );
  }

  return (
    <PagePanel
      icon={Repeat}
      title="Scheduled jobs"
      hint="Recurring work, when it next runs, and what happened last time."
      actions={
        <Button size="sm" className="gap-1.5" onClick={() => setCreating(true)}>
          <Plus className="size-3.5" />
          New job
        </Button>
      }
    >
      <div className="flex flex-col gap-3">
        <MetricStrip metrics={metrics} />

        <RuleCard icon={Repeat} title="Jobs" count={jobs.length}>
          {jobs.length === 0 ? (
            <RuleEmpty
              title="No scheduled job yet."
              meaning="Escalation rules only fire when something sweeps for them. Add an escalation sweep to have them checked automatically."
              action={
                <Button size="sm" variant="outline" onClick={() => setCreating(true)}>
                  Schedule the first job
                </Button>
              }
            />
          ) : (
            <div className="overflow-x-auto">
              <table className="w-full min-w-[860px] text-[12.5px]">
                <thead>
                  <tr className="border-b text-[10.5px] tracking-wide text-muted-foreground uppercase">
                    <th className="px-4 py-2 text-left font-medium">Job</th>
                    <th className="px-4 py-2 text-left font-medium">Schedule</th>
                    <th className="px-4 py-2 text-left font-medium">Last run</th>
                    <th className="px-4 py-2 text-left font-medium">Next</th>
                    <th className="px-4 py-2" />
                  </tr>
                </thead>

                <tbody>
                  {jobs.map((job) => (
                    <tr
                      key={job.id}
                      className={cn("border-b align-top last:border-0", !job.isActive && "opacity-55")}
                    >
                      <td className="px-4 py-2.5">
                        <p className="font-medium">{job.name}</p>
                        <p className="text-[11px] text-muted-foreground">
                          {KIND_LABELS[job.kind] ?? job.kind}
                        </p>
                      </td>

                      <td className="px-4 py-2.5">
                        {job.schedule}
                        <p className="font-mono text-[11px] text-muted-foreground">{job.cron}</p>
                      </td>

                      <td className="px-4 py-2.5">
                        {job.lastRunAt ? (
                          <>
                            <p className="flex items-center gap-1.5">
                              {job.lastOutcome === "Failed" ? (
                                <CircleX className={cn("size-3.5", TONE_TEXT.danger)} />
                              ) : job.lastOutcome === "Skipped" ? (
                                <CircleCheck className={cn("size-3.5", TONE_TEXT.warning)} />
                              ) : (
                                <CircleCheck className={cn("size-3.5", TONE_TEXT.success)} />
                              )}
                              {stamp(job.lastRunAt)}
                              <span className="text-muted-foreground tabular-nums">
                                {job.lastDurationMs}ms
                              </span>
                            </p>
                            {job.lastMessage ? (
                              <p className="max-w-[280px] text-[11px] text-muted-foreground">
                                {job.lastMessage}
                              </p>
                            ) : null}
                          </>
                        ) : (
                          <span className="text-muted-foreground">Never</span>
                        )}
                      </td>

                      <td className="px-4 py-2.5 whitespace-nowrap">
                        {job.isActive && job.nextRunAt ? (
                          stamp(job.nextRunAt)
                        ) : (
                          <span className="text-muted-foreground">—</span>
                        )}
                        {job.failureCount > 0 ? (
                          <p className="mt-0.5">
                            <Pill tone="danger">{job.failureCount} failures</Pill>
                          </p>
                        ) : null}
                      </td>

                      <td className="px-4 py-2.5 text-right whitespace-nowrap">
                        {busy === job.id ? (
                          <Loader2 className="ml-auto size-3.5 animate-spin text-muted-foreground" />
                        ) : (
                          <div className="flex items-center justify-end gap-1.5">
                            <Switch
                              checked={job.isActive}
                              onCheckedChange={() =>
                                act(
                                  job.id,
                                  () =>
                                    automationApi.updateJob(job.id, {
                                      ...toInput(job),
                                      isActive: !job.isActive,
                                    }),
                                  job.isActive ? "Job paused" : "Job resumed"
                                )
                              }
                            />
                            <Button
                              size="sm"
                              variant="outline"
                              className="h-7 gap-1 px-2 text-[11px]"
                              onClick={() =>
                                act(job.id, () => automationApi.runJob(job.id), "Job ran")
                              }
                            >
                              <Play className="size-3" />
                              Run
                            </Button>
                            <Button
                              size="sm"
                              variant="ghost"
                              className="h-7 px-2 text-[11px]"
                              onClick={() => setEditing(job)}
                            >
                              Edit
                            </Button>
                          </div>
                        )}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </RuleCard>
      </div>

      <Dialog
        open={creating || editing !== null}
        onOpenChange={(open) => {
          if (!open) {
            setCreating(false);
            setEditing(null);
          }
        }}
      >
        <JobForm
          job={editing}
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

function stamp(iso: string) {
  return new Date(iso).toLocaleString("en-IN", {
    day: "numeric",
    month: "short",
    hour: "numeric",
    minute: "2-digit",
  });
}

function toInput(job: ScheduledJob): SaveScheduledJob {
  return { name: job.name, kind: job.kind, cron: job.cron, isActive: job.isActive };
}

function JobForm({ job, onDone }: { job: ScheduledJob | null; onDone: () => void }) {
  const [name, setName] = React.useState(job?.name ?? "");
  const [kind, setKind] = React.useState(job?.kind ?? "EscalationSweep");
  const [cron, setCron] = React.useState(job?.cron ?? "0 * * * *");
  const [saving, setSaving] = React.useState(false);

  async function save() {
    setSaving(true);
    try {
      const body: SaveScheduledJob = {
        name: name || KIND_LABELS[kind] || kind,
        kind,
        cron,
        isActive: job?.isActive ?? true,
      };

      if (job) await automationApi.updateJob(job.id, body);
      else await automationApi.createJob(body);

      toast.success(job ? "Job updated" : "Job scheduled");
      onDone();
    } catch (error) {
      toast.error("Could not save the job", {
        description: error instanceof ApiError ? error.message : "Network error.",
      });
    } finally {
      setSaving(false);
    }
  }

  return (
    <DialogContent className="sm:max-w-md">
      <DialogHeader>
        <DialogTitle>{job ? "Edit job" : "Schedule a job"}</DialogTitle>
        <DialogDescription>
          Only work this system knows how to do can be scheduled.
        </DialogDescription>
      </DialogHeader>

      <div className="grid gap-3">
        <div className="grid gap-1.5">
          <Label>What to run</Label>
          <Select value={kind} onValueChange={setKind}>
            <SelectTrigger>
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              {Object.entries(KIND_LABELS).map(([value, label]) => (
                <SelectItem key={value} value={value}>
                  {label}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </div>

        <div className="grid gap-1.5">
          <Label htmlFor="job-name">Name</Label>
          <Input
            id="job-name"
            value={name}
            onChange={(e) => setName(e.target.value)}
            placeholder={KIND_LABELS[kind]}
          />
        </div>

        <div className="grid gap-1.5">
          <Label htmlFor="job-cron">Schedule</Label>
          <div className="flex flex-wrap gap-1">
            {PRESETS.map((preset) => (
              <Button
                key={preset.cron}
                type="button"
                size="sm"
                variant={cron === preset.cron ? "default" : "outline"}
                className="h-7 text-[11.5px]"
                onClick={() => setCron(preset.cron)}
              >
                {preset.label}
              </Button>
            ))}
          </div>
          <Input
            id="job-cron"
            value={cron}
            onChange={(e) => setCron(e.target.value)}
            className="font-mono text-[12px]"
          />
          <p className="text-[11.5px] text-muted-foreground">
            Standard cron: minute, hour, day, month, weekday.
          </p>
        </div>
      </div>

      <DialogFooter>
        <Button onClick={save} disabled={saving} className="gap-1.5">
          {saving ? <Loader2 className="size-4 animate-spin" /> : <Repeat className="size-4" />}
          {job ? "Save job" : "Schedule it"}
        </Button>
      </DialogFooter>
    </DialogContent>
  );
}
