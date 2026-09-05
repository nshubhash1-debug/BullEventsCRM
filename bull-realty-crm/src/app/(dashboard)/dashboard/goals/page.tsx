"use client";

import * as React from "react";
import Link from "next/link";
import {
  AlertTriangle,
  CalendarDays,
  CheckCircle2,
  LayoutDashboard,
  Loader2,
  MoreHorizontal,
  Pencil,
  Plus,
  RefreshCw,
  Target,
  Trash2,
  TrendingUp,
} from "lucide-react";
import { toast } from "sonner";

import { GoalFormDialog } from "@/components/goals/goal-form-dialog";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { formatValue } from "@/lib/dashboard/format";
import {
  HEALTH_META,
  goalsApi,
  scopeLabel,
  type Goal,
  type GoalListResponse,
  type GoalScope,
} from "@/lib/goals/api";
import { cn } from "@/lib/utils";

export default function GoalsPage() {
  const [data, setData] = React.useState<GoalListResponse | null>(null);
  const [loading, setLoading] = React.useState(true);
  const [error, setError] = React.useState<string | null>(null);
  const [reloadToken, setReloadToken] = React.useState(0);

  const [scope, setScope] = React.useState<string>("all");
  const [activeOnly, setActiveOnly] = React.useState<string>("all");
  const [editing, setEditing] = React.useState<Goal | null>(null);
  const [formOpen, setFormOpen] = React.useState(false);

  React.useEffect(() => {
    let live = true;

    goalsApi
      .list({
        scopeType: scope === "all" ? undefined : (scope as GoalScope),
        activeOnly: activeOnly === "active",
      })
      .then((result) => {
        if (!live) return;
        setData(result);
        setError(null);
      })
      .catch((cause: Error) => {
        if (live) setError(cause.message);
      })
      .finally(() => {
        if (live) setLoading(false);
      });

    return () => {
      live = false;
    };
  }, [scope, activeOnly, reloadToken]);

  function reload() {
    setReloadToken((token) => token + 1);
  }

  async function remove(goal: Goal) {
    try {
      await goalsApi.remove(goal.id);
      toast.success(`“${goal.name}” deleted`);
      reload();
    } catch (cause) {
      toast.error((cause as Error).message);
    }
  }

  const goals = data?.goals ?? [];
  const summary = data?.summary;

  return (
    <div className="flex flex-col gap-2.5">
      {/* Header */}
      <div className="overflow-hidden rounded-md border bg-card shadow-xs">
        <div className="flex flex-wrap items-center justify-between gap-2 px-3.5 py-2">
          <div className="flex items-center gap-5">
            <Link
              href="/dashboard"
              className="flex items-center gap-1.5 text-[17px] font-semibold tracking-tight text-muted-foreground transition-colors hover:text-foreground"
            >
              <LayoutDashboard className="size-[18px]" />
              Dashboard
            </Link>
            <Link
              href="/dashboard/calendar"
              className="flex items-center gap-1.5 text-[17px] font-semibold tracking-tight text-muted-foreground transition-colors hover:text-foreground"
            >
              <CalendarDays className="size-[18px]" />
              Calendar
            </Link>
            <span className="flex items-center gap-1.5 text-[17px] font-semibold tracking-tight text-primary">
              <Target className="size-[18px]" />
              Goals
            </span>
          </div>

          <Button
            size="sm"
            className="h-8"
            onClick={() => {
              setEditing(null);
              setFormOpen(true);
            }}
          >
            <Plus /> New goal
          </Button>
        </div>
      </div>

      {/* Summary + filters */}
      <div className="flex flex-wrap items-center gap-2 rounded-md border bg-card px-3 py-2 shadow-xs">
        <Select value={scope} onValueChange={setScope}>
          <SelectTrigger className="h-8 w-40 text-[13px]">
            <SelectValue />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="all">Every scope</SelectItem>
            <SelectItem value="Company">Company</SelectItem>
            <SelectItem value="Branch">Branch</SelectItem>
            <SelectItem value="User">Individual</SelectItem>
          </SelectContent>
        </Select>

        <Select value={activeOnly} onValueChange={setActiveOnly}>
          <SelectTrigger className="h-8 w-40 text-[13px]">
            <SelectValue />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="all">All periods</SelectItem>
            <SelectItem value="active">Running now</SelectItem>
          </SelectContent>
        </Select>

        {summary && summary.total > 0 ? (
          <div className="flex flex-wrap items-center gap-1.5">
            <Stat label="Achieved" value={summary.achieved} tone="good" />
            <Stat label="On track" value={summary.onTrack} />
            <Stat label="Behind" value={summary.behind} tone="warn" />
            <Stat label="Missed" value={summary.missed} tone="bad" />
            <Badge variant="outline" className="h-7 font-normal tabular-nums">
              {summary.averagePercent}% average
            </Badge>
          </div>
        ) : null}

        <Button
          variant="outline"
          size="sm"
          className="ml-auto h-8"
          onClick={reload}
        >
          <RefreshCw /> Refresh
        </Button>
      </div>

      {/* Body */}
      {loading && !data ? (
        <div className="flex items-center justify-center py-16">
          <Loader2 className="size-4 animate-spin text-muted-foreground" />
        </div>
      ) : error ? (
        <div className="flex flex-col items-center justify-center gap-2 rounded-md border border-dashed bg-card py-16 text-center">
          <AlertTriangle className="size-5 text-destructive" />
          <p className="text-sm font-medium">Could not load goals</p>
          <p className="text-[13px] text-muted-foreground">{error}</p>
        </div>
      ) : goals.length === 0 ? (
        <div className="flex flex-col items-center justify-center gap-3 rounded-md border border-dashed bg-card py-16 text-center">
          <span className="flex size-11 items-center justify-center rounded-full bg-muted text-muted-foreground">
            <Target className="size-5" />
          </span>
          <div>
            <p className="text-sm font-medium">No goals yet</p>
            <p className="text-[13px] text-muted-foreground">
              Set a target on anything the CRM measures — revenue, visits, calls
              or a conversion rate.
            </p>
          </div>
          <Button
            size="sm"
            onClick={() => {
              setEditing(null);
              setFormOpen(true);
            }}
          >
            <Plus /> Create the first goal
          </Button>
        </div>
      ) : (
        <div className="grid gap-3 md:grid-cols-2 xl:grid-cols-3">
          {goals.map((goal) => (
            <GoalCard
              key={goal.id}
              goal={goal}
              onEdit={() => {
                setEditing(goal);
                setFormOpen(true);
              }}
              onDelete={() => remove(goal)}
            />
          ))}
        </div>
      )}

      {formOpen ? (
        <GoalFormDialog
          key={editing?.id ?? "new"}
          open={formOpen}
          onOpenChange={setFormOpen}
          goal={editing}
          onSaved={reload}
        />
      ) : null}
    </div>
  );
}

/* ------------------------------------------------------------------ *
 * Card
 * ------------------------------------------------------------------ */

function GoalCard({
  goal,
  onEdit,
  onDelete,
}: {
  goal: Goal;
  onEdit: () => void;
  onDelete: () => void;
}) {
  const health = HEALTH_META[goal.health];

  // The bar is capped so an overshoot does not run off the card, but the label
  // still reports the real figure.
  const filled = Math.min(100, Math.max(0, goal.percentComplete));
  const expectedMark = Math.min(
    100,
    goal.targetValue === 0 ? 0 : (goal.expectedByNow / goal.targetValue) * 100
  );

  return (
    <article className="flex flex-col gap-3 rounded-lg border bg-card p-3.5 shadow-xs">
      <header className="flex items-start gap-2">
        <div className="min-w-0 flex-1">
          <h3 className="truncate text-[13.5px] font-semibold" title={goal.name}>
            {goal.name}
          </h3>
          <p className="truncate text-[11.5px] text-muted-foreground">
            {/* A ratio's measure label names only the numerator, which reads as
                if the goal counted records rather than divided them. */}
            {scopeLabel(goal)} ·{" "}
            {goal.isRatio ? "Conversion rate" : goal.measureLabel}
          </p>
        </div>

        <Badge
          variant="outline"
          className={cn("h-5 shrink-0 px-1.5 text-[10px] font-normal", health.className)}
        >
          {goal.health === "Achieved" ? (
            <CheckCircle2 className="mr-0.5 size-3" />
          ) : null}
          {health.label}
        </Badge>

        <DropdownMenu>
          <DropdownMenuTrigger asChild>
            <Button
              variant="ghost"
              size="icon"
              aria-label={`${goal.name} options`}
              className="size-6 shrink-0 text-muted-foreground"
            >
              <MoreHorizontal className="size-4" />
            </Button>
          </DropdownMenuTrigger>
          <DropdownMenuContent align="end" className="w-40">
            <DropdownMenuItem onClick={onEdit}>
              <Pencil /> Edit
            </DropdownMenuItem>
            <DropdownMenuSeparator />
            <DropdownMenuItem variant="destructive" onClick={onDelete}>
              <Trash2 /> Delete
            </DropdownMenuItem>
          </DropdownMenuContent>
        </DropdownMenu>
      </header>

      <div className="flex items-baseline gap-1.5">
        <span className="text-2xl leading-none font-semibold tracking-tight tabular-nums">
          {formatValue(goal.actual, goal.format)}
        </span>
        <span className="text-[12px] text-muted-foreground">
          of {formatValue(goal.targetValue, goal.format)}
        </span>
        <span
          className="ml-auto text-[13px] font-medium tabular-nums"
          style={{ color: health.color }}
        >
          {goal.percentComplete}%
        </span>
      </div>

      {/* Progress, with a tick showing where the goal should be today */}
      <div className="relative h-2 w-full overflow-hidden rounded-full bg-muted">
        <div
          className="h-full rounded-full transition-[width]"
          style={{ width: `${filled}%`, backgroundColor: health.color }}
        />
        {goal.health !== "Achieved" && expectedMark > 0 && expectedMark < 100 ? (
          <span
            title={`Should be at ${formatValue(goal.expectedByNow, goal.format)} by today`}
            className="absolute top-0 h-full w-0.5 bg-foreground/40"
            style={{ left: `${expectedMark}%` }}
          />
        ) : null}
      </div>

      <dl className="grid grid-cols-3 gap-2 text-[11px]">
        <Cell label="Pace">
          <span
            className={cn(
              "font-medium tabular-nums",
              goal.pacePercent >= 100
                ? "text-emerald-600 dark:text-emerald-400"
                : "text-amber-600 dark:text-amber-400"
            )}
          >
            {goal.pacePercent}%
          </span>
        </Cell>
        <Cell label="Projected">
          <span className="flex items-center gap-0.5 font-medium tabular-nums">
            <TrendingUp className="size-3 text-muted-foreground" />
            {formatValue(goal.projected, goal.format)}
          </span>
        </Cell>
        <Cell label="Days left">
          <span className="font-medium tabular-nums">{goal.daysRemaining}</span>
        </Cell>
      </dl>

      <footer className="flex items-center gap-1.5 border-t pt-2 text-[11px] text-muted-foreground">
        <CalendarDays className="size-3 shrink-0" />
        {new Date(goal.startDate).toLocaleDateString("en-IN", {
          day: "numeric",
          month: "short",
        })}
        {" – "}
        {new Date(goal.endDate).toLocaleDateString("en-IN", {
          day: "numeric",
          month: "short",
          year: "numeric",
        })}
        <span className="ml-auto">{goal.datasetLabel}</span>
      </footer>
    </article>
  );
}

function Cell({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <div className="flex flex-col gap-0.5">
      <dt className="text-muted-foreground">{label}</dt>
      <dd>{children}</dd>
    </div>
  );
}

function Stat({
  label,
  value,
  tone,
}: {
  label: string;
  value: number;
  tone?: "good" | "warn" | "bad";
}) {
  if (value === 0) return null;

  return (
    <Badge
      variant="outline"
      className={cn(
        "h-7 gap-1 font-normal tabular-nums",
        tone === "good" && "border-emerald-500/40 text-emerald-700 dark:text-emerald-400",
        tone === "warn" && "border-amber-500/40 text-amber-700 dark:text-amber-400",
        tone === "bad" && "border-destructive/40 text-destructive"
      )}
    >
      {value} {label.toLowerCase()}
    </Badge>
  );
}
